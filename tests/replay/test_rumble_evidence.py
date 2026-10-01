"""G00 tooling/fixture qualification. Does not test a production Rumble adapter."""
from collections import Counter
import gzip
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import types
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import rumble_evidence as evidence
import rumble_synthetic as synthetic


class SanitizerTests(unittest.TestCase):
    def test_identity_text_aliases_preserve_equality_and_distinctions(self):
        sanitizer = evidence.Sanitizer()
        first = synthetic.message(text="Synthetic private test text")
        second = synthetic.message(user="synthetic-other-user", text="Synthetic private test text")
        third = synthetic.message(text="Another synthetic private text")
        safe = [sanitizer.sanitize(m) for m in [first, first, second, third]]
        self.assertEqual(safe[0], safe[1])
        self.assertNotEqual(safe[0]["username"], safe[2]["username"])
        self.assertEqual(safe[0]["text"], safe[2]["text"])
        self.assertNotEqual(safe[0]["text"], safe[3]["text"])
        self.assertNotIn("Synthetic private test text", json.dumps(safe))

    def test_money_dates_array_order_and_nulls_are_preserved(self):
        data = synthetic.snapshot([synthetic.message(2), synthetic.message(1)], [synthetic.rant(cents=125)])
        clean = evidence.Sanitizer().sanitize(data)
        chat = clean["livestreams"][0]["chat"]
        self.assertEqual([m["created_on"] for m in chat["recent_messages"]],
                         [synthetic.stamp(2), synthetic.stamp(1)])
        self.assertEqual(chat["recent_rants"][0]["amount_cents"], 125)
        self.assertIsNone(clean["channel_id"])
        self.assertIsNone(clean["subscribers"]["latest_subscriber"])
        self.assertEqual(clean["livestreams"][0]["stream_key"], "[REDACTED]")
        self.assertTrue(chat["recent_messages"][0]["profile_pic_url"].startswith("https://example.invalid/"))

    def test_unknown_capture_field_fails_closed(self):
        for value in ["synthetic-only", {"username": "synthetic-only"}, ["synthetic-only"]]:
            with self.assertRaises(evidence.EvidenceError):
                evidence.Sanitizer().sanitize({"unreviewed_private_field": value})

    def test_unredacted_key_is_rejected_without_value_in_error(self):
        with self.assertRaises(evidence.EvidenceError) as caught:
            evidence.Sanitizer().sanitize({"stream_key": "synthetic-only-credential"})
        self.assertNotIn("synthetic-only-credential", str(caught.exception))

    def test_timestamps_must_have_timezone(self):
        with self.assertRaises(evidence.EvidenceError):
            evidence.Sanitizer().sanitize({"created_on": "2026-01-01T00:00:00"})

    def test_scanner_rejection_prevents_read(self):
        failed = subprocess.CompletedProcess([], 1, b"finding", b"")
        with patch.object(evidence.subprocess, "run", return_value=failed), \
                patch.object(Path, "read_bytes") as read:
            with self.assertRaises(evidence.EvidenceError):
                evidence.safe_read(Path("never-read"))
            read.assert_not_called()

    def test_no_substring_copy_from_free_text_or_urls(self):
        sanitizer = evidence.Sanitizer()
        source = {"text": "Synthetic URL https://example.invalid/private?token=placeholder",
                  "username": "synthetic-user-value", "profile_pic_url": "https://example.invalid/private-user-path"}
        clean = sanitizer.sanitize(source)
        for original in source.values():
            self.assertNotIn(original, json.dumps(clean))


class PublishedCaptureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads(evidence.safe_read(evidence.FIXTURES / "capture-manifest.json"))
        cls.compressed = evidence.safe_read(evidence.FIXTURES / "captured.jsonl.gz")
        cls.plain = evidence.scan_bytes(gzip.decompress(cls.compressed), "replay test input")
        cls.records = [json.loads(line) for line in cls.plain.splitlines()]
        cls.fields = json.loads(evidence.safe_read(evidence.FIXTURES / "observed-fields.json"))
        cls.schema = json.loads(evidence.safe_read(evidence.FIXTURES / "observed-schema.json"))

    def test_all_785_polls_chronological_and_captured_provenance(self):
        self.assertEqual([r["sourcePollId"] for r in self.records], list(range(1, 786)))
        dates = [evidence.utc(r["observed_at"]) for r in self.records]
        self.assertEqual(dates, sorted(dates))
        self.assertEqual({r["provenance"] for r in self.records}, {"captured-sanitized"})
        self.assertEqual(Counter(r["outcome"] for r in self.records), {"ok": 785})

    def test_checksums_and_deterministic_gzip(self):
        self.assertEqual(hashlib.sha256(self.plain).hexdigest(), self.manifest["replaySha256"])
        self.assertEqual(hashlib.sha256(self.compressed).hexdigest(), self.manifest["replayGzipSha256"])
        recompressed = evidence.pack_replay(self.plain)
        self.assertEqual(recompressed, evidence.pack_replay(self.plain))
        self.assertEqual(gzip.decompress(recompressed), self.plain)
        self.assertEqual(self.compressed[3:8], b"\0" * 5)  # No filename flag or mtime.
        self.assertEqual(self.compressed[9], 255)  # Portable OS header.

    def test_counts_match_manifest_and_known_report(self):
        actual = evidence.metrics(self.records)
        self.assertEqual(actual, self.manifest["metrics"])
        self.assertEqual(actual["pollsWithLivestream"], 751)
        self.assertEqual(actual["chatArrayObservations"], 33207)
        self.assertEqual(actual["distinctChatBaseFingerprints"], 111)
        self.assertEqual(actual["distinctRantBaseFingerprints"], 1)
        self.assertEqual(actual["distinctFollowerBaseFingerprints"], 53)
        self.assertEqual(self.manifest["existingExportPolls"], 1)

    def test_every_field_type_presence_and_array_count_preserved(self):
        self.assertEqual(evidence.field_inventory(self.records), self.fields)
        self.assertEqual(len(self.fields), self.manifest["fieldPaths"])
        index = {r["path"]: r for r in self.fields}

        def inspect(node, path="$"):
            self.assertNotIn("required", node)
            expected = index[path]
            self.assertEqual(node.get("x-type-counts", {}), expected["types"])
            if expected["types"]:
                self.assertEqual(node["x-observations"], expected["observations"])
            for key, child in node.get("properties", {}).items():
                inspect(child, path + "[" + json.dumps(key) + "]")
            if "items" in node:
                self.assertEqual(node["x-max-observed-length"], expected["max_length"])
                self.assertEqual(node["x-empty-array-observations"], expected["empty_arrays"])
                inspect(node["items"], path + "[]")
        inspect(self.schema)

    def test_generated_output_values_use_only_reviewed_policy(self):
        def walk(value, key=None):
            if isinstance(value, dict):
                for k, child in value.items():
                    walk(child, k)
            elif isinstance(value, list):
                for child in value:
                    walk(child, key)
            elif isinstance(value, str):
                if key in evidence.Sanitizer.DATES:
                    evidence.utc(value)
                elif key in evidence.Sanitizer.DOMAINS:
                    domain = evidence.Sanitizer.DOMAINS[key]
                    pattern = ("https://example.invalid/" if domain in {"avatar", "ingest"} else "") + domain + r"-\d{4}"
                    self.assertRegex(value, "^" + pattern + "$")
                elif key == "stream_key":
                    self.assertEqual(value, "[REDACTED]")
                elif key == "type":
                    self.assertIn(value, {"user", "channel"})
                elif key == "visibility":
                    self.assertIn(value, {"public", "private", "unlisted"})
                else:
                    self.fail("Unreviewed string field in public capture")
        for record in self.records:
            walk(record["payload"])


class SyntheticCaseTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        base = evidence.FIXTURES / "synthetic"
        cls.index = json.loads(evidence.safe_read(base / "index.json"))
        cls.cases = {name: json.loads(evidence.safe_read(base / (name + ".json")))
                     for name in cls.index["cases"]}

    def test_cases_reproduce_generator_and_have_distinct_provenance(self):
        generated = {c["id"]: c for c in synthetic.cases()}
        self.assertEqual(self.cases, generated)
        self.assertEqual(len(self.cases), 26)
        for case in self.cases.values():
            self.assertEqual(case["provenance"], "synthetic")
            self.assertTrue(case["expected"])
            self.assertIn("not a live-capture", case["expectationScope"])
            self.assertEqual([s["sequence"] for s in case["steps"]], list(range(1, len(case["steps"]) + 1)))
            dates = [evidence.utc(s["observed_at"]) for s in case["steps"]]
            self.assertEqual(dates, sorted(dates))

    def chat(self, case, step):
        return self.cases[case]["steps"][step]["payload"]["livestreams"][0]["chat"]["recent_messages"]

    def test_duplicate_case_has_real_multiset_increase(self):
        old = self.chat("duplicate-occurrence-increase", 0)
        new = self.chat("duplicate-occurrence-increase", 1)
        self.assertEqual(len(old), 1)
        self.assertEqual(new, [old[0], old[0]])
        self.assertEqual(new, self.chat("duplicate-occurrence-increase", 2))

    def test_rotated_window_overlap_is_49_and_gap_overlap_is_zero(self):
        for name, overlap in [("full-window-rotation", 49), ("full-window-zero-overlap", 0)]:
            before, after = self.chat(name, 0), self.chat(name, 1)
            self.assertEqual((len(before), len(after)), (50, 50))
            old = {evidence.base_key("s", m) for m in before}
            new = {evidence.base_key("s", m) for m in after}
            self.assertEqual(len(old & new), overlap)

    def test_error_polls_have_no_success_payload_and_offline_gate_is_explicit(self):
        case = self.cases["errors-and-offline-debounce"]
        for step in case["steps"]:
            if step["outcome"] != "ok":
                self.assertIsNone(step["payload"])
        self.assertEqual(case["expected"]["offlineAtStep"], 7)
        self.assertEqual(case["steps"][2]["retry_after"], "60")
        self.assertIn("GMT", self.cases["retry-after-date"]["steps"][1]["retry_after"])

    def test_unverified_gift_mutation_has_no_new_purchase_identity(self):
        case = self.cases["gift-mutation-unverified"]
        a, b = [s["payload"]["gifted_subs"]["recent_gifted_subs"][0] for s in case["steps"][1:]]
        self.assertNotEqual(a["remaining_gifts"], b["remaining_gifts"])
        self.assertEqual({k: v for k, v in a.items() if k != "remaining_gifts"},
                         {k: v for k, v in b.items() if k != "remaining_gifts"})
        self.assertEqual(case["expected"]["authoritativeGifts"], 0)
        self.assertFalse(case["expected"]["liveVerified"])
        self.assertEqual(case["schemaOrigin"], "official-example-derived")

    def test_controls_and_monetary_oracles_are_present(self):
        for name, op in [("application-restart", "restart"), ("credential-change", "credential-change"),
                         ("dedupe-reset", "dedupe-reset")]:
            self.assertTrue(any(s.get("op") == op for s in self.cases[name]["steps"]))
        self.assertEqual(self.cases["rant-cents-authority"]["expected"]["usdAmountMinor"], 125)
        steps = self.cases["rant-repeat-expiry"]["steps"]
        rant = steps[1]["payload"]["livestreams"][0]["chat"]["recent_rants"][0]
        self.assertGreater(evidence.utc(rant["created_on"]), evidence.utc(steps[0]["observed_at"]))
        self.assertGreater(evidence.utc(rant["expires_on"]), evidence.utc(steps[1]["observed_at"]))
        self.assertGreater(evidence.utc(steps[3]["observed_at"]), evidence.utc(rant["expires_on"]))


@unittest.skipUnless((ROOT / "references/rumbleLiveAPIScraper.zip").exists(), "Local private archive absent; public fixtures remain testable")
class LocalSourceComparisonTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.raw, cls.selected, _, cls.metadata = evidence.source_data(ROOT / "references/rumbleLiveAPIScraper.zip")
        plain = evidence.scan_bytes(gzip.decompress(evidence.safe_read(evidence.FIXTURES / "captured.jsonl.gz")), "source-comparison fixture")
        cls.records = [json.loads(line) for line in plain.splitlines()]

    def test_every_source_record_matches_sanitized_output_and_no_private_value_survives(self):
        sanitizer = evidence.Sanitizer()
        expected = [{**r, "provenance": "captured-sanitized", "payload": sanitizer.sanitize(r["payload"])} for r in self.raw]
        # Avoid assertion diffs containing private data on a regression.
        self.assertTrue(expected == self.records, "Source-to-fixture comparison failed")
        evidence.verify_private_replacement(self.raw, self.records, sanitizer)
        self.assertEqual(evidence.metrics(self.raw), evidence.metrics(self.records))

    def test_original_recorder_test_suite_and_inference(self):
        # Run scanner-approved source in memory; never load credentials or record.
        recorder = types.ModuleType("recorder")
        exec(compile(self.selected["recorder.py"], "archive-recorder", "exec"), recorder.__dict__)
        tests = types.ModuleType("archive_tests")
        with patch.dict(sys.modules, {"recorder": recorder}):
            exec(compile(self.selected["test_recorder.py"], "archive-tests", "exec"), tests.__dict__)
            suite = unittest.defaultTestLoader.loadTestsFromModule(tests)
            result = unittest.TextTestRunner(stream=io.StringIO()).run(suite)
        self.assertEqual(result.testsRun, 10)
        self.assertTrue(result.wasSuccessful(), "Original recorder tests failed")
        inference = {}
        for row in self.raw:
            recorder.observe(inference, row["payload"])
        supplied = json.loads(self.selected["data/reports/fields.json"])
        self.assertTrue(list(recorder.inventory(inference)) == supplied["fields"], "Original inference/report mismatch")


if __name__ == "__main__":
    unittest.main()

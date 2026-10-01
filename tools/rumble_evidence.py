#!/usr/bin/env python3
"""G00 evidence tooling. No network calls, recording, or production event engine.

Reads only explicitly selected archive members after deterministic Sonar scanning.
Raw captures and reversible alias maps never leave process memory.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime
import gzip
import hashlib
import io
import json
from pathlib import Path
import sqlite3
import statistics
import subprocess
import sys
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[1]
PREFIX = "rumbleLiveAPIScraper/"
FIXTURES = ROOT / "tests/fixtures/rumble"
SOURCE_MEMBERS = (
    "README.md", "recorder.py", "test_recorder.py", "make_launchd.py", ".gitignore",
    "data/local.rumble-schema-recorder.plist", "data/reports/report.md",
    "data/reports/fields.json", "data/reports/schema.json", "data/recorder.lock",
    "data/capture.sqlite3", "data/captures.jsonl.gz",
)


class EvidenceError(Exception):
    """Errors contain descriptions only, never source values."""


def scan_bytes(data: bytes, label: str) -> bytes:
    result = subprocess.run(
        ["sonar", "analyze", "secrets", "--stdin"], input=data,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False,
    )
    if result.returncode or b"No secrets found" not in result.stdout:
        raise EvidenceError(
            f"STOP: {label} did not pass deterministic secrets scanning. "
            "Do not read it: credentials could enter chat/logs/telemetry. "
            "Rotate any credential at its source and remove it before continuing."
        )
    return data


def safe_read(path: Path) -> bytes:
    result = subprocess.run(
        ["sonar", "analyze", "secrets", str(path)],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False,
    )
    if result.returncode or b"No secrets found" not in result.stdout:
        raise EvidenceError("STOP: file scan failed; do not read or publish the file.")
    return path.read_bytes()


def encode(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True,
                       separators=(",", ":"), allow_nan=False) + "\n").encode()


def utc(value: str) -> datetime:
    date = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if date.tzinfo is None:
        raise EvidenceError("Naive timestamp in evidence.")
    return date


def source_data(archive: Path):
    archive_bytes = safe_read(archive)
    # Do not extract the archive; notably never read .secrets/, .git/ or pycache.
    with zipfile.ZipFile(io.BytesIO(archive_bytes)) as bundle:
        selected = {
            name: scan_bytes(bundle.read(PREFIX + name), name)
            for name in SOURCE_MEMBERS
        }
    database = bytearray(selected["data/capture.sqlite3"])
    # A checkpointed WAL-mode file cannot deserialize standalone in SQLite.
    # Change journal-mode header bytes on the in-memory copy only. Verify integrity.
    if database[:16] != b"SQLite format 3\0":
        raise EvidenceError("Invalid SQLite file header.")
    database[18] = database[19] = 1
    connection = sqlite3.connect(":memory:")
    try:
        connection.deserialize(bytes(database))
        if connection.execute("PRAGMA integrity_check").fetchall() != [("ok",)]:
            raise EvidenceError("Capture database integrity check failed.")
        if connection.execute("PRAGMA foreign_key_check").fetchall():
            raise EvidenceError("Capture references are inconsistent.")
        db_schema = [row[0] for row in connection.execute(
            "SELECT sql FROM sqlite_master WHERE sql IS NOT NULL ORDER BY name"
        )]
        rows = connection.execute(
            "SELECT p.id,p.observed_at,p.http_status,p.elapsed_ms,p.outcome,"
            "p.payload_hash,x.body,x.raw_bytes FROM polls p "
            "LEFT JOIN payloads x ON p.payload_hash=x.hash ORDER BY p.id"
        ).fetchall()
        inference = connection.execute(
            "SELECT value FROM state WHERE key='inference'"
        ).fetchone()
        stored_payloads = connection.execute("SELECT COUNT(*) FROM payloads").fetchone()[0]
        compressed_bytes = connection.execute(
            "SELECT COALESCE(SUM(LENGTH(body)),0) FROM payloads"
        ).fetchone()[0]
    finally:
        connection.close()
    bodies = [zlib.decompress(row[6]) if row[6] is not None else None for row in rows]
    # Scan decompressed data before semantic inspection. Binary/archive scanning
    # does not establish that compressed credentials are absent.
    scan_bytes(b"\n".join(body for body in bodies if body), "decompressed payloads")
    scan_bytes(inference[0].encode(), "inference state")
    export = scan_bytes(gzip.decompress(selected["data/captures.jsonl.gz"]),
                        "decompressed existing export")
    records = []
    for row, body in zip(rows, bodies):
        if body is not None:
            if hashlib.sha256(body).hexdigest() != row[5] or len(body) != row[7]:
                raise EvidenceError("Stored payload hash/length mismatch.")
        records.append({"sourcePollId": row[0], "observed_at": row[1],
                        "http_status": row[2], "elapsed_ms": row[3],
                        "outcome": row[4], "payload": json.loads(body) if body else None})
    if len(records) != 785 or len({r["sourcePollId"] for r in records}) != 785:
        raise EvidenceError("Unexpected poll coverage; review new input before publishing.")
    if any(utc(a["observed_at"]) > utc(b["observed_at"])
           for a, b in zip(records, records[1:])):
        raise EvidenceError("Poll ID order is not chronological.")
    return records, selected, db_schema, {
        "distinctStoredPayloads": stored_payloads,
        "compressedPayloadBytes": compressed_bytes,
        "existingExportPolls": len(export.splitlines()),
        "sourceArchiveSha256": hashlib.sha256(archive_bytes).hexdigest(),
    }


class Sanitizer:
    """Sequential aliases, not reversible hashes; fail closed on unknown keys.

    Date strings, numbers and public enum values are retained. All other strings
    are replaced, including badge values, categories, titles and avatar URLs.
    Mapping tables are never written or logged.
    """
    CONTAINERS = {
        "followers", "subscribers", "gifted_subs", "livestreams", "categories",
        "primary", "secondary", "chat", "latest_follower", "recent_followers",
        "latest_subscriber", "recent_subscribers", "latest_gifted_sub",
        "recent_gifted_subs", "latest_message", "recent_messages", "latest_rant",
        "recent_rants", "badges",
    }
    NUMERIC = {
        "now", "max_num_results", "since", "num_followers", "num_followers_total",
        "num_subscribers", "num_gifted_subs", "amount_cents", "amount_dollars",
        "watching_now", "likes", "dislikes", "is_live",
    }
    DATES = {"created_on", "expires_on", "followed_on", "scheduled_on"}
    DOMAINS = {
        "username": "user", "user_id": "account", "channel_id": "channel",
        "channel_name": "channel-name", "id": "stream", "text": "message",
        "title": "title", "slug": "category", "badges": "badge",
        "profile_pic_url": "avatar", "server_url": "ingest",
    }

    def __init__(self):
        self.aliases: dict[str, dict[str, str]] = {}

    def alias(self, domain: str, value: str) -> str:
        mapping = self.aliases.setdefault(domain, {})
        if value not in mapping:
            serial = len(mapping) + 1
            label = f"{domain}-{serial:04d}"
            mapping[value] = (
                f"https://example.invalid/{label}" if domain in {"avatar", "ingest"}
                else label
            )
        return mapping[value]

    def sanitize(self, value, key=None):
        if isinstance(value, dict):
            if key is not None and key not in self.CONTAINERS:
                raise EvidenceError("Unreviewed object field in capture.")
            return {k: self.sanitize(v, k) for k, v in sorted(value.items())}
        if isinstance(value, list):
            if key not in self.CONTAINERS:
                raise EvidenceError("Unreviewed array field in capture.")
            return [self.sanitize(item, key) for item in value]
        if value is None and key in self.CONTAINERS:
            return None
        if key in self.DATES:
            if not isinstance(value, str):
                raise EvidenceError("Unreviewed date type.")
            utc(value)
            return value
        if key in self.NUMERIC:
            if value is not None and not isinstance(value, (int, float, bool)):
                raise EvidenceError("Unreviewed numeric type.")
            return value
        if key in self.DOMAINS:
            if value is None:
                return None
            if not isinstance(value, str):
                raise EvidenceError("Unreviewed identity/string type.")
            return self.alias(self.DOMAINS[key], value)
        if key == "stream_key":
            if value != "[REDACTED]":
                raise EvidenceError("STOP: capture has an unredacted ingest credential.")
            return "[REDACTED]"
        if key == "type" and value in {"user", "channel"}:
            return value
        if key == "visibility" and value in {"public", "private", "unlisted"}:
            return value
        raise EvidenceError("Unreviewed field/value; sanitize policy requires review.")


def kind(value):
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int):
        return "integer"
    if isinstance(value, float):
        return "number"
    if isinstance(value, str):
        return "string"
    return "array" if isinstance(value, list) else "object"


def field_inventory(records):
    """Independently recompute recorder inventory including unknown array items."""
    nodes = {}

    def visit(value, path="$", parent=None):
        item = nodes.setdefault(path, {"path": path, "types": Counter(),
                                       "observations": 0, "parent": parent})
        typ = kind(value)
        item["types"][typ] += 1
        item["observations"] += 1
        if typ == "object":
            for key, child in sorted(value.items()):
                visit(child, path + "[" + json.dumps(key) + "]", path)
        elif typ == "array":
            item["empty_arrays"] = item.get("empty_arrays", 0) + (not value)
            item["max_length"] = max(item.get("max_length", 0), len(value))
            nodes.setdefault(path + "[]", {"path": path + "[]", "types": Counter(),
                                          "observations": 0, "parent": None})
            for child in value:
                visit(child, path + "[]")

    for record in records:
        if record["outcome"] == "ok":
            visit(record["payload"])
    result = []
    for path, node in sorted(nodes.items()):
        row = {k: v for k, v in node.items() if k != "parent"}
        row["types"] = dict(row["types"])
        parent = node["parent"]
        row["parent_object_observations"] = nodes[parent]["types"].get("object") if parent else None
        result.append(row)
    return result


def base_key(stream_id, item, rant=False):
    fields = [stream_id, item.get("created_on"), item.get("username"), item.get("text")]
    if rant:
        fields += [item.get("amount_cents"), item.get("expires_on")]
    else:
        fields += [sorted(item.get("badges", []))]
    return encode(fields)


def metrics(records):
    messages, rants, follows, stream_ids = set(), set(), set(), set()
    states, windows, message_count, rant_count, follow_count = [], [], 0, 0, 0
    full_windows = zero_overlap = duplicates = desc = asc = equal = 0
    previous = {}
    for record in records:
        payload = record["payload"] or {}
        streams = payload.get("livestreams", [])
        states.append(tuple((s["id"], s["is_live"]) for s in streams))
        followers = payload.get("followers", {}).get("recent_followers", [])
        follow_count += len(followers)
        follows.update(encode([f["username"], f["followed_on"]]) for f in followers)
        for stream in streams:
            stream_ids.add(stream["id"])
            chat = stream["chat"]
            current = chat["recent_messages"]
            keys = Counter(base_key(stream["id"], m) for m in current)
            duplicates += sum(n - 1 for n in keys.values())
            messages.update(keys)
            message_count += len(current)
            rants.update(base_key(stream["id"], r, True) for r in chat["recent_rants"])
            rant_count += len(chat["recent_rants"])
            windows.append(len(current))
            full_windows += len(current) == 50
            old = previous.get(stream["id"])
            if old and sum(old.values()) == len(current) == 50 and not (old & keys):
                zero_overlap += 1
            previous[stream["id"]] = keys
            dates = [utc(m["created_on"]) for m in current]
            desc += all(a >= b for a, b in zip(dates, dates[1:]))
            asc += all(a <= b for a, b in zip(dates, dates[1:]))
            equal += bool(dates) and len(set(dates)) == 1
    intervals = [(utc(b["observed_at"]) - utc(a["observed_at"])).total_seconds()
                 for a, b in zip(records, records[1:])]
    transitions = [{"sourcePollId": records[i]["sourcePollId"],
                    "streamPresent": bool(states[i]),
                    "liveFlags": [flag for _, flag in states[i]]}
                   for i in range(1, len(states)) if states[i] != states[i - 1]]
    return {
        "polls": len(records), "outcomes": dict(Counter(r["outcome"] for r in records)),
        "pollsWithLivestream": sum(bool(s) for s in states),
        "pollsWithoutLivestream": sum(not s for s in states),
        "firstPoll": records[0]["observed_at"], "lastPoll": records[-1]["observed_at"],
        "distinctStreams": len(stream_ids), "chatArrayObservations": message_count,
        "distinctChatBaseFingerprints": len(messages), "rantArrayObservations": rant_count,
        "distinctRantBaseFingerprints": len(rants), "followerArrayObservations": follow_count,
        "distinctFollowerBaseFingerprints": len(follows), "maxChatWindow": max(windows),
        "fullChatWindows": full_windows, "consecutiveFullWindowZeroOverlap": zero_overlap,
        "duplicateBaseOccurrencesWithinSnapshots": duplicates,
        "descendingTimestampWindows": desc, "ascendingTimestampWindows": asc,
        "allEqualTimestampWindows": equal, "streamStateChanges": transitions,
        "pollIntervalSeconds": {"min": min(intervals), "median": statistics.median(intervals),
                                "max": max(intervals)},
    }


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False) + "\n").encode())


def pack_replay(data):
    # GzipFile normalizes the OS header across Windows/Linux and Python versions;
    # an explicit level avoids Python 3.14's changed gzip.compress default.
    output = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=output, mtime=0, compresslevel=9) as stream:
        stream.write(data)
    return output.getvalue()


def build(archive):
    raw, selected, db_schema, metadata = source_data(archive)
    sanitizer = Sanitizer()
    clean = []
    for record in raw:
        clean.append({**record, "provenance": "captured-sanitized", "payload": sanitizer.sanitize(record["payload"])})
    source_metrics, clean_metrics = metrics(raw), metrics(clean)
    if source_metrics != clean_metrics:
        raise EvidenceError("Sanitization changed replay metrics.")
    source_fields = json.loads(selected["data/reports/fields.json"])
    inventory = field_inventory(raw)
    if inventory != sorted(source_fields["fields"], key=lambda row: row["path"]):
        raise EvidenceError("Recomputed field inventory differs from supplied report.")
    if inventory != field_inventory(clean):
        raise EvidenceError("Sanitization changed field types/presence/multiplicity.")
    if source_fields["polls"] != len(raw) or source_fields["outcomes"] != source_metrics["outcomes"]:
        raise EvidenceError("Polls/outcomes differ from supplied report.")
    for key, supplied in [("distinctStoredPayloads", "distinct_payloads"),
                          ("compressedPayloadBytes", "compressed_payload_bytes")]:
        if metadata[key] != source_fields[supplied]:
            raise EvidenceError("Stored payload totals differ from supplied report.")
    data = b"".join(encode(record) for record in clean)
    verify_private_replacement(raw, clean, sanitizer)
    scan_bytes(data, "sanitized replay")
    FIXTURES.mkdir(parents=True, exist_ok=True)
    # Deterministic gzip header: no original filename, no current modification time.
    target = FIXTURES / "captured.jsonl.gz"
    compressed = pack_replay(data)
    target.write_bytes(compressed)
    write_json(FIXTURES / "observed-fields.json", inventory)
    write_json(FIXTURES / "observed-schema.json", json.loads(selected["data/reports/schema.json"]))
    manifest = {
        "formatVersion": 1, "provenance": "captured-sanitized", **metadata,
        "metrics": clean_metrics, "replaySha256": hashlib.sha256(data).hexdigest(),
        "replayGzipSha256": hashlib.sha256(compressed).hexdigest(),
        "fieldPaths": len(inventory), "selectedArchiveMembers": list(SOURCE_MEMBERS),
        "captureDatabaseSchema": db_schema,
        "generator": {"python": sys.version.split()[0], "zlib": zlib.ZLIB_RUNTIME_VERSION,
                      "gzipLevel": 9, "gzipMtime": 0},
        "sanitization": {
            "version": 1, "mappingExported": False,
            "aliases": "sequential first occurrence in sorted object traversal; category-specific",
            "privateStrings": "user/account/channel/stream IDs, text, titles, categories, badges and URLs replaced",
            "retained": "timestamps, numbers, booleans, nulls, arrays/order, public type/visibility enum values",
            "warning": "Preserved dates/amounts/counters are correlation metadata; not complete anonymization.",
        },
    }
    write_json(FIXTURES / "capture-manifest.json", manifest)
    return manifest


def verify_private_replacement(raw, clean, sanitizer):
    """Review every replaced string at its source path, without logging values."""
    def compare(left, right, key=None):
        if kind(left) != kind(right):
            raise EvidenceError("JSON type changed during sanitization.")
        if isinstance(left, dict):
            if set(left) != set(right):
                raise EvidenceError("JSON field set changed during sanitization.")
            for k in left:
                compare(left[k], right[k], k)
        elif isinstance(left, list):
            if len(left) != len(right):
                raise EvidenceError("Array size changed during sanitization.")
            for a, b in zip(left, right):
                compare(a, b, key)
        elif isinstance(left, str) and key in sanitizer.DOMAINS:
            if left == right or right != sanitizer.aliases[sanitizer.DOMAINS[key]][left]:
                raise EvidenceError("Private string replacement audit failed.")
        elif left != right:
            raise EvidenceError("Retained source field changed.")
    for a, b in zip(raw, clean):
        compare(a["payload"], b["payload"])
        if any(a[k] != b[k] for k in a if k != "payload"):
            raise EvidenceError("Poll metadata changed.")
    # No private string may survive anywhere as a full output string, not just
    # at its original path. Dates and monetary metadata are intentionally retained.
    private_values = {v for mapping in sanitizer.aliases.values() for v in mapping if v}
    def strings(value):
        if isinstance(value, dict):
            for child in value.values():
                yield from strings(child)
        elif isinstance(value, list):
            for child in value:
                yield from strings(child)
        elif isinstance(value, str):
            yield value
    if private_values.intersection(strings(clean)):
        raise EvidenceError("Original private string survived elsewhere in fixture.")


def verify(archive=None):
    manifest = json.loads(safe_read(FIXTURES / "capture-manifest.json"))
    compressed = safe_read(FIXTURES / "captured.jsonl.gz")
    plain = scan_bytes(gzip.decompress(compressed), "decompressed sanitized fixture")
    records = [json.loads(line) for line in plain.splitlines()]
    if hashlib.sha256(plain).hexdigest() != manifest["replaySha256"]:
        raise EvidenceError("Replay checksum mismatch.")
    if hashlib.sha256(compressed).hexdigest() != manifest["replayGzipSha256"]:
        raise EvidenceError("Compressed replay checksum mismatch.")
    if metrics(records) != manifest["metrics"] or len(records) != 785:
        raise EvidenceError("Replay metrics/coverage mismatch.")
    if any(r["provenance"] != "captured-sanitized" for r in records):
        raise EvidenceError("Replay provenance mismatch.")
    if [r["sourcePollId"] for r in records] != list(range(1, 786)):
        raise EvidenceError("Poll coverage/order mismatch.")
    if any(utc(a["observed_at"]) > utc(b["observed_at"])
           for a, b in zip(records, records[1:])):
        raise EvidenceError("Replay chronology mismatch.")
    fields = json.loads(safe_read(FIXTURES / "observed-fields.json"))
    schema = json.loads(safe_read(FIXTURES / "observed-schema.json"))
    if field_inventory(records) != fields:
        raise EvidenceError("Sanitized replay field inventory mismatch.")
    if schema.get("additionalProperties") is not True or "required" in schema:
        raise EvidenceError("Observed schema was converted into a brittle contract.")
    if archive:
        raw, selected, _, metadata = source_data(archive)
        sanitizer = Sanitizer()
        expected = [{**r, "provenance": "captured-sanitized", "payload": sanitizer.sanitize(r["payload"])} for r in raw]
        if expected != records:
            raise EvidenceError("Fixture differs from independently sanitized source records.")
        verify_private_replacement(raw, records, sanitizer)
        if metadata["sourceArchiveSha256"] != manifest["sourceArchiveSha256"]:
            raise EvidenceError("Source archive provenance mismatch.")
        if schema != json.loads(selected["data/reports/schema.json"]):
            raise EvidenceError("Observed schema differs from supplied source.")
    return {"polls": len(records), "fieldPaths": len(fields), "sourceCompared": bool(archive),
            "privateReplacementAudit": "passed" if archive else "not run (requires local archive)"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["build", "verify"])
    parser.add_argument("--archive", type=Path)
    args = parser.parse_args()
    try:
        if args.command == "build":
            if not args.archive:
                parser.error("build requires --archive")
            result = build(args.archive)["metrics"]
        else:
            result = verify(args.archive)
        print(json.dumps(result, indent=2))
        return 0
    except EvidenceError as error:
        print(str(error), file=sys.stderr)
        return 1
    except Exception:
        # Never emit a source-bearing exception/traceback from private inputs.
        print("Evidence operation failed; inspect safe source/tooling without exposing raw input.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

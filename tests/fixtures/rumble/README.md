# Rumble evidence fixtures

These are G00 evidence artifacts, not a production event engine.

- `captured.jsonl.gz`: all 785 SQLite polls, sanitized; the original archive's
  existing export contained only one poll and was not used as the replay source.
- `capture-manifest.json`: source provenance checksum, sanitized checksums,
  verified metrics, database schema and sanitization policy.
- `observed-fields.json` and `observed-schema.json`: metadata-only observation
  inventories, not exhaustive production contracts.
- `synthetic/`: 26 fabricated scenarios, schema provenance and future G04
  expectations; `index.json` lists all cases.

Do not confuse repeated observations with distinct/new events. No live
subscriptions/gifts were captured; the one Rant repeats across snapshots.
Date, amount and counter metadata are intentionally preserved and can correlate
with external history; direct private strings and original URLs are replaced.
No reversible alias map is shipped. See [analysis](../../../docs/rumble-analysis.md)
for every observed field, dedupe limitations and the publication review.

Run `python tools/rumble_evidence.py verify` and
`python -m unittest discover -s tests/replay -v` from the repository root after
scanning `tools` and `tests/replay` with Sonar. Python 3.11+ and the Sonar CLI are
required. Passing these checks proves fixture/tooling fidelity, not adapter
behavior. The private archive is optional for public tests.

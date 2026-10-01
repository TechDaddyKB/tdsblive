# Rumble Live API evidence and replay design — G00

Analyzed on 2026-10-01. This document qualifies evidence and fixture tooling only;
it does not claim that the production Rumble adapter, financial engine or
Streamer.bot bridge has been implemented.

## Evidence inspected and source integrity

The local source is `references/rumbleLiveAPIScraper.zip`. Its secrets directory,
embedded Git history, Python bytecode and macOS metadata were not extracted or
read. The original archive and raw capture are private and excluded from Git.

Reviewed recorder source, its README and all 10 unit tests; the launch-agent
generator and existing plist; ignore rules and lock file; report, field inventory
and observed schema; SQLite table definitions, poll rows, compressed payloads and
inference state; and the existing compressed JSONL export. Selected members and
decompressed bodies passed deterministic Sonar secrets scanning before inspection.
The recorder's own tests passed in memory without configuring credentials,
starting a recorder, registering a service, or making API requests.

The input SQLite file is a standalone checkpointed WAL-mode database. Evidence
tooling copies its bytes into memory and changes only the copy's journal header
for SQLite deserialization. Integrity and foreign-key checks pass. Each payload's
SHA-256 and uncompressed byte count match its stored values. Recomputed inference
matches every field count in the supplied report. No original database is modified.

The three recorder tables are:

| Table | Columns and purpose |
|---|---|
| `payloads` | `hash TEXT PRIMARY KEY`, `body BLOB NOT NULL`, `raw_bytes INTEGER NOT NULL`; zlib-compressed, credential-redacted canonical JSON |
| `polls` | `id INTEGER PRIMARY KEY`, `observed_at TEXT NOT NULL`, `http_status INTEGER`, `elapsed_ms INTEGER NOT NULL`, `outcome TEXT NOT NULL`, `payload_hash TEXT REFERENCES payloads(hash)`; every poll, including failures |
| `state` | `key TEXT PRIMARY KEY`, `value TEXT NOT NULL`; incremental inference JSON |

The original recorder atomically commits payload, poll and inference; hashes
whole payloads for storage reuse, not event identity. API `now` changes can make
otherwise identical snapshots distinct. Its schema allows additional properties,
asserts no required fields, and marks observed-only presence. Null-only types and
empty-array item shapes must not become restrictive production validation.

The recorder validates the official HTTPS endpoint, refuses redirects, caps
responses at 16 MiB, avoids recording HTTP error bodies and credential-bearing
exceptions, honors numeric/date Retry-After, and uses a post-request delay with
positive jitter and capped error backoff. Its default ten seconds is a recorder
setting, not an official API rate limit. The launch plist is machine-specific
and is not a production deployment artifact. The companion's own UI continues
to support HTTP; that does not change the upstream Rumble HTTPS endpoint.

## What the capture proves

| Measurement | Verified result |
|---|---:|
| Polls / successful outcomes / distinct stored payloads | 785 / 785 / 785 |
| Livestream present / absent | 751 / 34 |
| Distinct stream IDs | 1 |
| Recent-message array entries across snapshots | 33,207 |
| Distinct chat base fingerprints | 111 |
| Full 50-message windows | 506 |
| Descending creation-time windows | 751 of 751 |
| Duplicate chat base fingerprints within one snapshot | 0 |
| Consecutive full windows with zero overlap | 0 |
| Recent-Rant array entries / distinct Rant base fingerprints | 751 / 1 |
| Recent-follower entries / distinct follower fingerprints | 38,465 / 53 |
| Maximum recent followers / Rants / messages | 49 / 1 / 50 |
| Subscriber and gifted-sub arrays | Empty in every poll |
| Existing JSONL export | **Only 1 poll** |

Poll timestamps span `2026-09-22T18:03:58.763+00:00` through
`2026-09-30T01:29:07.926+00:00`. Recorded intervals range from 10.222 to
623,070.371 seconds, with median 10.784 seconds. The maximum interval separates
polls 1 and 2: a roughly seven-day discontinuity, not a week of continuous
observation. Poll 1 has no livestream; polls 2–752 contain the same live stream;
polls 753–785 contain none. Failed requests were not captured, so recovery and
offline debounce require synthetic coverage.

The first stream window contains 14 messages. There are 97 previously unseen
chat base keys after that per-stream baseline, versus only 90 changes to the
latest-message fingerprint. Four follower keys are newly observed after the
initial 49-entry follower baseline. These are offline set-difference observations,
not executed production adapter acceptance results; they illustrate why comparing
only latest-message status cannot substitute for inspecting the recent arrays.

Counts are observations or dedupe candidates, not proven numbers of new support
events. The single Rant appears repeatedly; it cannot prove detecting a newly
purchased Rant. Multiple real identical messages, array-order changes, large
bursts, multiple live streams and live subscriptions/gifts are absent. Timestamp
order is observed newest-first, not an enforced API contract.

Machine-readable evidence is in `tests/fixtures/rumble/capture-manifest.json`,
`observed-fields.json` and `observed-schema.json`. The field table below includes
containers, array-item nodes, occurrence counts, parent presence and uncertain
types; every recorded path is represented.

## Candidate events and qualification gates

| Candidate | Evidence and identity strategy | Remaining limitation |
|---|---|---|
| `ext.rumble.chat.message` | Recent array; stream, creation time, exact username/text, canonical badges, reconciled occurrence identity | No native message ID; bounded window and mutable badges can hide or mimic messages |
| `ext.rumble.rant` | Separate recent-Rant array; stream, creation time, username, cents, text, expiry | One repeated Rant only; new-purchase behavior needs synthetic then live checks |
| `ext.rumble.follow` | Recent followers; account/channel, username, followed timestamp | Counts alone never establish new follows; no native user ID on follower entries |
| `ext.rumble.subscription` | Live capture is empty; documentation-derived fixtures available | Null latest item and unknown array item shape are not evidence of unsupported subscriptions |
| `ext.rumble.gift_sub` | Documentation-derived gift fixtures available | Missing stable purchase ID/time; authoritative automation/ledger remains disabled until qualified |
| `ext.rumble.stream.online` / `offline` | Per-stream ID and live flag; observed presence transitions | Initial live status is not a new-online event; disappearance requires two successful confirmations |
| `ext.rumble.stats.viewer_count` / `likes` | Compare successful per-stream counters, including changes to zero | Baseline shows status only; counters are snapshots rather than user actions |
| `ext.rumble.health` | Transport/parser health state machine | No failures in capture; test timeout, invalid shape, HTTP error and recovery synthetically |
| `rumble.chat.possible_gap` | Consecutive full windows without fingerprint overlap | Diagnostic only; cannot count or reconstruct missing messages |

Channel/account context must use configured credential-context identity plus
observed `type`/user ID and channel ID when available; channel ID/name are null
throughout this capture. Never assume a missing channel ID means the same account
as a previous credential. Persist context changes separately without persisting
the credential URL as an identity.

The [official API examples](https://rumble.support/en/help/how-to-use-rumble-s-live-stream-api)
provide subscriber identity/time/amount fields and gifts with purchaser, video,
gift quantity/type and remaining count. They supplement absent live samples;
they do not establish gift purchase identity. A remaining-count decrement is
not another purchase. Keep unknown subscriber/gift structures redacted and
available for diagnostics. Do not infer paging or replay support from `since`
or `max_num_results` alone.

## Snapshot conversion and dedupe design for G04

1. Scope state by account/channel context and stream. First successful startup,
   credential change or explicit reset establishes a baseline. Suppress historic
   chat/Rants/follows/subscriptions/gifts. The first observed window of a newly
   discovered stream also establishes its chat/Rant baseline; a subsequent live
   transition may still produce an online event. Never announce an initial live
   baseline as a new transition.
2. Inspect complete recent arrays. `latest_*` is supplementary status, not an
   independent event source. Use strict timezone-aware UTC normalization for
   valid creation times and exact text/user strings; avoid whitespace/case changes
   that collapse legitimate messages. Deterministically sort badge representations
   without discarding repeated badges. Exclude avatar URLs from identity.
3. Hash an unambiguous canonical JSON tuple, not delimiter-concatenated values:
   chat `(context, stream, created, username, exact text, badges)`, Rant
   `(context, stream, created, username, cents, exact text, expiry)`, follow
   `(context, username, followed)`. Hash with SHA-256 and persist internal IDs.
   Native stable IDs, if later qualified, may replace fallback identities.
4. Match fingerprint multiplicities against the previous snapshot and durable
   recent occurrence state. Allocate occurrence indices for proven additional
   instances; array position is not a stable identity. Persist allocation and
   accepted-event keys transactionally. Reordering, shrinkage, disappearance and
   reappearance must not reset IDs. A same-count replacement of indistinguishable
   entries is unknowable; report this limitation rather than claiming exact delivery.
5. Badges/text may be edited. A badge-only change can alter the specified composite
   fingerprint without a new message. Qualify a mutation policy using a secondary
   core key and retained source evidence; the mutation fixture intentionally
   requires that explicit policy rather than inventing a definitive event count.
   Identical same-time messages and edited entries remain fundamentally ambiguous.
6. Preserve chat fingerprints until both older than 24 hours and outside the
   newest 10,000. Keep permanent financial uniqueness independent of chat cleanup.
   Accept snapshot events, dedupe changes and poll checkpoint together; downstream
   publication uses a durable outbox so crash/restart cannot double-count money.
7. Honor Retry-After (seconds or HTTP date); otherwise bounded exponential backoff.
   Production polling uses the plan's seven-second default, minimum five seconds,
   positive jitter and slower advanced settings. A transport/shape failure changes
   health only and does not advance an offline confirmation. Successful live
   recovery clears pending offline confirmation; state is per stream, not global.
8. If both consecutive windows are full and share no identities, record counts,
   overlap and actual elapsed polling interval. Emit observed new entries only;
   never create imaginary missing chat. Initial/new-context windows do not imply
   a gap. Out-of-order or malformed fields go to diagnostics with tolerant parsing.

Rant cents are authoritative integer USD minor units for the observed shape.
`amount_dollars` is display/supporting data; mismatches require diagnostics rather
than float-based totals. Do not include the same Rant independently from both
latest and recent representations. Expiration/disappearance never creates a new
support contribution. Follower-counter changes alone are likewise insufficient.

## Public-fixture sanitization and privacy review

`tools/rumble_evidence.py` reads a fixed archive-member allowlist without extraction.
It scans the archive, selected members, decompressed capture bodies and inference
before parsing, never reads the secrets file, never loads recorder credentials,
and never contacts Rumble. Unknown capture fields/values fail closed for manual
sanitization review; this is intentionally stricter than the future tolerant
production parser.

It replaces user/account/channel/stream IDs, chat/Rant text, titles, category
strings, badge strings and all URLs with sequential category-specific aliases.
URLs use `example.invalid`; stream keys must already be `[REDACTED]` or processing
stops. No original messages or identifiers, reversible alias table, per-message
hashes of private values, source code history or credential file is exported.
The archive checksum identifies the local provenance; fixture checksums identify
only sanitized bytes. Gzip has zero modification time, no source filename and a
portable OS header, with explicit compression level 9. The manifest records
Python/zlib versions; compressed bytes may differ across zlib versions, while
the canonical uncompressed content and its checksum remain the fidelity authority.

Preserved timestamps, quantities, cents, counts, booleans/nulls and arrays retain
the behavioral evidence requested by G00. They can still correlate with public
stream timing or donation amounts: these fixtures remove direct private string
content, but are **not complete anonymization**. Do not use them as a promise of
privacy against an observer who has external event history. Sanitized capture
badges/categories are aliases; known-badge rendering behavior belongs in synthetic
or separately licensed public fixtures.

Source-to-fixture verification checks every poll and every path for unchanged
JSON types/field presence, order, array cardinality, date/money values and consistent
aliases. It verifies no original private string survives as a complete output
string anywhere. Tests additionally allow only reviewed alias/date/enum forms in
public string values. All uncompressed fixtures receive deterministic scanning;
scanning the `.gz` archive alone is insufficient.

The publishable files are documentation, ignore rules, evidence/generator tooling,
tooling tests, sanitized capture/schema/field/manifest artifacts and synthetic
inputs. `references/` and raw/runtime/credential/build output are ignored. A fresh
local Git repository records G00 before any production code; remote repository
creation and SonarQube tracking remain G01.

## Replay and future adapter acceptance

Captured JSONL records contain `sourcePollId`, UTC `observed_at`, `http_status`,
`elapsed_ms`, `outcome`, `provenance: captured-sanitized`, and sanitized `payload`.
IDs 1–785 appear exactly once in chronological source order, including the long
real discontinuity. Do not accelerate timestamps by editing them: a replay runner
may use virtual time/speed controls. The production app does not require the
original recorder or archive.

Synthetic scenarios use `formatVersion`, `id`, `provenance: synthetic`, `schemaOrigin`,
ordered `steps`, and `expected`. Steps are polls or controls (restart, credential
change, reset). Their timestamps and amounts are fabricated. Some shapes derive
from observed data, others official examples or hypothetical changes; each is
labeled. Expected results are proposed G04 acceptance oracles, not evidence that
an adapter has already emitted those results.

The 26 scenarios cover historical baseline repetition; identical multiset growth
and unknowable same-count replacements; same text across users/times; reordering;
50-entry rotation and zero overlap; empty chat; timeout/429/500 and debounce;
HTTP-date Retry-After; online recovery and false-live; restart/credential/reset;
stream ID replacement; new/repeated/expired Rants and cents conflict; new follow;
documentation-derived subscription; gift mutation gate; unknown/null type drift;
malformed/invalid shape recovery; statistics to zero and count-only follows; badge
mutation ambiguity.

G04 must run these inputs through the actual .NET parser/snapshot engine with an
injected clock, mocked HTTP transport and temporary SQLite stores. Assert emitted
event IDs/counts/order, checkpoint rollback/restart behavior, independent stream
state, health transitions, Retry-After scheduling, and idempotent ledger/outbox
delivery. Local tests currently qualify fixture tooling and data fidelity only.
No live Rumble requests or paid interactions are required for G00.

Rebuild and validate with Python 3.11+ (SQLite deserialization support) and the
SonarQube CLI available:

```bash
sonar analyze secrets tools tests/replay
python tools/rumble_evidence.py build --archive references/rumbleLiveAPIScraper.zip
python tools/rumble_synthetic.py
python tools/rumble_evidence.py verify --archive references/rumbleLiveAPIScraper.zip
python -m unittest discover -s tests/replay -v
```

For a public checkout without the private reference archive:

```bash
sonar analyze secrets tools tests/replay
python tools/rumble_evidence.py verify
python -m unittest discover -s tests/replay -v
```

The two archive-dependent tests skip in a public checkout; published fixture tests
still run. Live bridge, true burst/multiplicity behavior, live subscriber/gift
identity, OBS interaction and Windows release qualification remain their owning
later goals, not implicit G00 deliverables.

## Complete observed field inventory

`Observations` counts values at the path, including repetitions. `Parent` is the
number of containing objects where a property could occur; array-item nodes have
no object parent. Empty-item nodes have unknown types, not an enforced null type.
`Array` reports empty observations / maximum length. All field counts were
recomputed from the 785 polls and match the recorder output.

| Path | Types (counts) | Observations | Parent | Array empty / max |
|---|---|---:|---:|---|
| `$` | object (785) | 785 | — | — |
| `$["channel_id"]` | null (785) | 785 | 785 | — |
| `$["channel_name"]` | null (785) | 785 | 785 | — |
| `$["followers"]` | object (785) | 785 | 785 | — |
| `$["followers"]["latest_follower"]` | object (785) | 785 | 785 | — |
| `$["followers"]["latest_follower"]["followed_on"]` | string (785) | 785 | 785 | — |
| `$["followers"]["latest_follower"]["profile_pic_url"]` | string (785) | 785 | 785 | — |
| `$["followers"]["latest_follower"]["username"]` | string (785) | 785 | 785 | — |
| `$["followers"]["num_followers"]` | integer (785) | 785 | 785 | — |
| `$["followers"]["num_followers_total"]` | integer (785) | 785 | 785 | — |
| `$["followers"]["recent_followers"]` | array (785) | 785 | 785 | 0 / 49 |
| `$["followers"]["recent_followers"][]` | object (38465) | 38465 | — | — |
| `$["followers"]["recent_followers"][]["followed_on"]` | string (38465) | 38465 | 38465 | — |
| `$["followers"]["recent_followers"][]["profile_pic_url"]` | string (38465) | 38465 | 38465 | — |
| `$["followers"]["recent_followers"][]["username"]` | string (38465) | 38465 | 38465 | — |
| `$["gifted_subs"]` | object (785) | 785 | 785 | — |
| `$["gifted_subs"]["latest_gifted_sub"]` | null (785) | 785 | 785 | — |
| `$["gifted_subs"]["num_gifted_subs"]` | integer (785) | 785 | 785 | — |
| `$["gifted_subs"]["recent_gifted_subs"]` | array (785) | 785 | 785 | 785 / 0 |
| `$["gifted_subs"]["recent_gifted_subs"][]` | **unknown** | 0 | — | — |
| `$["livestreams"]` | array (785) | 785 | 785 | 34 / 1 |
| `$["livestreams"][]` | object (751) | 751 | — | — |
| `$["livestreams"][]["categories"]` | object (751) | 751 | 751 | — |
| `$["livestreams"][]["categories"]["primary"]` | object (751) | 751 | 751 | — |
| `$["livestreams"][]["categories"]["primary"]["slug"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["categories"]["primary"]["title"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["categories"]["secondary"]` | object (751) | 751 | 751 | — |
| `$["livestreams"][]["categories"]["secondary"]["slug"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["categories"]["secondary"]["title"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]` | object (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_message"]` | object (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_message"]["badges"]` | array (751) | 751 | 751 | 153 / 1 |
| `$["livestreams"][]["chat"]["latest_message"]["badges"][]` | string (598) | 598 | — | — |
| `$["livestreams"][]["chat"]["latest_message"]["created_on"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_message"]["profile_pic_url"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_message"]["text"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_message"]["username"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_rant"]` | object (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_rant"]["amount_cents"]` | integer (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_rant"]["amount_dollars"]` | integer (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_rant"]["badges"]` | array (751) | 751 | 751 | 0 / 1 |
| `$["livestreams"][]["chat"]["latest_rant"]["badges"][]` | string (751) | 751 | — | — |
| `$["livestreams"][]["chat"]["latest_rant"]["created_on"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_rant"]["expires_on"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_rant"]["profile_pic_url"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_rant"]["text"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["latest_rant"]["username"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["recent_messages"]` | array (751) | 751 | 751 | 0 / 50 |
| `$["livestreams"][]["chat"]["recent_messages"][]` | object (33207) | 33207 | — | — |
| `$["livestreams"][]["chat"]["recent_messages"][]["badges"]` | array (33207) | 33207 | 33207 | 7800 / 2 |
| `$["livestreams"][]["chat"]["recent_messages"][]["badges"][]` | string (25613) | 25613 | — | — |
| `$["livestreams"][]["chat"]["recent_messages"][]["created_on"]` | string (33207) | 33207 | 33207 | — |
| `$["livestreams"][]["chat"]["recent_messages"][]["profile_pic_url"]` | string (33207) | 33207 | 33207 | — |
| `$["livestreams"][]["chat"]["recent_messages"][]["text"]` | string (33207) | 33207 | 33207 | — |
| `$["livestreams"][]["chat"]["recent_messages"][]["username"]` | string (33207) | 33207 | 33207 | — |
| `$["livestreams"][]["chat"]["recent_rants"]` | array (751) | 751 | 751 | 0 / 1 |
| `$["livestreams"][]["chat"]["recent_rants"][]` | object (751) | 751 | — | — |
| `$["livestreams"][]["chat"]["recent_rants"][]["amount_cents"]` | integer (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["recent_rants"][]["amount_dollars"]` | integer (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["recent_rants"][]["badges"]` | array (751) | 751 | 751 | 0 / 1 |
| `$["livestreams"][]["chat"]["recent_rants"][]["badges"][]` | string (751) | 751 | — | — |
| `$["livestreams"][]["chat"]["recent_rants"][]["created_on"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["recent_rants"][]["expires_on"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["recent_rants"][]["profile_pic_url"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["recent_rants"][]["text"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["chat"]["recent_rants"][]["username"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["created_on"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["dislikes"]` | integer (751) | 751 | 751 | — |
| `$["livestreams"][]["id"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["is_live"]` | boolean (751) | 751 | 751 | — |
| `$["livestreams"][]["likes"]` | integer (751) | 751 | 751 | — |
| `$["livestreams"][]["scheduled_on"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["server_url"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["stream_key"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["title"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["visibility"]` | string (751) | 751 | 751 | — |
| `$["livestreams"][]["watching_now"]` | integer (751) | 751 | 751 | — |
| `$["max_num_results"]` | integer (785) | 785 | 785 | — |
| `$["now"]` | integer (785) | 785 | 785 | — |
| `$["since"]` | null (785) | 785 | 785 | — |
| `$["subscribers"]` | object (785) | 785 | 785 | — |
| `$["subscribers"]["latest_subscriber"]` | null (785) | 785 | 785 | — |
| `$["subscribers"]["num_subscribers"]` | integer (785) | 785 | 785 | — |
| `$["subscribers"]["recent_subscribers"]` | array (785) | 785 | 785 | 785 / 0 |
| `$["subscribers"]["recent_subscribers"][]` | **unknown** | 0 | — | — |
| `$["type"]` | string (785) | 785 | 785 | — |
| `$["user_id"]` | string (785) | 785 | 785 | — |
| `$["username"]` | string (785) | 785 | 785 | — |

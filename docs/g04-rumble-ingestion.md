# G04 — Rumble ingestion contracts and qualification

TDSBLive supports local and authenticated optional LAN **HTTP; HTTPS is not required**. The upstream Rumble credential URL uses HTTPS. This distinction does not add a certificate requirement to the local editor.

## Configuration and credentials

`rumble.enabled` defaults to false. `pollIntervalSeconds` defaults to 7; the normal range is 5–10 seconds. `advancedSlowerPolling` permits intervals up to 86400 seconds, never below 5. `requestTimeoutSeconds` defaults to 15 (1–60). `offlineConfirmationPolls` defaults to 2 (2–10). `forwardTriggers` defaults to false. The editor exposes the interval, advanced mode, timeout, offline confirmations, startup enablement and forwarding switches. Persisted configuration changes require restarting the host.

The editor's Rumble panel accepts the Live API URL in a password field and clears it after submission, including failure. Session-only storage is the default: the credential stays in the host's `SensitiveValues` memory and is lost on restart. Clearing or replacing the credential establishes a new baseline. Persistent storage is available only through Windows DPAPI; other hosts return 501 when persistence is requested. No plaintext secret fallback exists. The URL, including its query, is never returned by status/configuration/export APIs or ordinary logs.

`POST /api/rumble/credential` takes `{ "value": "<private URL>", "sessionOnly": true }`. It starts polling for the current session even if the persisted `enabled` setting is false. Do not submit real credentials in shell arguments, chat, fixtures or Git. Only the official `https://rumble.com/-livestream-api/get-data` origin/path is accepted; custom ports, userinfo, fragments, redirects and other hosts are refused. `DELETE /api/rumble/credential` disconnects and removes both in-memory and Windows-vault values. All writes inherit host/origin validation, LAN authentication and CSRF protection.

`POST /api/rumble/reset-baseline` suppresses the next successful historical window; it does not erase permanent financial uniqueness or announce existing support as new. `GET /api/rumble/status` returns credential-presence, actual health, baseline, poll/next-poll times, failure count, live stream IDs and effective forwarding state, without the URL. `GET /api/rumble/deliveries` returns durable delivery outcome counts. Public request/response types are included in the OpenAPI snapshot and generated frontend definitions.

## Polling and parser

The production client uses an isolated `HttpClient` with redirects disabled and no URI-logging middleware. Responses are bounded to 16 MiB, including chunked bodies; JSON nesting is bounded to 32. Error/redirect bodies are never read. Transport failures, timeout, malformed JSON, oversized responses and invalid required stream structure change health, not offline state. A successful recovery resets the failure counter and resumes normal polling.

Normal delay is interval plus positive 0–10% jitter, after completing the request. Error backoff doubles from the configured interval to a 300-second base cap (advanced intervals longer than that are never shortened), then adds jitter. Numeric and HTTP-date Retry-After are honored even beyond that cap; negative/past dates do not reduce normal delay. Time is injectable for scheduling and replay.

The parser accepts unknown fields, redacts credentials before processing/persistence/inspection and preserves unknown source data in bounded inspector snapshots when raw retention is enabled. Known optional containers and bad records are handled conservatively. Required account and stream identity/live fields must establish a trustworthy snapshot; malformed stream lists cannot prove a disappearance. Strict timezone-aware dates normalize to UTC. Exact usernames and text are preserved; avatars do not establish identity. Badge representations are deterministically sorted while retaining multiplicity.

Only complete `recent_*` arrays establish events. `latest_*` and counters never independently create chat, support or follows. Viewer and like changes include zero. New streams have independent chat/Rant baselines; their online transitions are separate from historical entries.

## Durable snapshot reconciliation

State is scoped by credential context, observed account type/user/channel and stream, with separate live/replay/simulation namespaces. First successful configuration, startup, credential change or explicit reset baselines historical chat, Rants, follows and subscription candidates. Failed polls do not consume that pending baseline. Each optional recent collection also waits for its own first complete valid window; a missing/malformed array on startup cannot make later historical data alert. Initial live status is available immediately without an initial online alert.

Fingerprints use SHA-256 over JSON tuples rather than ambiguous delimiter concatenation. Chat includes scope, stream, UTC creation time, exact username/text and canonical badges. Rants include observed account/channel, stream, creation time, username, integer cents, exact text and expiry. Their identity deliberately survives credential rotation for the same observed account. Follows include account/channel context, username and followed timestamp. Missing or malformed identity fields produce diagnostics rather than invented identity/time.

Reconciliation retains maximum proven occurrence counts per core identity. Growth allocates additional occurrence indices; reorder, shrink, disappearance and reappearance do not reset indices. A secondary chat core key excludes mutable badges: badge-only changes produce `chatBadgeMutationAmbiguous`, with no new message at unchanged core multiplicity. The retained original full fingerprint plus occurrence index determines stable dedupe keys. These rules suppress ambiguous same-count replacements: without source IDs, a fresh message identical to an expired/edited entry is fundamentally indistinguishable. The application does **not** promise complete or exactly-once source delivery.

Chat history is pruned only when outside the newest 10000 retained core fingerprints **and** last seen more than 24 hours ago. This may retain more than 10000 occurrences and is intentionally conservative. Rant/support identity is never removed by chat pruning/reset. Previously accepted event uniqueness independently prevents duplicate support after store reopen or credential rotation. The G04 support event is an idempotent ledger input; financial aggregation/FX/ledger tables belong to G07.

`RumbleStates` migration stores snapshot state by credential context/provenance. A single SQLite transaction accepts new canonical events, general outbox rows, optional trigger-outbox rows, reconciliation state and poll checkpoint. Commit failure leaves all of them unchanged. The engine produces a new state without mutating the last committed state, and a failed commit never establishes an in-memory baseline. Existing G02/G03 data migrates in place; rollback/reapply remains qualified.

Previously live streams require the configured number of successful offline observations. Failed polls do not advance the count; successful live recovery clears it. Full valid consecutive chat windows with zero overlap produce `rumble.chat.possible_gap`, including previous/current counts, zero overlap and elapsed successful-poll interval. Only observed new entries are emitted; no missing messages are invented.

## Streamer.bot delivery and evidence gates

Forwarding requires both `rumble.forwardTriggers` and `streamerBot.forwardLiveEvents`. With either disabled, new polls do not enqueue automation. Canonical Rumble events map to the G03 registered trigger names and flatten explicit user/message/money/stream/metric fields, provenance, event/correlation ID and bridge path. Raw JSON is never forwarded. Replay/simulation never queue live triggers by default.

The trigger outbox is independent of browser delivery. Disconnection or missing discovery leaves rows pending; missing trigger discovery is refreshed at most every 30 seconds so late bootstrap registration can recover. Before an external send, the row is durably claimed as `dispatching`. Its result records acknowledged/rejected/uncertain outcomes. Startup/recovery changes interrupted claims to `uncertain` and never automatically reissues them. Acknowledgement proves server acceptance, not that arbitrary user action logic succeeded. The event ID is available to action authors for their own idempotency. Outcome counts and inspector diagnostics expose pending/uncertain delivery.

Observed Rants use integer `amount_cents` in USD with exact valuation. A conflicting dollars display yields `rantAmountConflict`; binary floating-point money is not used. Missing cents do not become invented money. Documentation-derived subscriptions emit `rumble.subscription.candidate` with an unverified-live label and no monetary object or authoritative trigger. Unknown gifts remain redacted snapshot evidence with `giftIdentityUnverified`. A `remaining_gifts` decrement is never another purchase. Authoritative subscription/gift automation and gift financial ingestion remain gated pending qualifying live identity evidence.

The [official API documentation](https://rumble.support/en/help/how-to-use-rumble-s-live-stream-api) supplements the observed empty subscriber/gift arrays, but does not prove stable gift purchase identity or historical replay support. The capture/equality/publication evidence is maintained in [Rumble analysis](rumble-analysis.md).

## Validation commands and current evidence

Scan source before execution and decompress/scan the captured fixture before .NET tests:

```sh
sonar analyze secrets src tests tools frontend/editor/src
python tools/prepare_rumble_replay.py
dotnet test TDSBLive.slnx -c Release --no-restore
npm run test:coverage
npm run typecheck
npm run lint
python -m unittest discover -s tests/replay -v
```

All 785 sanitized captured polls run through the actual parser/engine and temporary SQLite store. Expected post-baseline results are 97 chat messages, four follows, no new repeated Rant, one online and one debounced offline transition, and no zero-overlap gap. All 26 synthetic fixtures run through the same implementation, including actual database reopen on restart controls. Tests also cover transactional rollback, durable uncertain claims, retention, credential rotation, bounded/error HTTP transport and protected credential/reset endpoints. Fixtures are isolated replay provenance and never invoke local bots or alter production totals.

2026-10-01 live qualification: the operator entered the URL directly into the local session-only editor field. The upstream returned valid offline snapshots; a detected Rumble health event was delivered to real Streamer.bot 1.0.7. The dedicated synthetic receipt's native ID matched the original canonical event ID, verifying action execution rather than acknowledgment alone. No active stream was observed, so this check does not claim a live chat, paid Rant, subscription or gift interaction. Tests/qualification used a temporary data directory with raw retention disabled; only the namespaced receipt probe was bound.

The operator CLI's explicit `--bind-rumble-qualification` flag binds that safe probe to qualified Rumble triggers, never subscription/gift triggers. Its C# action accepts either the isolated G03 test ID or a canonical event ID, broadcasts a synthetic receipt and changes only a namespaced test marker. It sends no chat, controls no OBS scene, speaks no audio and changes no financial totals. Generated imports/native backups stay outside public Git. Native schema support remains qualified against Streamer.bot 1.0.7.

Final Windows/Sonar evidence and the goal status are recorded in [the implementation plan](implementation-plan.md#g04).

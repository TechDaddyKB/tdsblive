# G02 foundation qualification

Status: In progress. Final revision Windows checks and protected merge are pending.
G01 prerequisite is complete; G03–G13 remain separate goals.

## Acceptance traceability

| Requirement | Implementation | Qualification |
|---|---|---|
| HTTP host on 127.0.0.1:17474; no certificates | Host composition, binary-relative content root, editor assets | Isolated real Kestrel on 17474; editor/login/assets HTTP 200; conflict process exits 1 with actionable text; Linux SIGTERM exits 0 |
| Typed configuration and branding | Validated non-secret model, SQLite authority, atomic bootstrap fallback, restart semantics | Invalid/null/unknown fields rejected; reopened configuration survives missing fallback file; failed fallback update rolls back SQLite |
| Windows secret storage | Singleton per-user DPAPI vault; local-only provisioning, rotation | Real Windows encryption/reopen/replacement/delete/corruption test; no Linux plaintext fallback |
| Migrations, WAL and indexes | InitialFoundation and FoundationState EF migrations; foreign keys; provenance-scoped unique dedupe; timestamp/type indexes | Fresh/reopened database; down to zero/reapply; upgrade preserves initial events; integrity after forced crash |
| Canonical public contract | UUIDv7, UTC timestamps, normalized source/user/message/money/stream, redacted raw, provenance/correlation/bridge path | Contract validation and serialization tests; unknown safe fields retained; raw-off mode; docs/events.md |
| Transactional event acceptance | Event, checkpoint and outbox share a transaction | Eight competing accepts produce one event/outbox/checkpoint; forced failure rolls back all rows; duplicate advances checkpoint; reopen and idempotent delivery acknowledgment |
| Test/replay isolation | Separate provenance namespaces; simulation forced by test endpoint; persistence opt-in; no live adapters registered | Default test endpoint emits without persistence; persisted simulation excluded from live history after crash/restart; replay isolation tests |
| Logging and diagnostics | Structured redaction, 14-day SQLite/file retention, bounded independent SQLite log queue; health/failure counters | Known values, credential URLs/assignments removed; retention bounds; file I/O failure isolated; no exception message export |
| Isolated services and shutdown | Independent integration supervisors/retry, durable outbox, cancellable sockets; log drain then WAL checkpoint | Failed integration leaves host and healthy integration available; live outbox socket delivery; repeated checkpoint stop is idempotent |
| REST and generated frontend contract | Documented foundation endpoints; generated OpenAPI and TypeScript | Real host OpenAPI drift check; generator drift check; HTTP middleware and API tests |
| Bounded editor WebSocket | Subscribe/event/ping, type filtering, 32 connections, 256-frame queues, bounded incoming messages | Real TestServer WebSockets verify subscription, heartbeat, live outbox, filtering, simulation isolation and secret scrubbing |
| Browser shell | React editor/login, real host state, truthful disconnected integrations | Fresh Windows CI browser renders both pages, verifies host-ready response and no JavaScript errors; non-production screenshot artifact |
| Privileged endpoint security | Explicit host/origin checks, CSRF, bounded request bodies, login rate limit, revocable cookie/bearer authentication | Host/Origin/Sec-Fetch-Site rejection, CSRF rejection, session rotation/32-session concurrency test, logout; real Windows non-loopback HTTP authentication |

## Recorded evidence

- Public draft PR: https://github.com/camarokris/tdsblive/pull/3.
- Windows run 36845468111 passed runtime/browser/contract/report checks but failed
  Sonar due to PATH-based executable lookup. The qualifier now uses the absolute
  setup-dotnet installation directory.
- Windows run [36846286222](https://github.com/camarokris/tdsblive/actions/runs/36846286222)
  on `65d944c` passed runtime qualification; its Sonar gate failed on intentional
  HTTP/cookie transport findings. Two findings were accepted on 2026-10-01 with
  requirement-specific rationale. No broad security-rule exclusion was applied.
- Run 36846290263 was Advanced Security, not Windows CI; an earlier gate-pass
  attribution to that run was corrected. Final evidence uses the named workflow.
- Final SQLite persistence revision: local 41 backend tests pass, with two actual
  Windows tests intentionally skipped on Linux. Frontend's previous unchanged
  suite passed 14 tests. `tools/qualify_foundation.py` passes against the real
  Release host with temporary data and no production automation.
- All tracked/new source candidates passed `sonar analyze secrets`; runtime
  databases/configuration/credentials/logs, generated assets and reports are ignored.

## Public behavior and limits

See docs/foundation-api.md and docs/events.md for public contracts. Configuration
is authoritative in SQLite after initialization; the JSON file bootstraps a fresh
installation. Credential values are neither configuration fields nor REST-readable
secrets. HTTP LAN is explicitly authenticated but unencrypted. HTTPS is optional.

Outbox delivery means handoff to connected editor subscribers, not browser durable
acknowledgment or exactly-once external execution. Reconnect resubscribes and uses
REST live history; duplicate UUIDs around crashes are possible. An empty/no-match
subscriber set is still a completed editor handoff. Overlay delivery and permissioned
widget raw payloads remain G05/G12.

Logs before migration use the file sink. SQLite logging has a bounded asynchronous
queue and a two-second database busy timeout. Full queues or failed database writes
increment diagnostics and preserve the file fallback where available; logs are
best-effort diagnostics rather than transactional financial records. Shutdown
attempts to drain within its cancellation deadline.

No real Streamer.bot/Speaker.bot connection, Rumble polling, OBS rendering/audio,
financial ingestion, visual canvas editing or installer is claimed by G02. Those
capabilities retain their own acceptance and live-evidence gates. User Chrome
blocked localhost with ERR_BLOCKED_BY_CLIENT; its protections were untouched.
Rendered-shell evidence comes from a separate fresh Windows CI browser.

## Remaining completion work

- Pass final Windows CI and Sonar for the configuration/log persistence revision.
- Record final coverage/import evidence and any remaining non-blocking findings.
- Complete the protected PR merge and revalidate main; update G02 status/evidence.

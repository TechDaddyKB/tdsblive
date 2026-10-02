# Foundation HTTP and editor WebSocket API

Default origin: `http://127.0.0.1:17474`. HTTPS is not required. The configuration
file resides under `%LOCALAPPDATA%/TDSBLive`, separate from binaries and DPAPI
credentials. `TDSBLive__DataDirectory` overrides the data directory for isolated
tests. Configuration changes require restart. On first startup, `configuration.json`
provides the bootstrap settings; after migrations, SQLite holds the authoritative
non-secret configuration. Use PUT `/api/configuration` for subsequent changes.
Writes validate, update SQLite transactionally and replace the fallback file
atomically before committing. A crash before commit leaves the previous SQLite
configuration authoritative. Unknown fields, including credential fields, fail.

The host validates explicit hosts and same-origin requests. Writes require an
antiforgery cookie and `X-TDSBLive-CSRF` header obtained from `/api/auth/csrf`.
Cookie credentials use HttpOnly/SameSite Strict; they deliberately support HTTP.
There is no wildcard privileged CORS. WebSocket Origin is required and validated.
HTTP bodies are bounded at 64 KiB. Login permits ten attempts per client per minute.

LAN mode is explicit and requires a provisioned Windows DPAPI admin credential
before startup. Wildcard bind addresses need explicit `server.allowedHosts` for
the hostnames/IPs clients use. Non-loopback requests require bearer authentication
or an eight-hour revocable session. Unauthenticated editor requests redirect to
`/login`; only login, CSRF initialization and compiled public editor assets are
available before sign-in. HTTP LAN traffic is unencrypted. Linux has no plaintext
secret-store fallback; Windows qualification remains required.

| Route | Purpose |
|---|---|
| GET `/editor`, `/login` | React shell and sign-in |
| GET `/api/status` | Branding and truthful foundation/integration state |
| GET `/api/configuration` | Typed non-secret configuration |
| PUT `/api/configuration` | Validate/save; restart required |
| GET `/api/auth/csrf` | Cookie-bound write protection |
| POST `/api/auth/login`, `/api/auth/logout` | Create/revoke session |
| POST `/api/auth/provision` | Loopback-only Windows credential generation/rotation |
| POST/DELETE `/api/secrets/{name}` | Windows DPAPI credential update/delete; never read plaintext through REST |
| GET `/api/events?limit=100` | Live event history; limit 1–500 |
| POST `/api/test-event` | `{event, persist:false}`; isolated simulation |
| GET `/api/diagnostics`, `/api/diagnostics/logs` | Migration/event/outbox status and redacted daily log export |
| GET `/api/openapi/v1.json` | Generated HTTP contract |
| WS `/ws/editor` | Privileged bounded editor event stream |

## Setup and recovery

`GET /api/setup` returns `{step, reviewed, version}`. Steps range from 0 to 5.
`PUT /api/setup` saves progress using the current version; stale updates return
409. `reviewed:true` is valid only at step 5 and records the operator's review,
not integration verification. Setup writes require the usual authentication and
request protection. Progress is stored in SQLite and included in backups.

`POST /api/integrations/streamerbot/test` and
`POST /api/integrations/speakerbot/test` issue a correlated read-only `GetInfo`
request on the existing connection. They return a `BotConnectionState`, with
`connected` only after a successful response. Disabled/disconnected states are
reported without enabling a connection or changing saved settings. Failed probes
return `probeFailed` with a bounded failure kind; response bodies are not exposed.
They never speak, change queues, run actions or change the live connection state.
Writes require ordinary request protection. Save/restart connection settings
before testing; this does not probe unsaved form values. Streamer.bot documents
[`GetInfo`](https://docs.streamer.bot/api/websocket/requests#getinfo); Speaker.bot
0.1.7 supports it in the observed runtime, but older versions may reject optional
metadata while their documented speech/queue connection remains usable. Rejection
is a test limitation, not proof that those older runtimes cannot speak.

`POST /api/integrations/streamerbot/credential` accepts `{value, sessionOnly:true}`.
Session-only values stay in host memory and are cleared on restart. Setting
`sessionOnly:false` requires Windows DPAPI; other platforms return 501. Responses
never include the value. This operation does not execute actions or force a
reconnect; subsequent authentication uses the updated credential.

Recovery and process-control writes below additionally require a loopback peer,
even for an authenticated LAN operator:

| Route | Contract |
|---|---|
| GET `/api/application/status` | Host generation and queued operation |
| POST `/api/application/restart`, `/api/application/quit` | Queue one operation; stop after sending the response |
| POST `/api/recovery/backup` | Download a SQLite-consistent ZIP with assets and integrity manifest; excludes credential files |
| POST `/api/recovery/validate` | Upload raw ZIP bytes, at most 1 GiB; return `{id, expiresAt}` without replacing live data |
| POST `/api/recovery/restore` | `{id, confirm:true}`; requires the current checked archive, expires after 15 minutes |
| POST `/api/configuration/export` | Download non-secret connection settings; excludes overlays, financial records and assets |
| POST `/api/configuration/import` | Strict version-1 connection-settings document; disable imported integrations/LAN; restart required |

Backup archives are private: their database includes application history and
supporter records. Validation rejects unknown paths, incompatible schemas,
integrity failures and mismatched asset metadata. Restore drains and disposes the
host before replacing data, retains the old directory as a safety copy, disables
connections/rules, suppresses pending deliveries and revokes overlay tokens.
Existing local encrypted credentials remain local; they are not imported from
the archive. Restore failures stop without automatic relaunch. Successful
restart/restore preserves the original editor-launch preference.

Build the frontend with `npm run build` before starting the host. Assets are
generated into ignored `src/ExtensionSuite.Host/wwwroot/editor`. The HTTP contract
snapshot is `docs/contracts/openapi.json`; TypeScript definitions are generated
into `frontend/editor/src/generated/api-types.ts` using the separately locked
`tools/api-type-generator` package. Its TypeScript 5 peer requirement is isolated
from the application's TypeScript 6 toolchain. Restore with `npm ci --prefix
tools/api-type-generator`, then `npm run generate --prefix tools/api-type-generator`
after refreshing the snapshot from the running host. Contract drift verification
is checked by Windows CI against a real temporary-data host.

Structured redacted logs are retained for 14 days by default in SQLite and dated
JSONL files. Pre-migration startup diagnostics use the file sink. A failed file or
SQLite log write increments a diagnostic failure counter and does not terminate
the application. SQLite log writes use an independent 1,024-row bounded queue,
with up to 64 rows per transaction, so EF logging cannot block its own transaction. A full queue
or database failure leaves the file sink as fallback; shutdown drains the queue
before the final database checkpoint. Diagnostics expose file/SQLite write failure
counts and isolated integration health. Log messages omit credential values and exception messages.

## WebSocket operations

Send `{"op":"subscribe","types":["chat.message"]}`; the server replies
`{"op":"subscribed"}`. Exact event types are filtered; `"*"` selects all for the
privileged editor. A fresh connection starts with no subscriptions. Send
`{"op":"ping"}` to receive `{"op":"pong"}`. Event frames are
`{"op":"event","event":<CanonicalEvent>}` including provenance and correlation.

Limits: 32 concurrent connections, 128 selected types (128 characters each),
16 KiB inbound operation messages and 256 queued outbound frames per subscriber.
Invalid operations/frames terminate the session. Slow subscribers are disconnected
when their queue fills. Reconnect must resubscribe and reload REST history; repeat
UUIDs are possible around crashes. Widgets/overlay subscriptions, permissioned raw
access and the OBS overlay runtime are owned by G05/G12, not this admin channel.

Never include original capture data or credentials in API examples, fixtures,
diagnostic exports, screenshots or public documentation.

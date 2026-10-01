# Foundation HTTP and editor WebSocket API

Default origin: `http://127.0.0.1:17474`. HTTPS is not required. The configuration
file resides under `%LOCALAPPDATA%/TDSBLive`, separate from binaries and DPAPI
credentials. `TDSBLive__DataDirectory` overrides the data directory for isolated
tests. Configuration changes require restart; writes validate and replace the
file atomically. Unknown configuration fields, including credential fields, fail.

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

Build the frontend with `npm run build` before starting the host. Assets are
generated into ignored `src/ExtensionSuite.Host/wwwroot/editor`. The HTTP contract
snapshot is `docs/contracts/openapi.json`; TypeScript definitions are generated
into `frontend/editor/src/generated/api-types.ts` using the separately locked
`tools/api-type-generator` package. Its TypeScript 5 peer requirement is isolated
from the application's TypeScript 6 toolchain. Restore with `npm ci --prefix
tools/api-type-generator`, then `npm run generate --prefix tools/api-type-generator`
after refreshing the snapshot from the running host. Contract drift verification
is part of remaining G02 qualification.

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

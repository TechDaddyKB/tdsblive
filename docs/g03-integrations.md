# G03 bot integrations

TDSBLive connects independently to Streamer.bot and Speaker.bot over configurable `ws://` endpoints. Local HTTP is supported; HTTPS is not required. Integrations are disabled by default, including in CI. Enable them explicitly and restart after saving configuration. Defaults are Streamer.bot `127.0.0.1:8080/` and Speaker.bot `127.0.0.1:7680/`; the local qualification installation uses Speaker.bot port **7580**.

## Configuration and authentication

`GET /api/configuration` and CSRF-protected `PUT /api/configuration` expose the typed settings. Each integration has `enabled`, `host`, `port`, `endpoint`, `requestTimeoutSeconds` (1–60), `reconnectDelaySeconds` (1–60), and `maximumReconnectDelaySeconds` (initial delay–300). Streamer.bot additionally has `allowedActionIds` (up to 128 GUIDs) and `forwardLiveEvents`. Configuration changes require restart; credentials are never part of configuration responses.

Streamer.bot authentication follows the server Hello salt/challenge protocol. Store the password using CSRF-protected `POST /api/secrets/streamerbot-password`; Windows uses the G02 DPAPI secret vault and mirrors the value in memory. The Linux host returns 501 for credential provisioning; authentication is qualified through dependency-injected synthetic credentials in protocol tests. Linux has no plaintext persistence fallback. The installed Wine test server had WebSocket authentication disabled. Authentication failures are distinguished from connection failures. Speaker.bot's documented queue protocol does not define authentication; use loopback or a trusted restricted network. Its optional `GetInfo` metadata extension is not a required capability.

Requests have unique IDs, bounded concurrency (64), serialized sends and a whole-request timeout. Frames are limited to 1 MiB and JSON depth 32. An overloaded 1024-event queue closes the connection rather than silently dropping events. Reconnect uses independent bounded exponential delay with jitter. Event ingestion and request correlation run separately.

## Streamer.bot bootstrap and import

Build `python tools/streamerbot/build_import.py artifacts/tdsblive-g03.sb`. Scan source files before the command according to AGENTS.md. The generated artifact is ignored by Git. Import it using Streamer.bot's **Import** dialog, inspect the three namespaced actions, and enable/precompile their Execute C# sub-actions. The bootstrap's `Init` registers the Rumble, finance, overlay, and isolated test triggers. Import alone binds no platform automation.

The qualification probe is intentionally separate. Bind its action to **Custom → TDSBLive → Tests → Local qualification probe** when testing. It requires `tdsbliveTestId` to be a GUID, sets only `tdsbliveG03LastTestId`, and broadcasts a synthetic simulation. It never sends chat, controls OBS/VTube Studio, or speaks. Remove the probe binding after qualification if it is unnecessary.

Local live qualification installed the same C# action definitions into the stopped native action store, preserving all seven existing actions and a private backup, then restarted only Streamer.bot with its existing GE-Proton runner. Streamer.bot 1.0.7 compiled the bootstrap, registered all 13 triggers, and executed the bound probe through `ExecuteCodeTrigger`; its matching broadcast was observed both directly and by the actual TDSBLive host. This validates native action loading/compilation and registration; it does **not** claim that the graphical Import dialog was exercised. The generated `.sb` transport still needs that UI acceptance check. The explicit forwarder also compiled and executed through its selected GUID, emitting a synthetic Twitch chat message that the host classified as a simulation.

## Discovery, execution and VTube Studio

`GET /api/integrations` reports actual connection states and version when available. `GET /api/integrations/streamerbot/discovery` exposes `GetEvents`, `GetActions`, `GetCodeTriggers` results and capability flags; rejected optional requests are unsupported, not fabricated empty success. `POST .../discovery/refresh` refreshes capabilities.

`POST .../actions/execute` accepts `actionId`, `arguments`, and `executeLive` (default false). Live actions require an enabled discovered action whose GUID is in `allowedActionIds`. VTube Studio actions remain owned by Streamer.bot; select their GUIDs through this same allowlist. There is no parallel backend VTube Studio authority. `POST .../triggers/execute` accepts `eventName`, `arguments`, `executeLive`; live trigger dispatch requires `forwardLiveEvents` and a discovered registered code trigger. Its wire request uses `triggerName`, not a guessed method argument.

Results distinguish simulated, missing/disabled action, action not selected, forwarding disabled, missing trigger, unsupported capability, disconnected, rejected, acknowledged and uncertain. Acknowledgement means only that the server accepted the request. A send followed by timeout/disconnect/cancellation is uncertain and is never automatically retried. The latest 200 execution records are in memory; durable execution tracking belongs to G09. Reconnect does not reissue actions.

`TriggerArgumentMapper` flattens canonical events into `tdsbliveEventId`, correlation, origin, bridge-path JSON, provenance, platform/type/native type/time/native ID, user ID/login/name, message, amount minor units/currency/valuation kind and stream ID. Raw data is excluded. Subscription/gift registrations are explicitly evidence gated; G03 does not prove Rumble payload identities or authoritative gift support.

## Explicit forwarding and platform normalization

Event categories and exact names are discovered from the running server. TDSBLive subscribes to supported Twitch/YouTube/Kick/Ko-fi/General/Custom categories and Raw action diagnostics. Events that do not have a native WebSocket broadcast require a Streamer.bot action bound to that platform trigger and the `TDSBLiveForwardEvent.cs` template. Set `tdsbliveForwardedSource` (for example `Kofi`) and `tdsbliveForwardedType` (`Donation`) before that C# sub-action. Add a Newtonsoft.Json reference if the editor requires it. The template forwards only explicitly listed fields: messageId, userId/login/name, from, message, amount, currency, timestamp and isTest. Review provider-specific arguments rather than dumping every action argument.

`General.Custom` can carry a direct object (observed in 1.0.7) or a documented JSON-string wrapper. Both are supported. A forwarded origin/path that has returned to TDSBLive is rejected, and paths are bounded to 16 hops. Simulation/test flags always override a claimed live provenance.

Known chat/follow/subscription/gift/support/stream event names are classified conservatively. Native IDs provide dedupe identity; timestamped unknown envelopes use a hash, and receipt-only envelopes use distinct IDs to avoid suppressing legitimate identical messages. Unknown events retain credential-redacted diagnostics and an explicit uncertain-semantics limitation. Bits are nominal integer units. Other monetary data remains unknown valuation without invented decimal/currency precision or gift identity. Exact monetary ingestion and FX belong to G07. Repeated Rumble snapshots belong to G04, not this adapter.

## Speaker.bot protocol

`POST /api/integrations/speakerbot/speak`: voice alias, message (max 4096 characters), badWordFilter, executeLive (default false). `POST .../queue`: operation, optional value, executeLive. Supported operations: Pause, Resume, Clear, Stop, Enable, Disable, Events (`on`/`off`), Mode (`all`/`command`). No undocumented voice discovery or invented queue acknowledgements are exposed. Clear/Stop affect the real queue only after an explicit live request. Live qualification verified connection and Pause/Resume responses with Speaker.bot 0.1.7. Real audible output and automation policies are G09 evidence gates.

## Inspector and safe replay

The editor shows connection state, discovered actions/triggers, and a polling inspector with pause, search, sample inspection, copy, private-fixture download and isolated replay. HTTP copy falls back to a selected textarea when the Clipboard API is unavailable. Inspector storage is bounded to 500 entries / 2 MiB; payloads over 64 KiB are omitted and oversized entries become metadata-only. Disabling raw retention removes inspector payloads. Search is bounded to 128 characters and results to 200.

`GET /api/inspector`, `GET /api/inspector/{id}/fixture`, and CSRF-protected `POST /api/inspector/{id}/replay` own these operations. Replay assigns fresh IDs, forces replay provenance and defaults to no persistence. It never calls either bot or changes live financial totals. Optional explicit persistence stays in the replay namespace. Private fixtures and copies can contain personal capture content even after credential redaction: review before sharing; do not commit them. Fixtures are diagnostic records, not G00 public sanitized evidence. Public API schemas are in `docs/contracts/openapi.json` with generated frontend types; no database migration is introduced by G03.

## Validation

Local protocol and host tests cover correlation, event separation, authentication, discovery, timeout ambiguity, reconnect, unsupported/missing capabilities, allowlists, queue payloads, shutdown, redaction, bridge loops, normalization, bounds and replay isolation. Frontend tests cover polling, pause/search, samples, HTTP copy, private download, replay and error handling. Windows CI generates OpenCover and LCOV for SonarQube.

Explicit live repeat: after building the Release host and installing/binding the probe, run `python tools/qualify_bots.py --execute-local-test-trigger-and-queue --speaker-port 7580`. It uses temporary host data and only the dedicated trigger plus Speaker.bot Pause/Resume. Never include this command in unattended CI. Child output and credentials/nonces are not printed. `--refresh-contract` additionally refreshes the public OpenAPI snapshot, followed by the type-generator command.

Sources: [Streamer.bot WebSocket requests](https://docs.streamer.bot/api/websocket/requests), [authentication](https://docs.streamer.bot/api/websocket/guide/authentication), [RegisterCustomTrigger](https://docs.streamer.bot/api/csharp/methods/core/triggers/register-custom-trigger), [import/export](https://docs.streamer.bot/guide/core/import-export), [General.Custom](https://docs.streamer.bot/api/websocket/events/general/custom), [Speaker.bot queue requests](https://speaker.bot/api/websocket/requests).

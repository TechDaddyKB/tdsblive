# Visual editor and alerts (G06)

Status: implementation in progress. This document describes implemented contracts and remaining verification; it is not a completion claim.

The editor creates transparent multi-widget overlays at `/editor`. Presets are 1920×1080, 2560×1440, 3840×2160, 1080×1920, and custom dimensions (1–7680 pixels). A canvas has at most 100 widgets. Supported G06 widgets are Text, Image/GIF, Video, Audio, Combined Chat, and AlertBox. Layers use document order, with raise/lower, duplicate/delete, lock/hide, position/size/rotation properties, a 10-pixel grid, zoom and scroll. Keyboard controls include arrows (Shift for ten pixels), Ctrl/Cmd-Z, Shift-Z/Y, D, C/V, and Delete. Advanced multi-selection/grouping/alignment/distribution remain G11; arbitrary HTML/custom widget execution belongs to G12, and TTS/automation policies to G09.

The existing G05 `combined-chat` document remains a dedicated chat view. Additive `canvasEnabled`, `widgets`, and `revisionLimit` fields preserve compatibility with older documents. New canvases set `canvasEnabled: true`. The lightweight OBS runtime shares one socket across all canvas widgets; embedded chat does not open another connection. History delivery is tagged separately and feeds only chat, so even wildcard/chat-message AlertBoxes do not replay historical chat on source reload. Chat history still deduplicates UUIDs and reloads on reconnect. Separate OBS sources maintain independent queues and media playback.

## Persistence and public API

- `GET /api/overlays`: editor document list.
- `POST /api/overlays`: create a validated version-one document; duplicate IDs return 409.
- `GET/PUT /api/overlays/{id}`: document read/save; writes require the current version and return the incremented version. Stale writes return 409.
- `GET /api/overlays/{id}/revisions`: newest-first bounded revision metadata.
- `POST /api/overlays/{id}/revisions/{version}/restore`: body `{ expectedVersion }`; restore creates a new revision, never rewinds the current version. Missing/pruned revisions return 404, stale expectations 409.
- `POST /api/overlays/{id}/preview-events`: isolated test injection, described below.
- `GET/HEAD /assets/{sha256}`: validated assets, including media MIME inspection and range playback.

SQLite migration `20261001175602_VisualEditorRevisions` adds `OverlayRevisions`, keyed by overlay and version with cascade deletion. Initialization backfills an existing document's current revision. Each save transaction updates the document conditionally, inserts its revision, prunes older revisions and commits together. Retention defaults to 50, configurable from 1–200 per overlay, including the current revision. Restore validates retained asset references before saving.

Autosave debounces by 750ms. Edits during an in-flight write stay local and save next using the returned version. Undo/redo retain the newest persisted version. Version conflicts stop automatic retries and retain local edits; explicit reload resolves them. Switching documents or previewing flushes pending edits. The editor displays unsaved/saving/saved/conflict/error state and guards page unload while unsaved. Overlay JSON requests are bounded at 2 MiB; preview injection retains the 64 KiB limit.

## Alerts

AlertBox filters normalized event types and platform names. Presets use the actual community/support/integration contracts, including Twitch follows/subscriptions/gifts/Bits, YouTube paid/member events, Kick subscriptions, Ko-fi donations, Rumble Rants/follows, and custom Streamer.bot events. Rumble subscription/gift presets are explicitly unverified; they cannot establish upstream support or bypass G04's gated ingestion.

Templates substitute escaped `{user}`, `{type}`, `{platform}`, and `{message}` text. Alert media accepts validated image/GIF/video asset IDs; sound accepts audio IDs. Duration is 100–300000ms, priority −100–100, admission cooldown 0–3600000ms, group concurrency 1–8, and pending group limit 1–200. Group widgets must share concurrency, pending limit and overflow policy. Priority orders pending jobs, with FIFO ties. Overflow drops the oldest pending job or incoming job, as configured. Higher-priority interruption is opt-in and can interrupt only a lower-priority, interruptible active job. Groups can run independently.

The runtime deduplicates widget/event pairs with a bounded 10000-entry window. Cooldown starts at accepted admission. Completion/removal unmounts media and stops playback; changed widget settings cancel that widget's active/pending jobs and reset stale cooldown policy. Reconnect does not replay old support alerts. Image/media fetch failures and browser autoplay restrictions are reported visibly. OBS must permit playback and have the Browser Source's audio routing configured; software playback alone does not prove real sound output.

## Isolation and security

Preview links use `?preview=1`; preview audio is silent unless explicitly enabled. Normal sources reject simulation/replay, and limited LAN viewers cannot subscribe to simulations. Test injection targets only the selected overlay's non-limited preview sockets and never writes events, outbox, finance, or integration dispatch tables.

Synthetic mode accepts `type`, `platform`, `user`, `message`, and optional diagnostic `raw`. Developer native mode accepts `mode: "native"` plus a Streamer.bot `event`/`data` envelope in `raw`. It runs the existing redacting normalizer, replaces IDs/routing provenance with an isolated simulation, and publishes only to preview. Submitted live/test flags cannot enable production effects. The raw object never executes markup or scripts. Credential redaction precedes delivery. Editor writes require CSRF and admin LAN authentication; viewing tokens retain read-only per-overlay scope, including only referenced font/media assets. HTTP is supported; HTTPS is not required.

## Verification evidence

Windows CI run [36915186896](https://github.com/camarokris/tdsblive/actions/runs/36915186896) passed at commit `c6478b5135b5ff7f2e87ee12df54a7ef5c71135d`, including Windows build/tests, recovery, replay, browser qualification, coverage import and SonarQube quality gate. SonarQube new-code coverage is 87.8%, with A reliability/security/maintainability and zero new duplication. The operator confirmed successful real OBS alert video/audio playback on 2026-10-01 using the isolated qualification source. Later commits must also satisfy required checks before merge.

The isolated browser runs passed create, drag/resize, keyboard nudging, repeated duplicate/delete/undo/redo, autosave/reload/restore, preview and synthetic/native alert behavior, one socket, and no durable test history. The additional canvas audit passed reconnect without completed-alert replay, actual clipboard OBS URL verification, and live revision restore in a separately connected overlay, including removal of obsolete image/chat widgets. Backend revision/isolation tests and scheduler tests pass. Actual served GIF rendering, VP9 decoding, muted PCM audio playback, scoped LAN media and component tests pass. Real OBS video/audio evidence is now supplied by the operator confirmation above. G06 acceptance is verified; passing Windows/SonarQube checks remain required on the final commit before protected merge.

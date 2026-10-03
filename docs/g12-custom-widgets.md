# Custom widgets and portable packages

G12 implements source-spec sections 46, 48, 70, 71, 94 and 99's custom-code
boundary. G11 is the prerequisite; protected PR #15 is merged as `870222d` with
passing Windows and Sonar checks. G13's StreamElements shim remains separate.

## Authoring and settings

Add **custom** in the visual editor. Local, lazily loaded Monaco edits HTML,
CSS, JavaScript and a Settings JSON array without a CDN. Version-1 manifests
include a semantic `packageVersion`, author, source, fields, configured values,
subscriptions, explicit permissions, exact HTTPS domains and declared asset IDs.
Existing overlay revisions preserve custom source and configuration.

Fields use `{ "key": "label", "label": "Label", "type": "text" }` entries;
values live separately in `config`. The G11 typed renderer handles all field
types. Group nesting is limited to four levels, with at most 100 unique keys.
Subscriptions accept canonical names, arbitrary available/future event names,
or `*`. Discovery offers current Streamer.bot native names; normalization may
map these to canonical names, so choose the canonical type shown in the event
inspector for normalized events. Neither subscriptions nor discovery grant
permission to run bot actions.

## Execution and API

Each visible custom widget owns an `allow-scripts` iframe with an opaque origin,
no same-origin/top-navigation/popup/form grants and a fresh frame capability.
Its trusted renderer runs custom JavaScript in a dedicated worker with a
LinkeDOM virtual `document` and `window`. The browser compiles the custom program
inside a dedicated blob worker; the sandbox does not grant `eval`, `Function`
or `unsafe-eval`. HTML script tags/event attributes, links, nested frames,
objects and unauthorized media are removed. Direct
browser DOM access is intentionally unavailable. Custom code uses virtual DOM
methods such as `getElementById`, `querySelector`, `textContent`, `innerHTML`
and DOM event listeners; the renderer preserves unchanged media nodes during
updates. Click/input/change listeners on elements with IDs are mediated back
to the virtual DOM. Layout measurements, browser navigation, real Window APIs,
external script imports and direct media `.play()` are unsupported. This is
a security boundary, not a full browser/StreamElements compatibility claim.

```javascript
window.addEventListener('sbx:load', event => {
  document.getElementById('message').textContent = event.detail.config.label;
});
SBX.on('chat.message', event => {
  document.getElementById('message').textContent = event.message?.text ?? '';
});
const config = SBX.getConfig();
const session = SBX.getSession(); // connected and preview; no tokens or ledger
const state = await SBX.store.get();
await SBX.store.set({ count: (state.count ?? 0) + 1 });
```

Top-level `await` requires an async function. `SBX.on` returns an unsubscribe
function. `sbx:load`, `sbx:event`, `sbx:session`, and `sbx:config` lifecycle
events carry `detail`. `SBX.render(html)` replaces the virtual body. Storage
promises reject descriptively on denied permission, busy/unavailable storage,
invalid values or timeout. State is one JSON object per overlay/widget identity,
limited to 32 KiB; setting it replaces the previous object. Production and
preview stores are separate. State survives process restart and full backup
restore, but is deliberately excluded from portable packages. New/copied/
imported widget identities get independent stores.

## Capabilities and networking

All custom permissions default off:

| Permission | Granted capability |
| --- | --- |
| `chat` | Receive subscribed chat events |
| `financial` | Receive support/financial events and monetary/support facts |
| `raw` | Receive recursively credential-redacted raw event fields |
| `storage` | Read/write this widget's scoped persistent object |
| `audio` | Render declared audio/video media; silent preview still removes this grant |
| `network` | HTTPS fetch/images/media to explicitly named domains, subject to media permission |

Networking defaults disabled by CSP in both frame and inherited blob worker.
Exact lowercase DNS names only: no wildcards, ports, IP literals, localhost or
local/internal suffixes. There are no external script origins. The worker has
no frame navigation API; sanitized HTML cannot navigate the frame either. This
avoids the self-navigation gap in iframe-only CSP implementations. Explicit
network grants permit data transmission to those domains; review widget code
before granting them. The application origin and privileged endpoints are
never automatically added to the network allowlist.

Declared assets are fetched by the parent through authenticated headers and
passed as bounded data URLs that work in opaque frames. Use an asset ID as an image/audio/video `src`, or
`sbx-asset:ASSET_ID` in CSS URLs. Tokens, cookies, CSRF values and host credential
configuration are not sent to the worker or its source document. The parent
validates exact frame identity, opaque origin, fresh capability, operation,
payload size and current storage permission. It rejects sibling/spoofed/stale
messages. Declared media data is bounded to 64 MiB per widget. Rendering is bounded to 128 KiB/2,000 elements and worker messages to
60/second; storage permits at most eight concurrent requests.

## Portable containers

**Export overlay** creates `.sbxoverlay`; **Export selected widget** creates
`.sbxwidget`. **Import portable package** imports a full overlay as a new overlay,
or appends a widget to the selected overlay. Widget IDs and group IDs are remapped
on import, with group relationships retained. Custom permissions/network grants
are disabled on import; review source and opt into each capability explicitly.

ZIP containers include `manifest.json`, `widgets/UUID.json`, referenced
`assets/SHA256`, and `overlay.json` for overlay packages. Format version is 1;
unknown versions are rejected. Packages exclude widget state, revisions, access
tokens, integration credentials and application configuration. Credential-bearing
JSON and known sensitive values are rejected, as are rooted filesystem path
values. Do not put credentials into custom code/configuration.

Admission validates the whole archive before persisting objects: at most 32 MiB
compressed, 64 MiB expanded, 512 entries, 20 MiB per asset and 2 MiB per JSON
member. Streaming reads verify actual expanded lengths. Traversal, absolute
member paths, backslashes, drive paths, symlinks, duplicate/case-colliding names,
unexpected members, unresolved assets, invalid MIME/signatures, modified asset
hashes and contradictory widget definitions are rejected. Nothing is extracted
using archive-supplied filesystem paths. Asset admission reuses the application's
SVG/font/license checks. A late import conflict can leave unreferenced,
content-addressed asset metadata; no overlay is overwritten and retry is safe.

## Validation record

2026-10-03: focused real-browser qualification passes local Monaco model editing,
opaque frame/worker execution, blocked network/parent/navigation/cookie access,
arbitrary available-event delivery, preview-isolated persisted state, spoof
rejection and both package UI round-trips. Actual-process restart and backup
restore pass custom code/state restoration with the overlay browser reconnecting.
Local backend admission/storage/package tests pass. Source `416db6f` passes
Windows run [37104260203](https://github.com/TechDaddyKB/tdsblive/actions/runs/37104260203),
including native portable/installed EXE recovery, 482 backend tests without skips,
201 frontend tests and the Sonar quality gate (80.7% new-code coverage).
CodeQL passes. Actual OBS renders owned custom text, persisted state, image and
playing video; actual host restart/backup restore retain custom code and state.
Prior OBS state is restored and owned sources/hosts removed. The operator confirms the owned OBS tone was heard, closing the sound gate.
Documentation checkpoint `c86b45d` also passes Windows CI run 37105481853,
SonarQube and CodeQL. G12 is Complete; the implementation plan records the
acceptance evidence.

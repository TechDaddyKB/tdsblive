# UI redesign parity and acceptance checklist

G14 baseline: source inventory on 2026-10-04, before UI changes. These rows are
release gates, not claims that the redesigned implementation has passed.

| Existing capability | Destination | Qualification |
| --- | --- | --- |
| First-run review, timezone, branding, bot addresses/authentication, read-only tests | Overview / guided setup | Setup persistence, flags remain off; connection-probe tests |
| Streamer.bot/Speaker.bot status, connect/disconnect, permissions, actions/triggers discovery | Connections | BotPanel/BotPermissions tests; real read-only discovery |
| Event search/pause/sample/copy/private fixture/sanitized shape/isolated replay | Diagnostics | Inspector security/replay tests; owned sample browser workflow |
| Rumble credential, session-only storage, polling/reconnect, reset baseline | Connections | Existing Rumble settings/tests; never expose credential |
| Create/rename/select canvas, landscape/portrait/custom sizes, IDs | Overlays & Alerts | VisualEditor tests; create/reload/unique slug/size boundaries |
| All 15 widget kinds, assets and typed settings | Overlays & Alerts | Render and edit each kind, reload settings |
| Text/media/chat/alert/supporter/event-list/progress/custom properties | Properties tabs | WidgetProperties, SettingsFields, DonorProperties, CustomProperties tests |
| Position/size/rotation/grid/snap/zoom/pan, drag and resize | Canvas / View / Placement | advanced-editor qualifier, touch and resize checks |
| Multi-selection/group/ungroup/copy/paste/duplicate/delete | Layers / Arrange | sceneOperations and advanced-editor tests |
| Align/distribute/rotate/front/back/raise/lower/lock/hide | Arrange / Layers | Existing geometry assertions and keyboard checks |
| Keyboard undo/redo/nudge/select/group/copy/paste/delete | Canvas | Existing shortcuts plus form-field exclusion |
| Autosave/manual save/dirty/error/conflict/flush/reload/retention/restore | Global save / More | EditorSession tests; concurrent save and revision qualification |
| Alert filters/template/media/sound/volume/animation | Alert setup | Legacy defaults round-trip; event/platform filters |
| Alert groups/priority/duration/cooldown/concurrency/overflow/interruption | Alert behavior / Advanced | AlertQueue tests and live runtime reconnect tests |
| Synthetic/native preview/raw injection/audio/OBS URL/copy fallback | Test / OBS / Advanced | isolated preview and HTTP clipboard qualifier |
| Package import/export/new identities/permissions/state/assets/licensing | More / Media | PortablePackages tests, v1/v2 and reference remapping |
| Custom Monaco HTML/CSS/JS/JSON/manifest/fields/settings/subscriptions | Custom properties | custom-widgets qualifier and Monaco tests |
| Storage/chat/financial/raw/audio/network permissions/domains/assets | Custom Advanced | Sandbox and permission tests; import grants stay disabled |
| StreamElements lifecycle/fieldData/events/queue/status/local store | Custom Advanced | compatibility qualifier; limited compatibility labels |
| Combined chat title/font/colors/platforms/avatars/badges/duration/order/filters | Chat | CombinedChat/ChatPanel tests, actual OBS rendering |
| Upload media/fonts/license/refresh/select/playback | Media / shared picker | Asset validation, GIF/video/audio/font qualification |
| Contributions/totals/filters/periods/current stream/metadata/valuation | Supporters | financial qualifier; exact/estimated/unknown preserved |
| Currency/FX/manual rates/rules/identities/reconciliation/exclusion | Supporters / Advanced | FinancialControls/Rules/Identities and backend tests |
| Rules/actions/order/delete/enable/conditions/queue/cooldown | Automation | automation qualifier; new rules disabled |
| Speech voice/template/moderation/language/safety, sound variants/volume/timeout | Automation | speech/sound/receipt tests; no unintended live dispatch |
| Allowlisted Streamer.bot action/timer/revert/stack policies/effects | Automation | TemporaryEffects tests; VTube Studio-specific work held |
| Rule simulation/receipts/moderation approve/temporary diagnostics | Automation | simulation/receipt tests, isolation assertions |
| Backup/download/upload validation/restore confirmation/restart/configuration transfer/startup | Settings | Maintenance tests and native package recovery qualifier |
| Authenticated LAN/admin/overlay tokens/expiry/revocation/port/hosts | Settings | LAN/auth tests, scoped asset/replay restrictions |
| Login/logout/HTTP without secure-context APIs/diagnostic API | Settings / Diagnostics | HTTP LAN checks; existing endpoint compatibility |

## Baseline and evidence

Baseline commit: `e590c8eddede11eee9fd037a424b8a8c448b60d8`. A detached,
scanner-approved checkout was built with the pinned stack. Separate temporary
hosts and owned text widgets were used for both builds; integrations remained
disabled. Measurements below are single observations in Chromium 1.63.0's
Playwright distribution on Linux, not statistical performance claims. Load
includes navigation and selection of the sample canvas. Draft and resize timings
include two animation frames; panel opening is included in draft timing.

| Size | Baseline load / draft / resize (ms) | Candidate load / draft / resize (ms) | Horizontal document overflow, baseline → candidate |
| --- | --- | --- | --- |
| 1366×768 | 141 / 41 / 33 | 111 / 58 / 35 | No → No |
| 768×1024 | 24 / 29 / 33 | 24 / 63 / 35 | No → No |
| 390×844 | 22 / 29 / 33 | 20 / 63 / 35 | Yes → No |

Owned baseline/current captures are ignored local evidence in
`artifacts/ui-redesign/measure-baseline/` and `measure-current/`. The historical
baseline visibly uses widget-name placeholders; the candidate renders the draft.
No browser JavaScript errors occurred in either measurement run. Current source
commit is `9b00d4b`; documentation/screenshot commit is `df2406e`.

Every row above retains its destination and verification case. The current
full-browser suite exercises existing G02/G05–G09/G11–G13 assertions plus the
redesign scenarios; frontend/Core/Host regression tests cover the remaining
settings, permissions, recovery and data contracts. This does not replace native
Windows, actual audible output, manual screen-reader/zoom or participant evidence.
See [the current qualification record](ui-redesign-qualification.md) for exact
commands, evidence boundaries and the mandatory unresolved release checklist.

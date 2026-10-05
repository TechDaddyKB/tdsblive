# Source requirement matrix

This audit preserves all 99 numbered source-spec sections without publishing the
private reference. Titles identify sections; the linked public documents contain
reviewed implementation contracts and acceptance evidence. **Verified** refers to
the implemented capability and bounded evidence, not universal live-platform or
native streaming-PC verification. Individual limits below take precedence.

G00–G13 are Complete in the implementation plan within the recorded scope.
Current G13 browser/recovery/OBS/native-package/quality evidence is recorded in
[g13-compatibility.md](g13-compatibility.md). Historical evidence is labelled by owning goal; it
does not silently replace current G13 regression checks. No VTube Studio, paid
delivery or performance evidence limitation is hidden by a passing unit test.

| Section | Requirement | Classification | Evidence and limits |
| --- | --- | --- | --- |
| 01 | Project Objective | Verified | [architecture.md](architecture.md): Local hybrid architecture, pinned .NET/React toolchains and monorepo; VTube Studio clauses are deferred. |
| 02 | Core Architectural Decision | Verified | [architecture.md](architecture.md): Local hybrid architecture, pinned .NET/React toolchains and monorepo; VTube Studio clauses are deferred. |
| 03 | Preferred Technology Stack | Verified | [architecture.md](architecture.md): Local hybrid architecture, pinned .NET/React toolchains and monorepo; VTube Studio clauses are deferred. |
| 04 | Repository Layout | Verified | [architecture.md](architecture.md): Local hybrid architecture, pinned .NET/React toolchains and monorepo; VTube Studio clauses are deferred. |
| 05 | Streamer.bot Integration | Verified | [g03-integrations.md](g03-integrations.md): Real local bot discovery/bridge and inspector; arbitrary bindings additionally qualified in G12. |
| 06 | Streamer.bot Custom Trigger Bridge | Verified | [g03-integrations.md](g03-integrations.md): Real local bot discovery/bridge and inspector; arbitrary bindings additionally qualified in G12. |
| 07 | Rumble Integration | Verified | [rumble-analysis.md](rumble-analysis.md): 785-poll evidence and sanitized replay were committed before production adapter code; original secret/archive members excluded. |
| 08 | Rumble Credentials | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 09 | Rumble Polling | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 10 | Rumble Snapshot-to-Event Engine | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 11 | Rumble Chat Deduplication | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 12 | Gap Detection | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 13 | Rumble Rant Deduplication | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 14 | Follow Deduplication | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 15 | Subscriber and Gifted Subscriber Handling | Blocked by evidence | [rumble-analysis.md](rumble-analysis.md): No captured live subscriber/gift identity; tolerant candidates and diagnostics exist, authoritative money/actions remain gated. |
| 16 | Stream Online/Offline Detection | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 17 | Canonical Internal Event Envelope | Verified | [foundation-api.md](foundation-api.md): Canonical UTC UUIDv7 envelopes, provenance and redacted raw persistence. |
| 18 | Event Router | Verified | [g05-overlays-chat.md](g05-overlays-chat.md): Four-platform normalized/owned chat, bounded runtime, routing/reconnect and real OBS evidence; actual platform delivery is separately limited. |
| 19 | Arbitrary Streamer.bot Event Support | Verified | [g03-integrations.md](g03-integrations.md): Real local bot discovery/bridge and inspector; arbitrary bindings additionally qualified in G12. |
| 20 | Event Inspector | Verified | [g03-integrations.md](g03-integrations.md): Real local bot discovery/bridge and inspector; arbitrary bindings additionally qualified in G12. |
| 21 | Multi-Platform Combined Chat | Verified | [g05-overlays-chat.md](g05-overlays-chat.md): Four-platform normalized/owned chat, bounded runtime, routing/reconnect and real OBS evidence; actual platform delivery is separately limited. |
| 22 | Combined Chat Widget | Verified | [g05-overlays-chat.md](g05-overlays-chat.md): Four-platform normalized/owned chat, bounded runtime, routing/reconnect and real OBS evidence; actual platform delivery is separately limited. |
| 23 | Financial / Supporter Ledger | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Exact/nominal/unknown valuation, integer money, dated FX, manual identities and timezone-based periods; actual paid delivery remains unverified. |
| 24 | Financial Event Schema | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Exact/nominal/unknown valuation, integer money, dated FX, manual identities and timezone-based periods; actual paid delivery remains unverified. |
| 25 | Subscription Valuation | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Exact/nominal/unknown valuation, integer money, dated FX, manual identities and timezone-based periods; actual paid delivery remains unverified. |
| 26 | Bits | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Exact/nominal/unknown valuation, integer money, dated FX, manual identities and timezone-based periods; actual paid delivery remains unverified. |
| 27 | Currency Conversion | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Exact/nominal/unknown valuation, integer money, dated FX, manual identities and timezone-based periods; actual paid delivery remains unverified. |
| 28 | Supporter Identity | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Exact/nominal/unknown valuation, integer money, dated FX, manual identities and timezone-based periods; actual paid delivery remains unverified. |
| 29 | Leaderboard Periods | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Exact/nominal/unknown valuation, integer money, dated FX, manual identities and timezone-based periods; actual paid delivery remains unverified. |
| 30 | Donor Crown Widget | Verified | [g08-donor-widgets.md](g08-donor-widgets.md): Crown/rank/latest snapshots, filters, identity aggregation, loaded assets, transitions and OBS updates. |
| 31 | Leaderboard Widget | Verified | [g08-donor-widgets.md](g08-donor-widgets.md): Crown/rank/latest snapshots, filters, identity aggregation, loaded assets, transitions and OBS updates. |
| 32 | Ko-fi Donation TTS | Verified | [g09-automation.md](g09-automation.md): Generic allowlisted rules, TTS safety/moderation, durable execution, browser sound and owned audible receipts; paid delivery unverified. |
| 33 | TTS Safety | Verified | [g09-automation.md](g09-automation.md): Generic allowlisted rules, TTS safety/moderation, durable execution, browser sound and owned audible receipts; paid delivery unverified. |
| 34 | Twitch Bits → VTube Studio | Deferred by explicit scope | [implementation-plan.md](implementation-plan.md): Operator placed all VTube Studio work on hold on 2026-10-02; use Streamer.bot built-in integration. No real-model verification claim. |
| 35 | Temporary VTube Studio Toggles | Deferred by explicit scope | [implementation-plan.md](implementation-plan.md): Operator placed all VTube Studio work on hold on 2026-10-02; use Streamer.bot built-in integration. No real-model verification claim. |
| 36 | Bits → Sound Alerts | Verified | [g09-automation.md](g09-automation.md): Generic allowlisted rules, TTS safety/moderation, durable execution, browser sound and owned audible receipts; paid delivery unverified. |
| 37 | Alert System | Verified | [g06-editor-alerts.md](g06-editor-alerts.md): HTTP overlays, presets, media/assets, alert queues, isolated preview and revisions; G11/G12 extend editor and portability. |
| 38 | Alert Queue | Verified | [g06-editor-alerts.md](g06-editor-alerts.md): HTTP overlays, presets, media/assets, alert queues, isolated preview and revisions; G11/G12 extend editor and portability. |
| 39 | Overlay Server | Verified | [g06-editor-alerts.md](g06-editor-alerts.md): HTTP overlays, presets, media/assets, alert queues, isolated preview and revisions; G11/G12 extend editor and portability. |
| 40 | Overlay Definitions | Verified | [g06-editor-alerts.md](g06-editor-alerts.md): HTTP overlays, presets, media/assets, alert queues, isolated preview and revisions; G11/G12 extend editor and portability. |
| 41 | WYSIWYG Overlay Editor | Verified | [g11-advanced-editor.md](g11-advanced-editor.md): Repeated advanced interactions, typed controls, built-in widgets, save/reload and actual OBS qualification; custom widget contracts in G12. |
| 42 | Editor Autosave | Verified | [g06-editor-alerts.md](g06-editor-alerts.md): HTTP overlays, presets, media/assets, alert queues, isolated preview and revisions; G11/G12 extend editor and portability. |
| 43 | Built-In Widget Types | Verified | [g11-advanced-editor.md](g11-advanced-editor.md): Repeated advanced interactions, typed controls, built-in widgets, save/reload and actual OBS qualification; custom widget contracts in G12. |
| 44 | Widget Package Manifest | Verified | [g12-custom-widgets.md](g12-custom-widgets.md): Local Monaco, manifest/settings/capabilities, opaque sandbox, durable store and bounded ZIP admission with real browser/OBS/native Windows evidence. |
| 45 | Widget Settings Schema | Verified | [g11-advanced-editor.md](g11-advanced-editor.md): Repeated advanced interactions, typed controls, built-in widgets, save/reload and actual OBS qualification; custom widget contracts in G12. |
| 46 | Custom Code Widget | Verified | [g12-custom-widgets.md](g12-custom-widgets.md): Local Monaco, manifest/settings/capabilities, opaque sandbox, durable store and bounded ZIP admission with real browser/OBS/native Windows evidence. |
| 47 | StreamElements Compatibility Layer | Verified | [g13-compatibility.md](g13-compatibility.md): Opt-in lifecycle, fieldData, canonical listener/event envelopes, permission-scoped local keyed storage and bounded queue/status; browser/OBS/native regression evidence. No full remote compatibility claim. |
| 48 | Widget Sandboxing | Verified | [g12-custom-widgets.md](g12-custom-widgets.md): Local Monaco, manifest/settings/capabilities, opaque sandbox, durable store and bounded ZIP admission with real browser/OBS/native Windows evidence. |
| 49 | Asset Library | Verified | [g06-editor-alerts.md](g06-editor-alerts.md): HTTP overlays, presets, media/assets, alert queues, isolated preview and revisions; G11/G12 extend editor and portability. |
| 50 | Overlay Event Transport | Verified | [g05-overlays-chat.md](g05-overlays-chat.md): Four-platform normalized/owned chat, bounded runtime, routing/reconnect and real OBS evidence; actual platform delivery is separately limited. |
| 51 | Overlay Preview/Test System | Verified | [g06-editor-alerts.md](g06-editor-alerts.md): HTTP overlays, presets, media/assets, alert queues, isolated preview and revisions; G11/G12 extend editor and portability. |
| 52 | Database Tables | Verified | [database.md](database.md): EF SQLite tables or documented equivalent aggregate columns/JSON; WAL, foreign keys and tracked migrations. |
| 53 | Financial Event Constraints | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Durable financial uniqueness and indexed SQL aggregation; no in-memory whole-ledger leaderboard calculation. |
| 54 | Rumble Persistence | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 55 | Diagnostics Dashboard | Verified | [g13-compatibility.md](g13-compatibility.md): Measured poll latency/interval, viewers, accepted-event count, persisted duplicate/gap counters and aggregate-only diagnostic export; host/UI/browser privacy tests pass. |
| 56 | Rumble Raw Inspector | Verified | [g13-compatibility.md](g13-compatibility.md): Bounded private observed/unknown/changed type inspection and shape-only fixture export; scalar values and unknown field names removed from public-safe output. |
| 57 | Import Existing Rumble Recorder Data | Verified | [g13-compatibility.md](g13-compatibility.md): Production-engine replay of all 785 captured polls, chronological UTC JSONL/gzip admission, decompressed scanning and size limits; no persistence or live actions. |
| 58 | Replay Testing | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 59 | Automation Rules | Verified | [g09-automation.md](g09-automation.md): Generic allowlisted rules, TTS safety/moderation, durable execution, browser sound and owned audible receipts; paid delivery unverified. |
| 60 | Streamer.bot Actions | Verified | [g09-automation.md](g09-automation.md): Generic allowlisted rules, TTS safety/moderation, durable execution, browser sound and owned audible receipts; paid delivery unverified. |
| 61 | Sound Alert Example | Verified | [g09-automation.md](g09-automation.md): Generic allowlisted rules, TTS safety/moderation, durable execution, browser sound and owned audible receipts; paid delivery unverified. |
| 62 | VTube Studio Example | Deferred by explicit scope | [implementation-plan.md](implementation-plan.md): Operator placed all VTube Studio work on hold on 2026-10-02; use Streamer.bot built-in integration. No real-model verification claim. |
| 63 | Rumble Alert Example | Verified | [g09-automation.md](g09-automation.md): Generic allowlisted rules, TTS safety/moderation, durable execution, browser sound and owned audible receipts; paid delivery unverified. |
| 64 | Security | Verified | [security.md](security.md): Loopback default, authenticated LAN, CSRF/origin/host checks, overlay tokens, DPAPI and redacted structured logs; HTTP remains supported. |
| 65 | CORS | Verified | [security.md](security.md): Loopback default, authenticated LAN, CSRF/origin/host checks, overlay tokens, DPAPI and redacted structured logs; HTTP remains supported. |
| 66 | Secret Handling | Verified | [security.md](security.md): Loopback default, authenticated LAN, CSRF/origin/host checks, overlay tokens, DPAPI and redacted structured logs; HTTP remains supported. |
| 67 | Logging | Verified | [security.md](security.md): Loopback default, authenticated LAN, CSRF/origin/host checks, overlay tokens, DPAPI and redacted structured logs; HTTP remains supported. |
| 68 | Performance Targets | Blocked by evidence | [g10-qualification.md](g10-qualification.md): Native streaming-PC target is unverified. Operator accepted documented Wine CPU/RSS deviations for G10; these are deviations, not passing performance targets. G13 Linux baseline records CPU misses and memory below 300 MB for its disabled-integration workload; it does not qualify native PC targets. See [current measurements](g13-compatibility.md). |
| 69 | Database Performance | Verified | [g07-financial-ledger.md](g07-financial-ledger.md): Durable financial uniqueness and indexed SQL aggregation; no in-memory whole-ledger leaderboard calculation. |
| 70 | Backups | Verified | [g10-qualification.md](g10-qualification.md): Native portable/installer setup, backup/restore/reinstall/uninstall; real read-only bot connection tests. Portable packages separately qualified in G12. |
| 71 | Overlay Revision Export | Verified | [g12-custom-widgets.md](g12-custom-widgets.md): Local Monaco, manifest/settings/capabilities, opaque sandbox, durable store and bounded ZIP admission with real browser/OBS/native Windows evidence. |
| 72 | API | Verified | [foundation-api.md](foundation-api.md): Real-host OpenAPI and generated frontend types, protected HTTP API and isolation; Current G13 contract/type drift checks pass in the actual Windows process. |
| 73 | WebSocket Protocol | Verified | [g05-overlays-chat.md](g05-overlays-chat.md): Four-platform normalized/owned chat, bounded runtime, routing/reconnect and real OBS evidence; actual platform delivery is separately limited. |
| 74 | Configuration | Verified | [foundation-api.md](foundation-api.md): Typed config, migration-first startup, graceful drains, late-handshake cancellation, WAL checkpoint and isolated integrations. |
| 75 | Startup Sequence | Verified | [foundation-api.md](foundation-api.md): Typed config, migration-first startup, graceful drains, late-handshake cancellation, WAL checkpoint and isolated integrations. |
| 76 | Shutdown | Verified | [foundation-api.md](foundation-api.md): Typed config, migration-first startup, graceful drains, late-handshake cancellation, WAL checkpoint and isolated integrations. |
| 77 | Error Isolation | Verified | [foundation-api.md](foundation-api.md): Typed config, migration-first startup, graceful drains, late-handshake cancellation, WAL checkpoint and isolated integrations. |
| 78 | Test Strategy | Verified | [testing.md](testing.md): Unit/integration/replay/browser/native packaging and actual OBS checks; synthetic evidence never establishes paid delivery. |
| 79 | Rumble Replay Edge Cases | Verified | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production parser/state/outbox replay and local live integration evidence; no lossless delivery guarantee beyond snapshot window. |
| 80 | Acceptance Criteria — Rumble | Blocked by evidence | [g04-rumble-ingestion.md](g04-rumble-ingestion.md): Production replay/local live chat/bridge qualification exists. Actual paid Rant/subscriber/gift delivery remains unverified under the approved G10 evidence scope. |
| 81 | Acceptance Criteria — Overlay Editor | Verified | [g12-custom-widgets.md](g12-custom-widgets.md): Browser editor and arbitrary custom bindings plus actual OBS media/reconnect/restore; Current G13 browser/recovery/OBS regressions pass within the documented environment. |
| 82 | Acceptance Criteria — Combined Chat | Verified | [g05-overlays-chat.md](g05-overlays-chat.md): Four-platform normalized/owned chat, bounded runtime, routing/reconnect and real OBS evidence; actual platform delivery is separately limited. |
| 83 | Acceptance Criteria — Financial Ledger | Blocked by evidence | [g07-qualification.md](g07-qualification.md): All supported financial normalizers/valuation/periods have bounded fixtures and persistence tests. Actual multi-platform paid delivery is unverified under the approved G10 evidence scope. |
| 84 | Acceptance Criteria — Donor Crown | Verified | [g08-donor-widgets.md](g08-donor-widgets.md): Crown/rank/latest snapshots, filters, identity aggregation, loaded assets, transitions and OBS updates. |
| 85 | Installation Experience | Verified | [g10-qualification.md](g10-qualification.md): Native portable/installer setup, backup/restore/reinstall/uninstall; real read-only bot connection tests. Portable packages separately qualified in G12. |
| 86 | Setup Wizard | Verified | [g10-qualification.md](g10-qualification.md): Native portable/installer setup, backup/restore/reinstall/uninstall; real read-only bot connection tests. Portable packages separately qualified in G12. |
| 87 | StreamElements Investigation | Verified | [g13-compatibility.md](g13-compatibility.md): Official API investigation identifies the limited lifecycle/store/queue/status subset and documented migration/unsupported calls; remote totals and account-wide APIs remain unavailable. |
| 88 | Relevant Documentation | Verified | [implementation-plan.md](implementation-plan.md): Primary official references linked; observed capture remains authoritative. VTube Studio protocol work deferred. |
| 89 | Implementation Phases | Verified | [implementation-plan.md](implementation-plan.md): Stable goal/prerequisite chain, evidence-first replay/security/money rules and protected CI; VTube Studio clauses explicitly deferred. |
| 90 | Codex Development Rules | Verified | [implementation-plan.md](implementation-plan.md): Stable goal/prerequisite chain, evidence-first replay/security/money rules and protected CI; VTube Studio clauses explicitly deferred. |
| 91 | Required Documentation Produced by Codex | Verified | [g13-compatibility.md](g13-compatibility.md): Canonical architecture/event/Rumble/overlay/widget/database/security/testing/development/user docs, 99-section traceability and owned screenshot; ten offline chapters/eight images pass browser checks. |
| 92 | Required Developer Utilities | Verified | [g13-compatibility.md](g13-compatibility.md): Owned samples, isolated event replay, private backup, scanner-backed plain/gzip Rumble replay/log sanitization and aggregate diagnostic export; admission/privacy and actual-process tests pass. |
| 93 | Future Extensibility | Verified | [architecture.md](architecture.md): Adapters/strategies and canonical event boundaries retained; future source examples are extensibility targets, not shipped connectors. |
| 94 | Future Widget Marketplace | Verified | [g12-custom-widgets.md](g12-custom-widgets.md): Local Monaco, manifest/settings/capabilities, opaque sandbox, durable store and bounded ZIP admission with real browser/OBS/native Windows evidence. |
| 95 | Explicit Non-Goals for Initial Version | Deferred by explicit scope | [implementation-plan.md](implementation-plan.md): Cloud/SaaS, marketplace, mobile, Rumble chat sending, remote synchronization/control and full SE compatibility are explicit non-goals. |
| 96 | Highest-Priority Engineering Risks | Verified | [security.md](security.md): Snapshot gap/dedupe, financial uniqueness, sandbox and runtime bounds tested; snapshot loss, ambiguous external delivery and heavy widgets remain documented risks. |
| 97 | Definition of MVP | Verified | [g10-qualification.md](g10-qualification.md): Approved MVP integration/owned audible/OBS/native package evidence; VTube Studio deferred, native PC performance and actual paid delivery unverified. G11/G12 add advanced/custom capabilities. |
| 98 | First Codex Task | Verified | [rumble-analysis.md](rumble-analysis.md): 785-poll evidence and sanitized replay were committed before production adapter code; original secret/archive members excluded. |
| 99 | Expected Final User Experience | Verified | [g10-qualification.md](g10-qualification.md): Approved MVP integration/owned audible/OBS/native package evidence; VTube Studio deferred, native PC performance and actual paid delivery unverified. G11/G12 add advanced/custom capabilities. |

## Evidence disposition

- VTube Studio: explicitly deferred by the 2026-10-02 scope change; retained generic Streamer.bot dispatch does not qualify real-model behavior.
- Paid-platform delivery and Rumble subscriber/gift identity: blocked by missing live evidence. The G10 operator-approved owned-example scope permits the released capability while preserving these limits; do not advertise those routes as live-qualified.
- Native streaming-PC performance: blocked by missing evidence. Wine deviations were explicitly accepted in G10 and remain reported as deviations. Current G13 available-environment measurement does not establish native PC performance.
- G13 delivery: current browser, recovery, privacy, documentation, owned OBS sound/video, native Windows package and SonarQube evidence is recorded. Completion documentation follows protected PR delivery.
- HTTP: supported without a certificate. LAN authentication does not encrypt HTTP traffic.

## UI redesign requirements (G14–G21)

Historical source-section verification above remains unchanged. The
[approved plan and synthetic acceptance revision](ui-redesign-plan.md),
[completion audit](ui-redesign-completion-audit.md) and
[current qualification](ui-redesign-qualification.md) govern this extension.
The operator explicitly retained the current layout/styling.

| Requirement | Current evidence | Disposition |
| --- | --- | --- |
| UI-R01 | All 30 parity groups reviewed; legacy/full browser suites on managed/portable/installed hosts; 252 frontend / 88 Core / 425 Host tests | Verified |
| UI-R02 | Three fresh-project guided tasks per host variant create distinct follow/donation designs, test and copy OBS without internal identifiers | Verified synthetically |
| UI-R03 | System/default and retained explicit themes, shared semantic controls, screenshots/computed contrast; operator design opinion incorporated | Verified |
| UI-R04 | Seven viewports/short window, continuous resizing/Fit/manual geometry, emulated touch move/resize; 40 native 200%/400% zoom checks per host variant | Verified synthetically |
| UI-R05 | Shared actual unsaved presentation and media/custom samples; saved runtime first/all preview; actual OBS representative visuals and matching packaged runtime bytes | Verified within recorded environment |
| UI-R06 | Shared matching vectors/conditions, incoming/custom distinction, eligible IDs, first/all sets and legacy behavior, v1/v2/remapping | Verified |
| UI-R07 | Autosave/conflict/history/revisions/HTTP and native portable/installed backup/restart/restore/startup/uninstall | Verified |
| UI-R08 | Event/ledger/automation equality, denied draft network, isolated storage/permissions; muted RMS zero, opt-in nonzero, navigation resets | Verified synthetically |
| UI-R09 | Actual accessibility tree names/landmarks, keyboard modal/navigation focus, contrast/reduced motion, touch targets/drag alternatives | Verified synthetically |
| UI-R10 | Protected Windows/Sonar/CodeQL on `156ccfa`; exact native package full suites, actual Linux OBS, illustrated/offline guide and published/live-verified wiki `e964fe2` | Verified |

The explicit 2026-10-04 acceptance revision replaces unavailable observed sessions
and physical-hardware/speech/listening gates with measured synthetic delivery
checks. Human learning, screen-reader speech, physical touch and hearing are not
claimed; their scripts remain optional follow-up. Linux OBS and native Windows
package execution are separate evidence; Windows OBS and Wine are not inferred.
G00–G13 paid-platform/performance/compatibility limitations remain unchanged.
Documentation follow-up follows the same protected current-head checks.

## Tray and cumulative release requirements (G22–G24)

The [approved 1.0.1 plan](tray-release-plan.md) extends the matrix without
changing the original numbered requirements or prior acceptance dispositions.
The final 1.0.1 delivery below supersedes candidate publication gaps. Linux
delivery retains the operator-accepted representative qualification limits.

| Requirement | Current evidence | Disposition |
| --- | --- | --- |
| TR-R01 | Existing browser/protocol regression checks, preserved HTTP authority, owned restart/restore and current-source OBS reconnect | Windows qualified; native Linux accepted with documented limits; published 1.0.1 qualified on exact main 77c642c |
| TR-R02 | ec699fb actual Windows native menu/dialog probe; Open editor, Restart and Quit, light/dark and 125% scale | Windows verified; actual KDE/Hyprland Linux menus and 125/200/400% light/dark setup verified |
| TR-R03 | ec699fb actual Windows quiet/manual launch, duplicate launch and normal-browser handoff | Windows verified; representative Wine/UMU ownership, lifecycle and recovery verified; final package repetition limits accepted |
| TR-R04 | Actual control/host tests plus ec699fb restart, browser restore, retained profile/DPAPI credential and accepted quit | Windows verified; representative Wine/UMU ownership, lifecycle and recovery verified; final package repetition limits accepted |
| TR-R05 | ec699fb actual Explorer/fallback recovery, companion crash isolation, old-tray cleanup and stopped-backend guidance | Windows verified; representative Wine/UMU ownership, lifecycle and recovery verified; final package repetition limits accepted |
| TR-R06 | Bounded authenticated pipe/loopback protocol and ownership tests; unchanged HTTP/CSRF boundary | Windows and Linux implemented; private input/output bootstrap and duplicate acknowledgement verified |
| TR-R07 | Owned first-run, Wine/UMU, alias/duplicate and applications-menu checks | Implemented and accepted; representative desktop matrix limits retained |
| TR-R08 | Illustrated Windows tray guide and README; 21-chapter network-disabled guide preparation | Complete: 21 chapters/18 images, packaged offline checks, published/live-verified wiki 72e23c8 |
| TR-R09 | Central application version 1.0.1 and diagnostic Windows packages | Complete: immutable tag 77c642c, public Latest, four anonymously downloaded/hash-verified assets |
| TR-R10 | Full native Windows portable/installed checks; downloaded full-run Windows-under-Wine package in native Linux OBS renders/reconnects and delivers measured silent/tone/silent audio; protected PR #21 merged | Complete within accepted scope: exact-main run 37384546952, actual shipping-package Linux OBS, publisher 37388415088 |

Exact tested source/run links, screenshots and remaining gates belong to the
tray plan. Actual OBS signal measurements do not claim physical hearing, and
Linux OBS does not prove native Windows OBS behavior.


Final G24 source, checksums, CI, wiki and accepted scope are recorded in the
[delivery evidence](tray-release-plan.md#g24-final-delivery--2026-10-05).
v1.0.1 was published on 2026-10-05 at source
`77c642ce721335f6d7169ef914c0a241e2778bf0`. Subsequent documentation commits
record evidence without changing that release tag or application assets.

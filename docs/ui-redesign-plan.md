# TDSBLive UI and Editor Redesign

Approved 2026-10-04. This is the implementation authority for G14–G21 and extends
[the implementation plan](implementation-plan.md) without changing G00–G13.
Preserve every existing capability while making the whole application usable by
nontechnical people and adapting the editor to available window space.

## Approved direction and initial findings

Use system-default light/dark themes with a local override, full adaptive editing,
guided named triggers and conditions, accurate sample-based previews, and ordered
first-matching selection for new alert sets with an all-matches option.

The initial source and repository-screenshot review found one long application
page, crowded editor toolbars, inconsistent styling, technical trigger fields,
name placeholders instead of rendered widgets, vertically stacked narrow-screen
panels, fixed preview sizing, and a fixed initial zoom. Personal-browser access to
the local application was blocked; this is not live visual acceptance evidence.

## Acceptance revision — 2026-10-04

The operator subsequently instructed: “complete the ui redesign plan with
synthetic testing and limited interaction from me except when it comes to getting
my opinion on the ui/ux design.” This changes the acceptance method, not the
feature scope, requirement IDs, prerequisites, permission boundaries or protected
quality gates. The operator reviewed the current desktop/mobile direction and
chose **Keep the current layout and styling**.

Three fresh-project scripted task scenarios replace the unavailable participant
sessions as the delivery gate. Native browser zoom is measured through Chromium's
actual tabs zoom API; accessibility uses browser accessibility trees, keyboard
navigation, modal focus, contrast and reduced-motion checks; touch uses emulated
input and numeric/button alternatives. Audio uses decoded/playback signal and
mute/opt-in assertions, plus actual OBS visual/routing evidence. Human usability,
spoken screen-reader output, physical touch and listening remain optional
follow-up observations and must never be reported as passed. Exact native Windows
portable/installed executables must run the complete browser/parity suite.

G20/G21 can complete under this revised synthetic acceptance once every feature,
contract, browser/package/OBS scenario, protected check, documentation and wiki
publication is evidenced. The earlier human-only criteria below are retained as
historical intent; this section supersedes their use as blocking release gates.

## Requirements

| ID | Requirement |
| --- | --- |
| UI-R01 | Preserve every feature, setting, permission, shortcut, relationship and workflow. Advanced controls may move but remain accessible. |
| UI-R02 | Ordinary setup, alert creation, testing and OBS handoff require no internal event names, JSON, asset hashes or minor-unit currency knowledge. |
| UI-R03 | Consistent typography, spacing, colors, controls and feedback; selectable light/dark themes never change overlay designs. |
| UI-R04 | Every feature remains reachable at narrow widths, short heights, browser zoom and touch input. Resizing retains edits and selection. |
| UI-R05 | Accurate previews use actual widget rendering components and current unsaved edits. |
| UI-R06 | Named triggers, supported conditions and understandable design selection preserve legacy alert behavior. |
| UI-R07 | Retain autosave, undo/redo, revisions, conflicts, import/export, backup/restore and OBS addresses. |
| UI-R08 | Isolated simulations, opt-in preview audio, protected credentials and explicit custom permissions. |
| UI-R09 | Keyboard alternatives, visible focus, accessible labels, readable status, reduced motion and touch-friendly controls. |
| UI-R10 | Completion requires parity, real-browser qualification, OBS verification, docs/wiki and protected Windows checks. |

## Goal index

| Goal | Prerequisites | Requirements | Status |
| --- | --- | --- | --- |
| G14 Audit functionality and baseline | G13 | UI-R01, UI-R10 | Complete |
| G15 Design system and navigation | G14 | UI-R01–04, UI-R07, UI-R09 | Complete |
| G16 Adaptive editor workspace | G15 | UI-R01–04, UI-R07, UI-R09 | Complete |
| G17 Accurate safe WYSIWYG | G16 | UI-R05, UI-R08 | Complete |
| G18 Guided triggers and designs | G14, G15; integrate G17 | UI-R02, UI-R06–08 | Complete |
| G19 Remaining workflows | G15, G18 selectors | UI-R01–04, UI-R07–09 | Complete |
| G20 Accessibility/usability/compatibility | G16–G19 | UI-R01–10 | Complete |
| G21 Documentation and delivery | G20 | UI-R10 | Complete |

Each goal records tested commit, dated commands/results, screenshots, CI links,
and unresolved evidence. Missing acceptance keeps a goal In progress or Blocked;
real rendered-browser, native-package and OBS evidence remain required; mocked
unit tests alone cannot close these gates. Human follow-up is not a delivery gate
under the acceptance revision above.

## G14 — Audit functionality and establish the baseline

- Inventory every page, widget/property, command, integration setting, automation
  action, diagnostic tool and recovery operation.
- Map each capability to its destination and verification scenario, including
  custom code, typed settings, compatibility, permissions, reconciliation,
  moderation, LAN and packages. This is the mandatory parity checklist.
- Capture owned isolated desktop/tablet/phone examples; measure baseline loading,
  resizing, interaction and preview behavior and record limitations.

Acceptance: complete mapping and recorded baseline screenshots/measurements.

## G15 — Design system and application navigation

- Shared fields/buttons/cards/tabs/drawers/dialogs/status/tables/validation/empty
  states; neutral slate surfaces, blue accent and semantic light/dark tokens.
- System theme by default, explicit locally retained override.
- Hash navigation beneath `/editor`: Overview, Overlays & Alerts, Chat,
  Automation, Supporters, Media, Connections, Settings; secondary Diagnostics
  and Help. Keep guided setup prominent for first-time use and reopenable.
- Visible global connection/save status. Preserve unfinished forms; flush or
  resolve overlay changes before navigation, including back/forward.

Acceptance: mouse/keyboard reachability, reload/history, theme and edit retention.

## G16 — Adaptive editor workspace

- Focused create dialog; derive a unique ID from the name, editable in Advanced.
- Compact add/undo/redo/save/preview/test/OBS toolbar; Arrange/View/More menus.
- Searchable categorized widgets, dedicated layers; retain all transforms,
  grouping, alignment, distribution, ordering, locks, visibility and shortcuts.
- Content/Appearance/Placement/Behavior/Advanced property groups.
- Container width >=1200: layers/canvas/properties; 768–1199: single tabbed side
  panel; <768: labeled drawers/sheets while retaining canvas access.
- ResizeObserver fit-to-view on first load and in Fit mode; manual zoom remains
  user-controlled. Preserve canvas pixels/aspect ratio and contain scrolling.
- Touch selection/multiselection without modifiers, pan/resize controls and
  button/numeric alternatives to dragging.

Acceptance: landscape/portrait/custom/4K editing at every test size; resizing
never changes saved geometry.

## G17 — Accurate and safe WYSIWYG previews

- Reuse runtime widget rendering underneath editor selection handles for text,
  images/media, chat, alerts, supporters, event lists, progress and custom code.
- Clearly labeled sample chat/supporters, visible audio editing controls, and
  direct draft updates without saving just to see changes.
- Static alert design plus isolated animated tests, preview audio off by default.
- Retain sandbox/permissions and separate preview/live custom storage.
- Aspect-correct fitted preview; visible missing media/permission/playback errors.

Acceptance: representative designs match runtime/OBS; no live effects, ledger
writes, external actions or live custom-state changes from draft preview.

## G18 — Guided triggers and conditional alert designs

- Choose trigger → conditions → design → test → finish.
- Searchable named choices/descriptions/capability labels shared with automation,
  event lists, supporter filters, subscriptions and tests. Keep unknown legacy
  identifiers and advanced manual/raw input without silently dropping values.
- Distinguish incoming events from actions/outgoing executable code triggers.
  Reliable incoming custom identities only; support `tdsbliveAlertTrigger` on
  owned incoming custom broadcasts, otherwise generic custom-event selection.
- Optional native-money/quantity exact/minimum/range/multiple conditions, normal
  currency display and integer conversion. Missing/gated/incompatible data fails.
- Named alert sets with ordered stable widget references: new sets default first
  matching; all-matching option. Select before queue admission; cooldown/overflow
  never silently falls through. Selection sets differ from playback groups.
- Existing independent alerts retain all-matching until explicitly grouped.
- Plain summaries, test results/nonmatch reasons/overlap warnings, placeholder
  insertion and media thumbnails.

Acceptance: separate event designs and donation tiers without internal strings;
deterministic order, legacy behavior, custom identity and queue semantics.

## G19 — Remaining application workflows

- Connections: explicit connect/disconnect, tests/configuration/discovery and
  execution permissions. Chat: grouped appearance/filters and sample preview.
- Automation: guided conditions/actions, named actions/voices, ordinary amounts,
  summaries, moderation, simulation and receipts; retain existing engine.
- Supporters: readable responsive totals/contributions/details, filters,
  reported/estimated/unknown distinctions and all reconciliation/identity tools.
- Shared searchable Media library/picker: thumbnails, metadata, upload feedback,
  audio preview and licensing fields.
- Settings: branding/access/LAN/backups/restore/configuration transfer/startup
  and application controls. Diagnostics/Help: summaries with technical details
  and contextual explanations/recovery guidance.

Acceptance: G14 parity mapping passes; routine tasks use friendly controls and
all technical capabilities remain in contextual Advanced areas.

## G20 — Accessibility, usability and compatibility

- Both themes, keyboard/focus/screen-reader checks, reduced motion, drag
  alternatives and 44 CSS pixel primary touch/resize hit areas.
- Reflow normal controls at 320 CSS pixels and 400% zoom; contain necessary
  canvas/table two-dimensional scrolling. Follow W3C reflow/drag guidance:
  https://www.w3.org/WAI/WCAG22/Understanding/reflow.html and
  https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html.
- Delivery gate: three fresh-project synthetic scenarios each create two different
  trigger designs, test them and find OBS handoff without typing internal
  identifiers. Record automated milestones, failures and fixes. Exercise actual
  browser zoom, accessibility trees, keyboard focus, emulated touch and preview
  silence/opt-in signal alongside the complete parity suite.
- Optional follow-up: three observed nontechnical participants attempt the same
  task within ten minutes without assistance; retain the prepared script. These
  observations remain unavailable and are never reported as synthetic passes.

Acceptance: full parity, real-browser accessibility/responsive/synthetic tasks
and current protected package checks pass under the operator revision. Human
learning, screen-reader speech, physical touch and hearing remain separate.

## G21 — Document and deliver

- Updated plain-language guide/screenshots/offline docs and GitHub wiki navigation.
- Current evidence in this plan, implementation plan and requirements matrix.
- Regression suite, protected Windows packages, Sonar and security checks.
- Exact packaged build and isolated owned OBS rendering/audio checks; no broadcast.
- Scoped commits/protected PRs with exact tested SHAs and honest limitations.

Acceptance: docs match delivered build, old configurations remain usable and all
required current-build evidence exists.

## Interfaces and compatibility

- Read-only friendly trigger catalog: maintained canonical mappings, redacted
  discovery and reliable observed incoming identities with supported units.
- Optional alert selectors/conditions/ordered sets; absent fields preserve legacy.
- Backend live eligibility sends eligible widget IDs, not extra financial/raw data.
- Preview requests accept validated quantity/native-money/custom identity; retain
  synthetic/native modes, forced simulation and isolation.
- Shared matching specification/test vectors across backend and draft previews;
  update OpenAPI/generated frontend types with contract changes.
- Retain revisions, backups and reference remapping. New-behavior packages use v2;
  import v1/v2, export compatible content as v1. Older apps reject v2 rather than
  silently discard behavior.
- Retain HTTP/LAN/OBS URLs/integration authorities/permissions; VTube Studio held.

## Verification matrix and scope

Viewports: 320×568, 390×844, 768×1024, 1024×768, 1366×768, 1920×1080,
1024×500; continuous resizing and 200%/400% zoom.

Verify transforms/touch/shortcuts/history/autosave/navigation/conflicts/restore;
offline catalog/legacy/custom identities; condition boundaries/currencies/overlap/
first/all/queues/cooldowns/interruption/reconnect; draft/runtime/assets/audio/
permissions/isolation; v1/v2/remapping/backup/restart; existing chat/finance/
automation/integration/recovery/LAN/compatibility qualification.

No unified automation replacement, cloud service, marketplace, paid-platform
qualification expansion or new broadcasting behavior. Use pinned toolchains,
incremental delivery and the mandatory parity checklist.

## Current execution evidence

G14–G21 are Complete under the operator's 2026-10-04 synthetic acceptance
revision. The current layout/styling is retained. G00–G13 remain unchanged.

Delivery source `156ccfaaf150659bf9df5089de134a12794a7daf` passes [protected Windows qualification](https://github.com/TechDaddyKB/tdsblive/actions/runs/37216584852),
all-language CodeQL and SonarQube (82.1% new-code coverage, A ratings, no new
duplication, all hotspots reviewed). Core 88 / Host 425 have no failures or skips;
frontend 252 passes. Full legacy/redesign/synthetic browser suites pass for managed,
portable and installed hosts. Both exact native executables pass recovery, restart,
installer/startup/uninstall and network-disabled packaged-guide checks.

The mandatory 30-row parity inventory has been reviewed; no capability is missing.
Actual isolated OBS visuals/routing pass on Linux; all three runtime JS/CSS assets
match the downloaded Windows package byte-for-byte. This does not claim Windows
OBS, paid-platform delivery, observed human learning or physical input/hearing.

[The illustrated wiki guide](https://github.com/TechDaddyKB/tdsblive/wiki/Adaptive-Editor-and-Guided-Alerts)
is published at `e964fe287d7cd858b3e1347736865370f5f6fae6`. Remote/local heads match; live Home/guide navigation
and all five dedicated-guide images render. Offline delivery includes 20 chapters
and 12 owned images. Scoped [PR #19](https://github.com/TechDaddyKB/tdsblive/pull/19)
retains protected checks; completion documentation follows the same checks.

[Completion audit](ui-redesign-completion-audit.md),
[qualification history and exact package hashes](ui-redesign-qualification.md),
[parity checklist](ui-parity-checklist.md) and the requirements matrix record each
goal/requirement and its evidence boundaries. No approved redesign blocker remains;
optional human follow-up and fresh-prefix Wine qualification remain distinct.

# UI redesign candidate qualification

## Current synthetic acceptance revision — 2026-10-04

The operator authorized completing the full plan with synthetic testing and
limited interaction, and chose **Keep the current layout and styling** after
reviewing desktop/mobile examples. The [plan revision](ui-redesign-plan.md#acceptance-revision--2026-10-04)
supersedes earlier human-only release gates; it does not reduce functionality or
weaken protected checks. Earlier checkpoint evidence below remains historical.

Current local evidence for the synthetic qualification changes:

- `npm run lint`, `npm run typecheck`, `npm run test:coverage`: pass, 252 tests,
  48 files, 90.42% lines / 78.91% branches overall. Meaningful modal tests verify
  bidirectional Tab wrap, disabled/hidden exclusions, ordinary key behavior and
  opener restoration. Browser testing exposed the missing wrap; it was fixed.
- Release host build: zero warnings/errors. The full browser qualifier passes after rebuilding the frontend and host
  together, including existing G02/G05–G09/G11–G13 and redesign scenarios.
- `ui-acceptance.mjs`: three independent browser contexts with fresh projects at
  desktop/tablet/emulated-touch sizes create follow/donation designs, exercise
  all five guided steps, test matching and verify the copied existing OBS URL.
  Scripted elapsed times: 1.558 / 1.433 / 1.699 seconds. These are automation
  timings, never human learning or usability measurements.
- Native Chromium zoom is controlled through a disposable extension's
  `chrome.tabs.setZoom`/`getZoom`, using the pinned full Chromium channel.
  Browser-reported 200%/400% corresponds to 640/320 CSS pixels from 1280px.
  Forty checks cover all ten destinations in both themes, expanded details,
  document reflow and named controls/main landmarks in the actual accessibility
  tree. Saved widget content/geometry is unchanged. Keyboard traversal verifies
  modal containment/Escape/opener restoration and destination heading focus.
- A generated owned 440Hz WAV reaches Web Audio through the real media element:
  muted draft/saved preview RMS **0**, explicitly enabled preview peak RMS
  **0.09797** in one observation. Leaving/reopening restores disabled audio.
  Browser autoplay policy is enabled only for this disposable signal test;
  Chromium's process-wide mute prevents physical speaker output. No human
  hearing is claimed. Event history, ledger and automation receipts remain equal
  before and after the synthetic scenarios.
- `qualify_windows_package.ps1` now repeats the full regression/browser/parity
  and synthetic acceptance suite using both the actual portable and installed
  `TDSBLive.exe`. Only named targets inside the runner's owned package directory
  are allowed. Realpath checks reject unexpected/symlinked executable paths.
  CI installs pinned full Chromium for the native zoom extension. The job limit
  is 45 minutes to accommodate three full host variants; quality conditions,
  protected checks and fork-secret isolation are unchanged.

Evidence: ignored `artifacts/ui-redesign/acceptance-managed/` machine-readable
results and owned screenshots; CI uploads portable/installed equivalents.
The original human script and hands-on worksheet remain optional follow-up, not
synthetic passes. Screen-reader speech, physical hardware input, observed human
learning and human listening are not claimed. Fresh-prefix Wine remains
unqualified and is not inferred from native Windows results.

Current source OBS 32.2.2 / WebSocket 5.7.4 also passes an isolated preview check:
owned follow, $5 and $10 first-matching tiers; text, image, animated video, custom
worker text, chat, event list and progress. Saved 1280×720 source captures were
scanned and visually inspected. Preview remained labeled silent; input audio was
routed to OBS, monitoring off, volume 1. This records visual/routing evidence,
not actual listening. Event count remained zero. The program scene stayed
`TDSBLive G10 qualification`, stream/record remained inactive, Studio Mode was
restored to false, disposable input/scene removed, host stopped and data removed.
One owned representative capture is included in the illustrated user guide.

Protected checkpoint `7cbc121` failed before browser/package qualification:
Core 88 passed, Host 423 passed / 2 failed. A recovery cleanup retained a database
handle, and an unchanged configuration-save test returned HTTP 500. Inspection
found drained recovery test hosts were not disposed before replacement or final
cleanup. Their disposal is now explicit, with the existing bounded Windows-only
cleanup helper used after targeted pool release. Focused recovery/security tests
pass locally (7/7). The configuration-write failure is not yet attributed to a
root cause or declared fixed; the protected full rerun must pass. No production
persistence behavior or quality threshold was changed for this test repair.

Current completion gates: full updated browser suite, protected current-source
Windows portable/installed evidence and Sonar/CodeQL, final inventory review,
current OBS visual/routing verification, wiki publication/rendering and final
protected delivery. These remain pending until their actual results are recorded.

Native zoom approach follows the primary [Playwright extension documentation](https://playwright.dev/docs/chrome-extensions)
and [Chrome tabs zoom API](https://developer.chrome.com/docs/extensions/reference/api/tabs).

2026-10-04. This record concerns the G14–G21 candidate, not a completed release.
Baseline: `e590c8eddede11eee9fd037a424b8a8c448b60d8`. Initial implemented source:
`9b00d4b`; guide/screenshots: `df2406e`. Guided draft testing was completed in
`2ace282`, security/accessibility/complexity fixes in `040ff98`, and cross-overlay
alert-set clipboard preservation in `57706ba`. The approved plan was committed first in
`488246f`, and backend/contracts in `b5d31c2`. Subsequent CI evidence must identify
the exact tested head and must not inherit older goal passes silently.

## Implemented behavior

- Ten hash destinations preserve visited forms. Navigation, including history,
  flushes overlay changes and retains unresolved conflicts. Themes follow the
  system by default and persist only an application preference.
- The editor fits its container, provides alternate panels/drawers, actual draft
  widget presentations, touch-size handles and numeric/button alternatives.
  Fit resizing preserves saved geometry; manual zoom stays under user control.
- Alert creation uses searchable incoming choices, ordinary currency amounts,
  explicit incoming custom identities, and optional native-money/quantity filters.
  Large condition integers serialize as strings; legacy numeric input is accepted.
- Ordered alert sets select before playback admission. Existing independent alerts
  retain all matching behavior. Queue rejection never selects a different tier.
- Ordinary overlay delivery includes eligible IDs and excludes raw/support money.
  Isolated preview events do not persist or run automation. Draft custom storage
  is memory-only and network access is denied; a visible canvas notice
  explains draft restrictions and the saved Preview path. Saved preview/live scopes remain
  separate. Preview audio starts off and resets when leaving the editor.
- Legacy commands, custom code/permissions, reconciliation, configuration,
  LAN/HTTP, restore/revisions and v1 import/export remain reachable. Packages with
  conditional selection use v2 and remap stable design/set identities on import.
  Copying a complete alert set into another overlay also remaps its identities
  and preserves its ordered first/all mode; partial copies remain independent.

## Current local checks

Pinned Node 24.21.0/npm 11.19.0/.NET SDK 10.0.401; Linux host and isolated data.

| Check | Command | Result |
| --- | --- | --- |
| Secrets | `sonar analyze secrets` on inspected/changed source, tests, docs and images | Passed; no values exposed |
| Frontend static checks | `npm run lint`, `npm run typecheck` | Passed |
| Frontend tests | `npm run test:coverage` | 251 passed, 47 files; 90.40% lines, 78.90% branches overall (additional draft-safety and guided-input scenarios) |
| Backend tests | `dotnet test TDSBLive.slnx -c Release` | Core 88 passed; Host 421 passed, 4 Windows-only skips |
| Release build | `npm run build`; `dotnet build src/ExtensionSuite.Host -c Release` | Passed, zero backend warnings/errors |
| Control contrast | Browser-computed field/panel colors in both themes | At least 3:1 field boundaries and 4.5:1 text/placeholders; decorative card borders kept separate |
| Full browser regression | `node tools/browser-qualification/qualify.mjs` | Passed G02/G05/G06, G07, G08, G09, G11, G12, G13 and redesign scenarios |
| Foundation/restart/contracts | `python tools/qualify_foundation.py` | Passed HTTP shells, schema drift, crash/restart, redaction and isolation |
| Financial processes | `python tools/qualify_financial.py` | Passed crash/restart, precision, dedupe, reconciliation, conflicts and isolation |
| Compatibility diagnostic utility | `python tools/qualify_compatibility.py` | Passed owned aggregate diagnostic output; not audio evidence |
| Guide | `node tools/browser-qualification/guide-screenshots.mjs`; `python tools/build_offline_guide.py release/ui-redesign-offline-guide` | 20 chapters built; owned screenshots, scanned before inspection |
| Offline browser audit | `sonar analyze secrets release/ui-redesign-offline-guide-wizard`; `node tools/browser-qualification/offline-guide.mjs review` | Passed all 20 chapters, local navigation, loaded images and 390/1366px reflow with network disabled |
| Recovery process | `node tools/browser-qualification/recovery-process.mjs` | Passed open-browser restart/restore, custom/compatibility state, groups, assets and safety-paused integrations |
| Replay/tooling regression | `python -m unittest discover -s tests/replay -v` | 33 passed |
| Sonar CLI quality | `sonar analyze --staged --force --format json` | Secrets passed; Vortex unavailable (403), all quality analysis skipped; not a quality-gate pass |
| Local dependency analysis | `sonar analyze dependency-risks --format json` | Unavailable on current Sonar connection; not a dependency-security pass |

Logs/reports remain ignored local artifacts. Source baseline timings and capability
destinations are in [the parity checklist](ui-parity-checklist.md). Protected CI
and server-side Sonar/CodeQL results are recorded separately below.

| Goal | Acceptance evidence in this candidate | Remaining evidence |
| --- | --- | --- |
| G14 | Complete inventory/destinations/scenarios; owned baseline/current captures and timings | No audit blocker; inventory remains a release checklist |
| G15 | AppWorkflow tests; hash/history, retained forms/conflicts, system/explicit themes and all ten pages in the real browser | Final packaged parity review |
| G16 | Full advanced-editor assertions; continuous fit/manual resizing, geometry, keyboard/numeric alternatives and touch-style selection | Physical touch/native zoom in G20 |
| G17 | Shared presentation previews; unsaved edits, custom memory/network isolation, saved-preview sandbox regression and actual OBS alert visuals | Exact-package representative visual/audio review |
| G18 | Core/Host/shared vectors, guided controls, queue eligibility, first/all tiers, catalog/custom distinction and v1/v2 remapping | Final packaged scenario review |
| G19 | Existing chat/finance/automation/custom/recovery assertions plus Media/Diagnostics tests and 320px destination checks | Final packaged parity review |
| G20 | Both themes, seven viewports, focus/keyboard labels/reduced motion and drag alternatives | Participants, manual screen reader, physical input and native zoom |
| G21 | Scoped commits, draft PR, owned illustrated guide, offline browser audit, prepared wiki diff | Protected exact-head packages/security, published wiki, audible OBS and G20 |

## Protected delivery and documentation preparation

[Draft PR #19](https://github.com/TechDaddyKB/tdsblive/pull/19) preserves the main
branch protections. Initial head `f1a058e61b5313e9b0df55565c0ff424062e9d54` has
passing C#, JavaScript/TypeScript and Python CodeQL checks in
[run 37195659004](https://github.com/TechDaddyKB/tdsblive/actions/runs/37195659004).
[Windows run 37195660338](https://github.com/TechDaddyKB/tdsblive/actions/runs/37195660338)
was superseded/cancelled. On head `8c73fe2`,
[Windows run 37195852420](https://github.com/TechDaddyKB/tdsblive/actions/runs/37195852420)
passed all functional/native/browser/recovery checks but failed Sonar reliability
and security ratings. New-code coverage passed. Findings concerned delegated
menu click handling and unrestricted filesystem/command arguments in the offline
audit. `040ff98` removes that DOM click handler, accepts only named fixed guide
targets, moves the pinned secrets scan into CI and simplifies all seven functions
reported with critical complexity. The full local regression and offline audit
pass; invalid directory input is rejected. The protected exact-head rerun remains
required; no quality condition or branch protection was bypassed.

Current source checkpoint `57706ba654df61b798fcc224fda08e5f64f35501` passes
all three CodeQL languages in
[run 37197951540](https://github.com/TechDaddyKB/tdsblive/actions/runs/37197951540).
Its [Windows run 37197952964](https://github.com/TechDaddyKB/tdsblive/actions/runs/37197952964)
was superseded by documentation checkpoint `586da25`; its
[Windows run 37198204738](https://github.com/TechDaddyKB/tdsblive/actions/runs/37198204738)
was still pending when the final contrast fix was prepared. These entries are
historical checkpoints. The current protected result and exact head are available
from the draft PR checks; every source or documentation change requires its own
protected run before delivery.

On production source `3e15a0306f772f96ea18744854b040c6fe37e286`,
[Windows run 37199312387](https://github.com/TechDaddyKB/tdsblive/actions/runs/37199312387)
passed Core 88 and Host 425 tests with no skips, frontend 248 tests, process/browser
qualification, recovery, generated contracts and coverage-report validation.
The Sonar gate failed solely on 79.9% new-code coverage against 80%; earlier
reliability/security findings and all critical-complexity findings are resolved.
The remaining 123 nonblocking findings are style/maintainability items. Packages
were skipped. Additional tests now cover rejected draft-memory writes, retained
valid state without server storage, explicit draft silence, stale matching results,
fractional quantity errors, custom incoming identity and native-money/quantity
range summaries. Full local coverage passes with 251 tests. The protected gate
and exclusions remain unchanged; its exact-head rerun must pass before packaging.
All languages pass [CodeQL run 37199311062](https://github.com/TechDaddyKB/tdsblive/actions/runs/37199311062)
on the same production source.

Current protected checkpoint: `fa4c6d16bbef32f564146e0790f2f9eaedbfe43d` passes
[Windows run 37200780622](https://github.com/TechDaddyKB/tdsblive/actions/runs/37200780622)
and [all-language CodeQL run 37200779337](https://github.com/TechDaddyKB/tdsblive/actions/runs/37200779337).
Core 88, Host 425 (no skips), and frontend 251 tests pass. Sonar reports 81.9%
new-code coverage, zero new bugs/vulnerabilities, zero new duplication and 100%
reviewed hotspots. Native portable/installer startup, restart/restore,
reinstall/uninstall, default-off startup and opt-in startup pass. The shipped
guide passes the network-disabled 20-chapter browser audit.

Downloaded artifacts match `SHA256SUMS.txt` and the extracted portable package
passes secrets scanning:

| Artifact | SHA256 |
| --- | --- |
| `TDSBLive-0.1.0-win-x64.zip` | `51331a733680b221d97d812337e0ec17ed8488de7843960b4a183e7aa87991b3` |
| `TDSBLive-0.1.0-win-x64-setup.exe` | `55b8a28839de2808cc9bcc69fefd7ad19cf206dd7cfe3219679f6a4d37924d93` |

Additional Wine 11.17/Xvfb smoke attempts in fresh disposable prefixes did not
reach readiness. Disabling Wine's .NET loader produced a `System.Runtime.dll`
load error; retaining it still timed out. Software rendering and write-copy
settings did not produce a pass. The packaged `System.Runtime.dll` and
`coreclr.dll` match a previous local Wine package byte-for-byte. These observations
do not establish a root cause or qualify Wine; a prepared-environment check
remains outstanding. Temporary processes/prefixes/data were removed. The
existing host at port 17474 and the read-only Streamer.bot health endpoint
remained responsive. No production configuration was inspected or changed.

This delivery record changes documentation only after the tested source above;
its own protected PR checks remain required. Participants and the hands-on
acceptance below still prevent G20/G21 completion.

`npm audit --json` reports two low-severity affected packages (DOMPurify and
Monaco); the unchanged baseline reports the same two. No dependency/lockfile
changes were introduced. The registry suggests downgrading the pinned editor;
that is not accepted as a compatibility fix. There are no open GitHub code-scanning
alerts in the repository at this checkpoint. The current CodeQL checks must still
be recorded by exact head.

`tools/sync_user_guide.py` validated and prepared a separate wiki checkout at
`/tmp/tdsblive-ui-wiki`: 20 chapters, 11 owned illustrations, candidate guide and
navigation. The diff has not been published; stable released instructions must
not be silently replaced by a candidate while usability/package gates are open.

## Browser and visual evidence

The real Chromium suite checks 320×568, 390×844, 768×1024, 1024×768,
1366×768, 1920×1080 and short 1024×500 windows in both themes, continuous
container resizing, fitted/manual zoom, portrait/custom/4K geometry, touch-style
multi-selection, keyboard commands, autosave/revisions/conflicts/history, named
triggers/tiering, package remapping and preview custom-state/network isolation.
Expanded controls in all ten destinations are checked for document overflow at
320 CSS pixels. The necessary canvas/table scrolling remains contained.

Screenshots in ignored `artifacts/ui-redesign/` were inspected visually. Public
guide illustrations contain only made-up owned data, not production configuration.
The toolbar was shortened and alert setup moved ahead of placement settings after
inspection. A final contrast audit separated control borders from decorative
card borders and set explicit readable placeholders. Browser checks measure
actual computed field/parent colors using the relative-luminance formula and
the [W3C non-text contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html).
Browser assertions are not manual screen-reader or physical-touch
qualification, and 320 CSS pixels is not evidence of native 400% browser zoom.

## Actual OBS visual check

OBS 32.2.2 / obs-websocket 5.7.4 on Omarchy. Streaming and recording were inactive.
A disposable isolated host, scene and Browser Source used a 1920×1080 preview URL.
Direct OBS screenshots showed the owned follow greeting, $5 donation design, and
$10 first-selected large design; screenshots are `obs-follow.png`, `obs-small.png`
and `obs-large.png` in ignored `artifacts/ui-redesign/`. The host event count
remained zero after previews. No production bot or financial data was used.

Program scene remained `TDSBLive G10 qualification`; Studio Mode was restored to
off, disposable OBS objects removed and the temporary host/data cleaned up.
This verifies native OBS visual selection on Linux only. It is not exact Windows
package, audible output or paid-platform evidence.

The [hands-on qualification worksheet](ui-manual-qualification.md) is prepared
for native zoom, screen readers, physical touch, exact packages and audible OBS.

## Historical mandatory acceptance before the operator revision

- [ ] Three observed nontechnical sessions. The operator said participants are
  unavailable; [the script and recording sheet](ui-usability-test.md) are ready.
- [ ] Native browser zoom at 200%/400%, manual screen-reader announcements and
  physical touch input/focus checks on the packaged build.
- [ ] Audible output and silent-by-default checks in actual OBS with owned media.
- [x] Protected Windows build/test/installer/portable/restart checks on source
  `fa4c6d1`, Sonar quality gate and all-language CodeQL; no bypasses used.
  Documentation-only follow-up still follows protected checks.
- [x] Network-disabled offline-guide browser audit on the candidate source build.
- [x] Exact-packaged offline guide browser audit (network disabled).
- [ ] Current wiki publication and live navigation/rendering verification.
- [ ] Final release parity review against every inventory row and any failures
  observed in usability sessions; revised flow must be retested before G20 closes.

The human-only checklist above is superseded by the operator-approved synthetic
acceptance revision at the top of this document. No human observation is claimed.
The current protected-package, parity, OBS and publication gates still require
authoritative current evidence before G20/G21 completion.

### Additional saved-runtime and touch assertions

The full local browser suite passes after adding actual touch pointer move/resize
and a real saved-preview queue check. First-match admits only the large donation
design. All-matches plays both eligible donation designs; an observational DOM
collector records both without changing runtime queue state. Existing equal-
priority queue order remains intact; set order selects eligibility, not playback
priority. These assertions are included in the next protected checkpoint.

### Protected synthetic checkpoint and command-path repair

`7bc6932` / [Windows run 37215234793](https://github.com/TechDaddyKB/tdsblive/actions/runs/37215234793)
passes Core 88, Host 425 (no skips), frontend 252, foundation/financial/recovery,
managed-browser synthetic acceptance and generated contracts. The earlier two
native test failures did not recur after recovery host disposal; no production
configuration persistence was changed, and the prior HTTP 500 cause remains
unattributed. CodeQL passes all languages.

Sonar passes coverage (82.1%), reliability, maintainability, duplication and
reviewed-hotspot conditions, but flags CLI input entering the package executable
path (`jssecurity:S8701`). The runner now maps named choices to fixed literal
portable/installed paths before realpath validation; the argument is never a
path segment. An invalid `../../tmp/untrusted` choice is rejected before launch.
The protected rerun must verify this repair and then qualify both exact EXEs;
there is no exclusion, suppressed finding or changed gate threshold.

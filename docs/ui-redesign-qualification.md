# UI redesign candidate qualification

2026-10-04. This record concerns the G14–G21 candidate, not a completed release.
Baseline: `e590c8eddede11eee9fd037a424b8a8c448b60d8`. Implemented source:
`9b00d4b`; guide/screenshots: `df2406e`. The approved plan was committed first in
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
  is memory-only and network access is denied; saved preview/live scopes remain
  separate. Preview audio starts off and resets when leaving the editor.
- Legacy commands, custom code/permissions, reconciliation, configuration,
  LAN/HTTP, restore/revisions and v1 import/export remain reachable. Packages with
  conditional selection use v2 and remap stable design/set identities on import.

## Current local checks

Pinned Node 24.21.0/npm 11.19.0/.NET SDK 10.0.401; Linux host and isolated data.

| Check | Command | Result |
| --- | --- | --- |
| Secrets | `sonar analyze secrets` on inspected/changed source, tests, docs and images | Passed; no values exposed |
| Frontend static checks | `npm run lint`, `npm run typecheck` | Passed |
| Frontend tests | `npm run test:coverage` | 245 passed, 47 files; 90.35% lines, 77.60% branches overall |
| Backend tests | `dotnet test TDSBLive.slnx -c Release` | Core 88 passed; Host 421 passed, 4 Windows-only skips |
| Release build | `npm run build`; `dotnet build src/ExtensionSuite.Host -c Release` | Passed, zero backend warnings/errors |
| Full browser regression | `node tools/browser-qualification/qualify.mjs` | Passed G02/G05/G06, G07, G08, G09, G11, G12, G13 and redesign scenarios |
| Foundation/restart/contracts | `python tools/qualify_foundation.py` | Passed HTTP shells, schema drift, crash/restart, redaction and isolation |
| Financial processes | `python tools/qualify_financial.py` | Passed crash/restart, precision, dedupe, reconciliation, conflicts and isolation |
| Compatibility diagnostic utility | `python tools/qualify_compatibility.py` | Passed owned aggregate diagnostic output; not audio evidence |
| Guide | `node tools/browser-qualification/guide-screenshots.mjs`; `python tools/build_offline_guide.py release/ui-redesign-offline-guide` | 20 chapters built; owned screenshots, scanned before inspection |
| Sonar CLI quality | `sonar analyze --staged --force --format json` | Secrets passed; Vortex unavailable (403), all quality analysis skipped; not a quality-gate pass |

Logs/reports remain ignored local artifacts. Source baseline timings and capability
destinations are in [the parity checklist](ui-parity-checklist.md). Protected CI
and server-side Sonar/CodeQL results are recorded separately below.

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
inspection. Browser assertions are not manual screen-reader or physical-touch
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

## Outstanding mandatory acceptance

- [ ] Three observed nontechnical sessions. The operator said participants are
  unavailable; [the script and recording sheet](ui-usability-test.md) are ready.
- [ ] Native browser zoom at 200%/400%, manual screen-reader announcements and
  physical touch input/focus checks on the packaged build.
- [ ] Audible output and silent-by-default checks in actual OBS with owned media.
- [ ] Protected Windows build/test/installer/portable/restart checks on exact head,
  Sonar quality gate and required security checks; no bypasses permitted.
- [ ] Network-disabled offline-guide browser audit, current wiki publication and
  live navigation/rendering verification.
- [ ] Final release parity review against every inventory row and any failures
  observed in usability sessions; revised flow must be retested before G20 closes.

G20/G21 remain In progress until these gates are evidenced. No release, merge or
claim of complete redesign follows solely from passing local tests.

# TDSBLive 1.0.1 — Tray Controls and Published Release

This approved plan adds desktop controls and publishes a downloadable, cumulative
1.0.1 release. Preserve G00–G21 and the current browser editor's layout and styling.
This is the last planned feature release for a while. Bug and security fixes and
a future fully native Linux application remain possible.

## Approved choices

- Windows keeps `TDSBLive.exe` as its entry point. Add a shared native desktop
  companion using centrally pinned Avalonia.Desktop 12.1.3 and tracked lockfiles.
- Linux gets a native x64 tray companion. The existing Windows backend continues
  through direct Wine or UMU/Proton; a fully native Linux backend is deferred.
- Manual startup opens the editor and shows the tray. Optional Windows sign-in
  startup shows the tray quietly. Closing a browser does not stop TDSBLive.
- Tray actions are **Open editor**, **Restart**, and **Quit**. Restart and Quit
  require an accessible, Cancel-first confirmation reminding users to save edits.
- Qualify direct Wine and UMU/Proton. Retain Bottles installation instructions,
  but do not claim native-companion support for Bottles in this release.
- Publish v1.0.1 containing every change since v0.1.0. Do not backfill v1.0.0.

## Requirements

| ID | Requirement |
| --- | --- |
| TR-R01 | Preserve all existing workflows, HTTP/LAN support, settings, data, permissions, OBS URLs, browser controls and recovery behavior. |
| TR-R02 | Offer the three named tray actions on Windows and native Linux, with readable icon, tooltip and keyboard-accessible confirmations. |
| TR-R03 | Manual launch opens the editor; optional sign-in launch stays quiet. One backend and tray per profile; repeated launch opens that profile's editor. |
| TR-R04 | Restart and Quit reuse graceful shutdown and checkpointing. Restart and restore preserve the profile, runner, prefix and successful recovery result. Never kill unrelated applications. |
| TR-R05 | Missing tray registration gives a visible control window. Companion failure does not stop streaming. Restore icons after Explorer/tray-service restart; do not automatically restart a crashed backend. |
| TR-R06 | A separate authenticated loopback channel handles desktop status/restart/quit. Session tokens travel over redirected process pipes, never arguments, URLs, logs, saved config or exports. Existing HTTP authentication and CSRF stay intact. |
| TR-R07 | Linux first-run setup explains runner and prefix, preserves existing settings, uses structured arguments and native browser, and requires no root installation or automatic autostart. |
| TR-R08 | All affected repository, README, offline and wiki guidance is accurate, illustrated where possible, and written in plain language with literal UI labels. Audit every remaining chapter. |
| TR-R09 | Package version 1.0.1 is derived from one application version source. Publish four verified assets from the same protected immutable main commit, with checksums and public download verification. |
| TR-R10 | Complete only with actual Windows/Linux tray, lifecycle, OBS, packaged application, protected CI, security and documentation acceptance evidence. Preserve known performance and paid-platform qualification limits. |

## G22 — Desktop controls and Windows tray

Status: **Complete**
Prerequisites: G21 and merged 1.0 source
Requirements: TR-R01–TR-R06, TR-R10

### Deliverables

- Shared desktop companion, icon and native menus. Tooltip indicates Starting,
  Running, Restarting or Stopped unexpectedly. Busy commands are disabled.
- Open editor uses the actual configured loopback address and normal OS browser.
- Restart/Quit confirmations initially focus Cancel, warn overlays will stop and
  remind users to save changes. Do not claim tray actions save unfinished forms.
- Reuse ApplicationLifecycle, WebSocket/integration shutdown and DB checkpoint.
  Preserve browser restart/quit/restore. Support automatic, externally managed
  and headless desktop launch modes; existing tests/utilities remain headless.
- Windows host remains relaunch owner. Companion reconnects or is recreated
  cleanly after a successful restart/restore. Unrelated port conflicts produce a
  clear error, never attaching to or terminating the conflicting application.
- Separate authenticated loopback control channel and diagnostics status. Update
  OpenAPI/generated types if public diagnostics contracts change.
- One owner per profile; missing tray registration shows a small **TDSBLive is
  running** window containing the same controls. Closing it leaves the host
  running. Companion crashes leave the host running and expose recovery guidance.

### Acceptance evidence

Record exact tested commits, commands, results, screenshots and CI links. Verify
portable and installed Windows launch, quiet opt-in sign-in startup, all actions,
Cancel, repeated clicks/launches, nondefault ports, spaced paths, Explorer restart,
fallback/recovery, browser lifecycle, restore success/failure, SQLite integrity,
saved settings/credentials, OBS reconnect, update/reinstall/uninstall and retained
data. Keyboard focus, labels, light/dark and high-DPI controls must work.

### Blockers

None for G22's approved Windows scope. Protected delivery and the actual
portable/installed, lifecycle and owned OBS evidence are recorded below. Final
cumulative artifacts are requalified in G24. Native Windows streaming-PC
performance, physical hearing and native Windows OBS behavior are not claimed;
the established environment and acceptance dispositions remain explicit.

## G23 — Native Linux tray companion

Status: **Complete** (accepted with documented limitations)
Prerequisites: G22
Requirements: TR-R01–TR-R07, TR-R10

### Deliverables

- Native Linux x64 companion starts the Windows host with direct Wine or UMU.
  First-run setup chooses the installed runner and existing prefix, described as
  the folder holding the Windows app's settings. Offer a separate new-user
  default; never silently move an existing user to an empty profile.
- Store only nonsensitive launcher settings. Preserve runner, executable and
  prefix across restarts through structured process arguments/environment. No
  blanket Wine kills, shell interpolation or saved integration/admin credentials.
- Externally managed host mode suppresses Windows self-relaunch. Native companion
  relaunches only after an explicitly successful graceful restart or restore;
  process death or failed restore is not restart intent. Avoid duplicate Wine tray.
- StatusNotifierItem/AppIndicator tray, native Linux browser and native dialogs.
  Use stable Avalonia X11 support, with XWayland on Wayland. Do not opt into the
  early-stage native Avalonia Wayland backend for 1.0.1.
- Qualify Hyprland/Omarchy and KDE Plasma, an X11 session and a Wayland session.
  Explain GNOME's AppIndicator extension and show a fallback when no tray exists.
- Per-user launcher installation option; no root installation or auto-autostart.
  Keep Bottles backend instructions with an explicit companion qualification limit.

### Acceptance evidence

Record actual native tray/menu/dialog interactions under direct Wine and UMU,
unchanged existing prefixes, lifecycle and browser-initiated restore, stopped and
crashed hosts, missing/reappearing tray services, native-browser handoff and no
duplicate backend/tray. Measure companion plus backend resource use against the
baseline; investigate material regressions. Historical accepted Wine performance
deviations are limitations, not passing performance evidence.

Current acceptance map (2026-10-05; representative evidence does not replace
final cumulative-package qualification):

| Required scenario | Current evidence | Remaining work |
| --- | --- | --- |
| KDE X11 native menu, tooltip, browser, restart and quit | Native `2989331` with protected `5ff394f9` backend under Wine; actual interactions, retained profile, replaced backend, exit 0 and SQLite integrity | Repeat on final cumulative package |
| KDE Wayland/XWayland | Native `f20fdc8` with protected `5ff394f9` backend under Wine; actual menu/browser, Cancel-first Enter/Escape, confirmed restart/quit and full tray-service recovery | Repeat on final cumulative package; complete UMU matrix |
| Hyprland/Omarchy Wayland | Actual owned Hyprland 0.56.2 compositor with Quickshell: Wine representative and protected `7b58702` UMU menus, Cancel-first keyboard, confirmed restart, native browser and tray recovery | Remaining final cumulative qualification |
| Direct Wine and UMU native companion | Owned Wine GUI evidence; corrected native UMU default starts selected profile and passes KDE Wayland cancellation/restart/browser/quit | Remaining desktop/runner matrix and final exact packages |
| New and retained prefixes | Actual optional first-run window initializes its suggested Wine prefix; retained Wine/UMU, physical aliases and native ownership pass at repaired source 6791cfb | Repeat first-run and retained/alias scenarios on the final cumulative bundle |
| Missing/reappearing tray | Transport watcher replacement tests; actual KDE Wayland watcher loss shows fallback, full service restoration recovers menu | Final-package repeat; panel-only loss retains KDE stale-host limitation documented in guide |
| Browser restart/restore and crashed hosts | Actual Hyprland/Wine browser restart and validated restore retain the selected profile; repaired Wine/UMU crash cases keep one native owner, no automatic relaunch, and explicit keyboard close/reopen | Final cumulative-package repeats, including unsuccessful restore |
| Applications-menu shortcut | Actual optional checkbox installs the shortcut; GIO reopens the retained owned profile; repeated Wine/UMU launches keep one native companion/backend after the acknowledgement repair | Final packaged repetition |
| Window sizes, themes and keyboard | Both-theme short/narrow headless checks; actual Wayland keyboard cancellation; repaired Hyprland recovery floats and stays keyboard-operable at 320×240 | Complete actual Linux DPI/theme checks and repeat on final package |
| Resource use | Paired owned UMU runs at repaired 6791cfb source: later samples total 352.0 MiB PSS / 3.93% of one core with the companion versus 229.8 MiB / 3.53% backend-only; same profile, runner and editor workload | Longer allocation/GC investigation and final-package comparison; no steady-state acceptance claim yet |
| Protected current-source package | Prior full run at a507b1e passes; e286733 fails a missing-input qualifier fixture, now corrected. Current source 6791cfb is pushed; protected run 37367019353 passes the native Windows probe, then unstarted jobs fail to acquire hosted runners; those jobs are retried | Current protected checks, then final protected main build and cumulative artifact qualification |
| Documentation and public release | Candidate Linux/Windows guide preparation, screenshots and offline checks | G24 full audit/wiki/final OBS and package qualification, then publish v1.0.1 |

### Blockers

G23 is accepted with the documented memory, KDE and representative-matrix
limitations. Longer resource investigation and the remaining desktop/runner
matrix are future work. G24 still requires protected current-source checks,
exact cumulative-package qualification and publication. No user decision is
pending for the investigated Python examples.

## G24 — Documentation, qualification and publication

Status: **In progress**
Prerequisites: G22–G23
Requirements: TR-R08–TR-R10 and cumulative regression coverage of TR-R01–TR-R07

### Deliverables

- Audit all repository documentation (baseline: 76 files, 20 guide chapters and
  12 images). Update affected chapters and record the audit of remaining pages.
  Preserve historical goal/evidence records and clearly identify older screenshots.
  Track every page in the [documentation audit](tray-documentation-audit.md).
- README flow: download → install → start → find tray → open editor → finish
  stream → quit. Add an illustrated tray guide with Windows hidden-icons arrow,
  Linux panel, actions, confirmations, fallback and missing-icon troubleshooting.
- Update installation, startup, everyday use, updates, backup/recovery,
  troubleshooting, version guidance and developer lifecycle documentation. Use
  short numbered steps, literal labels, explained success states and optional
  Advanced details. No condescending language or assumed terminal knowledge.
- Generate offline HTML from canonical guide and synchronize public wiki and
  navigation. Verify rendered links/images/navigation and offline network-free use.
- Application/package metadata 1.0.1, one version source for builds/packages;
  public API version remains independent. Cumulative release notes include the
  UI redesign, G11–G21, README, DOMPurify fix and trays since v0.1.0.
- Add an explicit publication workflow using pinned actions and successful
  Windows/Linux qualification artifacts from one immutable protected main SHA.
  Keep required checks and fork-secret isolation. Tag v1.0.1 at that exact SHA.
- Create a draft, upload and verify all assets, then publish Latest with
  draft=false and prerelease=false. Retry idempotently; reject conflicting tags
  or assets instead of overwriting. Verify public release/latest/download without
  authentication, then compare freshly downloaded checksums.

### Required assets

1. `TDSBLive-1.0.1-win-x64-setup.exe`
2. `TDSBLive-1.0.1-win-x64.zip`
3. `TDSBLive-1.0.1-linux-x64-wine.tar.gz`
4. `SHA256SUMS.txt`

### Acceptance evidence

Run existing regression qualification, secret scanning, npm dependency checks,
SonarQube and CodeQL, browser/package/recovery checks, plus Linux build/integration
qualification. Test Cancel, commands/duplicates, failures, ownership, lifecycle,
state, credentials, accessibility and tray-service recovery. Verify exact final
packages in actual OBS for rendering/audio, using isolated owned examples without
broadcasting. Actual Windows/Linux desktop evidence and public downloads are
mandatory. Actions artifacts alone are not a published release.

Every goal records status, covered requirements, exact tested SHA, commands and
results, screenshots, CI links and unresolved evidence. Do not complete goals from
mock tests or builds alone. Protected PRs and scoped commits deliver the work;
no branch protections, secret isolation or existing authority boundaries weaken.

### Blockers

Unavailable real desktop/OBS evidence must remain explicit. Preserve established
paid-platform, human-usability and native Windows streaming-PC performance limits.

## Reference choices

- [Avalonia tray API](https://docs.avaloniaui.net/controls/navigation/trayicon)
- [Avalonia Linux platform support](https://docs.avaloniaui.net/docs/platform-specific-guides/linux)
- [Pinned desktop package](https://www.nuget.org/packages/Avalonia.Desktop/12.1.3)
- [GitHub release publication API](https://docs.github.com/en/rest/releases/releases#create-a-release)

## Execution record

2026-10-04: plan saved as the first implementation deliverable. Protected main
baseline is `670c48caf50e20dcd163fa6b40321b0425c30285`. Public releases currently
contain only v0.1.0; v1.0.0 is not published. Implementation goal is active.

G22 initial checkpoint (2026-10-04): shared bounded, authenticated loopback
transport and native Avalonia companion compile. Session credentials are sent
only over parent/child stdin and record formatting redacts them. The host keeps
its existing HTTP/CSRF boundaries, graceful checkpoint and restore owner; external
mode only reports restart-ready after successful post-shutdown work. An exclusive
profile lease and nonsensitive open-request marker prevent duplicate ownership.
31 focused control/lifecycle/browser tests and five headless confirmation tests
pass (Cancel initially focused, Enter cancels, Escape/close cancel, explicit
confirmation admits the named operation). The actual isolated managed host
passes `tools/qualify_desktop_control.py`: nondefault URL, duplicate launch,
tray and browser restart, browser backup restore, quit, retained spaced-path
profile and SQLite integrity. Full local regression passes 435 host tests and 88
core tests, with four Windows-only host tests explicitly skipped on Linux. This is
control-channel evidence, not native tray, packaged Windows/Linux or OBS evidence.
Local builds use the pinned 10.0.401 SDK with `-p:UseSharedCompilation=false`;
the default shared compilation path exits with SIGBUS and is not a passing build.
Windows packaging includes a self-contained companion and checks both binary
versions. Sign-in startup explicitly suppresses browser launch. CI derives version
from Directory.Build.props and runs managed and packaged control-channel checks;
existing browser suites explicitly opt out of desktop launch. Explicit LAN
interface bindings gain a local editor listener while preserving saved addresses,
OBS routes and existing HTTP authority; desktop diagnostics report missing
companion heartbeats. Native Windows
registration/fallback/Explorer behavior, packaged desktop qualification and all
G23/G24 gates remain outstanding. G22 is not complete.

Windows checkpoint (2026-10-04): source `74f0dd56f25bf5682f0c3620df8be880cb981b4d`
builds and passes the Windows unit/integration tests in
[run 37244864657](https://github.com/TechDaddyKB/tdsblive/actions/runs/37244864657),
but fails actual external browser restore qualification. The profile lease was
inside the folder that recovery replaces; Windows denies moving that folder while
its exclusive file handle remains open. Move the nonsensitive lease beside the
profile, retaining ownership throughout replacement. A regression now checks
folder replacement, trailing-separator identity, exclusivity and reacquisition.
The repaired actual isolated process check passes locally; Windows requalification
is required. `tools/qualify_windows_tray.ps1` adds actual isolated-runner native
UI, browser handoff, Cancel focus, Explorer/fallback, crash isolation, diagnostics,
restart/quit and screenshot qualification to both portable and installed packages.
It has not yet run on Windows. It requires an interactive CI desktop and rejects
blank screenshot evidence. Light/dark, high-DPI and final OBS gates remain explicit.

Second Windows checkpoint: `b3027899a75e89adcfb4c84a406fbc808cb5c144` passes
Windows .NET tests in
[run 37245914924](https://github.com/TechDaddyKB/tdsblive/actions/runs/37245914924).
Actual desktop-control lifecycle reaches final database inspection, but cleanup
fails because Python's SQLite context manager does not close its connection.
The qualification script now explicitly closes that owned inspection handle.
Local full regression also exposed a duplicate-launch race: the owner consumed
the open-request marker before the duplicate applied Unix file permissions.
Permissions are now applied atomically at creation, with deletion sharing and
no post-creation path operation. After these fixes, local full regression passes
436 host tests (four Windows-only skips), 88 core tests and five confirmation
tests. Actual isolated process qualification passes restart, restore, quit and
profile integrity. Status text now distinguishes restart, stop and startup,
and a failed address bind reports a separate recovery outcome. Windows native
UI, both Linux runners, OBS and publication remain outstanding.

Quality checkpoint: `a8ca9c059e7a80da0e96c228638254cadbae13e2` passes the
Windows actual control lifecycle and existing frontend, capture, recovery,
finance, rendered-browser and contract checks in
[run 37246586286](https://github.com/TechDaddyKB/tdsblive/actions/runs/37246586286).
Sonar rejects that candidate: 45.7% new-code coverage, reliability and security
findings. Native package qualification therefore has not run. Required checks
remain enforced. New headless control-window tests exercise Cancel/repeated
clicks, close-to-hide, tray-service loss/reappearance, both themes, browser URL
boundaries, pipe validation, connection loss and unsuccessful completion.
Real loopback host-session tests verify external ownership, local handoff,
heartbeat diagnostics and explicit shutdown outcomes. The coverage include now
also names the `TDSBLive.Desktop` assembly. Desktop resources are disposed,
lifecycle orchestration is separated into focused methods, native-call failures
are checked, and qualification executable inputs require existing, correctly
named host/SDK files with no arbitrary flags or scripts.

The updated local solution passes 453 host tests, 88 core tests and 22 desktop
tests with coverage. Four existing Windows-only host tests and the new native
notification-area ownership test remain explicitly skipped on Linux. Two Python
executable-admission tests and the actual owned process lifecycle pass. Local
coverage initially crashes with SIGBUS while writing the `/tmp/.dotnet/shm`
named-mutex mapping; a private `/tmp` mount using `bwrap` succeeds without changing
the system temporary folder. Use that isolated mount plus the pinned SDK and
`-m:1 -p:UseSharedCompilation=false` for local qualification. This is a local test
environment workaround, not native desktop acceptance. Requalification of the
new source on Windows/Sonar, actual native UI, all Linux and final release gates
remain required.

The [desktop lifecycle contract](desktop-lifecycle.md) records launch modes,
profile ownership, control authority, completion outcomes and verification
boundaries for the implementation candidate.

Native-package checkpoint: `89db2ffe8379d7fc9a568475e903cab8e1c1d065` passes
Sonar with 82.2% new-code coverage, A ratings, zero new duplication and all
hotspots reviewed. All-language CodeQL and the existing regression checks pass.
[Run 37248512240](https://github.com/TechDaddyKB/tdsblive/actions/runs/37248512240)
builds both Windows packages, reaches the actual registered Explorer icon, then
fails native menu activation because the pinned Avalonia menu peer does not
expose UI Automation InvokePattern. The harness now uses supported invocation
when available and otherwise verifies owned foreground/keyboard focus before
pressing Enter on a menu item. It also captures the actual tray popup. This
correction has not yet run on Windows; native acceptance remains incomplete.

Follow-up local tests verify the actual light/dark variant on the control window
and preserve known startup failure guidance when a concurrent status connection
closes. Host shutdown keeps service references captured before DI disposal,
retaining the original recovery ownership order. Local full regression passes
453 host, 88 core and 23 desktop tests, with five explicit Windows-only skips;
the desktop coverage run and actual owned process lifecycle also pass. These
follow-up source changes still need exact-head protected requalification.

Requalification source `55de8e7f871a55de47615dc797d971bc048f900c` is pushed in
PR #21. [Windows run 37250701114](https://github.com/TechDaddyKB/tdsblive/actions/runs/37250701114)
passes the regressions/contracts, all-language CodeQL and Sonar (82.0% new-code
coverage, A ratings, zero new duplication, all hotspots reviewed), and builds both
packages. Its native run captures a clear three-action tray menu, activates
**Open editor**, and identifies the correct address in the native browser. The
next browser screenshot fails the bounds check, so remaining native scenarios
are not accepted. Downloaded evidence was scanned after extraction; the actual
tray-menu PNG was visually inspected. The capture helper now waits for layout,
queries visible native-window geometry when available, clips only to the actual
virtual screen, preserves blank-image rejection, and records layout diagnostics
and measured DPI. This correction still needs native Windows execution.

The next native harness revision also exercises Enter, Escape, window-close and button cancellation,
normal manual launch versus quiet launch, and an owned DPAPI marker surviving
tray restart, browser backup restore, crash recovery and Quit. Its integrations stay
disabled; the marker is generated in memory, never printed, and its encrypted
file is scanned before inspection. Successful startup exercises actual vault
decryption; ciphertext hashes verify retention. PowerShell 7.6.0 parses the
updated script successfully using the private temporary mount. This syntax
check does not establish Windows UI behavior. Its in-memory PowerShell
backup/validate/restore requests also pass against an actual isolated managed
backend, with successful external completion and original settings after relaunch.
The native harness records screenshot sizes and measured window DPI; ordinary
DPI screenshots will not be relabeled as high-DPI evidence. The non-CI guard
rejects local execution before inspecting or changing the desktop.

### Current G22 acceptance map

| Acceptance area | Current evidence | Required follow-up |
| --- | --- | --- |
| Authenticated control, bounded messages and one admitted command | Source ec699fb passes all host/control tests and actual owned-process qualification in protected Windows run 37266440100 | Final cumulative artifact requalification in G24 |
| Profile ownership, spaced paths, nondefault port, safe restart/restore, SQLite integrity | Managed, portable and installed actual-process checks and browser lifecycle pass; the downloaded full-run Windows EXE under direct Wine also preserves owned state and clean shutdown | Final cumulative artifact requalification in G24 |
| Native icon, menu, confirmations, Explorer recovery and crash isolation | Source ec699fb passes complete actual portable and installed Windows probes, including accepted Restart/Quit, Explorer re-registration, browser restore, both crash paths and old-companion cleanup | Requalify Windows after shared Linux changes |
| Manual/quiet startup and saved credentials through lifecycle | Source ec699fb passes portable/installed native manual/quiet launch and owned DPAPI credential retention through restart, backup restore, recovery and quit | Requalify Windows after shared Linux changes |
| Light/dark appearance, keyboard access and high DPI | Source ec699fb passes portable/installed actual light/dark confirmations, Cancel keyboard alternatives and 125% physical geometry/focus checks; downloaded screenshots are visually inspected | Requalify Windows after shared Linux changes |
| Installer, optional startup, update/reinstall, uninstall and data retention | Protected full run passes both packages, replacement/reinstall, opt-in quiet-start shortcut, uninstall and retained data | Final cumulative artifact requalification in G24 |
| Existing OBS addresses, rendering/audio and reconnect | Downloaded full-run Windows EXE in owned Wine/native Linux OBS preserves the same Browser Source through restart/restore; actual state, dimensions, transparency and silent/tone/silent post-volume signal are verified | Final cumulative artifact requalification in G24; physical hearing and native Windows OBS are not inferred |

G22 is **Complete** for the qualified and protected Windows scope. G23 is
**In progress** with its prerequisite satisfied; G24 remains **Not started**.
Neither native Linux delivery nor release publication is claimed.

Appearance qualification preparation: the native harness now has a guarded
light/dark phase that observes actual Windows UISettings, renders both packaged
confirmation windows, checks their dominant background luminance, and restores
the CI user's original theme preferences in a finally block. Unsupported theme
observation is recorded as missing evidence, never a pass. A separate trusted
Windows desktop inventory job discovers the CI image's real scaling controls and
options without changing settings. It cannot satisfy or replace the protected
Windows build/test job; fork and Dependabot source cannot receive its secret.
Its generated metadata is scanned before artifact upload. This prepares actual
high-DPI qualification; it does not itself qualify high DPI.

The three PowerShell scripts parse successfully under the isolated temporary
mount, and the appearance/inventory guards reject non-CI execution before desktop
access. All changed sources pass deterministic secret scanning and whitespace
checks. Native execution of these additions remains required.

Source `fd5b16e158db838d373f35c063e6e2cd35e7e25c` passes protected Sonar and
regression steps in [run 37252517405](https://github.com/TechDaddyKB/tdsblive/actions/runs/37252517405),
then fails the browser-close scenario. Downloaded evidence was scanned after
extraction and visually inspected: capture now works, but Edge's own first-run
welcome screen covers the editor and prevents closing its window. The harness
now temporarily enables Microsoft's documented [HideFirstRunExperience policy](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/HideFirstRunExperience)
only for the isolated CI user, restores its original value in finally, and
requires visible **Your streaming workspace** content in addition to the correct
browser address/title. TDSBLive application code does not set browser policies.
The first-run screenshot is environmental evidence, not a passing editor visual.
The updated native harness, appearance phase and display inventory still need
exact-head Windows execution. G22 remains incomplete.

Display discovery source `199f79facb308b6c3cdbc02a6f7b006c94d62e5b`:
the separate desktop inventory job passes in
[run 37254730781](https://github.com/TechDaddyKB/tdsblive/actions/runs/37254730781).
Its downloaded, decompressed metadata passes scanning. Actual UISettings is
available with a white background, and the display page exposes enabled
`SystemSettings_Display_Scaling_ItemSizeOverride_ComboBox`, named
**Change the size of text, apps, and other items**, with **100% (Recommended)**
and **125%** options. The resolution selector is also enabled. Discovery changes
no settings. This identifies a safe native scaling control for the next actual
application check; it is not a passing high-DPI result. The main Windows job
for this source remains active at this checkpoint.

Prepared native scaling qualification uses that observed selector on the owned
CI desktop. It saves the selected scale, chooses the highest offered scale up to
200%, and requires the packaged Restart/Quit windows to report the corresponding
actual `GetDpiForWindow` value. Both buttons must remain visible, enabled,
unclipped and at least 44 logical pixels tall; Cancel must retain initial focus
and work with Enter. Screenshots record actual physical dimensions/DPI. A finally
block restores and verifies the original selection, then closes the owned
Settings window. It does not change resolution or synthesize DPI messages.
Secret scanning, PowerShell parsing and the non-CI guard pass locally. Actual
Windows execution remains pending; this preparation is not high-DPI acceptance.

Source `199f79facb308b6c3cdbc02a6f7b006c94d62e5b` completes regression,
Sonar and Windows packaging in
[run 37254730781](https://github.com/TechDaddyKB/tdsblive/actions/runs/37254730781),
then fails native Explorer re-registration. Downloaded/decompressed evidence
passes scanning. Actual screenshots now show the editor content, readable
light/dark confirmations and the missing-tray control window. These establish
the earlier browser/theme scenarios, not a complete portable/installed pass.
The companion now retries registration of its existing icon when the shell
returns but the actual registration probe still fails. This keeps the menu,
session and backend intact; pinned Avalonia tooltip updates alone use MODIFY
and cannot recover a failed ADD. A regression case covers recovery without
replacing the icon/session or reopening a hidden fallback. The native failure
path also records shell availability and owned process state. Current-build
native recovery and scaling evidence remain required.
Local pinned .NET desktop qualification passes **24 tests**, with the one
Windows-only ownership test explicitly skipped on Linux. It runs under the
owned temporary-directory mount with `--configuration Release --no-restore
-m:1 -p:UseSharedCompilation=false --settings coverage.runsettings
--collect:"XPlat Code Coverage"`; OpenCover/TRX remain ignored local evidence.

Documentation preparation inventories all 76 tracked Markdown files after
scanning, with per-page review/update destinations in the documentation audit.
All 76 audit links resolve. Plain-language tray, desktop companion and start-at-
login definitions are prepared. Final documentation, offline/wiki publication,
Linux delivery and OBS qualification remain outstanding.

Source `119694c8304fb35c79354be220d8ae80504d03b8` passes the existing build,
regression and Sonar steps in
[run 37256469350](https://github.com/TechDaddyKB/tdsblive/actions/runs/37256469350),
then fails opening **Restart** after the scaling step. Scanned native artifacts
contain the earlier editor/theme screenshots but no completed high-DPI or
Explorer recovery evidence. The harness now waits for the owned Settings window
to disappear, retries opening only a missing/dismissed tray menu, and retains
foreground/focus checks before keyboard activation. It records phase, owned
windows, registration/foreground status, screenshot DPI and restored scaling
selection on failure. The independent inventory returned an empty control list
after finding its window; it now waits explicitly for display controls rather
than treating an empty page as completed discovery. PowerShell parsing passes;
actual native scaling/recovery still must pass on the updated source.

Preparation adds an illustrated Windows tray chapter and three real owned
screenshots from source `199f79f` to canonical navigation. The offline build has
**21 chapters and 15 images**. A network-disabled browser verifies chapter links,
images, tray navigation and reflow at **320, 390 and 1366 pixels**. Candidate and
Linux/publication limits remain explicit; the public wiki is not updated yet.

The owned OBS fixture helper passes real managed-host overlay/asset setup,
stored state, authenticated restart/restore, explicit successful relaunch,
graceful quit and SQLite integrity without touching OBS or playing audio.
The current host was rebuilt with the pinned SDK before recording the pass;
an older local binary first timed out and is not acceptance evidence. The shared
HTTP helper now accepts legitimate empty 204 responses, exercised by actual
custom-state writes. The existing desktop process qualifier also passes after
that change. OBS 32.2.2/WebSocket 5.7.4 remains connected on Omarchy, with Browser
Source available and streaming/recording inactive (2026-10-05 02:44 UTC). No OBS
scene/output was changed. Final packaged OBS rendering/reconnect/audio, native
Linux delivery and release publication remain required.

The owned OBS fixture now accepts an explicit Linux Wine runner with the
packaged Windows EXE. It creates a fresh private prefix, preserves individual
arguments including spaced paths, and keeps control capabilities on stdin.
Cleanup waits only for that prefix's server; it never kills a Wine server and
retains temporary files if waiting fails. Five executable/ownership admission
tests pass. The unchanged managed-host fixture also passes actual isolated
restart/restore, retained overlay/custom state, quit and SQLite integrity after
this addition. Current Windows source `53ecc97` is under qualification in
[run 37258964197](https://github.com/TechDaddyKB/tdsblive/actions/runs/37258964197).
The Wine path still needs the current qualified Windows package for actual
execution. G22 remains in progress; G23/G24 prerequisites remain in force.

Managed-source OBS preparation (2026-10-05): an owned 1000×600 Browser Source
in native OBS renders the sample text, image, progress bar and stored custom
state. With that source left open, authenticated restart reconnects at progress
75/state 2; backup restore reconnects at progress 25/state 1. Actual screenshots
are scanned and inspected in ignored `artifacts/tray-managed-obs-preparation`.
The helper quits cleanly and removes its temporary profile after SQLite checks.
The previous OBS program scene is restored; the owned scene/source are removed.
Streaming and recording remain inactive. No tone is played. This verifies fixture
rendering/reconnect on the local managed build, not the final Windows package,
native tray or current-package audio acceptance.

Source `53ecc97` passes builds, regression, Sonar and Windows packaging in
[run 37258964197](https://github.com/TechDaddyKB/tdsblive/actions/runs/37258964197),
then fails the first **Open editor** interaction. Scanned native diagnostics show
the backend ready, Explorer available and the icon registered, with no owned
popup window. The new retry callback's `$probe` state was shadowed by the
readiness helper's case-insensitive `$Probe` parameter, so it never posted the
menu-open message. Executing that actual helper in isolated PowerShell reproduces
the scope collision and verifies that renamed `$menuState` remains accessible.
The harness now uses that name. This is a qualification bug fix, not new native
acceptance; updated portable/installed tray execution remains required. The
independent scanned display inventory now discovers enabled real controls and
the 100%/125% choices; it still does not establish high-DPI tray behavior.

Further documentation preparation adds version-aware Windows startup, tray
handoff, quiet sign-in, safe quit, updates and recovery instructions to six
existing guide chapters, retaining browser controls for older downloads.
The unpublished v1.0.1 status is explicit. The refreshed offline guide passes
network-disabled links/images and all 21 chapters at 320/390/1366 pixels.
That broader check found two unwrapped viewing URL links in the chat chapter;
offline link styling now wraps them without changing the destinations. Final
packaged/wiki documentation and G24 acceptance remain outstanding.

Source `dc7e173` stops before native packaging in
[run 37260439684](https://github.com/TechDaddyKB/tdsblive/actions/runs/37260439684):
two new Wine fixture tests attempted Linux drive symlinks on Windows after
mocking the platform. They now run only on actual Linux; the explicit rejection
of a Windows platform still runs everywhere. All five pass locally on Linux.
No existing Windows qualification is skipped or weakened.

An additional trusted Windows job builds the same current package and runs the
actual portable tray harness in parallel with the full regression pipeline.
Its scanned artifacts are explicitly labeled diagnostic builds and cannot
replace the required build/Sonar checks, installed-package qualification or
release-candidate artifacts. This allows native desktop issues and owned OBS
preparation to be investigated without waiting for unrelated suites each time.
The new job itself remains unverified until its actual Windows execution.

Source `5cecbf0` builds diagnostic ZIP/installer artifacts and reaches actual
125% Windows display scaling in both native jobs in
[run 37261243100](https://github.com/TechDaddyKB/tdsblive/actions/runs/37261243100).
Regression, SonarCloud, CodeQL and desktop inventory pass; portable native
qualification fails the scaled confirmation-button geometry check. The scanned
diagnostics show the fallback and confirmation windows open together. The
harness searched the whole process for **Restart**, allowing it to select the
fallback's disabled button. Confirmation buttons now resolve within their owned
dialog. Scaling checks retain enabled, visible, contained and 44-pixel minimum
requirements; screenshots and per-button geometry are recorded before failure.
Both modified scripts pass actual PowerShell parsing. This correction still
requires native execution; installed qualification and G22 remain outstanding.

Source `7f21575` completes the diagnostic package job but the portable tray probe
in [run 37263259477](https://github.com/TechDaddyKB/tdsblive/actions/runs/37263259477)
stops immediately after opening the actual 125%-scaled confirmation: its controls
or initial Cancel focus are not yet observed. The native diagnostic record has
no per-button geometry and no scaled screenshot, so it cannot distinguish an
initial automation frame from a persistent focus problem. Qualification now
captures the scaled window before that check and waits boundedly for the owned
dialog's controls and Cancel focus, without setting focus itself. A failure also
records which controls exist and their focus state. All geometry and keyboard
requirements remain enforced. Actual execution of this correction is pending.

Wine fixture preparation (2026-10-05): real fresh-prefix runs exposed two helper
problems: precreating drive mappings prevented C: initialization, and disabling
`mscoree` throughout launch prevented CoreCLR from loading `System.Runtime.dll`.
The latter failure was diagnosed from scanned owned-process output, not assumed
to be a backend defect. Initialization now lets Wine create its drives and
suppresses optional Mono/Gecko prompts only during `wineboot --init`; backend
launch retains builtin `mscoree`. Six admission/ownership tests pass on Linux.
The actual diagnostic Windows package from `5cecbf0` then passes the fresh owned
fixture: overlay/assets, persisted widget state, authenticated restart/restore,
SQLite checks and graceful quit. Its prefix/profile are removed after the scoped
Wine server finishes. Failed test prefixes are retained; cleanup stops only a
server whose exact owned prefix was independently verified, with existing Wine
applications untouched. This is diagnostic package preparation, not native tray,
final-package OBS, audio or G23 acceptance.

Actual Wine-package OBS preparation (2026-10-05): native OBS renders the owned
1000×600 overlay from the diagnostic Windows package. Keeping the same Browser
Source open, authenticated restart reconnects with progress 75/state 2; backup
restore reconnects with progress 25/state 1. Scanned and visually inspected PNGs
in ignored `artifacts/tray-wine-obs-preparation` retain transparent empty corners
and opaque content. The previous OBS program scene is restored, the owned
scene/source are removed, and the fixture quits and removes its private profile.
Streaming/recording stay inactive; no tone is played. This extends the managed
fixture evidence to an actual Windows package under direct Wine, while final
release packages, native desktop behavior and audio remain outstanding.

README preparation now explains finding the Windows icon, opening the editor,
save-before-quit, confirmations and fallback controls. It explicitly identifies
the public v0.1.0 download and unpublished v1.0.1 candidate; final Linux companion
instructions and published-release wording are still required before G24 delivery.

Read-only Linux qualification inventory (2026-10-05): this workstation exposes
Wayland and X11/XWayland, installed direct Wine, Xvfb, D-Bus session tooling and
busctl. UMU, KDE Plasma launchers, Xephyr and Weston are not installed. These
observations identify future G23 environment work; they do not qualify either
runner/desktop combination or change the prerequisite on G22. All owned Wine
fixture processes have finished; existing Wine applications were not stopped.

The full `7f21575` pipeline passes regression, SonarCloud and packaging, then
fails the scaled geometry check. Its scanned 120-DPI screenshot shows enlarged,
readable buttons; automation reports client width 440 and button height 44 while
the screenshot is 568 physical pixels wide. This indicates a measurement-unit
mismatch, rather than proving clipped or undersized rendering. Geometry reads
now use the same per-monitor-aware thread context as screenshots and compare
automation bounds with an independently queried native client rectangle. The
44-pixel logical minimum, scaled to physical pixels, remains unchanged;
mismatched coordinates still fail. PowerShell parsing and compilation of the
actual native helper pass locally; no Windows APIs are invoked by those checks.
This follows Microsoft's [UI Automation scaling guidance](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-screenscaling).
Native execution is required to verify the correction; no DPI acceptance is claimed.

Source `e70a948` passes actual portable Windows 125% scaling in
[run 37265069796](https://github.com/TechDaddyKB/tdsblive/actions/runs/37265069796):
both dialogs report 120 DPI, 55-pixel buttons, Cancel focus, enabled/visible
controls, and automation bounds identical to independent native client bounds.
Keyboard Cancel works and the original scale is restored. The probe then fails
Explorer recovery: the owned shell, companion and backend are alive, but the
icon rectangle cannot be obtained. Missing-service controls were shown and
closing them kept the backend running. Installed and full final native checks
remain pending.

The registration adapter and native harness now supplement the rectangle query
with a flags-zero `NIM_MODIFY` probe of the same owned HWND/ID. No icon fields
are valid for modification, so the request adds no icon and changes no tooltip,
image, message or visibility setting. This follows the documented x64
[NOTIFYICONDATAW layout](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-notifyicondataw)
and [Shell_NotifyIconW result contract](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw).
The purpose is to distinguish registration from availability of a screen position;
the cause of the Explorer failure and success of this correction still require
actual Windows evidence. No recovery pass is inferred from the change.

Local validation of the registration change uses the pinned SDK: the desktop
suite passes 24 cases, with both native Windows tests explicitly skipped on
Linux. The new native test creates only a hidden, uniquely named owned message
window with no icon and verifies that repeated registration probes remain false.
It destroys that window and unregisters its class afterward. Actual Windows
execution of that test remains required. The modified PowerShell harness parses,
its actual C# helper compiles, and the x64 probe layout measures 976 bytes.
Scanned `e70a948` Restart/Quit high-DPI and missing-tray screenshots are visually
inspected; their wording, spacing and Cancel-first controls are readable.

### Native Windows milestone — 2026-10-05

Source `ec699fb1549353160367903e594556830ded1598` passes the complete
**Windows native tray probe** in
[run 37266440100](https://github.com/TechDaddyKB/tdsblive/actions/runs/37266440100),
job `111624355065`. The downloaded `windows-native-diagnostics` artifact is
decompressed and secrets-scanned before inspection. Its manifest records the
same source SHA and labels the packages diagnostic, not approved for release.
The native `tray-result.json` reports actual packaged Windows UI evidence for:

- Native registration, quiet/manual startup, ordinary browser handoff, browser
  close, duplicate launch, and accessible Open editor/Restart/Quit controls.
- Initial Cancel focus and cancellation through button, Enter, Escape and window
  close; neither backend generation nor saved state changes on cancellation.
- Explorer loss, fallback, fallback close, and successful icon re-registration.
- Accepted restart, browser backup restore, old-companion cleanup, companion
  crash isolation, browser recovery, backend crash guidance without automatic
  restart, accepted quit and retained profile.
- An owned DPAPI credential surviving restart, restore, recovery and quit.
- Actual light/dark dialogs and 125% Windows scale at 120 DPI, including keyboard
  Cancel and restoration of the original 100% scale.

The current Restart, Quit and stopped-backend screenshots are also visually
inspected after scanning. Labels and warnings are readable and the confirmations
clearly offer Cancel first. The successful probe establishes Explorer recovery
for this source; it does not prove that icon overflow caused the earlier failure.

The separate full Windows job `111624355263` is still running at this checkpoint.
Its regression, SonarCloud, installed-package and final-package OBS/audio gates
remain independent requirements. G22 is still **In progress**; G23 and G24 retain
their prerequisites. No release publication or Linux companion is claimed.

### Current-source OBS and audio-signal preparation — 2026-10-05

The diagnostic Windows ZIP from the same `ec699fb` native run is safely extracted
into an owned directory and scanned after decompression. The shipped Windows
EXE runs under Wine 11.17 Staging in a fresh private prefix and isolated sample
profile, with integrations and LAN disabled. Native Linux OBS 32.2.2 keeps one
owned 1000 × 600 Browser Source open throughout authenticated restart and backup
restore. Scanned, visually inspected screenshots show progress 25/state 1 before
the test, progress 75/state 2 after restart, and progress 25/state 1 after restore.
PNG inspection independently confirms a transparent empty corner, alpha values
from 0 to 255, and unchanged 1000 × 600 dimensions.

A read-only observer uses the installed `obs_studio` client's
`InputVolumeMeters` subscription for this owned Browser Source only. No recording
or broadcast starts, no production input is changed, and credentials are neither
printed nor persisted. An explicitly triggered one-second sample tone produces:

| Phase | Before lifecycle | After backup restore |
| --- | --- | --- |
| Silent baseline | 292 samples; peak 0 | 563 samples; peak 0 |
| Explicit tone | 18 signal frames; peak 0.0799901 | 18 signal frames; peak 0.0799901 |
| Silent after removal | 144 samples; peak 0 | 331 samples; peak 0 |

These are actual OBS post-volume/mute signal measurements, not playback receipts
or settings. They establish delivery of the owned audio signal to OBS; physical
speaker/headphone listening and final-release-artifact audio acceptance remain
unverified. This is Windows-under-Wine/native-Linux-OBS evidence, not native
Windows OBS evidence. The prior program scene is restored, the owned scene and
input are removed, the fixture quits gracefully, and its private profile/prefix
are removed after SQLite integrity and owned Wine shutdown checks. Stream and
record outputs are confirmed inactive before and after qualification.

### G22 acceptance and protected delivery — 2026-10-05

The full [Windows run 37266440100](https://github.com/TechDaddyKB/tdsblive/actions/runs/37266440100)
for source `ec699fb1549353160367903e594556830ded1598` completes successfully.
Native tests pass **88 Core, 26 Desktop and 457 Host** cases, including both real
Windows registration cases; frontend tests pass **252** cases. Managed, portable
and installed browser/parity suites and actual-process recovery pass. Both full
native package probes independently pass all listed tray/lifecycle scenarios,
light/dark and 125% scale. Replacement/reinstall, startup off by default, opt-in
quiet startup, uninstall and retained data pass. The actual packaged guide passes
21 chapters, images, local navigation and network-disabled reflow at
320/390/1366 pixels. SonarCloud passes with A ratings, **83.4% new-code coverage**,
**0% duplication** and **100% hotspot review**; CodeQL passes.

Both full-run artifacts are downloaded and their SHA-256 values verified:

| Package | SHA-256 |
| --- | --- |
| `TDSBLive-1.0.1-win-x64.zip` | `53340cbb8d82a1491bf320a7042174e04660f4acff6c2ef38692425f6e80803c` |
| `TDSBLive-1.0.1-win-x64-setup.exe` | `f39eb3af465e1110ea5317726212a509b1377e235ebf86b154578ae478e31ddc` |

The safely extracted and scanned full-run Windows EXE is then actually exercised
in a fresh owned Wine prefix and native Linux OBS, separately from the diagnostic
package preparation. The same Browser Source shows progress/state **25/1 → 75/2
after restart → 25/1 after restore**. Scanned screenshots are visually inspected;
dimensions remain 1000 × 600, alpha spans 0–255 and the empty corner is transparent.
After restore, actual OBS meters report **324 silent samples**, **19 tone frames
at peak 0.0799901**, and **339 silent samples afterward**. This measured delivery
evidence retains the approved physical-listening and native-Windows-OBS limits.
The owned source/scene are removed, prior program scene restored, fixture quits,
SQLite integrity passes, and private prefix/profile are removed. Recording and
streaming remain inactive.

[PR #21](https://github.com/TechDaddyKB/tdsblive/pull/21) merges normally through
strict protected checks at `5ee550803cf18fa786e4edc2651fd53ecf035643`. Its tracked
tree matches the qualified source; no branch protection or fork-secret boundary
is bypassed. G22 is **Complete**. G23 starts from that merged delivery. This
does not publish 1.0.1 or complete G24; the final cumulative Windows/Linux artifacts
and public downloads must still be qualified together at one protected main SHA.

The post-merge [main run 37269145734](https://github.com/TechDaddyKB/tdsblive/actions/runs/37269145734)
also completes successfully at `5ee550803cf18fa786e4edc2651fd53ecf035643`, including
the full Windows job, actual native tray probe and desktop inventory. This is
separate current-main confirmation; it does not qualify later Linux changes.

### G23 initial implementation checkpoint — 2026-10-05

Branch `codex/tray-linux-v1.0.1` starts from merged G22. Source `1febf41` adds
nonsensitive runner/prefix/application/profile selections and private atomic
launcher-settings storage. Existing profiles must remain present; loading a
missing or empty saved setup cannot silently create a new one. New setup is an
explicit separate choice and refuses a nonempty destination. Discovery keeps
multiple existing profiles visible rather than selecting an arbitrary one.

Launch construction keeps file paths as separate arguments, selects external
desktop ownership, and leaves editor handoff to the native companion. A profile
inside C: retains its Windows path; an existing external profile uses an actual
selected-prefix drive mapping. UMU preserves its explicit Proton folder and
prefix. Backend DLL overrides retain unrelated choices and remain child-local.
No session capability is a member of the saved settings or launch arguments.
The control-pipe launch itself still requires implementation and real runner tests.

Pinned-SDK desktop tests pass **46 cases with two Windows-only skips** on Linux
(48 total), including actual Unix drive symlinks, 0600 settings-file permissions,
atomic replacement without touching an unrelated symlink target, malformed/large
file rejection, explicit new setup and unchanged existing sample settings. Secrets
scans and diff checks pass. No Wine/UMU process is launched by these model tests.

G23 remains **In progress**. Native first-run UI, backend lifecycle ownership,
actual StatusNotifierItem registration, per-user app-menu installation, packaging,
both runners/desktops and resource qualification remain outstanding. The native
Linux application is not yet delivered or claimed usable. G24 remains **Not started**.

### G23 first-run window checkpoint — 2026-10-05

Source `50ccdfd` adds a native Avalonia first-run window with named Wine/UMU
choices, local file/folder pickers, existing-profile selection and an explicit
new-setup checkbox. Switching runners remembers each custom program path.
Multiple discovered profiles require a choice; a missing existing profile never
becomes a new profile implicitly. Validation and start failures keep selections
available, and an in-flight start cannot be duplicated or dismissed midway.

The header and fields scroll together while Start/Cancel remain outside the
scroller. Nine headless form cases pass, including light/dark layouts at
320×360 and 640×700, accessible names and 44-pixel buttons. The complete pinned
desktop test suite passes **55 cases with two Windows-only skips** (57 total):
`dotnet test tests/unit/ExtensionSuite.Desktop.Tests/ExtensionSuite.Desktop.Tests.csproj
--no-restore -m:1 -p:UseSharedCompilation=false`, using SDK 10.0.401 and an owned
isolated temporary directory. Secrets scanning and staged diff checks pass.

These cases exercise form behavior with owned files, not Wine/UMU execution or
actual desktop rendering. The window is not yet wired into production startup.
Backend ownership/pipe/lifecycle, actual tray registration, application-menu
installation, packages and runner/desktop/resource evidence remain outstanding.
G23 remains **In progress**; G24 remains **Not started**.


Additional `5843c49` owned direct-Wine probe: after a separate startup, disposing
the native process/control reader handles leaves the backend reachable. Desktop
polling stays absent for 15 seconds, including its degraded-controls warning;
the same owned configuration and Running state remain available. The retained
test-driver capability then admits graceful Quit, and the owned Wine server
finishes before cleanup. SQLite integrity passes again. This qualifies closed
companion handles, not an actual killed native GUI process or tray recovery.

### G23 native backend bridge checkpoint — 2026-10-05

Source `5843c49` connects the native first-run window and saved launcher choices
to external backend startup. A fresh control capability travels only through
redirected stdin; stdout/stderr are discarded with bounded buffers, retaining
only the nonsensitive control-port marker. Incompatible/incomplete older app
folders fail before launch. New direct-Wine prefixes are initialized before .NET
startup without precreating Wine drive mappings or terminating prefix servers.

The native lifecycle loop requires both `restart-ready` and a zero process exit
before replacement. Failed/stopped outcomes or lost control connections show
recovery guidance without automatic relaunch. Disposing desktop controls closes
owned handles without killing the backend. Saved choices are written only after
the host reaches Running; a settings-write failure keeps current controls usable.
Manual starts use the native browser handoff. Windows attachment behavior remains
covered by the complete desktop regression suite.

Pinned local desktop tests pass **72 cases with two Windows-only skips** (74
total), including external completion/re-attachment, failure recovery, malformed
port markers, large private output and reader-error redaction. An ignored owned
native .NET probe additionally runs this actual process/pipe bridge against the
downloaded full-run Windows package from `ec699fb` under direct Wine 11.17. It
uses a separate prefix, Xvfb display, nondefault HTTP port and explicitly disabled
integrations/LAN. Startup, authenticated graceful restart, a fresh capability,
the same existing profile/configuration, authenticated graceful Quit and zero
process exits pass. The scanned owned SQLite database passes `integrity_check`.
Only the owned Wine server is waited on; the temporary profile/prefix/display
are cleaned up. No OBS settings, production profiles or broadcasting are changed.

This probe exercises `LinuxBackendProcess` directly, not the complete native
desktop lifecycle loop or an actual tray/menu. Its first prefix is prepared by
the established owned Wine fixture, so the new-user initialization branch is not
yet qualified by this evidence. Actual browser/dialog/menu operation, prefix
alias ownership, StatusNotifierItem registration/recovery, UMU, named desktops,
per-user app-menu installation, resources and final packaging remain outstanding.
Until registration probing is implemented, Linux conservatively offers fallback
controls rather than treating an Avalonia exporter as proof of a working tray.
G23 remains **In progress**; G24 remains **Not started**.

### G23 registration, native rendering and lifecycle coverage checkpoint — 2026-10-05

Source `77ce3ce` checks actual Linux StatusNotifierItem ownership through the
session bus. The watcher must report a host and exactly one active TDSBLive item
owned by this process. Service aliases are deduplicated; an exported item alone,
a foreign PID, stale item, duplicate owned item or changing watcher owner does
not establish a usable tray. Seven real private-D-Bus tests qualify these
protocol cases without changing the user's desktop watcher.

Source `fba2917` bundles the pinned Inter font and constrains the vertical setup
form to its viewport, including long paths and wrapped checkbox text. The actual
production entry point runs on an owned Xvfb display with the existing Omarchy
watcher. Its single owned item is registered; the exported menu contains Open
editor, Restart and Quit, disabled before backend startup. Scanned 640 × 700 and
320 × 360 screenshots are visually inspected. Escape closes setup with exit code
zero, without starting Wine, creating a prefix or saving launcher choices.
This is actual setup rendering and registration/menu metadata evidence; it does
not qualify visible panel/menu interaction, runner startup or service recovery.
Ignored evidence is retained under `artifacts/tray-linux-native-inventory`.

Source `f0223d8` fixes a recovery defect found by new startup tests: unsupported
or oversized launcher settings raise InvalidDataException and previously escaped
the recovery handler. They now show guidance without replacing launcher.json or
starting a backend. The native setup window and lifecycle task are exposed only
internally for inspection by the existing test assembly.

The Release desktop suite passes **92 cases with two Windows-only skips** (94
total). Thirteen new process/startup cases use owned Python transport fixtures,
not Wine or UMU. They exercise private stdin bootstrap, concurrent bounded output
draining, fresh restart capabilities, selected profile retention, explicit new
prefix initialization and failure, incomplete application rejection, malformed
markers, cancellation, closed companion handles, first-run/forced setup,
launcher recovery, browser-handoff requests, graceful restart/quit and failed
process exit without automatic relaunch. Actual Wine initialization, GUI crashes
and native-browser launching remain separate outstanding acceptance evidence.

Commands include pinned SDK `dotnet restore ... --locked-mode` and `dotnet test
tests/unit/ExtensionSuite.Desktop.Tests/ExtensionSuite.Desktop.Tests.csproj
--configuration Release --no-restore --settings coverage.runsettings
--collect:"XPlat Code Coverage" --logger trx`. Tests run with an isolated temporary
directory. Five coverage-transfer Python tests pass, and actual local OpenCover
export/import passes. Secret scans and diff checks pass. Generated reports and
owned fixtures stay ignored.

The last pushed Windows run at `62c883e`,
[37275159262](https://github.com/TechDaddyKB/tdsblive/actions/runs/37275159262),
passes native tray and functional qualification but fails Sonar's new-code
coverage gate at **64.0% against 80%**. Source `f0223d8` adds a Linux desktop CI
job and imports its scanned OpenCover evidence into the same run's Windows
analysis. Import requires the exact checkout commit and changes only source
paths; measured visits and branches remain unchanged. Windows analysis depends
on successful Linux tests. The existing quality threshold, coverage exclusions,
protected checks and fork-secret isolation are retained. Current hosted CI and
the resulting quality gate are not yet claimed passing.

G23 remains **In progress**. Actual menus/dialogs/browser and tray-service
recovery on the named desktops, both runners, fresh-prefix Wine initialization,
crash/restore behavior, canonical profile ownership across aliases, per-user
launcher installation, resource comparison and final Linux packaging remain
outstanding. G24's full documentation and cumulative release publication remain
unfinished. These are implementation/qualification tasks; no new user approval
is required to continue the authorized work.

### G23 alias and applications-menu checkpoint — 2026-10-05

Source `eca8553` resolves Linux directory links before selecting the Windows
data-directory argument. Wine's normal user name and Proton's steamuser alias,
prefix aliases and profile links under mapped drives select the same physical
profile. Saved runner, prefix and selected data-folder strings remain unchanged.
Discovery shows one entry for aliases of one setup while retaining distinct
setups. Four new actual-filesystem cases pass, including new destinations beneath
existing linked parents and bounded rejection of link cycles.

Existing C: links to an external setup remain usable even in a custom prefix
without Z:. Their original reference is retained when no canonical drive mapping
exists. Exclusive ownership across multiple such fallback aliases is not claimed;
that compatibility case and actual runner/restore ownership evidence remain open.

Source `80edb78` offers **Add TDSBLive to my applications menu**, unchecked by
default in native setup. It writes a per-user desktop entry with a packaged icon
and an explicit Change Linux setup action. No root installation or sign-in
startup is added. Installation runs only after an explicit checkbox choice and
before backend admission; failure keeps the form editable and allows starting
with the box unchecked. The app folder must stay in place.

The desktop entry uses GNU env's directory argument to execute the fixed native
app name directly. It does not invoke a shell or include runner choices, Windows
settings, credentials or session capabilities. String and argument quoting follow
the [desktop-entry specification](https://specifications.freedesktop.org/desktop-entry/latest/exec-variables.html).
An actual owned GIO launch passes with spaces, Unicode, dollar/percent signs,
quotes, backslashes, apostrophes, ampersands, parentheses and equals signs in the
app folder. Five installer/launch cases and three UI opt-in/failure cases pass.
Atomic replacement replaces a shortcut link without changing its unrelated
target. These are owned launcher tests; panel visibility on the named desktops
and launching the final packaged application remain outstanding.

The current Release desktop suite passes **104 cases with two Windows-only
skips** (106 total), with locked normal restore retained. The actual production
setup is also rerun on its owned Xvfb display. Scanned wide, narrow and scrolled
shortcut-section screenshots are inspected; the option is visible and unchecked,
the footer remains available, and Escape exits without creating a prefix or
launcher settings. The existing desktop watcher is not restarted or altered.

Hosted [run 37309907328](https://github.com/TechDaddyKB/tdsblive/actions/runs/37309907328)
at `f7dd60d` proves successful Linux tests and same-commit coverage import:
Sonar's new-code coverage is **90.3%**, above the unchanged 80% threshold. The
overall run still fails. Sonar flags the download command's unrestricted redirect
protocol (githubactions:S6506); `80edb78` restricts initial requests and redirects
to HTTPS. That restriction concerns CI downloads only; TDSBLive's existing HTTP
support remains available. The native Windows package probe independently fails
NU1004 because its runtime-specific lockfile lacked the explicit pinned Inter and
D-Bus references. Source `541d0c8` updates that lockfile without upgrading package
versions, and actual locked win-x64 restore passes locally.

Current hosted qualification after those corrections is still required. G23
remains **In progress**. Native menus/dialogs/browser, Wine/UMU and desktop
matrices, fresh-prefix initialization, crash/restore/service recovery, alias
ownership, resources and packaged Linux installation remain unfinished.
G24 remains **Not started**, with final beginner guidance, wiki/offline
synchronization and public cumulative v1.0.1 assets still required.

### G23 hosted gate diagnosis and cancellation repair — 2026-10-05

Hosted [run 37313451253](https://github.com/TechDaddyKB/tdsblive/actions/runs/37313451253)
at source `37b377d539288b2d938e1b2f42d888d6e8653469` completes the Linux desktop
tests, native Windows tray probe, CodeQL and all Windows functional checks
successfully. Sonar's security rating is A, new-code coverage is **90.1%**,
duplication is 0% and reviewed hotspots are 100%. The overall workflow fails
the reliability gate on csharpsquid:S8949: the optional Linux applications-menu
installation task does not explicitly receive cancellation. Windows release
packaging is consequently skipped. This is a fixable source issue, not a pending
user approval or a reason to mark the active implementation goal blocked.

The shortcut task now receives the companion's shutdown cancellation token.
Local pinned-SDK Release desktop qualification passes **104 tests**, with the
two Windows-only registration cases skipped on Linux (106 total, zero failures).
The six Linux package admission tests also pass. The corrected commit still
requires hosted analysis; these local results do not clear that gate or qualify
a release package. G23 remains **In progress** and G24 remains **Not started**.

### G23 Linux distribution pipeline checkpoint — 2026-10-05

The Linux bundle producer now combines a self-contained native x64 companion
with the complete Windows application from the same source commit. Windows
packaging includes a version/commit/target manifest; the Linux producer rejects
a mismatched manifest or checksum rather than relabeling an older backend.
Linux runtime lockfiles retain Avalonia 12.1.3 and the existing dependency pins.
The native publish includes Microsoft.NETCore.App **10.0.12**, selected by the
pinned 10.0.401 SDK, so users do not need a separate .NET installation.

Archive admission rejects traversal, links, encrypted entries, duplicate paths,
oversized expansion and private/runtime content. Extraction is scanned before
provenance inspection. The tar preserves executable permissions, fixes ownership
and timestamps, and carries the starter instructions, complete offline guide,
application license and Inter/Avalonia notices. The Inter notice corresponds to
the bundled font's 3.019 metadata and source commit
`0a5106e0bde18df09374066bf3a7998e3546307d`. Other dependency notice auditing remains
part of G24; these two notices do not establish a complete licensing audit.

The six package admission tests pass locally. Workflow YAML parses and the new
Linux package job depends on successful Windows qualification, downloads only
that run's Windows candidate, checks the exact checkout commit, builds the
bundle and exercises its native setup on an isolated X11 display/session bus.
It uploads candidates, not a GitHub release. Actual same-run bundle production
and the new hosted native package qualifier remain unverified until CI executes.

Separately, an actual self-contained native publish from `86237ef` passes owned
Xvfb first-run rendering at 640x700 and 320x360, registration of its own SNI item
with the existing Omarchy watcher, exported menu metadata, and Escape with exit
0. Cancel creates neither a Wine prefix nor saved choices. The narrow screenshot
shows wrapped introductory text, a reachable runner selector, contained scrolling
and visible Cancel/Start buttons. This is production native rendering evidence,
not headless UI assertions, a displayed tray-menu interaction, or a complete
bundle/Wine/UMU qualification. G23 remains **In progress**; its other acceptance
requirements and G24 publication remain outstanding.

### G23 cross-platform fixture and actual UMU diagnosis — 2026-10-05

Hosted [run 37318426039](https://github.com/TechDaddyKB/tdsblive/actions/runs/37318426039)
at `54cc472` passes Linux desktop tests and the native Windows tray probe, but
stops in Python replay tests. Python's Windows ZipInfo constructor normalizes a
backslash member name before the fixture writes it, so that fixture did not
contain the intended malformed path. The fixture now writes the exact original
member name, and archive admission examines original member names before host
normalization. All six Linux package tests pass locally. Corrected Windows
execution, Sonar analysis and same-run complete Linux bundle qualification remain
required; the skipped Linux package job is not a successful build.

An isolated upstream UMU 1.4.4 zipapp is downloaded and verified against its
published SHA256 `eb590691841f7fad3fc3ad8fd5db4ccb87849fe7948e62b28ece7a4ee48cc851`.
Both archive layers are decompressed and scanned before execution. With the
existing GE-Proton11-6 runner, an owned prefix initializes using UMU's verified
steamrt4 4.0.20260928.262390 runtime. No existing user prefix is changed.
An actual bridge probe using current native source and the previously qualified
`ec699fb` Windows package fails before desktop controls connect. A diagnostic
confirms EndOfStreamException reading the private bootstrap pipe; using a mapped
Windows executable path alone does not resolve it. UMU backend/tray support is
not qualified. Runner/pipe behavior is under investigation while retaining
TR-R06: session tokens must remain in redirected process pipes, not arguments,
environment variables, files, logs or configuration.

### G23 packaging security repair — 2026-10-05

Protected run [37320076790](https://github.com/TechDaddyKB/tdsblive/actions/runs/37320076790)
at `1af6d24` passes the Linux tests, native Windows tray probe and functional
Windows/browser/recovery tests, then fails the Sonar security gate. Release
package production is skipped. Findings concern tar member names, a caller-chosen
build executable and a caller-chosen qualification evidence path.

The repair validates every tar member before creating the archive, rejects links
and special files, constructs headers from permitted relative names, and opens
regular files without following links. Package builds use the pinned CI SDK via
the fixed dotnet command; qualification writes only to its fixed owned release
evidence directory. Seven Python package cases pass, including unsafe tar names,
links and FIFOs; workflow YAML parsing and diff checks pass. A new protected
analysis is required before these findings can be considered resolved.

G23 remains **In progress**. Actual UMU startup currently also fails: an owned
private-output bootstrap experiment is rejected by the Windows output-pipe
guard. That separate, uncommitted experiment is not included in this packaging
repair. No UMU acceptance or 1.0.1 publication is claimed; G24 remains
**Not started**.

### G23 actual UMU private bootstrap repair — 2026-10-05

The source delivered with this checkpoint keeps the legacy input-pipe handshake
for direct Wine and Windows. UMU selects runinprefix and an explicit external
output bootstrap: the backend creates a fresh capability, sends one bounded
private frame to the native launcher's redirected output pipe, and never logs
that frame. The launcher discards other runner output and disables child
Proton/runtime log files. Tokens remain absent from arguments, environment,
configuration, exports and recorded evidence.

An actual owned Windows metadata probe under UMU 1.4.4 / GE-Proton11-6 confirms
why the original guard failed: Wine reports a Unix pipe as FILE_TYPE_CHAR with
FILE_DEVICE_UNKNOWN. File output instead reports FILE_TYPE_DISK; terminal output
reports a console device and is not redirected. The compatibility check requires
Wine identity, redirected output and a successful matching device query. Native
Windows continues to require FILE_TYPE_PIPE. Unknown modes, file/terminal/null
output and failed queries are rejected before a capability can be emitted.

Local Release desktop tests pass 110 cases with two Windows-only skips. Host
desktop-session tests pass all 29 cases. Current native source and a locally
published Windows diagnostic build pass actual owned lifecycle probes under both
direct Wine 11.17 and UMU 1.4.4 / GE-Proton11-6 with steamrt4
4.0.20260928.262390. Each probe checks startup, configured loopback HTTP address,
retained profile and disabled integrations/LAN, successful graceful restart with
a fresh capability, graceful quit, backend/browser availability after companion
handles close for 15 seconds, and SQLite integrity. Owned Wine servers finish
without blanket termination. The first UMU lifecycle assertion exposed a fixture
error: rewriting its fallback configuration file did not change authoritative
SQLite settings. The corrected probe retains and reads that owned saved port.

Ignored evidence: artifacts/umu-stdio-metadata/{result,file-output,terminal-output}.log,
artifacts/umu-backend-probe/result.log,
artifacts/tray-linux-backend-probe/result-detach.log,
artifacts/tray-host-output-wine-pipe-tests and
artifacts/tray-linux-output-wine-pipe-tests. These are scanned local diagnostics,
not protected release assets. Protected packaging-fix run
[37324678416](https://github.com/TechDaddyKB/tdsblive/actions/runs/37324678416)
at a614f8a is still running when this checkpoint is recorded.

This resolves the observed UMU backend handshake failure. It does not establish
native tray-menu interaction, the named desktop/session matrix, browser restore,
final-package OBS behavior or release publication. G23 remains **In progress**
and G24 remains **Not started** until their remaining acceptance evidence exists.

### G23 security follow-up and committed protocol evidence — 2026-10-05

Run 37324678416 at a614f8a is terminal **Failure** at the Sonar security gate.
The four archive/build-executable findings are no longer open; one S2083 finding
remains on the qualification evidence write. Functional Windows/browser tests,
Linux tests and the native Windows probe pass, but package production is skipped.
The evidence location now uses a fixed path independent of the caller's package
path, rejects directory aliases, creates a private directory and exclusively
creates its log and JSON files. Nine package cases pass, including preservation
of existing evidence and refusal of links both inside and outside release.
New hosted analysis must confirm the remaining finding is resolved.

The protocol source checkpoint is cfe7143. A Windows diagnostic publish built
from that committed source passes the actual UMU lifecycle probe again. An owned
metadata probe invokes the production host guard directly: it returns true for
the actual UMU pipe and false for actual file and terminal output. These probes
do not emit a credential while testing rejected destinations. Current-build
protected Windows/Linux package qualification remains required; the goal is
active, G23 is **In progress** and G24 is **Not started**.

### G23 successful protected package checkpoint — 2026-10-05

Protected [run 37327204777](https://github.com/TechDaddyKB/tdsblive/actions/runs/37327204777)
at `4314c2cb9dbce2db107c2d4ccf9c69cb817e90ef` is terminal **Success**.
Windows desktop inventory, the actual native Windows tray probe, Linux desktop
tests, the complete Windows build/test/security/package job, and the Linux
companion package job all pass. SonarQube's required quality gate passes.
The same-run Windows candidate is used to build the Linux bundle; the packaged
native first-run rendering and Cancel qualification also pass. Windows and Linux
candidate artifacts are available from that run. This supersedes the earlier
outstanding hosted package/security checks, but is not a published release or
complete acceptance of the Linux desktop matrix.

The downloaded bundle and included Windows backend both identify tested PR
merge commit `5ff394f9ce5addf6bbccb8b8847e50056761b742`, whose parents are protected
main `5ee550803cf18fa786e4edc2651fd53ecf035643` and PR head `4314c2c`.
The workflow head identifies the PR head; the package source marker identifies
its checked-out merge. The downloaded Linux archive checksum is verified against
the same-run checksum file. Archive contents are bounded, extracted as regular
files/directories and secrets-scanned before marker inspection or execution.
Final public assets still require one immutable protected main source commit.

An isolated actual native companion published from `4314c2c`, using the owned
direct-Wine profile and the `cfe7143` diagnostic Windows backend, displays the
Open editor, Restart and Quit menu through a real Quickshell/Qt menu host.
The Restart confirmation displays its warning and Cancel-first controls.
Enter on the initial Cancel button and Escape dismiss the confirmation while
retaining the running backend. Confirmed Restart returns the owned editor at
its configured address, retains profile/settings and disabled integrations/LAN,
and increases recorded browser handoffs from one to two. Evidence includes
artifacts/tray-linux-native-actions/{menu-visible,restart-confirm,restart-returned}.png.
The first one-shot HTTP assertion ran during the expected restart outage;
bounded polling confirms recovery. This was a qualification-fixture correction,
not evidence of a backend restart defect.

The subsequent scripted Quit-dialog lookup times out and the fixture gracefully
cleans up its owned processes. No native Quit, tray recovery, desktop-matrix or
final-package OBS pass is inferred from that attempt. The published backend used
for these local menu checks is diagnostic, not the protected candidate above.

The goal remains active. Remaining delivery work is the outstanding G23 desktop
and ownership cases, followed by G24 beginner documentation/wiki/offline updates,
final artifact/OBS verification and public v1.0.1 publication. Earlier delays
came from UMU bootstrap compatibility and packaging security repairs; neither
is an outstanding blocker at this checkpoint. No user approval or participant
availability is required to continue the authorized work.

### G23 protected Linux candidate menu and lifecycle evidence — 2026-10-05

The downloaded Linux bundle from run 37327204777, including its same-source
Windows backend at merge commit `5ff394f9`, passes actual native GUI checks on an
owned Xvfb display and private session bus with Quickshell 0.3.1 as the tray host
and direct Wine 11.17. This does not substitute for KDE/Hyprland session evidence.
All integration connections and LAN remain disabled; a separate prefix and browser
profile are used, without changing production output or broadcasting.

Observed cases: native browser launch at the configured nondefault address;
displayed three-action menu; Open editor handoff; fallback control window after
owned tray-host removal without backend loss; native icon/menu after tray-host
restoration; Quit Cancel using both Enter on the initial Cancel button and Escape;
confirmed Restart retaining profile, address and settings and reopening the
browser; confirmed Quit with companion exit 0; and retained SQLite integrity.
Browser handoffs increase from one to two for Open editor, then to three for
Restart. Cancellation leaves that count and the running backend unchanged.
Owned fixture processes and Wine server shut down without blanket termination.

The earlier dialog-search helper matched process ID or title. When the fallback
window was also visible, it could focus that window instead of a confirmation.
The corrected search requires both the owned process ID and exact dialog title;
the cancellation and confirmed lifecycle cases above are repeated with that
condition. The earlier screenshots/assertions using the ambiguous search are
not relied on for those cases. Native tray availability and readable status are
observed; the full desktop tooltip and resource/ownership matrix remain required.

Ignored scanned captures are in artifacts/tray-linux-native-actions:
menu-click-instrumented.png, protected-fallback.png, restart-correct-dialog.png,
restart-confirmed-package.png, quit-cancel-enter-package.png,
quit-cancel-escape-package.png and quit-confirmed-package.png. The owned retained
profile is artifacts/tray-isolated-temp/tdsblive-native-actions-km2rr5yp;
its SQLite integrity_check returns ok. These contain owned example data only.

Documentation preparation now adds guided native Wine and UMU setup instructions,
existing-prefix retention, optional launcher shortcut, Linux update instructions,
README/tray guidance and an actual packaged setup screenshot. Manual Wine,
Bottles and UMU workflows remain available for older downloads. Public availability
is still described accurately as v0.1.0; v1.0.1 remains an unpublished candidate.
The generated offline guide passes network-disabled Chromium qualification:
21 chapters, local links/navigation, every image loaded, and every chapter
reflowing at 320, 390 and 1366 CSS pixels. Local browser execution requires the
owned private /tmp mount; a first launch using the exhausted system /tmp fails
before any guide is inspected. Wiki synchronization, the complete documentation
audit and final release evidence remain outstanding. G23 remains In progress;
G24's final qualification/publication has not started.

### G23 isolated KDE X11 checkpoint — 2026-10-05

Documentation and packaged native-menu evidence are pushed at `6c717cc`.
The preceding full protected run 37331507046 at `1a062c4` is terminal Success,
including both package jobs and the required quality gate. The newer protected
run 37334967173 at `6c717cc` remains live; it is not cancelled to publish this
checkpoint.

An actual Plasma 6.7.4 / KWin X11 desktop now runs on an owned Xvfb display,
private session bus, temporary XDG directories and private home. Its temporary
runtime comes from 78 distribution packages with checked sizes and verified
signatures; decompressed content is scanned before execution. No system package
installation or package scripts run. The system bus is inaccessible and X11
compositing is disabled for this test. The first attempts exposed fixture gaps:
the desktop shell package and activity daemon were missing; those are corrected.
A preflight using the real home is discarded as qualification evidence. Its test
editor window is closed and owned services finish before the private-home rerun.

The protected `5ff394f9` Linux bundle displays its native icon and three-action
menu in Plasma's actual panel. The owned editor opens at its configured address,
and the exact owned Quit dialog receives initial Cancel focus; Enter dismisses
it while the backend/profile/settings remain available. These are partial KDE
X11/direct-Wine observations, not the full KDE/Wayland/UMU acceptance matrix.
Ignored scanned captures include kde-owned-running.png, kde-native-menu.png and
kde-quit-confirm.png in artifacts/tray-linux-native-actions.

Hover qualification finds a specific remaining defect: no readable TDSBLive
tooltip appears after a three-second hover, while the same panel's clock tooltip
does appear. Captures kde-tooltip-delayed.png and kde-clock-tooltip-control.png
record that comparison. The pinned Avalonia Linux SNI tooltip implementation
previously observed in source returns empty tooltip fields; remedy and actual
desktop requalification remain required for TR-R02. The visible title used by
the Quickshell test panel is not sufficient proof of a real desktop tooltip.
G23 remains In progress. This is actionable implementation work, not an external
approval or participant blocker.

### G23 Linux tooltip repair and transport evidence — 2026-10-05

The active goal is progressing through Linux compatibility qualification; it is
not waiting for operator approval or a participant. Protected Windows/Linux run
[37334967173](https://github.com/TechDaddyKB/tdsblive/actions/runs/37334967173)
completed successfully for source `6c717cc`. That run predates the repair below
and does not qualify the new implementation.

The pinned Avalonia Linux implementation exports empty SNI `ToolTip` fields.
`LinuxNativeTray.cs` now exports the fixed three-action menu and current status
through the existing pinned Tmds D-Bus transport. The application retains
Avalonia's native dialogs and the Windows tray implementation. The exporter uses
public transport APIs, controlled status strings and the existing icon; it adds
no dependency or integration-data exposure. Watcher replacement, bounded
registration retries, disabled commands and disposal remain supported.

Eight tests exercise the exporter over an owned actual session bus, including
tooltip/icon properties, menu actions and disabled states, malformed requests,
watcher replacement, disposal, grouped requests and activation. The complete
desktop suite passes on the reviewed worktree based on `da90c92`: **118 passed,
2 Windows-only skipped, 0 failed**. Command: pinned .NET 10.0.401
`dotnet test tests/unit/ExtensionSuite.Desktop.Tests/ExtensionSuite.Desktop.Tests.csproj
-c Release --no-restore -m:1 -p:UseSharedCompilation=false --settings
coverage.runsettings --collect:"XPlat Code Coverage" --logger trx`, with an owned
temporary directory. Scanned ignored evidence is under
`artifacts/tray-linux-tooltip-reviewed-tests/`; its TRX records a 38-second run.

An earlier local native publish of this exporter, paired with the protected
candidate Windows backend, visibly displays **TDSBLive — Running** in the owned
KDE Plasma X11 session. Scanned captures `kde-fixed-tooltip.png` and
`kde-fixed-menu.png` under `artifacts/tray-linux-native-actions/` record the
tooltip and three menu actions. This resolves the observed defect in that local
prototype; it does not establish latest-commit packaged lifecycle or complete
the desktop/runner matrix. Protected qualification of this repair and the
remaining G23 scenarios are next. G23 remains **In progress**; G24's final
documentation, qualification and public release remain outstanding. There is
still no published v1.0.1 release.

### G23 KDE lifecycle and protected test repair — 2026-10-05

An owned KDE Plasma X11 session exercises native publish `2989331` with the
protected merge `5ff394f9` Windows backend, direct Wine 11.17 and a private
sample profile. Actual captures under
`artifacts/tray-linux-native-2989331-evidence/` show the Running tooltip, three
menu actions and Restart/Quit confirmations. The observed actions cover native
browser opening, Enter cancellation for Restart and Quit, Escape cancellation
for Quit, explicit Restart and explicit Quit. Restart preserves companion PID
`1498260` while replacing its backend child `1503461` with `1522646`; the owned
profile, disabled integrations and disabled LAN remain unchanged. Confirmed Quit
returns native exit code 0, the owned fixture cleans up, and a read-only SQLite
integrity check returns `ok`.

Stopping and restarting only the owned Plasma panel restores the tooltip and
menu without changing the backend. However, KDE's watcher continues reporting
`IsStatusNotifierHostRegistered=true` while that panel is absent, and no fallback
window appears in that case. This is a recorded recovery limitation, not a
fallback acceptance pass. The beginner tray guide now explains using the
editor's Backup and recovery controls in this situation and includes a scanned,
visually checked actual KDE tooltip screenshot.

A separate owned KDE Wayland/XWayland session starts the same native publish and
backend and visibly renders the tray menu, tooltip, editor and native Restart
dialog. Its synthetic keyboard/pointer confirmation interactions do not prove
restart or cancellation: the dialog remains open. Browser-initiated Quit does
stop the app with native exit code 0 and all owned processes are cleaned up.
Evidence is under `artifacts/tray-linux-wayland-2989331-evidence/`. Wayland
interaction and lifecycle qualification remain explicitly incomplete.

Protected run
[37341966066](https://github.com/TechDaddyKB/tdsblive/actions/runs/37341966066)
finishes with a failure in the existing Rumble late-registration test while
waiting for acknowledgement. Linux desktop coverage, Windows native tray probe
and Windows desktop inventory pass; Linux packaging is skipped after the failed
host test. No passing full-run or package claim is made for this source.

The late-registration test now observes all **65** unavailable events parked,
checks that none executed, introduces the owned trigger, advances an injected
discovery clock past the refresh interval, and verifies exactly one canonical
execution/acknowledgement and the **64** still-parked viewer events. It retains
its 60-second cancellation deadline, crash/cleanup handling and SQLite reopen
assertions. This removes its real-time refresh wait without weakening the
acceptance conditions. All **33 RumbleReplayTests** pass locally with coverage
in **12 seconds**, with no skips or failures; scanned ignored evidence is under
`artifacts/tray-discovery-clock-tests/`. Protected Windows confirmation is next.

The Linux package qualifier also now uses `xdotool search --all`, requiring the
owned PID and expected window title together. Its **9 distribution/isolation
tests** pass; actual current-source packaged-window verification remains part of
the next protected run. G23 remains **In progress**, and G24 remains outstanding.

The regenerated beginner offline guide also passes its network-disabled browser
audit: **21 chapters, 18 images**, local navigation and reflow at
**320/390/1366 pixels**. This includes the new actual KDE illustration and
recovery guidance. Full wiki and final-package acceptance are still pending.


### G23 Wayland lifecycle and vendor finding investigation — 2026-10-05

The user authorized investigation of the two scanner findings in the owned
SteamRT4 Python runtime. Both copies of `lib/python3.13/urllib/request.py` have
SHA-256 `e30e1285f957dad785d401c79fac9fd701c5cb2b8607b6d74d5f7082d612dcd4`.
A bounded in-memory AST comparison proves that line 56 is inside the module
documentation string and that the entire string exactly matches
[official CPython 3.13 source](https://github.com/python/cpython/blob/3.13/Lib/urllib/request.py).
These are public authentication examples, not application credentials. No
flagged value was printed from either local file, no vendor code was modified,
and no credential rotation is required for these two false positives. This
classification applies only to the two verified files and exact contents;
future findings or changed contents still require investigation. The runtime
was rescanned before resuming qualification.

An owned KDE Plasma 6.7.4 Wayland/XWayland session now qualifies the native
`f20fdc8` diagnostic publish with the protected `5ff394f9` Windows backend under
Wine 11.17. Evidence is retained in
`artifacts/tray-linux-wayland-f20fdc8-qualified/`. Input is sent through the
owned nested compositor output, rather than incorrectly targeting its inner
XWayland display. The actual Restart dialog closes on Enter without changing
the native or backend process; explicit Tab/Enter confirmation replaces backend
PID 1609718 with 1613088 while native PID 1609572 and the selected profile remain
unchanged. Quit also cancels on Enter and Escape; explicit confirmation exits
the native process with code 0 and stops its owned backend. LAN and integrations
remain disabled throughout. Open editor visibly opens the native owned browser.

Stopping the owned KDE watcher produces the visible three-action fallback.
Restoring the watcher and panel recovers the tray menu. Watcher restoration
alone was captured before full recovery; that capture does not prove immediate
recovery. Panel-only loss still has KDE's stale host flag limitation and the
published candidate guide explains the editor recovery controls.

A ten-second idle sample measures native RSS 211,546,112 bytes and CPU 0.3% of
one core; backend RSS 260,792,320 bytes and CPU 3.6%. The combined sample excludes
Wine server, compositor and browser. It is not a performance pass or a baseline
comparison. Equivalent backend-only measurement and investigation remain due.

Protected [Windows/Linux qualification run 37345261968](https://github.com/TechDaddyKB/tdsblive/actions/runs/37345261968)
and [Advanced Security run 37345267170](https://github.com/TechDaddyKB/tdsblive/actions/runs/37345267170)
both completed successfully at PR head `f20fdc8f0084878788b216f9f7cd9cb2caa0a950`.
The full run includes Windows inventory/native tray, Linux desktop coverage,
Windows build/regression/SonarQube/packages, offline guide and Linux bundle
first-run qualification. Downloaded artifact source markers and exact final
protected-main packages still require inspection and qualification.

G23 remains **In progress**; G24 is **Not started**. The prepared native UMU GUI
fixture can now run after its scoped source/runtime scans. Hyprland, production
new-prefix setup, final package evidence, documentation audit/wiki, actual OBS
and public v1.0.1 downloads remain required.


The current protected Linux candidate was downloaded from run 37345261968,
its SHA-256 verified as
`626bb8e61d0bf394884ae3844d42d54f5b609f8e74e890ddcd311a3c7a0f898f`,
and its 1,208 tar members admitted only after rejecting links, special files,
absolute/traversing paths and oversized archives. Extracted files pass secrets
scanning. Native and Windows backend package markers both identify checkout
`9e06170f9b1045a794b5fcb9e8f73f13b18e5337`, the protected PR merge of main
`5ee550803cf18fa786e4edc2651fd53ecf035643` and head
`f20fdc8f0084878788b216f9f7cd9cb2caa0a950` (tree
`ba8cc715efb42e26c8338b72ea240f12ff1648e1`). This distinction preserves exact
artifact provenance; the binaries are not claimed to be a head-only build.

The first UMU GUI fixture ended cleanly after its 30-second readiness deadline
without a working backend. Its owned processes were stopped and logs scanned;
no GUI acceptance is inferred. The next diagnostic fixture uses both binaries
from the same protected candidate and respects the application's five-minute
startup allowance, with intermediate captures to distinguish slow startup from
an error window.


The intermediate native capture shows the friendly runner-failure setup window,
so the UMU result was not merely slow startup. A bounded owned diagnostic run
with desktop capability exchange disabled exits 1; its scanned log identifies
UMU pressure-vessel attempting to mount the fixture's nonexistent
`/tmp/owned-no-system-bus`. The fixture had deliberately advertised that path
to avoid the production system bus. It now advertises its real owned private
bus instead, preserving isolation while allowing the container bind. No
production source or vendor runtime change is needed for this fixture error.
The failed diagnostic processes are confirmed stopped before the corrected
protected-candidate GUI fixture starts.


The corrected fixture reaches Proton but the current protected backend then
exits 3 with `Cannot get symbol u_charsToUChars from libicuuc` (scanned owned
logs in `artifacts/tray-linux-wayland-umu-bus-diagnostic-f20fdc8/`). Removing
fixture-only Qt/library search paths gives the same result. A subsequent owned
native GUI run with .NET Windows NLS enabled and the production DLL overrides
successfully reaches the selected HTTP profile under UMU. This narrows the
compatibility repair to Windows globalization selection in the UMU child;
[Microsoft documents the NLS option](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization#nls).
This option retains culture-specific support; invariant globalization is not
used. The diagnostic candidate remains unchanged, and the compatibility
setting is currently supplied by the fixture environment. Native GUI lifecycle
and a production default repair still require qualification.


### G23 retained-prefix Proton globalization repair — 2026-10-05

The same protected candidate now starts the owned selected profile under UMU
1.4.4, GE-Proton11-6 and SteamRT4 with NLS enabled. Its native Running menu and
editor are visually verified in
`artifacts/tray-linux-wayland-umu-nls-gui-f20fdc8-evidence/`; LAN and integrations
are disabled. This initial GUI run stops at an immediate dialog-disappearance
assertion and does not prove cancellation or restart. The qualifier now waits
for owned-dialog activation and disappearance with bounded polling, avoiding
an assertion before the compositor/UI has processed the input.

Production `LinuxLauncherSettings.CreateStartInfo` supplies
`DOTNET_SYSTEM_GLOBALIZATION_USENLS=1` by default only to the UMU Windows child.
An explicit inherited choice remains respected. Native Linux and direct-Wine
process settings are unaffected, and invariant globalization is not enabled.
Existing selected prefixes, saved configuration, runner logs and Windows
package settings are not modified. The whole desktop suite passes **118 tests,
2 Windows-only skips, total 120**, with evidence in
`artifacts/tray-umu-nls-desktop-tests/`. Assertions cover the child compatibility
default, retained explicit mode, unchanged invariant setting and Wine behavior.

A fresh local native diagnostic publish is being checked with the protected
`9e06170f` Windows backend, without a fixture-supplied NLS variable, to verify the
production default in actual UMU startup and GUI lifecycle. Current protected
checks and final cumulative packages must be rebuilt after this source change.
G23 remains **In progress** and G24 remains **Not started**.


The production-default diagnostic completes the actual KDE Wayland/XWayland
UMU GUI flow with no fixture-supplied NLS environment variable. Evidence is in
`artifacts/tray-linux-wayland-umu-default-nls-evidence/`; native publish is
`artifacts/tray-umu-nls-native/`, paired with protected backend `9e06170f`.
Native PID 1818078 stays unchanged; Enter cancels Restart with child 1818204
retained, while explicit Tab/Enter Restart replaces that child with 1822052.
Quit cancels with both Enter and Escape, retaining child 1822052 and the same
profile, port 57109 and disabled integration/LAN settings. Open editor visibly
opens another owned native Chromium tab at the same editor address. Explicit
Quit exits the native application with code 0; the fixture closes all owned
processes. The selected database passes read-only SQLite integrity checking and
retains the owned configuration and port. This proves representative UMU GUI
behavior with the repair, not the complete desktop matrix or final package.

A separate run of all **18 LinuxLauncherSettingsTests** with inherited
`DOTNET_SYSTEM_GLOBALIZATION_USENLS=0` passes with no skips, proving that the
compatibility default respects an explicit ICU choice. The full **118-pass**
desktop suite and actual default-mode GUI evidence remain the relevant repair
checks. No subsequent functional source changes were made after those checks.

### G23 Hyprland input and protected-package checkpoint — 2026-10-05

Protected [Windows/Linux run 37354024481](https://github.com/TechDaddyKB/tdsblive/actions/runs/37354024481)
passes every job for head `a507b1e224990bb75b1fb75b561f3c7851786648`:
Windows inventory, native tray probe, Linux desktop tests/coverage, Windows
build/tests/SonarQube/regression/packages and scanned Linux bundle. All PR checks,
including [CodeQL run 37354024126](https://github.com/TechDaddyKB/tdsblive/actions/runs/37354024126),
pass. The downloaded Linux archive has SHA-256
`879fbee9280d787d68ba609c00a5ab288af2c9949122e54e85bba135b685c178`.
Its 1,208 tar entries were bounded and validated before extraction; extracted
files pass deterministic secrets scanning. Native and backend package metadata
both identify `7b58702b62478ac958a6848959883b32b1ad83ed`, the protected PR
merge of main `5ee5508` and head `a507b1e`. The backend archive checksum is
`5c5001c4b2f3ec46699e94c08707bbc51efd860f58b90c374e1ea7fe101699b7`.
This candidate is not a published release or a final main build.

Actual isolated Hyprland 0.56.2-2 (commit `efb5099`), Aquamarine 0.15.0-2,
Quickshell 0.3.1 and XWayland run inside an owned virtual KWin parent on a
private session bus, private HOME/XDG directories and private network namespace.
Only the AMD render node is available; physical display card nodes and the
production Quickshell session are excluded. The real 1280×900 compositor output
is captured with `grim`, scanned and visually inspected.

Ephemeral keyboard/keymap clients and global XTest did not reliably exercise the
owned native dialogs. Those fixture attempts are not application acceptance or
proof of an application defect. The repaired test input keeps a Wayland virtual
pointer and a static US evdev virtual keyboard connected to the owned seat.
The helper rejects runtime paths outside `/tmp/h-` and sockets other than the
owned `wayland-1`. It uses official unstable Wayland protocols and a test-only
compiler, and is not shipped or added as an application dependency.

With representative native `a507b1e` and protected `9e06170` backend under Wine
11.17, actual tray pointer actions open the menu and native dialogs. Enter cancels
Restart without replacing backend `1961213`; Tab then Enter confirms Restart
and replaces it with `1963830`. Both Enter and Escape cancel Quit. Open editor
opens the retained loopback address in the owned Linux Chromium profile. Removing
Quickshell displays the fallback controls; restoring it restores the actual
three-item menu. Tab then Enter confirms Quit and the companion exits 0. A
read-only SQLite integrity check passes afterward; sample name, port 47191,
disabled LAN and disabled integrations remain unchanged. Evidence resides in
ignored `artifacts/tray-hyprland-native/wine-a507b1e-stable-keyboard/`.

The exact downloaded protected `7b58702` native/backend bundle also starts under
UMU 1.4.4 and GE-Proton11-6 in the actual Hyprland fixture. No fixture NLS override
is supplied: the production child default is exercised. Enter cancels Restart
without replacing child `1969638`; Tab then Enter confirms it and replaces the
owned runner child with `1972009`, retaining the profile and port 49595. Enter
and Escape cancel Quit; native browser handoff and missing/reappearing tray
checks pass. Evidence is in ignored
`artifacts/tray-hyprland-native/umu-protected-7b58702-exact/`.
Tab then Enter confirms final UMU Quit; native exit is 0. A read-only SQLite
integrity check afterward passes, with the sample profile, port 49595 and disabled
LAN/integrations retained.

The exact protected `7b58702` package also repeats the Hyprland scenarios under
direct Wine. Enter cancels Restart with child `1979453` retained, then Tab/Enter
replaces it with `1980777`. Enter/Escape cancel Quit, Open editor adds an actual
native-browser handoff, and loss/restoration of Quickshell shows fallback/restores
the menu. Tab/Enter confirms final Quit and native exit is 0. Evidence is retained
in ignored `artifacts/tray-hyprland-native/wine-protected-7b58702-exact/`.
Post-shutdown read-only SQLite integrity passes. Sample name, port 60655, disabled LAN and disabled integrations are retained.

The fallback window remains visible after the icon returns. Its close action
hides the window and leaves the backend running; it does not imply a remaining
tray failure. The beginner guide now explains that users can close this window
and use the returned icon. G23 remains **In progress**; G24 remains **Not started**.

### G23 actual new-prefix startup regression — 2026-10-05

The protected `7b58702` package was exercised through its real first-run window
with no saved launcher settings, no inherited Wine prefix and no existing app
profile. Choosing **Start a new empty TDSBLive setup**, the suggested folder and
**Add TDSBLive to my applications menu** installs the owned shortcut, but startup
fails visibly. The suggested path's launcher-settings parent does not yet exist;
Wine cannot create that nested prefix. No backend is started or existing profile
changed. Ignored captures in
`artifacts/tray-hyprland-native/wine-protected-7b58702-new-prefix/` show the actual
choice, shortcut selection and failure message. This is a confirmed first-run
bug, not a runner/runtime false-positive or a passing initialization check.

`LinuxBackendProcess.InitializeWineAsync` now creates the selected prefix directory
before Wine initialization. Wine still creates `drive_c` and `dosdevices`; the
launcher does not precreate drive mappings or stop an existing prefix server.
The owned runner regression now rejects a missing prefix and launcher-created
drive mappings, and the new-profile scenario uses a missing nested parent. The
focused regression fails before this repair and passes afterward. The full
local desktop suite passes **118 tests**, with **2 Windows-only skips** (120 total),
using pinned .NET 10.0.401; results are retained in ignored
`artifacts/tray-new-prefix-tests/desktop-tests.trx`. The repaired native diagnostic build then passes the actual first-run checkbox
flow on the owned Hyprland compositor: production Wine initialization creates
its own drive mappings, the default loopback editor at port 17474 opens, LAN and
all integrations remain disabled, and the selected profile is saved under the
suggested prefix. Selecting the optional applications-menu checkbox creates the
owned desktop entry. Evidence is retained in ignored
`artifacts/tray-hyprland-native/wine-new-prefix-repaired-gui/`.
This uses local repaired native source with the protected `7b58702` backend;
a new protected bundle and final shortcut-launch qualification are still required.
G23 stays **In progress**.

### G23 applications-menu and duplicate-launch investigation — 2026-10-05

The actual optional shortcut installed by the repaired first-run window is opened
through GIO on the owned Hyprland compositor. After the companion quits, the
shortcut starts the retained owned setup, its real Running tray menu and its
Linux browser at port 17474. Evidence is retained in ignored
`artifacts/tray-hyprland-native/wine-retained-shortcut-9c54648-v2/`.
An earlier external GIO attempt had inherited display credentials and no valid
owned XWayland display; that failed fixture is not an application defect or a
shortcut qualification pass. The successful GIO action runs inside the fixture's
sanitized display environment.

Opening the shortcut again while that native companion is running does request
the existing editor and preserves the single backend. However, the duplicate
native process remains with a misleading **Set up TDSBLive on Linux** window.
Actual process metadata and a scanned screenshot confirm the defect: companion
`2022649` retains backend child `2022681`, while duplicate `2024519` has no child
and displays setup. Owned processes and the compositor were stopped afterward;
production Quickshell remains untouched.

The repair adds an exact, bounded existing-profile acknowledgement to the
external launch pipe. The input handshake is consumed before acknowledging,
and output mode requires a pipe. No capability or port is returned. The native
launcher recognizes the acknowledgement only with child exit 0 and exits the
second companion; failed exits still report startup failure. Ordinary automatic
Windows duplicate launch retains its existing behavior. No new control authority,
HTTP endpoint or stored credential is introduced.

The local desktop suite passes **119 tests**, with **2 Windows-only skips**
(121 total), including both runner modes and rejection of unsuccessful duplicate
exits. **35 host desktop/session/ownership tests** pass with no skips, including
bounded acknowledgement, invalid-pipe rejection and no ordinary-launch frames.
Results reside in ignored `artifacts/tray-duplicate-profile-tests/`.
The new native/Windows diagnostic builds use pinned toolchains, tracked RID
lockfiles and production publish profiles. Actual repaired Wine GUI repetition now passes: the production setup window
retains the owned prefix/profile and installs its shortcut through the optional
checkbox. Opening that real desktop entry with GIO while companion `2049559`
is running requests another actual editor window. Process inspection afterward
finds exactly that one companion with original backend `2052294`, and compositor
window inspection finds no second setup window. Browser handoff count increases
from 5 to 6. Confirmed tray Quit exits 0 and the owned fixture stops cleanly.
Ignored evidence is in `artifacts/tray-hyprland-native/wine-duplicate-repaired-gui/`.
The actual retained-prefix UMU repetition passes with the production NLS default:
companion `2069811` retains runner child `2072738`, the second native launch exits,
and the existing owner opens another browser window (handoff count 7→8).
After the duplicate child exits, actual process inspection finds only the original
companion and runner child, and compositor inspection finds no second setup window.
Ignored evidence is in
`artifacts/tray-hyprland-native/umu-duplicate-repaired-gui-v3/`.
Two earlier fixture attempts fail before qualification because of XWayland
readiness/overlapping display setup and an existing owned runtime-cache link;
neither is counted as an application failure or pass. The final repetition checks
XWayland readiness and validates the existing cache target before starting the app.
An extra menu toggle prevents its subsequent Quit-dialog repeat from activating;
that additional dialog case is not claimed. The fixture uses the authenticated
owned-browser Quit during cleanup. Both native processes are absent afterward;
retained UMU runner/prefix/profile choices and read-only SQLite integrity pass.
A new protected package remains required. G23 stays **In progress**.


### G23 recovery, crash ownership and protected qualifier follow-up — 2026-10-05

Actual isolated Hyprland/Wine repetition at source `e286733` confirms browser
Restart and validated Backup/restore preserve the selected profile, port 17474,
disabled LAN and disabled integrations. Restoring the owned backup returns setup
progress from step 3 to saved step 1 and replaces only the owned backend. Opening
the same owned setup through prefix/profile aliases requests the existing editor:
one native companion and backend remain, with browser handoffs increasing 11→12.
Ignored evidence: `artifacts/tray-hyprland-native/wine-lifecycle-e286733/`.

Killing the exact owned backend during this qualification does not automatically
relaunch a child after ten seconds. However, reopening the launcher then leaves
two native tray items: the original stopped companion and a new running companion.
This is a confirmed native ownership defect. The stopped controls also tile into
a narrow, clipped window under Hyprland, hiding their recovery buttons. Both
issues require repair and real desktop repetition before G23 completion.

The repair adds a private, per-profile native lease with an empty activation
marker. Physical directory aliases share ownership; missing prefixes are not
created by this check. Reopening activates the existing editor or stopped controls
without starting another backend. Closing stopped controls releases native
ownership; reopening afterward remains an explicit recovery action. Setup and
recovery windows declare their X11 dialog role; recovery controls scroll within
the available height. No control token, new HTTP authority, automatic crash
restart, global desktop setting or unrelated application shutdown is added.
Local desktop tests pass **124 tests**, with **2 Windows-only skips** (126 total),
including ownership release, aliases, no new prefix mappings, private empty lease
files and reachable recovery controls at 320×240 in both themes. These checks
alone do not qualify actual native layout or Windows behavior.

Protected run [37362730849](https://github.com/TechDaddyKB/tdsblive/actions/runs/37362730849)
at `e286733` fails the actual desktop lifecycle qualifier: its external duplicate
launch omits the required private bootstrap input. Windows tray inventory/probe
and Linux desktop tests pass; dependent Linux packaging is skipped. The fixture
now supplies a private input frame and asserts the exact nonsecret acknowledgement,
exit 0 and absence of the private token in output. The updated local isolated
lifecycle qualifier passes. Protection is retained; a new protected run is required.

Three ten-second idle samples with the diagnostic companion active show native
CPU 0.2–0.3 percent of one core and PSS about 115–116 MiB; backend CPU is 3.2–3.5
percent with PSS about 292–296 MiB. These process-only samples exclude browser,
compositor and Wine helpers. An equivalent backend-only baseline is still needed;
they are not a performance acceptance pass. Production Quickshell and user data
remain untouched. G23 remains **In progress**; G24 remains **Not started** and
v1.0.1 remains unpublished.


The first native lease GUI repetition catches an immediate-duplicate startup
failure: shutting down before Avalonia enters its event loop makes the duplicate
exit unsuccessfully. Startup is now posted onto that loop. A fresh pinned native
publish and full local desktop suite again pass **124 tests**, **2 Windows-only
skips**. This timing repair is required by actual desktop evidence, not a fixture
exception.

Actual Hyprland/Wine repetition of the repaired working source then passes:
companion `2205497` retains backend `2205612` while a duplicate exits 0 and the
watcher retains exactly one registered tray item. Browser handoffs increase 16→17;
physical profile aliases increase them to 18 with the same owner and backend.
After an exact owned backend crash, no child restarts after ten seconds. Reopening
exits 0, leaves exactly one registered item and activates that original companion's
stopped controls. The actual recovery window now floats at 440×464 without a user
window rule. At 320×240 it scrolls; native Tab/Return reaches **Close desktop
controls** and exits 0. Evidence is retained in ignored
`artifacts/tray-hyprland-native/wine-native-owner-repaired-v2/`.
This is a local diagnostic publish from working source after `e286733`, not a
final protected package or publication. UMU repetition, new protected checks and
the remaining G23/G24 release evidence remain required.


Wine recovery after that explicit close also passes: the owned applications-menu
entry opens the same retained profile, disabled LAN/integrations remain unchanged,
and browser handoffs increase to 19. Authenticated owned-browser Quit exits 0;
fixture processes stop cleanly. The equivalent actual UMU/Proton repetition uses
companion `2215483` with original runner child `2215547`: running duplicates and
physical aliases exit 0 and open the existing editor (handoffs 20→21→22). After
the exact owned backend crash, no child is restarted after ten seconds. Reopening
again leaves one registered native tray item and the same stopped controls.
Native Tab/Return closes those controls with exit 0. Ignored evidence:
`artifacts/tray-hyprland-native/umu-native-owner-repaired-v2/`.

UMU recovery after explicit close reopens that same profile through the real
owned applications-menu entry (handoffs increase to 23), retaining disabled LAN
and integrations. Owned-browser Quit and fixture cleanup complete. No qualified
case enables live integrations, broadcasts, external execution or financial writes.


### G23 paired resource measurement and hosted-runner retry — 2026-10-05

Repaired source is committed and pushed as
`6791cfbc27d6ffec9a742562dd43e983b722b11d` on draft PR #22. Current protected
run [37367019353](https://github.com/TechDaddyKB/tdsblive/actions/runs/37367019353)
passes **Windows native tray probe**. Windows inventory and Linux coverage never
receive runners: each annotation reports **The job was not acquired by Runner of
type hosted even after multiple attempts**. Dependent build/package jobs are
skipped. Failed/unstarted jobs are retried without changing source or protection;
the passed native probe remains passed. This infrastructure failure is distinct
from the repaired missing-input qualifier at `e286733`.

Paired actual UMU runs use the same owned prefix/profile, current diagnostic
Windows backend, compositor, browser/editor workload and production runner/NLS
choices. The baseline starts the backend with desktop controls off; the comparison
uses the native companion. Neither run enables LAN or integrations. Three
30-second samples per run record CPU and proportional memory for the backend and,
when present, native companion. Browser, compositor and Wine/UMU helpers are
excluded from both totals. Later two-sample means are **352.0 MiB PSS / 3.93% of
one core** with the companion and **229.8 MiB / 3.53%** backend-only: about
**122.2 MiB additional PSS / 0.40 percentage points of one core** in this example.
The added proportional memory is material (about 53% of that process-only baseline),
not hidden by counting shared RSS twice. Evidence resides in ignored
`artifacts/tray-hyprland-native/umu-resource-{with,without}-6791cfb/`.

A subsequent ten-sample, five-minute run shows native PSS growing from 117.0 to
127.9 MiB between first and last samples, predominantly private dirty pages;
backend PSS varies rather than following the same monotonic trend. This does not
prove a leak or settle steady-state use. Numeric allocation/garbage-collection
counters are being investigated using pinned Microsoft `dotnet-counters`
**10.0.745401**, installed only in ignored artifacts. No heap dump, environment
inspection or private control-frame export is collected. Its process attachment
is restricted to the reviewed owned native executable/PID; user applications and
production Quickshell are untouched. These preliminary resource measurements
are not a performance acceptance pass. G23 remains **In progress** and v1.0.1
remains unpublished; the latest public release is verified as v0.1.0.


### G23 actual Linux high-DPI setup repair — 2026-10-05

An actual owned native setup window at 200% scaling on the 1280×900 Hyprland
output exposes a screen-fit defect at `6791cfb`: its 1280×1400 client starts at
(0, −250), leaving setup controls outside the output. Escape cancels with exit 0
and does not start or change the owned backend. Ignored scanned evidence:
`artifacts/tray-hyprland-native/setup-dpi-6791cfb/`.

The repair constrains Linux setup/recovery window dimensions to the selected
screen's logical working area and centers the resulting size. Explicit setup
sizes shrink; auto-sized recovery content retains its scroll viewport. Screen
limits adjust minimum dimensions where needed, allowing smaller available areas.
Windows recovery placement is unchanged. Overlay geometry, profile choices and
backend authority are untouched. The full local desktop suite passes **124
cases**, with **2 Windows-only skips** (126 total), after this repair.

A fresh pinned native diagnostic publish of repaired working source passes the
same actual 200% setup case: its floating client is **1232×804 at (24, 48)**,
within the output, with **Cancel** and **Start TDSBLive** visible and scrollable
fields. The scanned screenshot is visually inspected. Native Escape cancels
with exit 0; the original tray remains Running and no backend action is requested.
Evidence: `artifacts/tray-hyprland-native/setup-dpi-repaired-after-6791cfb/`.
A new current protected build and final-package DPI repetition remain required.

The first three-minute numeric counter collection on the owned native process
records GC heap growth from **7.78 to 14.57 MB**, about **7.0 MB allocated**, and
zero collections in all generations during that interval. This explains much of
that interval's resident growth as uncollected managed allocation; it does not
establish retained live objects or prove absence of a leak. An extended numeric
collection remains in progress. Neither collection dumps memory or credentials.
The hosted-runner retry passes Windows inventory but again fails to acquire the
Linux hosted runner; its annotation repeats the same infrastructure failure.
The successful Windows native probe and inventory are preserved. Protection is
not changed or bypassed. G23 remains **In progress**, G24 **Not started**, and
the full goal remains active.

The extended ten-minute numeric collection completes with 120 samples per
counter: GC heap grows **19.05→42.00 MB**, working set **196.68→222.00 MB**,
about **23.12 MB** is allocated, and no generation records a collection in that
interval. Thus collection has not yet distinguished live retention from ordinary
uncollected allocations. Natural collection/longer observation remains required;
no leak or steady-state pass is claimed. Both counter exports are scanned before
numeric analysis. The owned native process remains available for that bounded
follow-up, with all integrations and LAN disabled.

### Accepted release disposition and final setup fit — 2026-10-05

The operator explicitly requests: accept the current issues, record them for
future investigation, commit and publish 1.0.1, then update the full wiki from
this plan. G23 is accepted with limitations; this does not turn incomplete
matrix repetitions or performance targets into passes. Remaining exact-package
qualification, protected build/security checks, publication and documentation
belong to G24. The goal stays active until the release and wiki are delivered.
[Known issues](known-issues.md) records the memory investigation, KDE stale tray
host behavior and retained compatibility/qualification boundaries.

At source `48ba8c1`, a controlled private appearance service on a separate owned
session bus verifies actual native Linux light/dark changes at 125% and 200%.
The 400% case reveals an initial-placement race: although the resized client is
1184×708, it opens outside the 1280×900 output. Fitting before Show, as well as
on Opened, resolves it without changing the editor or backend. A fresh native
publish of the repaired working source passes actual light/dark setup checks at
125% (800×840 at 240,30), 200% (1232×804 at 24,48), and 400% (1184×708 at 48,96).
Native Escape cancels every case with exit 0 and no backend action. Screenshots
are scanned and visually inspected. Ignored evidence:
`artifacts/tray-hyprland-native/setup-themes-initial-sizing-48ba8c1/`.
The pinned full desktop suite passes **124**, with **2 Windows-only skips** and
no failures; TRX/coverage are under `artifacts/tray-initial-sizing-tests/`.

The second ten-minute numeric counter export records heap 7.04→30.04 MB and
working set 229.22→229.29 MB (maximum 229.33 MB). The heap decreased from the
previous observation's 42.00 MB before this interval; no collection event was
captured inside this interval, so its exact timing is not asserted. After the
collector exits, three 30-second samples record native RSS 229.29/229.29/229.35
MB and CPU 0.30/0.27/0.27% of one core. This supports a bounded plateau, not a
long-term leak or performance pass. Evidence:
`artifacts/tray-hyprland-native/umu-resource-with-counters-6791cfb/`.
Owned-browser Quit returns 0 and all tracked fixture processes stop. Production
Quickshell remains untouched.

Protected run 37370910176 on `48ba8c1` passes Windows native tray and inventory,
but cannot acquire its Linux hosted runner and skips dependent package jobs.
The failed/unstarted jobs are retried. GitHub's current Actions incident
[3q1yb5m7ltvb](https://www.githubstatus.com/incidents/3q1yb5m7ltvb) confirms runner
assignment delays across configurations. Required checks and secret isolation
are unchanged. Current repository API checks list no open Dependabot or CodeQL
alerts. v1.0.1 remains unpublished at this checkpoint; publication is required.

### G24 current-source reliability repair — 2026-10-05

Protected run [37374537766](https://github.com/TechDaddyKB/tdsblive/actions/runs/37374537766)
at `0c7a9fb3d1ccf0ececca7fcc9f36e83ab81ec2d8` passes native Windows tray,
Linux desktop tests, Windows inventory, .NET/frontend regression tests, replay,
HTTP/crash/financial recovery, real browser qualification and restart/restore.
All three CodeQL analyses pass. SonarQube reports security, coverage (86.8%),
duplication and reviewed hotspots passing, but blocks reliability on S3869:
the native Linux ownership lease extracted a raw descriptor using
`SafeHandle.DangerousGetHandle`. Packaging is correctly prevented.

The `flock` P/Invoke now accepts `SafeFileHandle` directly, allowing the runtime
to hold a safe reference during the native call. No suppression or protection
exception is added. The full pinned Linux desktop suite passes **124**, with
**2 Windows-only skips**, zero failures; owned lease acquisition, duplicate
activation, aliases and release/reacquisition remain covered. Ignored local
evidence: `artifacts/tray-safehandle-tests/`. New protected checks and final main
packages are required; v1.0.1 and the prepared wiki are not yet published.

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

Status: **In progress**
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

### Blockers

Actual X11/Wayland and both named desktop/runner evidence remain required.

## G24 — Documentation, qualification and publication

Status: **Not started**
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

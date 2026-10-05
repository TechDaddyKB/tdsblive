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

Status: **In progress**
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

Actual current-build Windows desktop evidence is required. Build and mock results
alone do not satisfy this gate; record unavailable evidence explicitly.

## G23 — Native Linux tray companion

Status: **Not started**
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
| Authenticated control, bounded messages and one admitted command | Host/control tests and actual owned-process qualification | Exact-head protected Windows checks |
| Profile ownership, spaced paths, nondefault port, safe restart/restore, SQLite integrity | Actual owned-process checks; successful earlier Windows regression steps | Exact-head packaged checks; native browser lifecycle |
| Native icon, menu, confirmations, Explorer recovery and crash isolation | Native icon/menu and Open editor activation verified; Edge first-run screen blocks browser-close progress | Run corrected native harness on portable and installed packages; inspect screenshots |
| Manual/quiet startup and saved credentials through lifecycle | Native quiet startup and initial owned DPAPI storage verified; individual startup/vault tests pass | Complete actual-package manual startup and credential retention through lifecycle |
| Light/dark appearance, keyboard access and high DPI | Actual theme variant and keyboard behavior in headless tests | Real Windows screenshots and interactions in both themes and at high DPI |
| Installer, optional startup, update/reinstall, uninstall and data retention | Packages build; inherited native package harness preserves these checks | Successful exact-head package qualification |
| Existing OBS addresses, rendering/audio and reconnect | Historical OBS evidence remains preserved | Actual current-package isolated OBS verification |

G22 remains **In progress**. G23 and G24 retain their prerequisites and remain
**Not started**; neither native Linux delivery nor release publication is claimed.

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

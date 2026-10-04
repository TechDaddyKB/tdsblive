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

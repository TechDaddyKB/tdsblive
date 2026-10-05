# Desktop controls and application lifecycle

This describes 1.0.1's Windows and native Linux desktop controls. Linux still
runs the Windows backend through installed Wine or UMU/Proton. See the
[tray release plan](tray-release-plan.md) for exact acceptance evidence and
[known issues](known-issues.md) for accepted limits and future investigation.

## What users do

The packaged Windows application starts its desktop companion. The tray menu
offers **Open editor**, **Restart** and **Quit**. Closing the editor browser leaves
TDSBLive running. Restart and Quit first show a confirmation with **Cancel**
focused. With that initial focus, Enter cancels. Escape and closing the
confirmation also cancel the operation.
Users must save unfinished editor changes before confirming; a tray command does
not save browser forms. Restart briefly stops overlays; Quit stops them until the
application is opened again.

If the notification area disappears, a **TDSBLive is running** control window
offers the same actions. Closing that window hides it and leaves the backend
running. A companion failure leaves the backend running and changes
**Desktop controls** diagnostics to `degraded` after missed heartbeats. The
browser's application controls remain available. A backend failure shows recovery
guidance; neither component silently restarts a failed backend.

## Launch modes

`TDSBLive:DesktopMode` is a developer launch setting, separate from integration
settings and overlay designs.

| Mode | Behavior |
| --- | --- |
| Omitted or `automatic` | On Windows, start the companion when the packaged `desktop/TDSBLive.Desktop.exe` exists. Unpackaged hosts retain their previous headless behavior. |
| `external` | An owning launcher exchanges a fresh capability through redirected process pipes and owns any successful relaunch. A Windows companion is not spawned. |
| `off` | Keep desktop controls off for utilities, isolated browser tests and existing headless workflows. |

In packaged manual startup, `TDSBLive:OpenEditor` defaults to true. The optional
Windows sign-in shortcut explicitly sets it to false. Existing shortcut and OBS
addresses remain supported; desktop handoff uses a configured local editor
address, including nondefault ports. A LAN interface can receive an additional
loopback listener without changing its saved LAN binding or HTTP authority.

## Control authority

Desktop control uses a separate IPv4 loopback TCP listener on an ephemeral port.
It is not an HTTP endpoint and does not weaken cookies, Origin checks, CSRF,
LAN authentication, overlay permissions or existing integration authority.

Each session has a random 32-byte capability represented as 64 hexadecimal
characters. A Windows host gives it to its child through redirected standard
input. Direct Wine uses the same input-pipe handshake; only the listener port is
reported on standard output, prefixed with `TDSBLIVE-DESKTOP `. UMU replaces input
with an empty stream, so the native launcher selects `TDSBLive:DesktopBootstrap=output`.
The host generates a fresh capability and sends it through the launcher's private
output pipe with `TDSBLIVE-DESKTOP-BOOTSTRAP `. Ordinary output never emits that
frame. The output mode requires external ownership and a pipe; files and terminals
are rejected. Wine reports its Unix pipe as a redirected character handle with
an unknown device type, so the guard checks Wine identity and device information.
The native launcher parses bounded frames in memory and discards remaining
output without logging it. UMU's Proton/runtime log-file options are disabled
for this child. Credentials do not travel in process arguments, environment,
URLs, logs, configuration, backup or export. Capability record formatting redacts
the value.

Messages are newline-delimited JSON, bounded to 4096 bytes. Authentication uses
constant-time comparison. The server admits at most eight simultaneous clients,
with bounded request and reply deadlines. Commands are `status`, `wait`, `restart`
and `quit`; restore remains an authenticated browser workflow. Invalid clients
are closed without echoing their input. An admitted lifecycle command stops even
if the requesting client disconnects before receiving its reply.

## Ownership and completion

A desktop-enabled host holds an exclusive profile lease. The lease is an empty
file beside the profile, named from a hash of its canonical path. Keeping it
outside the profile allows Windows recovery to replace the data folder while
ownership remains exclusive. A repeated launch writes an empty, nonsensitive
open-request marker inside the profile and exits. Its owner consumes that marker
and opens the editor. Unix permissions are applied at marker creation, so immediate
consumption cannot race a later permission operation.

For an externally managed Linux launch, that duplicate child also returns the
bounded `TDSBLIVE-DESKTOP-ALREADY-RUNNING` acknowledgement through its launcher
pipe. Input mode consumes the private bootstrap before exiting; output mode
requires a pipe. The acknowledgement contains no port or capability. Only the
exact marker followed by child exit 0 lets the second native companion exit;
a failed child remains an error and cannot attach to an existing control session.
This prevents a successful editor-open request from leaving another setup window.

The Linux companion also holds its own per-profile lease in the user's private
runtime directory (or its launcher settings directory when no runtime directory
is available). Directory aliases resolve to the same profile identity. The lease
and activation marker are empty, owner-readable files outside the Windows prefix;
checking ownership never creates Windows mappings or a replacement profile.
Reopening activates the existing editor or native recovery controls, without
starting a second backend. This native ownership survives a backend crash until
the user chooses **Close desktop controls**. Recovery is explicit: close those
controls, then reopen the launcher. Activation grants no restart or quit authority.

The host shuts down subscriptions and integrations, checkpoints the database,
then applies any prepared recovery. The lease remains held through replacement
and is released before a successor is started. Only an explicitly successful
restart or restore can request relaunch. A failed recovery retains its safety
copy and reports failure.

| Completion | Meaning |
| --- | --- |
| `quit` | A confirmed quit completed. Close the companion. |
| `relaunched` | The Windows host successfully started its replacement. Close the old companion. |
| `restart-ready` | An external launcher may relaunch after successful restart or restore. |
| `failed` | Shutdown, recovery or replacement launch failed. Show recovery guidance. |
| `port-conflict` | Startup could not bind the configured address. Do not attach to or terminate another application. |
| `stopped` | The host stopped without explicit restart intent. Do not infer relaunch from process death. |

## Verification boundaries

`tools/qualify_desktop_control.py` runs an actual disposable backend with owned
sample settings and tests browser and desktop lifecycle, backup restore, duplicate
launch, retained data and SQLite integrity. Its entry points are restricted to
existing `TDSBLive.exe`, or `dotnet`/`dotnet.exe` plus `ExtensionSuite.Host.dll`.
It uses individual process arguments and permits no arbitrary script or flags.
This is process/protocol evidence; it is not native tray or OBS evidence.

`tools/qualify_windows_tray.ps1` runs only against an owned temporary package on
an isolated interactive Windows CI desktop. It exercises native menus, Cancel
focus, browser handoff, Explorer/fallback recovery, crash isolation and screenshots.
It must never restart Explorer on a user's production desktop. The Windows
registration adapter queries the actual shell notification icon and is tied to
the pinned Avalonia implementation; requalify it when updating Avalonia.

The guarded appearance helper changes only the disposable CI user's theme,
observes Windows UISettings and captures actual light/dark confirmations. The
scaling helper selects an ordinary scale from Windows' display Settings, checks
actual native window DPI, visible button bounds and keyboard Cancel, then restores
the original scale in finally. It neither changes resolution nor injects simulated
DPI notifications. Script parsing and safety guards are preparation evidence;
the actual packaged Windows run must pass these phases.

`tools/serve_tray_obs.py` prepares a separate owned OBS example with a random
loopback port, temporary profile, disabled integrations/LAN, stored custom state,
progress, an image and an explicitly requested short tone. It accepts only the
known packaged host or pinned SDK/host assembly through the shared executable
admission checks. Authenticated restart/restore must report `restart-ready` and
exit cleanly before the owning fixture starts a successor. It does not control
OBS, run live actions or play audio during `--self-check`. Interactive commands
support observation in a separately owned Browser Source; native packaged OBS
screenshots, reconnect and audio evidence remain separate requirements. Quit
stops the owned host, checks SQLite integrity and removes its temporary profile.

For a Windows package tested on Linux, add `--wine /usr/bin/wine` alongside
`--executable` and the actual package's `TDSBLive.exe` path. This qualification
option creates a fresh private Wine prefix beside the temporary test profile;
it never accepts or reuses an existing prefix. Arguments remain separate,
desktop mode stays external, and the session capability still uses stdin only.
Wine initializes its own drives before the backend launches. For this temporary
test only, process-local Mono/Gecko overrides prevent optional installation
prompts during initialization. Backend launch restores Wine's builtin `mscoree`,
which the packaged runtime needs to load assemblies. The example uses native
OBS/browser rendering.
Existing prefixes and the user's environment are not changed.
Cleanup waits for that prefix's Wine server; a timeout retains the temporary
files for inspection instead of killing Wine processes. `--self-check` remains
silent and does not touch OBS. This helper is preparation for packaged OBS
checks, not delivery or qualification of the G23 native Linux companion.

Headless UI tests cover controls and connection behavior. They run serially
because Avalonia's platform registrations are process-wide. Coverage includes
both `ExtensionSuite.*` assemblies and `TDSBLive.Desktop`; handwritten desktop
logic is not excluded from the required quality gate. Real desktop, packaged
application, accessibility and OBS evidence remain separate release requirements.

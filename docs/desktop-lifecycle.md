# Desktop controls and application lifecycle

This describes the 1.0.1 implementation candidate. Native Windows desktop,
packaged application and OBS qualification must pass before release. The native
Linux launcher is the next goal; this document does not claim it is delivered.
See the [tray release plan](tray-release-plan.md) for acceptance evidence.

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
| `external` | An owning launcher supplies a fresh capability through redirected standard input and owns any successful relaunch. A Windows companion is not spawned. |
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
input. An external launcher gives a fresh capability to the backend through its
input pipe. Only the listener port is reported on standard output, prefixed with
`TDSBLIVE-DESKTOP `. Credentials do not travel in process arguments, environment,
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
Cleanup waits for that prefix's Wine server; a timeout retains the temporary
files for inspection instead of killing Wine processes. `--self-check` remains
silent and does not touch OBS. This helper is preparation for packaged OBS
checks, not delivery or qualification of the G23 native Linux companion.

Headless UI tests cover controls and connection behavior. They run serially
because Avalonia's platform registrations are process-wide. Coverage includes
both `ExtensionSuite.*` assemblies and `TDSBLive.Desktop`; handwritten desktop
logic is not excluded from the required quality gate. Real desktop, packaged
application, accessibility and OBS evidence remain separate release requirements.

# Installation

For everyday installation, follow [Install, update and uninstall](user-guide/Install-and-Update.md), then [First setup](user-guide/First-Setup.md). These are the canonical instructions, with screenshots and expected results. The [user-guide home](user-guide/Home.md) links the remaining tasks.

The Windows x64 installer is per-user, with optional login startup disabled by default. The portable ZIP includes .NET and ASP.NET Core; a separate runtime install is unnecessary. Keep the entire extracted folder together. Application data lives separately under `%LOCALAPPDATA%\TDSBLive`; uninstall preserves it. Download a private backup before updating or deleting data.

The local editor uses `http://127.0.0.1:17474/editor`. HTTP is supported; HTTPS and certificates are not required. [Authenticated LAN access](user-guide/LAN-Access.md) is optional. Normal setup needs no command line.

Release status and qualified versions are maintained in [G10 qualification](g10-qualification.md). Native Windows Actions package checks and Wine runtime checks are separate evidence; native Windows streaming-PC performance is deferred. Use only qualified project downloads.

The cumulative **1.0.1 candidate** adds a blue tray icon with **Open editor**,
**Restart** and **Quit**. Manual Windows startup opens the editor; optional
sign-in startup stays quiet. Closing the browser leaves the app running. Follow
[Tray and desktop controls](user-guide/Tray-and-Desktop-Controls.md) to find the
icon, save before restart/quit and use the fallback control window. Before an
update, save, make a backup, choose **Quit**, and wait for shutdown before
replacing application files.

Public release status and the native Linux companion are tracked in the
[1.0.1 plan](tray-release-plan.md). The candidate is not yet a published download;
existing Wine/Bottles and Proton/UMU instructions remain available in the guide.

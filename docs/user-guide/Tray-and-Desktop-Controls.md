# Find TDSBLive, open the editor and quit

This page describes the **1.0.1 release candidate**. Publication and native Linux
instructions are still being completed. Use the guide bundled with your download
for that version's controls.

TDSBLive runs in the background while OBS uses its chat and overlay pages. Its
small **tray icon** gives you a way to find it again and stop it when you finish.
Closing the editor's browser tab leaves TDSBLive running.

## Start it on Windows

1. Open **TDSBLive** from your installed shortcut, or double-click `TDSBLive.exe`
   in the fully extracted application folder.
2. Wait for the editor to open in your normal browser.
3. Look beside the clock on your taskbar for TDSBLive's blue icon with a white
   **T**. If you cannot see it, click the **up arrow** to show hidden icons.

If you chose **start at login** during installation, signing in starts the app
quietly. Use **Open editor** from its tray menu when you want the editor. This
setting does not start an OBS broadcast.

## Choose a tray action

Right-click the TDSBLive icon to open its menu.

![The actual Windows tray menu with Open editor, Restart and Quit](images/tray-windows-menu.png)

| Choose | What happens |
| --- | --- |
| **Open editor** | Opens the editor in your normal browser, using the app's current address and port. |
| **Restart** | Asks for confirmation, stops TDSBLive briefly and starts it again with your saved setup. Your overlays pause while it restarts. |
| **Quit** | Asks for confirmation and stops TDSBLive. Its pages stop working until you start the app again. |

Opening your TDSBLive shortcut again also asks the running copy to open its
editor. You do not need to start several copies to find the editor.

## Restart without losing an unfinished edit

1. Save your changes in the editor and wait for its save status.
2. Right-click the tray icon and choose **Restart**.
3. Read the message. Choose **Cancel** if you still have something to save.
4. Choose **Restart** when you are ready for the brief pause.
5. Check your editor and OBS pages after the app starts again.

![The actual Restart confirmation reminding you to save first](images/tray-windows-restart.png)

**Cancel** receives keyboard focus first. Pressing Enter at that point cancels.
Pressing Escape or closing the confirmation also cancels. Use Tab to move to
the action you intend before activating it.

Restarting does not save unfinished browser forms for you. Saved settings and
overlays remain available. Session-only connection secrets need to be entered
again after a restart.

## Finish a stream

1. End your broadcast using OBS's normal controls.
2. Save any final editor changes. Make a backup after important changes.
3. Right-click TDSBLive's icon and choose **Quit**.
4. Choose **Quit** in the confirmation.

The TDSBLive tray icon disappears when the app has stopped. Quitting TDSBLive
does not close OBS, Streamer.bot or Speaker.bot. Start TDSBLive again using its
shortcut when you next need it.

## If there is no tray icon

First check Windows' hidden-icons up arrow. When the desktop tray itself is
unavailable, TDSBLive offers a small **TDSBLive is running** window with the same
three buttons.

![The actual control window used when the desktop tray is unavailable](images/tray-windows-fallback.png)

Closing this window leaves the background app running. Choose **Quit** to stop
it. The editor's **Backup and recovery** controls remain another way to restart
or quit when the editor still works.

If desktop controls say TDSBLive stopped unexpectedly, check whether the editor
still works. If it does, use its application controls. If it does not, open
TDSBLive from your shortcut. See [Troubleshooting](Troubleshooting.md) if it cannot
start; do not stop other applications or delete your data to clear an unknown
port conflict.

For existing Linux installations, continue using the [Wine/Bottles](Install-on-Linux-Wine.md)
or [Proton/UMU](Install-on-Linux-Proton.md) instructions while the native companion
is being qualified. A future native Linux backend is a separate project.

Next: [Everyday use](Everyday-Use.md), or [Backup and recovery](Backup-and-Recovery.md).

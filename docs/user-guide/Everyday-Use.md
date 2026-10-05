# Everyday use: before and after a stream

Once setup works, you do not need to repeat the installation. Use a short routine so a forgotten connection or old page address does not surprise you.

## Before the stream

1. Start Streamer.bot and Speaker.bot if you use them. Check that their WebSocket servers are running.
2. Start **one** copy of TDSBLive. On Linux, use the same bottle or prefix that contains your working settings.
3. Open [the editor](http://127.0.0.1:17474/editor). In the 1.0.1 tray build,
   a manual start opens it for you; choose **Open editor** from the tray to return
   later. A quiet sign-in start waits for you to open it.
4. Enter any session-only secrets again. Check the connections you actually use.
5. If you track a stream-period total, deliberately set its start time for this session. Do not reset totals just because you opened the editor.
6. Open OBS and select the scene you intend to use.
7. Send a harmless new chat message and check it in the browser and OBS.
8. Check alert placement and audio with a safe local example if you changed them.
9. Review which automation rules are enabled. A restored or newly imported rule may intentionally be disabled.
10. Start your public broadcast only when your normal OBS checks are complete.

TDSBLive does not need your editor tab to remain open, but the background application must remain running. OBS and the bots you use must also remain available.

## During the stream

Use the private reading dock for chat, and the editor for adjustments. Watch the save status before assuming a change has been stored. Make small changes so you can see what each one does.

If a source disconnects, check its connection status first. Use **Open editor**
to return to a running tray build. Older builds can create a port conflict if
you launch extra copies. If chat appears twice, check whether you enabled both
direct WebSocket delivery and a duplicate forwarding action.

Supporter totals reflect the events and date range the application knows about. They do not confirm a payout or prove every paid event arrived.

## After the stream

1. End the broadcast using OBS's normal controls.
2. Save any final layout changes.
3. Download a backup after meaningful changes, especially before an update or runner change.
4. In the 1.0.1 tray build, choose **Quit** from the tray and confirm **Quit**.
   You can also choose **Quit TDSBLive** in **Backup and recovery**.
5. Close the bots if you no longer need them.

Closing the browser tab alone does not quit TDSBLive. Session-only credentials are cleared when the application restarts, so plan to enter them again next time.

See
[Find TDSBLive, open the editor and quit](Tray-and-Desktop-Controls.md) for its
Windows and Linux screenshots and missing-icon help. The confirmation
starts on **Cancel**, so Enter cancels before you change the selection. Escape
or closing the confirmation also keeps the app running. Tray controls
do not save unfinished editor forms, and quitting TDSBLive does not quit OBS
or the other applications you started.

## When something changes

For a new application version, follow [Install and update](Install-and-Update.md). For a new computer, first make a [backup](Backup-and-Recovery.md), then install on the destination and validate the backup before restoring it. For a second computer used only to view pages, read [LAN access](LAN-Access.md).

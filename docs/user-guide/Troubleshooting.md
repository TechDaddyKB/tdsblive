# Troubleshooting: start with the smallest check

Work through the section that matches what you see. Change one thing at a time, then check again. Keep your working program folder and backup while investigating.

## The editor says “site cannot be reached”

1. Check that TDSBLive is running. Opening an offline guide does not launch it.
2. Use [http://127.0.0.1:17474/editor](http://127.0.0.1:17474/editor) on the same computer. If you saved another port, use that port.
3. Check for an error in the launch terminal or application output. Another process using the port can prevent startup.
4. Do not launch repeated extra copies. Quit the unwanted copy before starting another.
5. If you are using another computer, follow [LAN access](LAN-Access.md). Its own `127.0.0.1` is not your host computer.

A local HTTP page does not need an HTTPS certificate. Changing the address to HTTPS is not a fix for an application that has not started.

## The EXE will not launch

On Windows, confirm that you downloaded the application rather than Source code, and extracted the complete ZIP. The EXE needs its neighboring files. Try a local writable folder rather than a cloud-synced location.

On Linux, check that your Wine or UMU command exists and that its quoted EXE path matches the actual extracted folder. Note the exact error. Follow the chosen [Wine](Install-on-Linux-Wine.md) or [Proton](Install-on-Linux-Proton.md) path consistently; do not mix random commands from both.

Bottles Flatpak may need access to the application folder. Follow its folder-access instructions rather than granting access to the whole filesystem. The TDSBLive package includes its runtime; other bots have separate requirements.

## My settings look like they vanished after a Linux launch

Check the bottle or `WINEPREFIX` you used. A different environment has a different Windows-style data folder and can look like a fresh installation.

Quit the current copy and use the original launcher, bottle or prefix. Do not delete either environment while investigating. If you really moved data, use a validated [backup](Backup-and-Recovery.md).

## A bot connection test fails

Check that the bot is running **and its WebSocket server is started**. Match the address, port and authentication on both sides. Save TDSBLive's settings before testing and restart if requested.

A common Streamer.bot port is 8080 and a common Speaker.bot port is 7680, but your actual settings take priority. Session-only secrets must be entered again after restarting.

A successful test means the connection is reachable. You still need the relevant account connection, events or permissions for a particular feature.

## Chat is connected but messages are missing or duplicated

Send a fresh message after the connection is ready. Rumble's initial baseline intentionally avoids treating old data as new activity. Check the selected chat sources and filters.

For duplicates, inspect whether the same event arrives directly over WebSocket and through an explicit forwarding action. Do not attach the bundled forwarder to every chat event when direct delivery already exists.

## The browser shows the overlay but OBS does not

Check the active scene and source visibility. Confirm the source is not covered by another source. In the Browser Source, turn **Local file** off and paste the live overlay URL from the editor.

Match width and height to the intended canvas, then refresh the source if needed. Keep TDSBLive running. Check a new event in the actual OBS preview.

If the Browser Source option itself is missing on Linux, check your distribution's OBS browser-component package. This is an OBS installation issue, rather than a widget setting.

## The preview is silent, or OBS audio is silent

Check the selected media, audio permission for a custom widget, preview-audio control and volume. A browser preview and an OBS source have different playback and monitoring paths.

In OBS, check the relevant meter, source audio settings and monitoring route. Listen to a short local test. A play indicator alone is not proof of audible output.

For speech, verify that your chosen voice alias works in Speaker.bot and that the rule is enabled. Simulation only shows intended rule behavior; it does not establish actual playback.

## A total or threshold looks wrong

Check the selected period, stream start time, financial time zone and currency. Check whether an amount is exact, estimated or unknown.

A USD threshold in minor units uses cents: 500 means $5.00. Bits use their event quantity. Read [Supporter totals](Supporter-Totals.md) and [Automation](Automation.md) before changing values.

## An advanced button is missing

Check your version first. The earlier v0.1.0 MVP predates the TDSBLive 1.0 adaptive editor, custom-widget packages and local compatibility features. See [Before you begin](Before-You-Begin.md). A missing newer control is not a reason to erase your current setup.

## A custom widget is blank or stops

Start with [Custom widgets](Custom-Widgets-and-Portable-Packages.md). Check the saved source, Settings JSON, subscriptions and individually enabled permissions. A portable import turns permissions off until you review them.

Use a minimal widget to check the local APIs before adding more code. External network access is restricted to approved HTTPS domains. Parent-page access, arbitrary navigation and external script imports are not supported ways around the sandbox.

For a migrated widget, [local compatibility](Local-StreamElements-Compatibility.md) supports a limited set of behaviors, not the full hosted StreamElements service.

## A restore succeeded but things are disconnected

That can be expected: restored integrations and rules return to safe states, secrets need re-entry, and earlier viewing tokens are revoked. Review connections, rules and links using [Backup and recovery](Backup-and-Recovery.md), then perform a local message test.

## The Linux computer gets too busy

Live compatibility checks have exceeded CPU targets. Watch load during a local rehearsal, including OBS and the bots you use. Test a smaller layout, fewer active custom widgets or less media, then compare the actual result.

Do not assume a green Windows CI run proves performance on your Linux streaming computer. Keep the working runner version while trying changes separately.

## Ask for help with useful, private-safe information

Include the application version or source revision, operating system, Wine or Proton runner version if applicable, what you tried, the exact symptom, and whether it happens in the browser, OBS or both.

Remove passwords, tokens, private URLs, account details and private chat from screenshots or logs. Share a small reproducible example instead of a whole profile, backup or prefix. Use the [project's issue tracker](https://github.com/TechDaddyKB/tdsblive/issues).

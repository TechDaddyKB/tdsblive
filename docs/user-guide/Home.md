# TDSBLive user guide

TDSBLive brings chat, stream overlays and alerts together on your own computer.
Streamer.bot connects your streaming accounts and runs your actions.
Speaker.bot handles speech. OBS displays your stream.

## Start here

If you already have TDSBLive running, open the editor at
[http://127.0.0.1:17474/editor](http://127.0.0.1:17474/editor).
Keep TDSBLive running while you use its overlays in OBS.

- [Install and update](Install-and-Update.md): choose an installer or ZIP, update safely and uninstall.
- [First setup](First-Setup.md): connect the services you use and review them before streaming.
- [Chat and OBS](Chat-and-OBS.md): show chat on your stream and add a reading dock.
- [Backup and recovery](Backup-and-Recovery.md): protect saved data and restore a checked copy.
- [Overlays and alerts](Overlays-and-Alerts.md): build a layout and test it before adding it to OBS.
- [Supporter totals](Supporter-Totals.md): understand periods, valuations and linked identities.
- [Automation](Automation.md): configure and review speech and sound rules.
- [LAN access](LAN-Access.md): optionally use another computer on your network.
- [Troubleshooting](Troubleshooting.md): everyday checks and help with common problems.

This guide covers installation, first setup, everyday use, connections,
overlays, alerts, supporter totals, automation, backup, updates and recovery.
Illustrations use made-up examples so they do not expose your account details.

Local web pages use **HTTP**. You do not need an HTTPS certificate. Access from
another computer is optional and requires authenticated LAN setup.

## Known limitations

The available live test environment is Wine/Proton. GitHub Actions separately
tests native Windows builds and packages. These are different kinds of checks:
a successful Windows build does not prove OBS playback, and a Wine/Proton test
does not prove performance on a native Windows streaming PC.

Native Windows streaming-PC performance testing is explicitly deferred by the
project owner. Wine/Proton tests exceed the CPU and memory targets; this
limitation is documented for the MVP release rather than claimed as a pass.
Your streaming computer may behave differently.
VTube Studio work is also on hold; use Streamer.bot's built-in integration.

Paid-event checks use made-up, owned examples in separate test data. They check
supporter totals and local speech/sounds without spending money. Actual paid
Rumble Rant, Ko-fi donation and Twitch Bits delivery has not been verified.

## Project and developer information

- [Project repository](https://github.com/TechDaddyKB/tdsblive)
- [Implementation goals and current evidence](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/implementation-plan.md)
- [G10 release work](https://github.com/TechDaddyKB/tdsblive/pull/13)
- [Developer architecture](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/architecture.md)

You do not need the developer documents for ordinary use. They describe how the
application is built and how its integrations are tested.

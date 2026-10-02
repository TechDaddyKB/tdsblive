# Troubleshooting and everyday checks

Before a stream, start TDSBLive and the bots you use. Open the editor, check
their status, send a new chat message and check OBS. Listen to a short alert or
speech test through your actual audio routing. Keep a recent backup.

## The editor will not open

Keep TDSBLive running and try `http://127.0.0.1:17474/editor` on the same
computer. Use your chosen port if it differs. Avoid opening several copies;
another copy or another application can occupy the port.

For a remote computer, use the streaming computer's allowed LAN address and
sign in. `127.0.0.1` always means the computer opening the URL. See
[LAN access](LAN-Access.md) for network setup.

## A bot is disconnected

Start that bot and enable its WebSocket server. Compare its address and port
with the saved TDSBLive settings. Restart TDSBLive after changing connection
settings. If Streamer.bot requires authentication, supply its password locally.
Session-only passwords must be entered again after host restart.

Connected status proves a connection, not that every action or trigger works.
Refresh discovery when actions change. Verify custom-trigger registration
before using Rumble forwarding. Platform accounts are connected in Streamer.bot.

## Rumble says awaiting baseline

Check the private Live API URL and the API's availability. Wait for the poll
cycle. A baseline records what was already present; those historical messages
do not generate fresh alerts. Resetting the baseline intentionally repeats that
suppression. Session-only URLs must be entered again after restart.

## Chat appears in the browser but not OBS

Check the Browser Source URL and size. Use the transparent overlay URL for the
source and the streamer chat URL for your reading dock. Refresh the source
after a change, then send a new message. Check platform filters, ignored users,
bot filters and maximum message settings.

Badges and emotes depend on the message metadata and available provider media.
Use a fresh message to check a media fix. GIFs use recognized supported payloads;
plain text that resembles a GIF label is not enough evidence of GIF delivery.

## An alert is silent

Check the selected media, OBS audio routing, mute state and monitoring. In the
editor preview, enable preview audio. For speech, check Speaker.bot and the
configured voice alias. The current setup uses **local english**.

A successful dispatch receipt does not prove sound reached OBS or your audience.
Listen and check the meter. Do not repeatedly dispatch an uncertain live action
until you know whether it already happened.

## Supporter totals look unexpected

Check the selected period, timezone, stream start and ledger state. Review
exact, nominal and unknown valuations, foreign-currency conversion and linked
identities. Preview events do not change production totals. Unverified event
shapes stay gated. See [Supporter totals](Supporter-Totals.md).

## A saved change is missing

Check the editor save status before refreshing. A conflict can mean another
editor changed the same item. Reload the saved version or review revision
history rather than assuming the last visible edit was stored. Keep backups
before major changes.

## Ask for help

Record the application version, the operation you tried, the expected result
and the actual result. Include whether you use Windows or Wine/Proton and which
bot/OBS versions are involved. The editor's Diagnostics link provides local
health information for investigation.

Keep credentials, Rumble URLs, private viewing links, raw chat captures and
backup archives out of public issues. Review diagnostic material before sharing.
Use made-up examples when possible. Report problems through the
[project issue tracker](https://github.com/TechDaddyKB/tdsblive/issues).

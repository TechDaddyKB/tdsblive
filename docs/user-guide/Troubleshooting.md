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

The Rumble panel shows the last poll, configured interval, measured request
latency, live viewer total, events accepted during this process, duplicate
records suppressed and possible snapshot gaps. Duplicate/gap counters survive
restart in the polling state; baseline suppression is not counted as a duplicate.
A possible gap means the API window may have missed messages, not that missing
messages were recovered. Unknown viewer values remain labelled Unknown.

For developer inspection, search the Event inspector for `rumble.snapshot` and
choose **Inspect sample**. It includes the last stored private snapshot and
observed/unknown/changed field types. Subscriber and gift shapes still need live
evidence before authoritative support processing. **Save sanitized shape** strips
values and replaces unknown field names for structural tests. Ordinary copied
samples and private fixtures can contain personal messages; keep them private.

## Sharing diagnostics

In **Backup and recovery**, choose **Export sanitized diagnostics**. This file
contains aggregate event/delivery counts and basic application metadata, without
messages, supporter records, configuration or logs. Review it before sharing.
Backups and ordinary log downloads remain private. Developers can use the
[online utility guide](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/g13-compatibility.md) to generate owned samples or sanitize log
structure without copying original messages.

## Migrating a custom widget

The [online local compatibility guide](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/g13-compatibility.md) explains the optional
StreamElements lifecycle/store shim. It supports simple owned widgets within
TDSBLive's sandbox. Cloud counters, remote account storage, full session totals
and browser-library imports are unavailable. Review source and grant permissions
explicitly; a descriptive unsupported-call error means that call needs migration.

In the widget JavaScript, opt in with `const SE_API = SBX.enableStreamElements();`
and register the usual `onWidgetLoad`, `onEventReceived` and `onSessionUpdate`
listeners. Define local fields, event subscriptions and permissions in the custom
widget settings. `fieldData` comes from those local settings. Chat uses the
`message` listener with a canonical TDSBLive event; adapt code that expects the
original remote message shape. Grant storage permission before using
`SE_API.store.get(key)` or `SE_API.store.set(key, value)`. Keys hold JSON objects
for this widget instance and survive restart/backup restore; they do not synchronize
with a StreamElements account.

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

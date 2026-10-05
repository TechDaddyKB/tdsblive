# TDSBLive reviewed specification

This is the supplied requirements baseline, reviewed for public publication. The stable goals and approved implementation decisions in [docs/implementation-plan.md](docs/implementation-plan.md) govern sequencing and resolve alternatives in this baseline.

The cumulative 1.0.1 release adds the approved [tray requirements TR-R01–TR-R10](docs/tray-release-plan.md)
and [adaptive UI requirements](docs/ui-redesign-plan.md). Windows and native Linux
desktop controls preserve existing backend/integration authority, HTTP, saved
data and OBS addresses. Linux's backend continues through Wine or UMU/Proton;
a fully native backend is deferred. [Known issues](docs/known-issues.md) records
the explicitly accepted limits. These additions preserve the original numbered
requirements below; [release notes](docs/releases/1.0.1.md) explain the download.

Approved choices: TDSBLive branding; MIT license; .NET 10 LTS and EF Core SQLite; React/TypeScript/Vite; Windows x64 releases; port 17474 to avoid Streamer.bot HTTP conflicts. Local and opt-in authenticated LAN **HTTP are supported; HTTPS is not required**. Original captures/credentials stay private. Public implementation evidence is [Rumble analysis](docs/rumble-analysis.md) and the sanitized fixtures, never the original ZIP. G00 is complete; remaining goals are tracked in the implementation plan. References to supplied/uploaded material below mean this reviewed evidence, not permission to publish raw capture or credential files.

---

Streamer.bot Unified Streaming & Overlay Extension

1. Project Objective

Build a locally hosted extension/companion system for Streamer.bot that provides:

First-class Rumble livestream integration.

Rumble events exposed as Streamer.bot triggers/actions-compatible events.

Multi-platform normalized event ingestion.

Multi-platform combined chat including Rumble.

Persistent financial/supporter tracking across platforms.

StreamElements-style overlays and widgets.

A browser-based WYSIWYG overlay/widget editor.

An OBS-compatible local overlay server.

Extensible custom widgets capable of reacting to any Streamer.bot event.

Alerting, sound, TTS, VTube Studio, and other automation capabilities.

Daily/weekly/monthly/yearly/all-time supporter leaderboards.

The project must be designed as an extensible platform rather than a collection of hard-coded alerts.

The working name in the source tree may be:

StreamerBot.ExtensionSuite

The name must be easy to change later.

2. Core Architectural Decision

Do not attempt to implement the entire application as Streamer.bot inline C#.

Use a hybrid architecture:

┌──────────────────────────┐
│       Streamer.bot       │
│                          │
│ Actions / Triggers       │
│ Existing Twitch/YT/Kick  │
│ VTube Studio Integration │
│ Ko-fi Integration        │
└────────────┬─────────────┘
             │ WebSocket API
             │
┌────────────▼──────────────────────────────┐
│ StreamerBot.ExtensionSuite Host          │
│                                          │
│ Event Router                             │
│ Rumble Adapter                           │
│ Chat Aggregator                          │
│ Financial Ledger                         │
│ FX Service                               │
│ Automation Engine                        │
│ Overlay Runtime                          │
│ Asset Manager                            │
│ SQLite Database                          │
│ REST/WebSocket API                       │
└────────────┬─────────────────────────────┘
             │
      ┌──────┴─────────────┐
      │                    │
┌─────▼──────────┐   ┌─────▼──────────┐
│ Browser Editor │   │ OBS Browser    │
│ WYSIWYG        │   │ Sources        │
└────────────────┘   └────────────────┘

Streamer.bot remains the automation/event authority.

The ExtensionSuite provides services Streamer.bot does not natively provide well:

Rumble polling

deduplication

normalized event storage

cross-platform aggregation

overlays

visual editor

supporter ledger

local asset hosting

event replay/testing

reusable widgets

3. Preferred Technology Stack

Backend

Preferred:

.NET 8 LTS or newer supported LTS

ASP.NET Core

Kestrel

SQLite

Dapper or EF Core

System.Text.Json

Microsoft.Extensions.Hosting

WebSocket support

structured logging via Serilog or equivalent

The application should support a self-contained Windows x64 release.

Design backend abstractions so Linux hosting could be added later.

Frontend

Preferred:

TypeScript

React

Vite

Zustand or equivalent lightweight state management

React Moveable / interact.js / equivalent for canvas manipulation

Monaco Editor for custom HTML/CSS/JS

native WebSocket client

Avoid unnecessarily heavy application frameworks.

Database

SQLite.

Enable:

PRAGMA journal_mode=WAL;
PRAGMA foreign_keys=ON;

Use database migrations.

4. Repository Layout

Create a monorepo similar to:

/
├── README.md
├── SPEC.md
├── docs/
│   ├── architecture.md
│   ├── events.md
│   ├── rumble.md
│   ├── overlays.md
│   ├── widgets.md
│   ├── database.md
│   ├── security.md
│   └── testing.md
│
├── src/
│   ├── ExtensionSuite.Host/
│   ├── ExtensionSuite.Core/
│   ├── ExtensionSuite.Data/
│   ├── ExtensionSuite.StreamerBot/
│   ├── ExtensionSuite.Rumble/
│   ├── ExtensionSuite.Overlays/
│   ├── ExtensionSuite.Finance/
│   └── ExtensionSuite.Web/
│
├── frontend/
│   ├── editor/
│   └── overlay-runtime/
│
├── streamerbot/
│   ├── bootstrap/
│   ├── import/
│   └── examples/
│
├── widgets/
│   ├── alert-box/
│   ├── combined-chat/
│   ├── donor-crown/
│   ├── event-list/
│   ├── media/
│   └── custom-widget/
│
├── tests/
│   ├── unit/
│   ├── integration/
│   ├── replay/
│   └── fixtures/
│
└── tools/

5. Streamer.bot Integration

5.1 Connection

Connect to the Streamer.bot WebSocket API.

Default:

ws://127.0.0.1:8080/

Host, port, password/authentication, and reconnect settings must be configurable.

Use the official Streamer.bot client protocol or @streamerbot/client where appropriate.

The backend should automatically reconnect using bounded exponential backoff.

Connection state must be visible in the ExtensionSuite UI.

6. Streamer.bot Custom Trigger Bridge

Provide an importable Streamer.bot bootstrap action containing C#.

Its Init() method must register ExtensionSuite triggers with:

CPH.RegisterCustomTrigger(...)

Use categories such as:

Custom
└── ExtensionSuite
    ├── Rumble
    ├── Finance
    └── Overlay

Rumble triggers should include at minimum:

Rumble
├── Chat Message
├── Rant
├── Follow
├── Subscription
├── Gifted Subscription
├── Stream Online
├── Stream Offline
├── Viewer Count Changed
├── Like Count Changed
└── API Health Changed

Suggested internal event names:

ext.rumble.chat.message
ext.rumble.rant
ext.rumble.follow
ext.rumble.subscription
ext.rumble.gift_sub
ext.rumble.stream.online
ext.rumble.stream.offline
ext.rumble.stats.viewer_count
ext.rumble.stats.likes
ext.rumble.health

The ExtensionSuite host should invoke registered code triggers using Streamer.bot's code-trigger API.

Every event must supply a dictionary of arguments.

Example:

{
  "platform": "rumble",
  "eventType": "chat.message",
  "userName": "ExampleUser",
  "displayName": "ExampleUser",
  "message": "hello",
  "timestamp": "2026-01-01T00:00:00Z",
  "badges": [],
  "avatarUrl": "...",
  "rumbleStreamId": "...",
  "eventId": "..."
}

7. Rumble Integration

The uploaded rumbleLiveAPIScraper project must be treated as primary implementation evidence.

Do not discard or replace its discoveries with assumptions.

Important observed results from the capture:

785 successful API polls were captured.

751 snapshots contained a livestream.

recent_messages reached 50 entries.

recent_rants were observed.

latest_message and latest_rant are snapshots and repeat across polls.

chat messages do not expose an obvious stable message ID in the observed schema.

followers expose:

username

profile picture

follow timestamp

chat exposes:

username

profile picture

badges

message text

creation timestamp

Rants expose:

username

profile picture

badges

text

creation timestamp

expiry timestamp

amount in dollars

amount in cents

livestream objects expose:

id

is_live

title

scheduled time

created time

categories

watching_now

likes

dislikes

visibility

no real subscriber or gifted-subscriber events occurred in the captured sample.

subscriber and gifted-sub schemas therefore remain partly unverified.

Do not create a brittle parser that rejects unknown fields.

Use tolerant JSON parsing.

Unknown fields should be retained in raw event data.

8. Rumble Credentials

The Rumble Live API URL is effectively a credential.

Never log it completely.

Store it using one of:

Windows DPAPI protected storage, preferably.

encrypted local secrets file.

environment variable.

UI logging must redact query parameters and stream keys.

Example UI display:

https://rumble.com/.../[REDACTED]

9. Rumble Polling

User configuration:

Enabled: yes/no
Polling Interval: 5-10 seconds
Default: 7 seconds

Minimum:

5 seconds

Maximum normal UI setting:

10 seconds

An advanced configuration may allow slower polling.

Do not permit less than 5 seconds unless a future Rumble API explicitly documents that behavior as safe.

Add up to 10% jitter so multiple installations do not synchronize perfectly.

On HTTP failures:

do not emit false offline/chat events

record health state

honor Retry-After

exponential backoff

cap error backoff

automatically resume normal polling after recovery

10. Rumble Snapshot-to-Event Engine

Rumble is a snapshot source, not an event stream.

The extension must convert snapshots into reliable event-like records.

This is one of the highest priority areas of the project.

Initial Poll

The initial successful poll after:

first configuration

application restart

credential change

dedupe reset

must establish a baseline.

Existing:

messages

Rants

followers

subscribers

gift subscriptions

must not fire as new events.

11. Rumble Chat Deduplication

Never use:

latest_message != previous_latest_message

as the primary algorithm.

Instead examine the complete recent_messages array.

Create a deterministic fingerprint from stable observed properties:

stream_id
created_on
username
text
badges

Example canonical representation:

rumble-chat:
<streamId>:
<createdOn>:
<username>:
<normalizedText>:
<normalizedBadges>

Hash with SHA-256.

However, identical legitimate messages may exist.

Therefore each snapshot must treat duplicate fingerprints as a multiset.

Assign an occurrence index based upon snapshot ordering:

SHA256(baseFingerprint + ":" + occurrenceIndex)

Persist recently seen IDs.

Suggested retention:

24 hours or last 10,000 Rumble message fingerprints

whichever is larger.

Do not rely exclusively on in-memory state.

12. Gap Detection

Because the Rumble recent-message window is capped, it is possible for more messages to arrive between polls than the API exposes.

Detect possible gaps.

Track overlap between:

previous recent_messages
current recent_messages

If:

both snapshots contain a full recent window

and overlap disappears unexpectedly

record:

rumble.chat.possible_gap

Include:

{
  "previousCount": 50,
  "currentCount": 50,
  "overlapCount": 0,
  "pollIntervalMs": 7000
}

Expose this in diagnostics.

Do not invent missing messages.

13. Rumble Rant Deduplication

Treat Rants independently from normal messages.

Suggested fingerprint:

stream_id
created_on
username
amount_cents
text
expires_on

Store a stable internal UUID for every detected Rant.

Rants represent monetary support and must also enter the financial ledger.

For observed Rants:

currency = USD

Prefer amount_cents as the canonical monetary representation.

Example:

amount_cents = 100
amount = 1.00
currency = USD

14. Follow Deduplication

Use:

username
followed_on

plus channel/profile context.

Fingerprint:

rumble-follow:<channel>:<username>:<followed_on>

Do not treat follower-count changes alone as authoritative follow events.

15. Subscriber and Gifted Subscriber Handling

The uploaded dataset did not capture live examples.

Implement based on:

official Rumble schema/documentation

tolerant parsing

fixture-driven test cases

future captured examples

Never make subscriber parsing dependent on currently unobserved fields.

Unknown payloads should be recorded so the adapter can be updated without data loss.

Include a diagnostics page displaying newly encountered Rumble JSON shapes.

16. Stream Online/Offline Detection

Use the livestream ID plus is_live.

Online event:

stream transitions from absent/not-live -> is_live=true

Offline event:

previously-live stream disappears
or is_live becomes false

To avoid transient network/API errors:

Require two consecutive successful polls indicating offline before firing Stream Offline.

Make the debounce configurable.

Never interpret a failed HTTP request as Stream Offline.

17. Canonical Internal Event Envelope

Normalize all incoming events to:

{
  "id": "UUIDv7",
  "occurredAt": "ISO-8601 UTC",
  "receivedAt": "ISO-8601 UTC",

  "source": "streamerbot|rumble|kofi|internal",
  "platform": "twitch|youtube|kick|rumble|kofi|system",

  "type": "chat.message",

  "nativeType": "Rumble.ChatMessage",
  "nativeId": null,

  "user": {
    "platformUserId": null,
    "login": null,
    "displayName": null,
    "avatarUrl": null,
    "badges": []
  },

  "message": {
    "text": null
  },

  "monetary": null,

  "stream": {
    "id": null,
    "title": null
  },

  "dedupeKey": null,

  "raw": {}
}

Keep raw source payload unless disabled for privacy.

18. Event Router

All sources feed a common internal event bus.

Sources include:

Streamer.bot WebSocket
Rumble poller
Ko-fi
ExtensionSuite internal events
future adapters

The router must support:

typed subscriptions

wildcard subscriptions

filtering

event history

replay

test events

overlay broadcasts

financial ingestion

logging

automation routing

19. Arbitrary Streamer.bot Event Support

The overlay/widget system must not be hard-coded to a small list of trigger types.

When connected to Streamer.bot:

query available events

query available custom/code triggers

expose them in the editor

subscribe to applicable WebSocket events

permit widgets to bind against them

Widget editor UI:

Event Source:
  Streamer.bot
  ExtensionSuite
  Rumble
  Financial Events

Event:
  [searchable dropdown]

The UI should allow a user to inspect a sample event payload.

20. Event Inspector

Include a real-time developer/event inspector.

Display:

timestamp
source
platform
type
event name
user
monetary value
payload

Functions:

Pause
Resume
Filter
Search
Copy JSON
Replay Event
Save Fixture

This will be critical for debugging custom widgets.

21. Multi-Platform Combined Chat

Create a normalized chat service.

Initial platforms:

Twitch
YouTube
Kick
Rumble

Existing supported platforms should be sourced from Streamer.bot whenever possible.

Rumble comes from ExtensionSuite.

Canonical chat object:

{
  "id": "...",
  "platform": "rumble",
  "timestamp": "...",
  "user": {
    "id": null,
    "login": "user",
    "displayName": "user",
    "avatarUrl": "...",
    "badges": []
  },
  "message": {
    "text": "hello"
  },
  "raw": {}
}

22. Combined Chat Widget

Create a default Combined Chat widget.

Settings:

Platforms:
  ☑ Twitch
  ☑ YouTube
  ☑ Kick
  ☑ Rumble

Show platform icon
Show avatar
Show badges
Show username
Show message
Show timestamp
Message duration
Maximum visible messages
Animation in
Animation out
Background opacity
Font
Font size
Platform colors
Ignored users
Ignored prefixes
Hide bot messages

Support:

vertical scrolling
newest top/bottom
fade out
persistent mode

Rumble messages must never duplicate when subsequent API snapshots contain the same message.

23. Financial / Supporter Ledger

Create a persistent ledger capturing financial viewer support.

Required types:

Twitch Bits
Twitch subscriptions
Twitch gifted subscriptions
YouTube Super Chats
YouTube Super Stickers if exposed
YouTube memberships
YouTube gifted memberships
Kick subscriptions
Kick gifted subscriptions
Ko-fi donations
Rumble Rants
future donation platforms

The system must distinguish:

actual monetary amount
inferred/nominal monetary value
unknown monetary value

Never silently invent a dollar value.

24. Financial Event Schema

{
  "id": "UUIDv7",

  "eventId": "...",

  "platform": "twitch",
  "type": "bits",

  "userPlatformId": "...",
  "userDisplayName": "...",

  "occurredAt": "...",

  "quantity": 500,

  "nativeAmountMinor": 500,
  "nativeCurrency": "USD",

  "usdAmountMinor": 500,

  "fxRate": 1.0,
  "fxRateDate": "2026-01-01",
  "fxProvider": "USD",

  "valuationMethod": "exact|fx|configured_nominal|unknown",

  "metadata": {}
}

Money should be stored as integer minor units wherever possible.

Never use binary floating point for authoritative financial calculations.

25. Subscription Valuation

Gift/subscription events frequently do not provide reliable viewer-spend or creator-revenue values.

Therefore add a configurable valuation system.

Example:

Twitch Tier 1 Gift:
$X.XX nominal supporter value

Twitch Tier 2:
$X.XX

Twitch Tier 3:
$X.XX

Kick Subscription:
$X.XX

YouTube Membership:
$X.XX

YouTube Gift Membership:
$X.XX

Every resulting record must be marked:

valuationMethod = configured_nominal

The UI must make it clear that these values are estimates.

Do not treat estimated values as exact financial/accounting data.

26. Bits

Track:

bits quantity
username
event timestamp
message

Support a configurable nominal USD value per bit.

The default may be configurable during setup rather than hard-coded into database logic.

27. Currency Conversion

All monetary events must retain native currency.

Example:

10 EUR

Store:

nativeAmount
nativeCurrency

fxRate
fxRateDate
fxProvider

usdAmount

Use a replaceable interface:

ICurrencyRateProvider

Cache rates locally.

Prefer historical conversion using the event date.

If a historical rate is unavailable:

use latest available rate

mark valuation as estimated

preserve source currency

Never recompute historical leaderboard values each time an exchange rate changes.

The stored USD valuation should represent the conversion used when the event was ingested/reconciled.

28. Supporter Identity

Do not automatically assume:

Twitch "Bob"
YouTube "Bob"
Kick "Bob"

are the same human.

Create:

supporter
supporter_identity

tables.

Allow manual identity linking from the UI.

Example:

Supporter: Bob

Linked:
Twitch: bob123
YouTube: TheBob
Kick: boblive
Rumble: bob

Leaderboards should aggregate linked identities.

29. Leaderboard Periods

Required:

Today
This Week
This Month
This Year
All Time

Use the streamer's configured timezone.

Do not assume UTC boundaries for daily/monthly leaderboards.

Also permit:

Current Stream
Custom Date Range

30. Donor Crown Widget

Create a widget showing the highest-value supporter for a selected period.

Example:

👑 TOP SUPPORTER
SomeViewer
$427.31

Configuration:

Period
Current Stream
Today
Week
Month
Year
All Time

Include event types
Include platforms
Minimum amount
Show avatar
Show platform badges
Show amount
Show crown
Animation on leader change
Transition duration
Text template
Fonts
Colors
Image assets

When leader changes:

old leader -> animated transition -> new leader

31. Leaderboard Widget

Also create a ranked variant:

1. Alice     $412.22
2. Bob       $350.00
3. Charlie   $244.95

Configurable count:

1-25

32. Ko-fi Donation TTS

Streamer.bot already receives Ko-fi donation events.

Use those events rather than requiring a second Ko-fi polling integration.

Default automation:

Ko-fi Donation
      ↓
normalize financial event
      ↓
persist
      ↓
optional alert
      ↓
Speaker.bot TTS

Configurable TTS template:

"{from} donated {amount} {currency}. {message}"

Settings:

Enabled
Minimum donation
Maximum message length
Speak username
Speak amount
Speak donation message
Voice alias
Bad-word filter
Strip URLs
Strip excessive punctuation
Ignore anonymous message
Queue behavior

Speaker.bot integration must support configurable:

host
port
voice alias
bad-word filter

Default Speaker.bot address:

127.0.0.1

Default port:

7680

33. TTS Safety

TTS messages are untrusted viewer input.

Provide:

maximum characters
URL stripping
repeated-character collapsing
bad-word filtering
optional allowed languages
optional minimum donation
optional manual moderation mode

Example:

AAAAAAAAAAAAAAAAAAAAAA

should optionally normalize to a configurable safe length.

34. Twitch Bits → VTube Studio

Streamer.bot already contains VTube Studio integration.

Do not create a redundant VTube Studio client unless needed.

Create configurable automation mappings:

Bits Range -> VTube Studio action

Example:

100 Bits
Trigger Hotkey: ToggleHat

250 Bits
Trigger Hotkey: ToggleJacket

500 Bits
Trigger Hotkey: DemonMode

Allow:

exact
minimum
range
multiple-of

behavior.

Preferred execution route:

Twitch Cheer event
→ ExtensionSuite rule
→ Streamer.bot action
→ VTube Studio Trigger Hotkey

or an ExtensionSuite-provided Streamer.bot action template.

The VTube Studio hotkey should be selectable by name or ID.

35. Temporary VTube Studio Toggles

Some clothing toggles should automatically revert.

Support:

Trigger Hotkey
wait N seconds
Trigger Hotkey again

or separate enable/disable hotkeys.

Configuration:

Duration
Stack behavior:
  Extend
  Restart
  Ignore
  Queue

Example:

100 bits -> bunny ears for 60 seconds

additional 100 bits:
extend timer to 120 seconds

36. Bits → Sound Alerts

Create a sound-alert rule system.

Example:

100 bits -> bonk.mp3
500 bits -> airhorn.wav
1000 bits -> boss-theme.ogg

Sound assets must be hosted from the local ExtensionSuite asset library.

The actual browser overlay should play the sound so OBS captures it naturally.

Do not require operating-system default audio playback.

Provide:

volume
cooldown
queue
interrupt
ducking metadata
random variant
minimum
maximum
exact amount

37. Alert System

Create an AlertBox widget comparable in concept to StreamElements.

Supported events should include any normalized event.

Built-in alert presets:

Follow
Subscription
Gift Subscription
Bits
Super Chat
Membership
Gift Membership
Kick Subscription
Ko-fi Donation
Rumble Rant
Rumble Follow
Rumble Subscription
Rumble Gifted Subscription
Custom Streamer.bot Trigger

Each alert may contain:

image
GIF
video
audio
HTML
text
animations
duration
layout
TTS

38. Alert Queue

Implement a real alert queue.

Properties:

priority
duration
cooldown
queue group
concurrency
interruptible
maximum queue length

Example queue groups:

main-alerts
sounds
tts

Alerts in different groups may run simultaneously if configured.

39. Overlay Server

Run a local HTTP server.

Default example:

http://127.0.0.1:17474/

Configurable port.

Endpoints:

/editor
/overlay/{overlayId}
/api/...
/assets/{assetId}
/ws/editor
/ws/overlay/{overlayId}

OBS uses:

http://127.0.0.1:17474/overlay/{overlayId}

The page must render with a transparent background.

40. Overlay Definitions

Each overlay:

{
  "id": "...",
  "name": "Main Stream",
  "width": 1920,
  "height": 1080,
  "background": "transparent",
  "widgets": []
}

Supported presets:

1920x1080
2560x1440
3840x2160
1080x1920
Custom

41. WYSIWYG Overlay Editor

The editor must work in a normal browser.

Features:

drag
resize
rotate
duplicate
delete
copy/paste
multi-select
group
ungroup
lock
hide
snap
grid
align
distribute
z-index
undo
redo
zoom
pan
keyboard nudging

UI layout similar to:

┌────────────┬─────────────────────────────┬───────────────┐
│ Widgets    │                             │ Properties    │
│            │        Canvas               │               │
│ Layers     │                             │ Events        │
│            │                             │               │
└────────────┴─────────────────────────────┴───────────────┘

Do not directly copy StreamElements copyrighted UI assets or code.

Use StreamElements only as behavioral inspiration.

42. Editor Autosave

Changes should autosave.

Requirements:

500-1000 ms debounce
revision history
unsaved status indicator
restore previous revision

Maintain a limited revision history.

Example:

50 revisions per overlay

configurable.

43. Built-In Widget Types

Initial widgets:

Alert Box
Combined Chat
Donor Crown
Donor Leaderboard
Event List
Text
Image
Video
Audio/Sound Alert
Goal Bar
Progress Bar
Latest Supporter
Custom HTML/CSS/JS

Architecture must permit new widget packages without modifying editor core.

44. Widget Package Manifest

Example:

{
  "id": "builtin.combined-chat",
  "name": "Combined Chat",
  "version": "1.0.0",
  "entry": "index.html",
  "settingsSchema": "settings.json",
  "eventSubscriptions": [
    "chat.message"
  ]
}

Third-party widget packages should eventually be installable.

45. Widget Settings Schema

Support field types similar in capability to modern overlay editors:

text
textarea
number
slider
checkbox
dropdown
multi-select
color
font
image
audio
video
duration
event
action
user
platform
button
hidden
group

Example:

{
  "fontSize": {
    "type": "slider",
    "label": "Font Size",
    "min": 8,
    "max": 120,
    "value": 36
  }
}

46. Custom Code Widget

Provide:

HTML
CSS
JavaScript
Fields/Settings JSON

Use Monaco Editor.

Custom widgets receive lifecycle events.

Example:

window.addEventListener("sbx:load", ...)
window.addEventListener("sbx:event", ...)
window.addEventListener("sbx:session", ...)

Also provide a friendly API:

SBX.on("chat.message", handler);
SBX.on("financial.event", handler);

SBX.getConfig();
SBX.getSession();
SBX.store.get();
SBX.store.set();

47. StreamElements Compatibility Layer

Do not promise full compatibility.

Implement an optional compatibility shim for common StreamElements custom-widget patterns.

Support where practical:

onWidgetLoad
onEventReceived
onSessionUpdate
fieldData
listener
event

Optional compatibility object:

SE_API

Initially provide only safe/local equivalents where straightforward.

Examples:

SE_API.store.get
SE_API.store.set
SE_API.resumeQueue
SE_API.getOverlayStatus

Unsupported calls must throw a clear descriptive warning rather than silently fail.

Goal:

Allow many simple StreamElements custom widgets to be manually migrated with minimal changes.

Do not scrape or copy proprietary StreamElements widget code.

48. Widget Sandboxing

Custom JavaScript is untrusted.

Each custom-code widget must run inside an iframe.

Suggested sandbox:

sandbox="allow-scripts"

Do not grant:

allow-same-origin
top navigation
cookies
parent DOM access

Communicate through postMessage.

Provide configurable network permissions.

Default:

external network requests disabled

Advanced users may allow domains explicitly.

49. Asset Library

Implement local asset management.

Supported:

PNG
JPEG
WebP
GIF
SVG
MP3
WAV
OGG
WebM
MP4
fonts where licensing permits

Store:

id
filename
MIME
size
hash
upload time
metadata

Deduplicate identical files using SHA-256.

Serve through:

/assets/{id}

Never expose arbitrary filesystem paths to widget JavaScript.

50. Overlay Event Transport

Overlay renderer should connect to ExtensionSuite using WebSocket.

Message:

{
  "op": "event",
  "event": {
    "id": "...",
    "type": "chat.message",
    "platform": "rumble"
  }
}

Only transmit events required by widgets loaded in that overlay when practical.

Support reconnect and resubscription.

51. Overlay Preview/Test System

Editor must let the user fire simulated events.

Examples:

Test Twitch Follow
Test 500 Bits
Test Rumble Rant
Test Ko-fi $10
Test YouTube $20 Super Chat
Test Chat Message

Allow raw JSON event injection in developer mode.

Simulation must not enter financial totals unless:

"Persist test event"

is explicitly selected.

52. Database Tables

Minimum tables:

settings
secrets_metadata

events
event_dedupe

rumble_poll_state
rumble_stream_state

chat_messages

supporters
supporter_identities
financial_events
fx_rates
valuation_rules

overlays
overlay_revisions
widgets
widget_settings
widget_state

assets

automation_rules

application_logs

53. Financial Event Constraints

Add unique source identifiers whenever available.

Example:

UNIQUE(platform, native_event_id)

For sources without IDs:

unique dedupe_key

No financial contribution should be inserted twice.

Financial deduplication must survive application restart.

54. Rumble Persistence

Persist at least:

last successful poll
last stream ID
stream live state
dedupe fingerprints
recent message window hashes
recent rant hashes
recent follower hashes
poll health

This ensures restart does not replay previous messages as new alerts.

55. Diagnostics Dashboard

Create a status page:

Streamer.bot        Connected
Rumble              Connected
Speaker.bot         Connected
Overlay Server      Running
Database            Healthy

Last Rumble Poll    2 seconds ago
Poll Interval       7 seconds
Poll Latency        184 ms

Rumble Stream       LIVE
Viewer Count        127

Events Received     14,424
Duplicates Dropped  52,932
Possible Gaps       0

56. Rumble Raw Inspector

Developer-only page:

Last API response
Observed fields
Unknown fields
Schema changes
Subscriber payloads
Gifted-sub payloads

Allow:

Copy sanitized JSON
Save sanitized fixture

Secrets must always be redacted.

57. Import Existing Rumble Recorder Data

Add an optional development/import command capable of reading the existing scraper's exported JSONL data.

Purpose:

regression testing

replay

event detection validation

dedupe validation

Example:

ExtensionSuite.exe rumble replay captures.jsonl.gz

The production application does not need the original recorder running.

58. Replay Testing

Create tests which replay the provided captured Rumble data in chronological order.

Assertions:

no message emitted twice
no Rant emitted twice
no historical message emitted on baseline
stream online emitted once
stream offline emitted once per transition
network failure does not generate offline
restart does not replay chat

Run replay tests automatically in CI.

59. Automation Rules

Provide a lightweight rule system.

Example:

when:
  type: twitch.bits
  min: 100

then:
  - streamerbot_action: "Toggle Bunny Ears"

Another:

when:
  type: kofi.donation
  min_usd: 5

then:
  - speakerbot:
      voice: EventVoice
      text: "{user} donated {amount}. {message}"

Another:

when:
  type: rumble.rant

then:
  - overlay_alert: "Rumble Rant"
  - financial_ledger: true

60. Streamer.bot Actions

The UI should query existing Streamer.bot actions so users may choose actions by name.

Store stable action GUID internally.

Display human-readable action name.

If an action disappears:

Missing Streamer.bot Action

rather than silently doing nothing.

61. Sound Alert Example

Configuration UI:

Trigger:
Twitch → Bits

Condition:
100 ≤ Bits < 500

Action:
Play Overlay Sound

Sound:
bonk.mp3

Volume:
80%

Cooldown:
5 seconds

62. VTube Studio Example

Trigger:
Twitch → Bits

Condition:
Exactly 500

Action:
Streamer.bot Action

Action:
VTS - Toggle Jacket

Allow multiple actions per rule.

63. Rumble Alert Example

Trigger:
Rumble → Rant

Condition:
Amount ≥ $5

Actions:
1. Add financial contribution
2. Show Rumble Rant alert
3. Optional TTS

Variables:

{user}
{amount}
{message}
{avatar}

64. Security

Default bindings:

127.0.0.1 only

Do not expose the editor or OBS overlay server externally by default.

If user binds to:

0.0.0.0
LAN IP

require authentication.

Use generated bearer/session token.

Protect:

editor
admin API
WebSocket admin channel
asset upload
configuration endpoints

OBS overlays should use per-overlay access tokens when remote access is enabled.

65. CORS

Default:

same-origin only

Do not use:

Access-Control-Allow-Origin: *

for privileged APIs.

66. Secret Handling

Never place secrets into:

overlay HTML
widget payloads
browser logs
event raw payloads sent to widgets
normal application logs

Redact:

Rumble API URL
Streamer.bot password
Ko-fi verification secret
tokens
stream keys

67. Logging

Structured logs.

Levels:

Trace
Debug
Information
Warning
Error
Critical

Default file retention:

14 days

Provide log download/export.

Sensitive values must be scrubbed.

68. Performance Targets

On a normal streaming PC:

Backend idle CPU:

<1%

Normal chat activity:

<3% average backend CPU

Memory target:

<300 MB backend

Overlay renderer should avoid unnecessary DOM reflows.

The overlay editor may use more resources than OBS runtime.

OBS overlay pages should be lightweight.

69. Database Performance

Indexes:

events(occurred_at)
events(type)
chat_messages(occurred_at)
financial_events(occurred_at)
financial_events(supporter_id)
financial_events(platform)
supporter_identities(platform, platform_user_id)

Leaderboards should use indexed SQL aggregation rather than loading all records into application memory.

70. Backups

Provide:

Backup Database
Restore Database
Export Configuration
Import Configuration

Export overlays/widgets separately as portable packages.

Example:

my-overlay.sbxoverlay
my-widget.sbxwidget

Use ZIP containers containing manifest + assets.

71. Overlay Revision Export

Overlay package:

manifest.json
overlay.json
widgets/
assets/

No absolute filesystem paths.

72. API

Provide internal REST API.

Example:

GET  /api/status
GET  /api/events
GET  /api/overlays
POST /api/overlays
GET  /api/overlays/{id}
PUT  /api/overlays/{id}

GET  /api/widgets
POST /api/widgets

GET  /api/chat

GET  /api/supporters
GET  /api/leaderboard?period=month

GET  /api/assets
POST /api/assets

GET  /api/integrations/streamerbot
GET  /api/integrations/rumble
GET  /api/integrations/speakerbot

POST /api/test-event

Document with OpenAPI.

73. WebSocket Protocol

Use explicit operation envelopes.

Example subscribe:

{
  "op": "subscribe",
  "types": [
    "chat.message",
    "financial.event"
  ]
}

Event:

{
  "op": "event",
  "event": {}
}

Heartbeat:

{
  "op": "ping"
}

74. Configuration

Use a typed configuration model.

Example:

{
  "server": {
    "host": "127.0.0.1",
    "port": 17474
  },

  "streamerBot": {
    "host": "127.0.0.1",
    "port": 8080
  },

  "rumble": {
    "enabled": true,
    "pollIntervalSeconds": 7
  },

  "speakerBot": {
    "host": "127.0.0.1",
    "port": 7680
  }
}

Secrets must be stored separately.

75. Startup Sequence

Load config
↓
Open database
↓
Run migrations
↓
Start HTTP server
↓
Connect Streamer.bot
↓
Discover Streamer.bot events/actions
↓
Connect Speaker.bot
↓
Initialize overlay event bus
↓
Load automation rules
↓
Start Rumble polling

76. Shutdown

Graceful shutdown must:

stop accepting new work
stop Rumble polling
flush event queue
persist dedupe state
close WebSockets
checkpoint SQLite WAL if appropriate
close database

77. Error Isolation

Failure of one integration must not kill the host.

Example:

Rumble unavailable

must not stop:

overlays
Twitch events
YouTube chat
Kick chat
financial database
editor

Use isolated hosted services.

78. Test Strategy

Unit tests

Test:

Rumble fingerprints
Rumble multiset dedupe
FX conversion
financial dedupe
leaderboard periods
identity linking
event normalization
automation conditions

Integration tests

Test:

Streamer.bot WebSocket mocked server
Speaker.bot mocked server
overlay WebSocket
REST API
SQLite migrations

Browser tests

Use Playwright.

Test:

create overlay
add widget
drag
resize
save
reload
test event
render OBS page

79. Rumble Replay Edge Cases

Fixtures must cover:

same message in consecutive polls
same text from different users
same text twice from same user
message array rotation
50-message full window
zero array overlap
network timeout
HTTP 429
HTTP 500
empty livestream array
stream restart
new stream ID
old Rant repeated
Rant expiration
application restart

80. Acceptance Criteria — Rumble

Rumble integration is complete when:

API can be configured securely.

Poll interval can be set between 5 and 10 seconds.

Messages appear in ExtensionSuite within approximately one polling interval.

Repeated snapshots do not duplicate chat.

Restart does not replay old chat.

Rants fire once.

Followers fire once.

stream-online/offline transitions are reliable.

Rumble events can trigger Streamer.bot actions.

Rumble messages appear in combined chat.

Rants enter financial ledger.

API errors do not create false events.

81. Acceptance Criteria — Overlay Editor

Complete when a user can:

Open editor in browser.

Create a 1920x1080 overlay.

Add widgets.

Drag/resize them.

Change properties.

Save.

Copy generated OBS URL.

Add that URL to OBS Browser Source.

Receive live Streamer.bot events.

Receive Rumble events.

Create a custom HTML/CSS/JS widget.

Bind widgets to arbitrary Streamer.bot events.

test widgets without a real donation/follow.

82. Acceptance Criteria — Combined Chat

Complete when:

Twitch message
YouTube message
Kick message
Rumble message

can appear in one overlay.

Each message must identify platform.

A Rumble message visible across five Rumble API polls must only render once.

83. Acceptance Criteria — Financial Ledger

Complete when supporter totals can combine:

Bits
donations
Super Chats
subscriptions
gift subscriptions
memberships
gift memberships
Kick subscriptions
Ko-fi
Rumble Rants

using transparent valuation rules.

The application must be able to answer:

top supporter today
top supporter this week
top supporter this month
top supporter this year
top supporter all time

84. Acceptance Criteria — Donor Crown

When financial event ingestion changes the highest supporter:

widget updates without browser refresh

Supporter identity linking must change totals correctly.

85. Installation Experience

Target final installation:

1. Run installer.
2. ExtensionSuite starts.
3. Browser opens setup wizard.
4. Enter Streamer.bot connection.
5. Import provided Streamer.bot bootstrap.
6. Enter Rumble Live API URL.
7. Optionally configure Speaker.bot.
8. Create overlay.
9. Copy OBS Browser Source URL.
10. Done.

Avoid requiring command-line usage from normal users.

86. Setup Wizard

Pages:

Welcome
↓
Streamer.bot
↓
Rumble
↓
Speaker.bot
↓
Financial valuation defaults
↓
Overlay setup
↓
Complete

Each integration should have:

Test Connection

button.

87. StreamElements Investigation

Use the documented StreamElements overlay workflow as UX inspiration.

Important concepts to replicate functionally:

overlay canvas
widget palette
property editor
single browser-source overlay URL
custom HTML/CSS/JS
custom fields
event-based widgets
event queue
persistent widget storage
test events

Do not clone proprietary implementation details, assets, or source code.

If browser automation is available during development, the existing authenticated StreamElements session may be used to manually inspect:

editor interaction patterns
widget placement behavior
layer controls
property panels
testing workflow
custom widget workflow

The goal is feature parity in capability, not pixel-for-pixel copying.

88. Relevant Documentation

Primary project references:

Streamer.bot API:

https://docs.streamer.bot/api

Streamer.bot examples:

https://docs.streamer.bot/examples

Streamer.bot JavaScript/TypeScript client:

https://github.com/streamerbot/client

StreamElements overlay documentation:

https://docs.streamelements.com/overlays

Speaker.bot WebSocket API:

https://speaker.bot/api/websocket

Also use:

official Rumble Live Stream API documentation

official VTube Studio API documentation

Streamer.bot VTube Studio integration documentation

Streamer.bot Ko-fi documentation

The uploaded Rumble scraper, observed schema, and captured data take priority when determining what the current Rumble response actually looks like.

89. Implementation Phases

Phase 0 — Project Foundation

Build:

solution layout
ASP.NET host
SQLite
migrations
logging
REST API
frontend shell
configuration
tests

Deliverable:

Application starts and editor shell loads.

Phase 1 — Streamer.bot Bridge

Build:

WebSocket connection
auto reconnect
GetEvents
GetActions
GetCodeTriggers
custom bootstrap
event inspector

Deliverable:

Streamer.bot events appear live in Event Inspector.

Phase 2 — Rumble

Build:

secure URL storage
poller
parser
baseline
chat dedupe
Rant dedupe
follows
subs
gift subs
stream state
health
replay tests

Deliverable:

Rumble events trigger Streamer.bot actions.

This phase is mandatory before moving to advanced overlays.

Phase 3 — Combined Chat

Build:

normalized chat
Twitch
YouTube
Kick
Rumble
combined chat widget

Deliverable:

OBS displays unified chat.

Phase 4 — Overlay Runtime

Build:

overlay definitions
runtime page
WebSocket event delivery
asset serving
widget host
OBS support

Deliverable:

OBS browser source displays an overlay and responds to events.

Phase 5 — WYSIWYG Editor

Build:

canvas
layers
move
resize
properties
snap
undo
redo
autosave
test events

Deliverable:

Nontechnical user can create an overlay.

Phase 6 — Custom Widgets

Build:

HTML
CSS
JS
fields
sandbox
SBX API
Monaco
event subscriptions
storage

Deliverable:

Developer can create arbitrary widgets.

Phase 7 — Alert System

Build:

AlertBox
queue
animations
sounds
media
templates
test alerts

Phase 8 — Financial Ledger

Build:

financial event normalization
supporter identities
FX
valuation rules
leaderboards
Rumble Rants
Ko-fi
Bits
Super Chats
subscriptions
gift memberships/subs
Kick

Phase 9 — Donor Widgets

Build:

Donor Crown
Leaderboard
Latest Supporter
Current Stream Leader

Phase 10 — Automations

Build:

Ko-fi TTS
Bits → sound
Bits → VTS
generic event rules

Phase 11 — Packaging

Build:

installer
first-run wizard
backup
export/import
self-contained release
documentation

90. Codex Development Rules

Codex must follow these rules throughout implementation.

Rule 1

Do not replace evidence-based Rumble handling with guessed API structures.

Rule 2

Never emit historical Rumble snapshot entries as new events after startup.

Rule 3

All dedupe state affecting money or viewer alerts must survive restart.

Rule 4

Never use floating-point numbers for authoritative currency calculations.

Rule 5

Do not assume same usernames across platforms represent the same person.

Rule 6

Do not expose Rumble URL, stream keys, passwords, or API tokens to overlays.

Rule 7

Overlay widgets may fail independently without crashing an entire overlay.

Rule 8

Integration failures may not crash ExtensionSuite.

Rule 9

Every new event source must produce the canonical internal event envelope.

Rule 10

Any user-generated custom widget JavaScript must execute in an isolated sandbox.

Rule 11

All database schema changes require migrations.

Rule 12

All Rumble event-detection changes require replay tests against stored capture fixtures.

91. Required Documentation Produced by Codex

Codex must maintain:

README.md
docs/architecture.md
docs/installation.md
docs/rumble.md
docs/events.md
docs/overlays.md
docs/widgets.md
docs/automation.md
docs/database.md
docs/security.md
docs/development.md

rumble.md must explain the snapshot/deduplication problem in detail.

92. Required Developer Utilities

Include commands or internal tools for:

database migration
Rumble capture replay
event fixture replay
generate sample events
database backup
sanitize logs
export diagnostics

93. Future Extensibility

Design adapters so new platforms can be added later.

Interface concept:

public interface IEventSource
{
    string Name { get; }

    Task StartAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<NormalizedEvent> Events(
        CancellationToken cancellationToken);
}

Possible future sources:

Facebook Live
TikTok
Patreon
Fourthwall
Streamlabs
StreamElements
Discord
custom webhooks

Financial/event code must not contain platform-specific switch statements everywhere.

Use adapters/strategies.

94. Future Widget Marketplace

Do not implement a marketplace now.

Architecture should make possible:

.sbxwidget packages
manifest
version
author
permissions
assets
settings
runtime

Potential permissions:

network
persistent storage
financial events
chat events
raw events
audio

95. Explicit Non-Goals for Initial Version

Do not initially build:

cloud hosting
user accounts
multi-tenant SaaS
remote StreamElements synchronization
remote OBS control
Rumble chat message sending
full StreamElements API compatibility
plugin marketplace
mobile editor

Keep the first version local-first.

96. Highest-Priority Engineering Risks

Treat these as engineering risks requiring tests, not afterthoughts.

Rumble message loss

50-item snapshot limit means bursts can exceed polling window.

Mitigation:

5-10 second polling
window comparison
gap diagnostics
persistent state

Rumble duplicate messages

Same entries appear across polls.

Mitigation:

fingerprints
multiset comparison
persistent dedupe

Financial double counting

Reconnect/replay could duplicate events.

Mitigation:

native IDs
dedupe keys
database unique constraints

Arbitrary custom JavaScript

Custom widgets execute user code.

Mitigation:

iframe sandbox
CSP
postMessage
permission model

OBS performance

One complex overlay can consume substantial resources.

Mitigation:

single shared WebSocket
lightweight runtime
avoid editor libraries in runtime bundle
event filtering
DOM limits

97. Definition of MVP

The MVP is not complete until all of the following work together:

Rumble poller
Rumble chat dedupe
Rumble Rant event
Rumble → Streamer.bot custom trigger
Twitch/YT/Kick/Rumble combined chat
local overlay server
OBS browser source
basic WYSIWYG editor
AlertBox
Ko-fi TTS
Bits sound alert
Bits VTube Studio action
financial event database
Rumble Rant financial entry
donor crown widget

This is the first usable release.

98. First Codex Task

Before implementing production code:

Inspect every file in the supplied rumbleLiveAPIScraper.

Do not expose .secrets/rumble-url.

Inspect:

README

recorder implementation

schema.json

fields.json

report.md

capture database schema

Write:

docs/rumble-analysis.md

Document:

every observed Rumble field

candidate event types

dedupe strategy

unknown/unobserved structures

replay testing design

Build replay fixtures from sanitized captured data.

Only then implement ExtensionSuite.Rumble.

After Rumble analysis is committed, implement Phase 0 and Phase 1.

Do not attempt the overlay editor before the event architecture and Rumble adapter have stable tests.

99. Expected Final User Experience

A streamer should eventually be able to:

Launch Streamer.bot.

Launch ExtensionSuite automatically.

Open the local ExtensionSuite dashboard.

See Twitch, YouTube, Kick, Rumble, Ko-fi, Speaker.bot, and VTube Studio connection health.

Build an overlay visually.

Add Combined Chat.

Add AlertBox.

Add Top Supporter Crown.

Copy one browser-source URL into OBS.

Create a rule saying:

100 Bits → Toggle VTube Studio Hat

Create:

500 Bits → play airhorn.mp3

Enable:

Ko-fi Donation → Speaker.bot TTS

Receive a Rumble Rant.

See that Rant:

trigger a Streamer.bot action

appear as an alert

enter the supporter database

potentially change the Donor Crown

Receive Rumble chat without duplicate messages appearing every poll.

The streamer should not need StreamElements for any of these functions.

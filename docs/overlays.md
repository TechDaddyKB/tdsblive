# Overlays

Follow [Chat and OBS](user-guide/Chat-and-OBS.md) for transparent combined chat and the optional light/dark streamer dock. Follow [Overlays and alerts](user-guide/Overlays-and-Alerts.md) to create, position, resize, configure, save and preview a canvas. OBS consumes one browser-source URL per overlay; the application must remain running.

`/overlay/{id}` serves the transparent runtime. `/chat/{id}` serves the streamer reading view. Assets are served through validated `/assets/{id}` routes. A filtered, bounded overlay WebSocket handles events and state changes; reconnect reloads state and subscriptions. Preview messages remain isolated from live automation and ordinary financial totals.

The editor and OBS runtime are separate bundles. Canvas revisions preserve recoverable layouts. Alert groups have bounded queues, configured overflow/interruption policies and supported local media. Browser audio completion and external speech dispatch are different outcomes; listen through the actual OBS path before relying on audio.

Detailed contracts: [chat transport](g05-overlays-chat.md), [basic editor and alerts](g06-editor-alerts.md), [donor widgets](g08-donor-widgets.md), and [foundation API](foundation-api.md). Advanced canvas tools belong to G11, custom-widget sandboxing and portable overlay/widget packages to G12, and limited StreamElements compatibility to G13. These later goals are not part of the G10 MVP.

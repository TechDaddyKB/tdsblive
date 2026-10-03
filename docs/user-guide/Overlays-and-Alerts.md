# Overlays and alerts

An overlay is a web page OBS places over your video. A widget is one piece of
that page, such as chat, an image, a donor list or an alert.

## Create an overlay

1. Open **Visual Overlay Editor** in the TDSBLive editor.
2. Enter a **New overlay name** and a **New overlay ID**. Use a short ID without
   spaces, such as `main-alerts`.
3. Choose a **Canvas preset** that matches your OBS layout, or choose Custom and
   enter its width and height.
4. Click **Create overlay**.
5. Add the widgets you need from the Widgets list.

This example shows a saved text widget. The center is your canvas; its settings
are on the right. Your overlay starts empty until you add a widget.

![Visual editor with a welcome text widget](images/visual-editor.png)

Choose a widget on the canvas or in Layers. Drag it to position it, use its
resize handle to change its size, and adjust its properties. Grid snapping
helps keep positions neat. Raise or lower a layer to change which item appears
in front. A hidden layer does not appear; a locked layer cannot be dragged.

Changes save through the editor. Check **Editor save status** and use **Save
now** before closing. If saving fails or another editor changed the overlay,
resolve that message before assuming your changes are stored.

## Add it to OBS

1. Click **Copy OBS URL** for the selected overlay.
2. In OBS, add a **Browser Source** and paste that URL.
3. Match the source width and height to your canvas.
4. Check the actual source in OBS. Keep TDSBLive running.

If your browser cannot copy automatically, TDSBLive displays **OBS URL:**
followed by the link. Select that link and copy it manually, then paste it into
OBS. Automatic clipboard access is a convenience; HTTP still works without it.

The URL uses local HTTP and needs no certificate. On another computer,
`127.0.0.1` means that other computer; it does not point back to your streaming
PC. Remote viewing requires the separately configured authenticated LAN flow.

## Set up an alert

1. Add **AlertBox** to the overlay.
2. Select it and configure the alert's event, text and media settings.
3. Upload your own media using **Upload widget media** in Assets, then choose it
   in the widget settings. Supported types include images, GIFs, audio and
   video; the upload limit is 20 MiB per file.
4. Position and resize the alert, then save.
5. Open **Preview** before sending a test event.

For a sound test, turn on **Enable preview audio**. Browser audio rules and OBS
routing can affect playback. Listen through OBS and check its audio meter before
depending on a sound during a stream.

## Test without changing live totals

Under **Test events**, choose the test type and platform, then click **Send
isolated test event**. The event reaches only this overlay's preview viewers.
It does not persist, change financial totals or execute external automation.
Your normal OBS source is a live view, so use the preview to check these events.

Advanced raw injection is for developer diagnostics. Ordinary alert setup does
not require it. Preview success does not prove a real platform event reaches
the application; check that separately for each service you use.

## Undo or restore a layout

Use **Undo** and **Redo** for recent edits. **Revision history** lists retained
saved versions. Restoring an older version creates a new saved revision rather
than silently deleting the history. Retained revisions are limited; keep a
[backup](Backup-and-Recovery.md) for longer-term recovery.

## Advanced canvas controls

Shift/Ctrl-click layers to select several. Group selection makes them move,
rotate and resize together; Ungroup selection releases them. Copied groups stay
independent. Alignment, distribution, selection lock/visibility and front/back
buttons are above the canvas. Grid visibility is independent from snapping.
Use Pan canvas or middle-button dragging to move around a zoomed canvas.

Add Event List for filtered recent activity. Goal Bar and Progress Bar can use a
manual value or a supporter total in USD. Ledger bars use your financial period
and filters; preview shows no production totals. See the online
[advanced editor guide](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/g11-advanced-editor.md)
for full controls and limits. Custom-widget authoring belongs to the next milestone.

## Custom code widgets

Choose **Add custom** to edit HTML, CSS, JavaScript and Settings JSON in the local Monaco editor. Custom code runs with a virtual DOM in an isolated worker inside its sandboxed iframe. Use `document.getElementById`, DOM text/HTML updates and `SBX.on` for canonical or arbitrary available events. Browser navigation, parent access and real browser Window APIs are unavailable.

Permissions default off. Enable chat, financial, redacted raw data, persistent storage, media/audio or exact HTTPS network domains only after reviewing the code. Silent preview suppresses custom media/audio as well. Declared media assets use asset IDs in `src`; CSS can use `sbx-asset:ASSET_ID`. The full API, schema and limits are in the [custom-widget guide](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/g12-custom-widgets.md).

**Export overlay** and **Export selected widget** download portable `.sbxoverlay` and `.sbxwidget` ZIPs. **Import portable package** creates a new overlay or adds a widget to the selected overlay, remaps identities and disables custom permissions for review. Widget state and access tokens are excluded. Full backup/restore retains custom code and state.

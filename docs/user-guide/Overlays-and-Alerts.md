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

Advanced canvas tools and custom-widget authoring belong to later goals. This
guide describes the current basic editor, not every planned future feature.

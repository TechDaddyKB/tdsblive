# Advanced editor and widgets

**Version requirement:** TDSBLive 1.0.1 or newer. The earlier v0.1.0 MVP predates these controls. Read [Before you begin](Before-You-Begin.md) if a button is missing.

Start with [a basic overlay](Overlays-and-Alerts.md). This chapter helps when you have several items to arrange, or want an event list or goal display. No custom code is needed for these built-in widgets.

## Select and move several items

Choose items on the canvas or in **Layers**. Ctrl-click or Shift-click adds items to a selection. Group selected items when you want them to move together; grouping is flat rather than groups inside groups.

Try the change on a small selection, then check the result. **Undo** and **Redo** help you recover an unwanted editing step. Save the result you intend to keep.

Common shortcuts are Ctrl+A to select all, Ctrl+C and Ctrl+V to copy and paste, Ctrl+D to duplicate, Ctrl+G to group, and Ctrl+Shift+G to ungroup. Arrow keys move by one pixel; Shift with an arrow moves by ten. Delete removes the selected items. Use Undo if you remove the wrong item.

## Line things up

Use alignment controls to put selected items along the same edge or center. Distribution spreads the spaces between three or more selected items evenly.

The grid uses ten-pixel spacing. Showing the grid and snapping to the grid are separate choices: seeing grid lines does not necessarily make items snap. Turn snapping on if you want that behavior.

Zoom changes how large the canvas looks while editing; it does not change the final overlay dimensions. Zoom ranges from 10% to 200%. Use middle-mouse panning to move your view of the canvas.

## Show recent events with Event List

Add an **Event List** widget, then choose the sources and event types it should include. A short list is easier for viewers to read than a long history.

It shows recent items from the retained event history rather than promising a full platform archive. The recent list is capped at 100 entries and retained history at 500. Templates format the displayed information, with inserted values escaped rather than treated as arbitrary HTML.

Test with a safe example and confirm that the chosen filters include it. If an event is missing, check the filters and source connection before changing the template.

## Show progress with Goal

A **Goal** widget displays progress toward a target. Choose either a manual amount or the appropriate ledger source and currency.

For a manual example, set the current amount to 25 and the target to 50. The expected progress is 50%. For a USD ledger example, 12.50 toward 100.00 gives 12.5%.

The progress bar stops filling at 100%, while the amount text can still show that the goal was exceeded. This avoids drawing a bar beyond its intended space. A ledger goal remains subject to the totals' date, currency and estimate rules.

## Save and verify

Check autosave status or use **Save now**, then inspect the live page in OBS. Revisions help recover earlier layouts, but make a full [backup](Backup-and-Recovery.md) before major changes.

The editor supports up to 100 widgets in an overlay. Staying within that limit does not prove a complex media-heavy layout will run comfortably on every computer. Check your actual streaming setup.

Next: [Custom widgets and portable packages](Custom-Widgets-and-Portable-Packages.md), or [Everyday use](Everyday-Use.md).

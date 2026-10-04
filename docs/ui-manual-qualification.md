# Remaining hands-on UI qualification

Use an isolated owned sample project and the exact candidate package. Record
date, tester, Windows/browser/OBS versions, tested commit and package SHA256.
Each result needs an observation or capture; an unchecked item is outstanding.
The separate [nontechnical-user script](ui-usability-test.md) requires three
participants and has not yet been performed.

## Browser zoom, accessibility and input

- Set native browser zoom to 200%, then 400%, using the browser's own menu.
  Record both the percentage and CSS viewport width. Visit all ten destinations,
  expand advanced settings and open dialogs. Ordinary controls must reflow;
  necessary canvas/table scrolling stays inside its region.
- Resize continuously through portrait and short windows with a selected widget
  and unsaved changes. Confirm selection, form contents and saved pixel geometry.
  Fit follows resizing; a chosen manual zoom remains unchanged.
- Use only the keyboard to select layers, add/edit/test a named alert, open/close
  menus and dialogs, navigate back/forward, copy the OBS URL and restore a revision.
  Confirm visible focus and restoration to each opener. Editing text must not
  trigger canvas shortcuts.
- With the available screen reader, confirm field names, selected destinations,
  dialog titles, step labels, save errors/conflicts and test matching results.
  Record missing/duplicate announcements. Check contrast in both themes and
  enable reduced motion before repeating an alert test.
- On physical touch hardware, select multiple layers with the checkbox, use
  drawer tabs, pan and resize. Repeat using numeric placement and arrangement
  buttons. Record hardware, browser and any unreachable or obscured controls.

## Exact package and audible OBS

- Use the protected CI portable/installer artifacts and record their hashes.
  Keep production profiles, data and integration execution permissions separate.
  Repeat old-document/v1 and v2 imports, identity remapping, backup/restart/restore
  and the parity checklist on this package.
- Confirm OBS streaming and recording are off. Use a disposable Browser Source
  in a temporary Studio Mode preview scene, preserving the program scene and
  original Studio Mode state. Point only to the isolated candidate host.
- Use owned follow and $5/$10 donation examples. Capture which design plays in
  first/all matching modes and verify the configured overlay size and opacity.
- Attach owned audio media. Verify the editor draft and saved preview start
  silent. Explicitly enable preview audio, test once and record actual hearing
  together with OBS audio activity. A playback receipt or meter alone does not
  count as audible evidence. Disable preview audio and leave/reopen the workspace;
  confirm silence again.
- Confirm preview tests added no live events, ledger entries, bot actions or live
  custom state. Remove the temporary source/scene, restore OBS state, stop the
  isolated host and remove its temporary data.

## Recording sheet

| Scenario | Pass / fail / outstanding | Observed evidence | Fix and retest commit |
| --- | --- | --- | --- |
| Native 200% zoom | Outstanding | | |
| Native 400% zoom | Outstanding | | |
| Keyboard/focus | Outstanding | | |
| Screen-reader announcements | Outstanding | | |
| Both themes/contrast/reduced motion | Outstanding | | |
| Physical touch and drag alternatives | Outstanding | | |
| Exact Windows package/parity/recovery | Outstanding | | |
| Exact packaged OBS visuals | Outstanding | | |
| Preview silence and actual audible output | Outstanding | | |
| Side-effect isolation and cleanup | Outstanding | | |

Do not close G20/G21 or publish a completed redesign release until failures are
fixed and these records and participant observations pass on the final build.

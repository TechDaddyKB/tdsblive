# Advanced editor and built-in widgets

G11 extends the MVP canvas without adding custom code execution. HTTP remains
supported. Custom HTML/CSS/JS, Monaco and portable widget packages belong to G12.

## Canvas operations

Select a layer in the canvas or Layers list. Shift/Ctrl-click adds or removes
layers; Ctrl+A selects all. Group selection combines selected layers into one
flat group. Selecting any member selects its group; ungrouping releases members.
Group IDs are independent of alert queue groups. Regrouping merges selected
groups; nested groups are not created.

Movement, nudging, rotation, resizing, copy/paste, duplication, alignment and
z-order work with selections. Groups preserve spacing during alignment and
distribution. A single selected layer/group aligns against the canvas; multiple
units align against their shared bounds. Distribution requires three independent
layers/groups. Rotation uses the selection center. Group resizing scales member
sizes and relative positions. Geometry uses the existing bounded canvas
coordinates; operations that would create invalid geometry are rejected.

Ctrl+C/Ctrl+V use an editor-local clipboard. Ctrl+D duplicates. Copied groups get
new IDs and stay independent of originals. Ctrl+G groups; Ctrl+Shift+G ungroups.
Arrow keys move one pixel; Shift+arrows move ten. Delete removes the selection.
Ctrl+Z/Ctrl+Shift+Z or Ctrl+Y undo/redo each committed operation. Typing in fields
does not trigger canvas shortcuts. Locked selections cannot transform, regroup,
delete or reorder; the selection lock button unlocks them. Visibility and
z-order have explicit selection controls.

Grid visibility and 10-pixel snapping are independent. Zoom ranges from 10% to
200%. Use Pan canvas or middle-button dragging to scroll the viewport. These
view controls do not change overlay geometry. Autosave retains the 750 ms
debounce, version conflicts and bounded revisions. Server-supplied defaults are
accepted without repeated saves when there is no newer local edit.

## Built-in widgets and requirement trace

| Source section 43 widget | Implementation and evidence |
|---|---|
| Alert Box | Existing queue/preset/media implementation; G06/browser regression |
| Combined Chat | Existing filtered four-platform feed; G05/browser regression |
| Donor Crown, Donor Leaderboard | Existing ledger projections and appearance; G08/browser regression |
| Latest Supporter | Existing latest counted supporter, period/filter/appearance; G08/browser regression |
| Text, Image, Video, Audio/Sound Alert | Existing escaped text and uploaded media; G06 media/browser regression |
| Event List | Filtered, deduplicated, bounded canonical events with templates, expiry, order and font |
| Goal Bar, Progress Bar | Manual or ledger USD source, target, label, colors, orientation and value/percent display |
| Custom HTML/CSS/JS | Explicitly assigned to G12, not claimed complete by G11 |

Event List has type/platform filters and ignored users. Empty platform selection
means all; `*` selects all event types. Templates support `{user}`, `{type}`,
`{platform}` and `{message}` as escaped text. It retains at most 100 visible
entries and the shared runtime retains at most 500 events. Transient entries
expire by their received timestamp. Live history returns only the newest 500
stored live events, filtered to visible Event List widgets; an older matching
event outside that bounded window is not recovered. Reconnection preserves UUID
dedupe. Preview events remain ephemeral and history does not trigger alerts.

Progress targets use ordinary units for manual values and USD for ledger values.
Ledger bars reuse the existing period/platform/support-kind/minimum supporter
filters. Unvalued and gated amounts are excluded and estimate warnings remain
visible. Monetary totals retain exact integer strings for display; percentage
calculation caps the integer amount before numeric conversion. Bars clamp their
fill at 100% while showing actual overshoot. Preview never reads production
financial totals. Both bar kinds share settings/runtime behavior.

## Typed settings (source section 45)

`SettingsFields` supplies reusable typed controls with stable keys, bounded
numbers/durations, grouped fieldsets and asset MIME filtering. Its schema covers
text, textarea, number, slider, checkbox, dropdown, multiselect, color, font,
image/audio/video, duration, event, action, user, platform, button, hidden and
group. Event/action/user choices are provided by the caller; configured stable
IDs remain visible while their integration is disconnected. Buttons call the
editor handler and never dispatch a live action automatically. Hidden fields
render no control. Settings are validated again by backend widget contracts.

Event List and progress properties use these schemas; other MVP property forms
retain their existing controls. Typed renderer tests exercise every field type,
asset filtering, numeric/font rejection and stable action IDs. This is the G11
typed-control capability, not G12's untrusted custom-schema/package platform.

## Runtime and security

All canvas widgets share one reconnecting WebSocket. Runtime imports no editor,
Monaco or settings UI code. Event expiry shares the existing alert timer, with
one-second time updates. `/api/overlays/{id}/events` supports the same revocable,
overlay-scoped token as the existing chat history route. It excludes raw and
monetary payloads and never exposes privileged `/api/events` through that token.
Invisible widgets do not subscribe. Server filtering applies to live socket and
REST history; simulation delivery stays limited to the target owned preview.

Current acceptance evidence and remaining gates are recorded in the G11 section
of [the implementation plan](implementation-plan.md#g11). Real OBS media,
reconnect/restore, native Windows CI and Sonar quality results must be recorded
before G11 is marked Complete.

# Optional: local StreamElements compatibility

**Version requirement:** TDSBLive 1.0.1 or newer. This optional feature is absent from the earlier v0.1.0 MVP. See [Before you begin](Before-You-Begin.md).

This feature helps some custom widgets written around StreamElements-style events run locally in TDSBLive. It is a limited compatibility layer, not the whole StreamElements website or its cloud services. You do not need it for TDSBLive's built-in widgets.

## 1. Begin with a safe copy

Keep the original widget source and make a copy to try locally. Create or import a custom widget. In its JavaScript source, enable the local compatibility layer before the existing event handlers:

```javascript
const SE_API = SBX.enableStreamElements();
```

This is a code opt-in, not a switch that imports a remote account. Review the code and settings before allowing it to run.

Do not paste account credentials into the source. Cloud account access, arbitrary remote scripts and an unrestricted browser environment are not supplied by this option.

## 2. Review what the widget actually needs

Check the source for the events, stored values, media and network calls it expects. Compare them with the supported behavior in the [G13 compatibility documentation](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/g13-compatibility.md).

The local bridge includes supported lifecycle and event mapping, configuration, permission-gated storage and media behavior. Enabling compatibility does not enable every permission. Approve only the chat, financial, storage, media, audio or exact HTTPS domains the widget needs.

If the widget depends on an unsupported hosted service, change that part or use another widget. Repeatedly enabling permissions will not create a missing cloud API.

## 3. Test a little at a time

First check that the widget loads and shows its initial content. Then try one safe local event. Check a setting change, and check storage only if the widget needs it.

For images and video, inspect the actual displayed result. For audio, listen through the intended output path. A request being accepted is not proof that you saw or heard the media.

Preview and live widget state are separate. After preview succeeds, save and check the live URL in OBS. Test the effects of a restart if the widget relies on persistent state.

## 4. Keep recovery simple

Make a full application backup after a working configuration. Portable packages exclude stored widget state and restart with permissions disabled on import, so they are useful for sharing a design rather than preserving an entire working session.

If something stops, return to the smallest working example, inspect the specific unsupported call or denied permission, and use [Troubleshooting](Troubleshooting.md). Full StreamElements compatibility is not claimed.

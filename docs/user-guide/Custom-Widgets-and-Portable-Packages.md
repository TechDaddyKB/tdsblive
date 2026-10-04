# Custom widgets and portable packages

**Version requirement:** these features require newer G12/G13 builds. They are newer than the published v0.1.0 MVP. See [Before you begin](Before-You-Begin.md).

A custom widget uses HTML for its content, CSS for appearance, JavaScript for behavior and JSON for settings. If you do not write code, you can still import a trusted package and use its exposed settings. You do not need a custom widget to display ordinary chat or basic alerts.

## 1. Import a package carefully

A `.sbxoverlay` file shares an overlay. A `.sbxwidget` file shares a widget. Obtain packages from someone you trust, then use the corresponding import control in the editor.

Review the import preview before confirming. Imported identities are remapped so a package does not silently overwrite your existing items. Permissions start disabled. An import can succeed while a widget remains unable to receive events or play media until you approve the required abilities.

A portable package includes the supported design and assets. It excludes credentials, private application data, revision history and persistent widget state. Use a full [application backup](Backup-and-Recovery.md) for recovery, rather than treating a portable package as one.

## 2. Review permissions one at a time

A custom widget should receive only what it needs. Chat, financial data, raw events, storage, media, audio and network access are separate permissions, initially off.

For example, a chat display may need chat permission but not financial data. A sound widget needs the appropriate media and audio abilities. Network access is restricted to approved, exact HTTPS domains; approving one domain does not grant access to every site.

Subscriptions say which events the widget asks for. They do not grant permission by themselves. If you subscribe to chat but leave chat permission off, it should not receive chat.

Test the widget locally after reviewing these choices. Do not turn every permission on just to make a blank widget disappear.

## 3. Adjust settings before changing code

Select the widget and read its exposed settings. Change one value, save, and check the result. Use preview to understand it before checking the live OBS page.

Preview has separate stored state from live use. A value written in preview is not proof that the same value exists in the live widget. Persistent widget storage is scoped to widget identity and limited to 32 KiB. It is not a place to store account credentials.

## 4. Export a design

Save the intended layout, then use the export control for the overlay or selected custom widget. Give the package a useful name. Before sharing it, inspect your own code and settings for personal information; package exclusion rules cannot make a secret you deliberately typed into widget source safe to publish.

Tell the recipient which newer application build it requires and which permissions it needs. They should review those permissions after import.

## Optional: start writing a small widget

The source editor provides separate HTML, CSS, JavaScript and Settings JSON areas. It uses a locally provided code editor; an online editor account is not needed.

Start with a plain label before adding event handling. A settings field can use a key such as `label`, a visible label such as `Label`, and type `text`. Use `SBX.getConfig()` to read configuration and `SBX.on` to subscribe to the supported events, following the [custom-widget API documentation](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/g12-custom-widgets.md).

Custom scripts run in a restricted worker with a virtual document interface. They cannot treat the editor as an unrestricted browser page. Parent-page access, arbitrary navigation, external script imports and direct media playback are not supported escape routes. Use the documented bridge APIs for supported media and events.

Save often and inspect errors with a small example. If borrowed code relies on hosted StreamElements behavior, read [Local StreamElements compatibility](Local-StreamElements-Compatibility.md) before expecting it to run unchanged.

Next: [Troubleshooting](Troubleshooting.md), or [Backup and recovery](Backup-and-Recovery.md).

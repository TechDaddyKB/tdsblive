# Backup and recovery

A backup is a saved copy you can return to after an unwanted change or a problem. Make one while the application is working, before you need it.

## Choose the right kind of copy

An **application backup** protects the application's supported saved data, including overlays, assets, history and supporter information. Newer custom-widget builds also include their supported stored widget state. It is the right starting point for recovery or moving your setup.

A **portable overlay or widget package** shares a design. It intentionally leaves out credentials, private user data, revision history and stored widget state. It is not a full recovery backup. Read [Custom widgets and portable packages](Custom-Widgets-and-Portable-Packages.md).

A **connection-settings export** is another separate item. It is not a full application backup. Importing settings applies safe defaults, including disabled imported connections and LAN access; review them afterward.

## 1. Download a backup

1. Open the editor's **Backup and recovery** page.
2. Choose **Download backup**.
3. Save the ZIP in a backup folder outside the running program folder.
4. Rename your saved copy with a useful date and description, without changing its `.zip` extension.
5. Keep an additional copy on another drive if you want protection against drive failure.

![Backup and recovery controls](images/backup-recovery.png)

The screenshot shows the backup area. Read the result and validation messages when using your own copy.

Backups can contain private chat or supporter information. Keep them private. They do not include connection passwords or credentials, so plan to enter those again after recovery.

## 2. Validate before restoring

A restore changes your current setup. First download a fresh backup of the current state, even if you intend to replace it.

1. Choose the backup file in **Backup and recovery**.
2. Use **Check backup** and read the result.
3. Review the preview: check that it is the backup you intended and that it is compatible with this version.
4. Resolve any validation problem before continuing. Do not edit a ZIP to force a rejected restore.
5. Select **I want to replace my current saved data with this backup**, then choose **Restore checked backup** only after reviewing its effect.

A backup from a newer application may not work with an older one. Keep the original program version and its backup when updating.

## 3. Reconnect safely afterward

Restoring returns the application to a safe state. Integrations and automation rules are paused or disabled as required, and previous viewing tokens are revoked. That means a restore can succeed even though chat, speech or an old shared viewing link no longer works immediately.

Re-enter connection secrets, check the services you use, and enable only the rules you intend to run. Create new viewing links if needed. Check a message and the actual OBS preview before a broadcast.

## 4. Protect a Wine or Proton environment too

On Linux, application data normally lives inside the prefix or bottle. After quitting its applications, copying that entire environment can preserve runner-specific setup in addition to the app's backup. Keep track of which environment contains which application.

A full environment copy can contain credentials and other private data, so treat it as private. Do not upload it as a support attachment. Do not delete an original environment until the destination works.

## Restart and quit controls

Use the application's own local **Restart TDSBLive** or **Quit TDSBLive** controls when needed. Closing a browser window only closes your view of the app. Remote LAN access deliberately does not expose every local recovery or process-control operation.

Next: [Everyday use](Everyday-Use.md), or [Troubleshooting](Troubleshooting.md).

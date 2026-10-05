TDSBLive for Linux

This folder has two parts:
- TDSBLive is the native Linux launcher and tray app.
- backend holds the complete Windows app. Wine or Proton runs this part.

Before you start
1. Extract the whole download into a folder you want to keep.
2. Have Wine installed, or have UMU and Proton installed.
3. Keep XWayland available on a Wayland desktop. The launcher uses stable X11.
   The applications-menu shortcut also uses GNU coreutils.

Open TDSBLive
1. Open the TDSBLive application in this folder.
2. Choose Wine or Proton (UMU), then its program and Windows settings folder.
3. Already used TDSBLive? Choose your existing saved setup.
   Only select "Start a new empty TDSBLive setup" for a separate new setup.
4. Leave the Windows app path pointing to backend/TDSBLive.exe.
5. Want a shortcut next time? Select "Add TDSBLive to my applications menu".
   This is optional and does not start the app when you sign in.
6. Select "Start TDSBLive". Your normal browser opens the editor.

While you stream
TDSBLive keeps running when you close the browser. Look for its blue T icon in
your panel. Its menu has Open editor, Restart and Quit. Restart and Quit ask you
to confirm. Cancel keeps the app running.

If your desktop has no tray, a small controls window gives you the same options.
GNOME may need an AppIndicator extension to show tray icons. You can keep using
the controls window without installing an extension.

When you finish
Choose Quit from the tray or controls window, then confirm. Closing the browser
alone does not quit TDSBLive.

Keep this whole folder in place. The menu shortcut points to it. To change your
Linux runner or saved setup, use "Change Linux setup" in the shortcut's menu.

More help
The offline guide is in guide/Home.html. LICENSE.txt and licenses contain license
notices.

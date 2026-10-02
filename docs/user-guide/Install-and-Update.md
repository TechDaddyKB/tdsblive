# Install, update and uninstall

> G10 release preparation: the installer and portable ZIP are release candidates.
> Final integrated qualification is still in progress. Use this guide with the
> candidate you are testing; this page does not announce a finished release.

TDSBLive runs on your streaming computer. Keep it open while OBS uses its chat
or overlays. You also need Streamer.bot for platform connections and actions.
Speaker.bot is needed only if you want speech.

## Choose a download

Use the **Windows x64 installer** for the simplest setup. It adds a Start menu
shortcut and can optionally start TDSBLive when you sign in.

Use the **Windows x64 ZIP** if you prefer to put the application in a folder
yourself. The ZIP includes the application runtime; you do not need to install
.NET separately.

Get files only from this project's approved candidate or
[GitHub Releases](https://github.com/TechDaddyKB/tdsblive/releases). A completed
MVP release has not yet been announced. Do not download a file from a chat link
claiming to be a release without checking its source.

## Install with the setup program

1. Download the file ending in **win-x64-setup.exe**.
2. Open it and follow the setup pages. Read the license when prompted.
3. Choose a desktop shortcut if you want one.
4. Leave **Start TDSBLive when I sign in to Windows** unchecked unless you want
   it to start automatically. This option is off by default.
5. Finish setup and choose **Open TDSBLive**, or open it from the Start menu.
6. Your browser should open the editor. If it does not, open
   [http://127.0.0.1:17474/editor](http://127.0.0.1:17474/editor).

The installer normally installs for your Windows account without administrator
rights. It puts application files in your local Programs folder. Your saved
data lives in a separate TDSBLive folder, so uninstalling the application does
not erase your overlays or supporter records.

The address above means “this computer.” It works locally over HTTP without a
certificate. If you changed the port, use your chosen port instead of 17474.

## Use the portable ZIP

1. Download the file ending in **win-x64.zip**.
2. Right-click it and choose **Extract All**. Choose a folder you can find again.
3. Open the extracted folder and double-click **TDSBLive.exe**. Do not run it
   from inside the ZIP preview.
4. Open the editor address above in your browser.

“Portable” describes the application folder. Saved data still lives separately
under your Windows account; it is not stored beside the EXE by default. Keep the
whole extracted application folder together.

## First launch

The editor opens **Guided setup** until you finish its review. Work through one
step at a time. Saved progress resumes after restart. Skip services you do not
use, and review connection status before relying on them during a stream.

For chat and OBS instructions, continue to [Chat and OBS](Chat-and-OBS.md).
For protecting your saved work, see [Backup and recovery](Backup-and-Recovery.md).

## Update safely

1. In the editor, download a backup. Keep that ZIP somewhere outside the
   application folder.
2. Choose **Quit TDSBLive** under Backup and recovery. Close the editor tab.
3. Download the approved newer version.
4. For an installer update, run the new setup program. For a ZIP update, extract
   the new version into a new folder instead of mixing old and new files.
5. Open the updated application. Review your connections and test chat and
   alerts before your next stream.

Do not run the old and new versions together. If the update fails, keep the
backup and the old application folder. Backup schema compatibility is checked
before restore; do not assume any older version can open newer saved data.

## Uninstall

For an installer installation, open **Settings → Apps → Installed apps** in
Windows, find **TDSBLive**, and choose **Uninstall**. Quit TDSBLive first.

For a ZIP installation, quit TDSBLive and remove its extracted application
folder. Neither method deliberately removes the separately stored user data.

To find that data folder, press Windows+R, enter `%LOCALAPPDATA%\TDSBLive`, and
press Enter. Keep a backup before manually removing it. Removing the data folder
also removes locally stored connection credentials, overlays and supporter data.

## If it does not open

- Keep the application running, then try the editor address in a browser.
- If another copy is already running, use its editor instead of opening more
  copies. A port conflict can prevent a second host from starting.
- If you selected another port, use that port in the editor and OBS URLs.
- If a download or installation fails, retain your backup and record which file
  and version you used when reporting the problem.

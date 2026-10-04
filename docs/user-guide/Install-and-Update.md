# Install, update and remove TDSBLive

Choose the instructions that match the computer where TDSBLive will run. You can use a native Linux OBS installation with TDSBLive running through Wine or Proton on that same computer.

## Choose your installation path

- **Windows:** use [Install on Windows](Install-on-Windows.md). The installer is convenient; the ZIP gives you direct control of the program folder.
- **Linux, using Wine:** use [Install on Linux with Wine](Install-on-Linux-Wine.md). This explains both a direct Wine launch and an optional Bottles workflow.
- **Linux, using Proton:** use [Install on Linux with Proton](Install-on-Linux-Proton.md). This explains a separate Proton environment through UMU.

Wine and Proton run the Windows application. There is no native Linux release implied by these instructions. If you already have a working Wine installation, you do not need to switch runners just because another option exists.

Read [Before you begin](Before-You-Begin.md) to distinguish the published MVP from newer development builds.

## Update without losing your working copy

An update replaces program files. Your settings and saved overlays are application data, which normally live elsewhere. Protect both before changing versions.

1. Open TDSBLive's **Backup and recovery** page and download a backup ZIP.
2. Put that ZIP somewhere you can find again. Use a name that includes the date and the version you are leaving.
3. Stop using the pages in OBS while updating. You do not need to delete the sources.
4. Use the application's **Quit TDSBLive** control. Closing the editor tab is not the same as quitting the application.
5. For an installer installation, run the new official installer. For a ZIP installation, extract the new version into a new folder; keep the old program folder until the new copy works.
6. On Linux, start the new copy with the **same Wine or Proton prefix** as before. A different prefix can look like a brand-new installation with no settings.
7. Open the editor and check your saved overlays, connections and a test message.
8. Check OBS before starting a broadcast. A running app does not prove that the correct overlay is on screen.

Do not run the old and new versions together. They may try to use the same port and data folder. If the update fails, quit it before returning to the old copy. See [Backup and recovery](Backup-and-Recovery.md) before restoring data; a newer data format may not be compatible with an older application.

## Remove the application

On Windows, use the normal installed-app removal screen if you used the installer. For a ZIP installation, quit TDSBLive and remove the program folder when you are sure you no longer need it.

Uninstalling the Windows application leaves application data available for recovery. Decide separately whether you want to keep it. Make a backup before deliberately removing the data folder.

On Linux, removing the extracted program folder and removing a prefix are different operations. A prefix may contain saved TDSBLive data and other applications. Do not delete a shared prefix or a whole bottle just to remove one program. Keep a backup first.

Next: use the installation page for your operating system, then [First setup](First-Setup.md).

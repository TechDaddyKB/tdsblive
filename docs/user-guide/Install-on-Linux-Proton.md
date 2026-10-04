# Install on Linux with Proton

Proton is a Windows compatibility system built for Linux gaming. It can also be used to try this Windows application, but that does not make TDSBLive a native Linux app. If your direct Wine setup already works, continuing to use it is a reasonable choice.

This page provides an upstream-supported way to launch GE-Proton outside Steam using **UMU**. The project's live checks do not qualify every UMU or Proton version. Treat a new runner as a setup to test before your next broadcast.

## 1. Understand the three pieces

**TDSBLive** is the application ZIP you download from this project. **GE-Proton** is the compatibility runner. **UMU** supplies the runtime environment used to launch that runner outside Steam.

GE-Proton's maintainers state that use outside Steam is supported through UMU. Do not substitute a direct call to a Wine binary buried inside GE-Proton, or an arbitrary `proton run` command, for this setup. See [GE-Proton's upstream instructions](https://github.com/GloriousEggroll/proton-ge-custom).

If you already use a launcher with UMU integration, follow that launcher's current instructions for adding an application. Keep a separate environment for TDSBLive and check which runner and prefix it uses. A shortcut alone does not tell you where saved data lives.

## 2. Install UMU using its instructions

Open the [UMU launcher project](https://github.com/Open-Wine-Components/umu-launcher) and follow its current installation instructions for your distribution. Use a supported package or the upstream method it documents. Package availability varies, so this guide does not give one installation command for every Linux distribution.

After installation, open your application menu and look for **Terminal**. This is a window where you type a command and press Enter. Paste the line below, using Ctrl+Shift+V if your terminal uses that shortcut, then press Enter:

```bash
umu-run --help
```

You should see UMU's help rather than “command not found.” If the command is missing, finish installing UMU before continuing. Steam itself is not required for the UMU method.

## 3. Extract the complete application ZIP

Download the Windows application ZIP from [TDSBLive Releases](https://github.com/TechDaddyKB/tdsblive/releases), then extract it into an ordinary local folder. This example uses `~/Applications/TDSBLive`.

Check that the actual EXE is `~/Applications/TDSBLive/TDSBLive.exe`. If there is another nested folder, adjust the command below to match. The Source code archive is not the application, and extracting only the EXE is not enough.

## 4. Launch with a separate prefix

A prefix is the runner's Windows-style folder for settings and saved data. This example deliberately uses a different name from the direct Wine guide, so the two environments are easy to tell apart.

```bash
WINEPREFIX="$HOME/.local/share/tdsblive-proton" PROTONPATH=GE-Proton umu-run "$HOME/Applications/TDSBLive/TDSBLive.exe"
```

`$HOME` means your home folder, and the quotation marks keep a path with spaces together. Adjust the EXE path if you extracted the files elsewhere. The `WINEPREFIX` portion selects the environment; it is not a second application you must download.

UMU's documented `PROTONPATH=GE-Proton` option obtains a GE-Proton runner. The first run may download both a runner and the runtime, so allow time and an internet connection. Read any errors before repeatedly launching extra copies.

Use the same prefix each time. Keep a note of the runner version that worked. The automatic runner selection above can change as upstream versions change; for a stable streaming setup, use UMU's documented selection of an installed runner and test an update separately before relying on it.

Run as your ordinary Linux user. Do not add `sudo`. Keep the terminal open during your first test, and quit through TDSBLive's own control when finished.

## 5. Check the editor and OBS

Open [http://127.0.0.1:17474/editor](http://127.0.0.1:17474/editor) in your normal Linux browser. You should see the editor or guided setup. The application must stay running while you use that page.

Native Linux OBS can display the same local URLs through a Browser Source. If OBS has no Browser Source option, check your distribution's OBS browser-component packaging; switching the TDSBLive runner will not add that component to OBS.

Before a real show, test a chat message, your chosen overlay and any sound you intend to use. Watch your system's CPU and memory use as well. Live project testing under Linux has not met the project's CPU targets.

## 6. Protect your data and connections

Saved data belongs to this prefix, not just to the extracted program folder. A different prefix can open as an empty installation. Return to your original launch command before assuming data was deleted.

Make an application backup before changing runners. If you copy a whole prefix, quit its applications first. Keep the original until you have verified the copy.

Start with session-only credentials. If Windows-style persistent credential protection does not work in your environment, re-enter secrets after a restart instead of storing them in plain text.

Install Streamer.bot and Speaker.bot using [their own Linux guidance](https://docs.streamer.bot/get-started/installation/linux). A Proton launch of TDSBLive does not supply those programs or their dependencies. They may run in their own working environment and connect over localhost.

Next: [First setup](First-Setup.md). You can return to [the Wine option](Install-on-Linux-Wine.md) if you prefer it; keep the environments and backups separate.

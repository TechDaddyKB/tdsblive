# TDSBLive

[![Latest release](https://img.shields.io/github/v/release/TechDaddyKB/tdsblive)](https://github.com/TechDaddyKB/tdsblive/releases/latest)
[![Windows CI](https://github.com/TechDaddyKB/tdsblive/actions/workflows/windows-ci.yml/badge.svg)](https://github.com/TechDaddyKB/tdsblive/actions/workflows/windows-ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Your chat, overlays, alerts and supporter tools—running on your own computer.**

TDSBLive is a local-first companion for Streamer.bot, Speaker.bot and OBS Studio.
Bring incoming events together, create graphics for your stream, track supporters
and configure speech, sounds and actions from a browser-based editor.

**[Download TDSBLive](https://github.com/TechDaddyKB/tdsblive/releases/latest)** ·
**[Read the user guide](https://github.com/TechDaddyKB/tdsblive/wiki)** ·
**[Get help](https://github.com/TechDaddyKB/tdsblive/issues)**

## Download and install

The public download is currently **v0.1.0**. The cumulative **v1.0.1** update is
being qualified and is not published yet. There is no public v1.0.0 download.
Check the release notes and bundled guide for the features in your download.

Official releases provide self-contained **Windows x64** builds. You do not need
to install .NET separately or build the source code to use them.

| Download | Best for |
| --- | --- |
| **Windows installer** — `TDSBLive-<version>-win-x64-setup.exe` | A guided, per-user installation with an application shortcut. |
| **Application ZIP** — `TDSBLive-<version>-win-x64.zip` | Managing the application folder yourself. Extract the entire ZIP, then run `TDSBLive.exe`. |
| **`SHA256SUMS.txt`** | Checking that your downloaded files match the release's published checksums. |

Get these files from the [latest release](https://github.com/TechDaddyKB/tdsblive/releases/latest).
The **Source code** archives are for developers, rather than ordinary installation.
Each application download includes an offline user guide and the supplied
Streamer.bot import. Start-at-login is optional and off by default.

See [Windows installation](docs/user-guide/Install-on-Windows.md) for full instructions.
For Linux compatibility setups, see the [Wine/Bottles guide](docs/user-guide/Install-on-Linux-Wine.md)
or [Proton/UMU guide](docs/user-guide/Install-on-Linux-Proton.md). These use the
Windows backend. The **v1.0.1 Linux candidate** bundles it with a native Linux
launcher, tray and setup window in `TDSBLive-1.0.1-linux-x64-wine.tar.gz`.
That candidate supports installed Wine or UMU/Proton and keeps your chosen prefix.
A fully native Linux backend remains deferred. The Linux bundle is not a public
release yet.

## Your first stream with TDSBLive

1. Install or extract the application and start **TDSBLive**.
2. Open **[http://127.0.0.1:17474/editor](http://127.0.0.1:17474/editor)** in your browser.
3. Follow guided setup and connect only the services you use. Streamer.bot handles
   its supported platform connections and actions; Speaker.bot is optional for speech.
4. Configure combined chat or an overlay, then test it with sample events.
5. Copy its viewing URL into an **OBS Browser Source**. Use the streamer chat page
   as a browser dock if you want a separate chat view for yourself.

Keep TDSBLive running while using its pages in OBS. Closing the editor tab does
not quit the application. Local HTTP works without HTTPS certificates.

Start with [First setup](docs/user-guide/First-Setup.md), then
[Chat and OBS](docs/user-guide/Chat-and-OBS.md) or
[Overlays and alerts](docs/user-guide/Overlays-and-Alerts.md).

## Open the editor and finish your stream

The **unreleased v1.0.1 candidate** adds a blue **T** icon near the Windows clock
or in the Linux desktop panel.
Starting it normally opens the editor in your usual browser. Optional start-at-login
stays quiet; use **Open editor** when you need it.

![Windows candidate tray menu: Open editor, Restart, Quit](docs/user-guide/images/tray-windows-menu.png)

1. Find the blue **T** near the clock. Click the hidden-icons arrow if it is tucked away.
2. Right-click it and choose **Open editor**, **Restart**, or **Quit**.
3. When you finish streaming, save changes in the editor, choose **Quit**, then
   confirm **Quit**. Your OBS overlays stop until you start TDSBLive again.

**Restart** and **Quit** first show a confirmation with **Cancel** selected.
Closing the browser or the **TDSBLive is running** control window keeps the app
running. If the icon is unavailable, that control window provides the same actions.
Tray actions do not save unfinished forms.

For screenshots and missing-icon help, read
[Tray and desktop controls](docs/user-guide/Tray-and-Desktop-Controls.md).
The current v0.1.0 download uses the editor's application controls; follow its
bundled guide until v1.0.1 is available.

On Linux, extract the complete Linux archive and open **TDSBLive**. Its first-run
window lets you choose Wine or Proton (UMU), your existing Windows settings folder
and your saved setup. Leave **Start a new empty TDSBLive setup** unchecked when
updating. The optional applications-menu shortcut does not enable automatic
sign-in startup. See the [Wine guide](docs/user-guide/Install-on-Linux-Wine.md)
or [UMU guide](docs/user-guide/Install-on-Linux-Proton.md) for each field.

## What you can do

- **Bring chat together:** display combined chat in OBS and use a separate streamer
  view, with platform indicators, supported badges and emotes.
- **Create overlays and alerts:** arrange text, images, chat and supporter widgets;
  customize alert wording, media, timing and playback queues.
- **Choose different alert designs:** the v1.0.1 source candidate provides named triggers,
  donation/quantity conditions and ordered first/all matching alert sets.
- **Customize and share designs:** the v1.0.1 source candidate adds advanced arrangement,
  sandboxed custom widgets and portable overlay/widget packages.
- **Connect Rumble events:** use the Rumble live API with persistent repeat-event
  protection and connection diagnostics.
- **Understand your support:** review contributions, linked supporter identities,
  leaderboards and stream totals, with reported, estimated and unknown values distinguished.
- **Automate your responses:** configure speech, overlay sounds and selected
  Streamer.bot actions, with simulations and execution history.
- **Protect your setup:** recover overlay revisions, export configuration and
  create or restore application backups.

The repository and wiki can include features newer than the latest packaged
release, including advanced editing, custom widgets, portable overlay packages
and guided conditional alert designs. **Release notes and the bundled guide
describe the version you downloaded.** See [version and feature guidance](docs/user-guide/Before-You-Begin.md)
before following instructions for a newer feature.

## Guides and updates

The [GitHub wiki](https://github.com/TechDaddyKB/tdsblive/wiki) and
[repository user guide](docs/user-guide/Home.md) provide illustrated instructions
for setup, chat, overlays, automation, supporter totals and recovery.
For offline reading, open **`guide/Home.html`** in your installed or extracted
application folder; keep the guide's images beside its pages.

Before updating, save your editor changes, make an application backup and quit
TDSBLive. Wait for it to stop before replacing application files. Run the new
installer, or extract the new ZIP into a separate folder. Check your connections
and OBS sources before your next broadcast. Your application data normally lives
under **`%LOCALAPPDATA%\TDSBLive`** on Windows, separately from the program files.

- [Install, update or remove](docs/user-guide/Install-and-Update.md)
- [Backup and recovery](docs/user-guide/Backup-and-Recovery.md)
- [Release history and version-specific notes](https://github.com/TechDaddyKB/tdsblive/releases)

## Help and privacy

Start with [Troubleshooting](docs/user-guide/Troubleshooting.md). If you need to
[report a problem](https://github.com/TechDaddyKB/tdsblive/issues), include your
TDSBLive version, operating system, installation method, what you expected and
the steps that reproduce the issue.

Keep credentials, private Rumble API URLs, access tokens and backups out of
public issues and screenshots. Optional authenticated LAN access is disabled
by default; follow the [LAN guide](docs/user-guide/LAN-Access.md) before enabling it.
HTTP does not encrypt network traffic.

## Development

Contributions are welcome. See [Contributing](CONTRIBUTING.md),
[Development](docs/development.md), [Architecture](docs/architecture.md) and
[Testing](docs/testing.md). Technical requirements and acceptance evidence are
maintained in the [implementation plan](docs/implementation-plan.md) and
[requirements matrix](docs/requirements-matrix.md).

<details>
<summary>Build and test from source</summary>

Use the exact SDK in `global.json`, Node in `.nvmrc`, npm in `package.json`, and
Python 3.14.7. The SonarQube CLI 1.9.0 is required by evidence tooling's mandatory
secrets-on-read safeguards. Windows CI installs its pinned binary automatically.

Scan files before reading them, as specified in [AGENTS.md](AGENTS.md). Then:

```bash
dotnet restore TDSBLive.slnx --locked-mode
npm ci
npm run build
dotnet build TDSBLive.slnx --configuration Release --no-restore
dotnet test TDSBLive.slnx --configuration Release --no-build --settings coverage.runsettings --collect:"XPlat Code Coverage" --logger trx --results-directory TestResults
npm run lint
npm run typecheck
npm run test:coverage
python tools/rumble_evidence.py verify
python -m unittest discover -s tests/replay -v
python tools/verify_coverage.py
```

Run the host with `dotnet run --project src/ExtensionSuite.Host` and open
`http://127.0.0.1:17474/editor`. Build frontend assets before the host so they are
copied to its output. Use `npm run dev --workspace @tdsblive/editor` for frontend
development. See the [foundation API guide](docs/foundation-api.md) for bootstrap
configuration, DPAPI credentials, authenticated LAN and generated contracts, and
the [security guide](docs/security.md) for permission and isolation boundaries.

</details>

## License

TDSBLive is free and open source under the [MIT License](LICENSE).

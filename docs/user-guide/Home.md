# TDSBLive: start here

TDSBLive brings stream chat, on-screen graphics and alerts together on your computer. This guide takes you from downloading the app to using it during a stream. You do not need to know programming to use the ordinary features.

**Already installed?** Start TDSBLive, then open [the editor](http://127.0.0.1:17474/editor) in your web browser. Keep the application running while OBS uses its pages. Closing the browser tab does not quit the application.

## Your first trip through the guide

Follow these steps in order. You can leave the optional features until your basic setup works.

1. Read [Before you begin](Before-You-Begin.md). It explains what each application does and which download contains which features.
2. Install for your computer: [Windows](Install-on-Windows.md), [Linux with Wine](Install-on-Linux-Wine.md), or [Linux with Proton](Install-on-Linux-Proton.md).
3. Follow [First setup](First-Setup.md) to connect the services you actually use.
4. Follow [Chat and OBS](Chat-and-OBS.md) to see your first working chat page.
5. Try [Overlays and alerts](Overlays-and-Alerts.md) when you want graphics or notifications on your stream.
6. Use [Everyday use](Everyday-Use.md) as your checklist before and after a stream.

**Something did not work?** Go to [Troubleshooting](Troubleshooting.md). If a word is unfamiliar, look in the [Glossary](Glossary.md).

## Choose what you want to do next

- [Install, update or remove TDSBLive](Install-and-Update.md).
- [Understand supporter totals](Supporter-Totals.md) and their dates, currencies and estimates.
- [Set up speech and sound](Automation.md), then test rules before enabling them.
- [Make a backup or restore one](Backup-and-Recovery.md).
- [Use another computer on your home network](LAN-Access.md), if you need to.
- [Arrange more complex overlays](Advanced-Editor-and-Widgets.md).
- [Use custom widgets and share packages](Custom-Widgets-and-Portable-Packages.md).
- [Try the limited local StreamElements compatibility option](Local-StreamElements-Compatibility.md).

## Read without the internet

The Windows application download includes a `guide` folder. Open `guide/Home.html` in your browser. Those pages and their pictures work without the internet. Links to downloads and other projects still require an internet connection. Keep the guide folder and its images together. The bundled guide belongs to the build you downloaded; the current wiki may include newer instructions. This reorganization will be included in future packages and does not replace the files inside the already published MVP download.

Screenshots in this guide show owned examples. Names, ports and example settings are illustrations; use the values in your own applications. Some pictures show an earlier interface, so use the accompanying instructions if your screen differs slightly.

## What has been tested, and what remains limited

The project has native Windows build, automated test and package checks. It also has live Linux/Wine checks, including local OBS rendering and an audible custom-widget test. These checks do not establish that every Linux distribution, Wine runner or Proton runner behaves the same way.

Linux compatibility testing has exceeded the project's CPU targets. The original MVP also exceeded its memory target in its Wine test. Later managed-host Linux qualification met the memory target, but did not meet the CPU targets; that was a separate workload, not a new Wine performance pass. Native Windows streaming-PC performance testing remains deferred. Check the load on your own streaming computer before relying on it for a show.

Actual paid Rumble Rant, Ko-fi donation and Twitch Bits delivery has not been verified. Local examples test behavior without requiring you to spend money. VTube Studio work is on hold; use Streamer.bot's built-in integration for that application.

## Optional project information

You do not need these technical documents for ordinary use.

- [Project repository](https://github.com/TechDaddyKB/tdsblive).
- [Implementation goals and current evidence](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/implementation-plan.md).
- [Developer architecture](https://github.com/TechDaddyKB/tdsblive/blob/main/docs/architecture.md).

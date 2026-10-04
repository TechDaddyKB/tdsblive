# Before you begin

Your goal for the first session is simple: start TDSBLive, open its editor, connect the chat service you use, and see a message. You do not have to set up speech, money tracking, custom code or network sharing on the first day.

## What each application does

**TDSBLive** combines incoming information and creates the web pages that show your chat and overlays. It runs in the background on your computer. Its editor is a page you open in a browser, rather than a separate desktop editing window.

**Streamer.bot** connects to supported streaming accounts and runs actions you configure. TDSBLive can connect to its WebSocket server. A WebSocket is simply a connection that stays open so new messages can arrive promptly.

**Speaker.bot** reads text aloud. It is optional. You can set up chat and graphics before setting up speech.

**OBS Studio** creates your broadcast. Its Browser Source displays a TDSBLive web page inside your video. A dock is a panel you read inside OBS; a dock does not automatically appear in the broadcast.

You can think of the arrangement as a short journey: your connected service receives a message, TDSBLive prepares the display, and OBS shows the display to viewers.

## Pick the right version before following feature instructions

The published release is currently **v0.1.0 MVP**. Download it from [GitHub Releases](https://github.com/TechDaddyKB/tdsblive/releases). It is the starting point for a normal installation, and has chat, basic overlays, totals, automation and recovery features.

The repository also contains newer completed work, identified as G11, G12 and G13. That work adds the advanced editor, custom widgets and portable packages, and limited local StreamElements compatibility. These additions are newer than the published MVP. They have not been issued as a new public release yet.

**A feature described here may require a newer build.** The advanced chapters say so at the top. If the button they describe is missing from the MVP, that does not mean you installed it incorrectly.

For a newer build, use only an artifact from this project's [GitHub Actions](https://github.com/TechDaddyKB/tdsblive/actions) attached to a successful, reviewed run for the source revision you intend to use. Downloading artifacts may require a GitHub account. A green build is a useful check, but a development artifact is not a newly published release. Keep your working version and backup while trying another build. Do not select a download from an unrelated fork or a failed run.

## Prepare a place for the application and a place for backups

For a ZIP installation, create an ordinary local folder such as `C:\Apps\TDSBLive` on Windows or `~/Applications/TDSBLive` on Linux. Avoid putting the running application in a cloud-synced folder. Extract the complete download there.

Make a separate backup folder. A second drive is useful if available. A backup beside the application helps with an accidental edit, but it will not help if that whole drive fails.

TDSBLive's normal application data is separate from its program files. On Windows it is under `%LOCALAPPDATA%\TDSBLive`. On Linux it is normally inside the Wine or Proton prefix. Moving the program folder alone does not move all your saved data.

## Things to have ready

- A 64-bit Windows installation, or a Linux system with a suitable Windows compatibility setup.
- A web browser. The local editor does not require an online browser account.
- OBS if you want to show pages on stream.
- Streamer.bot if you want its supported chat connections or actions.
- Speaker.bot only if you want speech through that service.
- The accounts you intend to connect, and their official sign-in instructions.

TDSBLive's Windows package includes its .NET runtime. You do not need to install .NET separately just to launch that package. Other programs, including Streamer.bot and Speaker.bot under Wine, have their own requirements.

## Keep private information private

A password, access token or private Rumble API URL can let someone use a connection as you. Do not paste it into screenshots, a public issue, a widget package or chat. The private Rumble URL is not your broadcast stream key.

Local pages use **HTTP**. You do not need an HTTPS certificate for normal use on the same computer. Keep access local while learning. Network sharing is a separate, optional setup.

Next: [Choose an installation method](Install-and-Update.md).

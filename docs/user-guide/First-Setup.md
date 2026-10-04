# First setup: connect only what you need

By the end of this page, the editor should show the connections you use, and you should know how to check them. You can skip an unused service and return later.

## 1. Start the application and open guided setup

Start one copy of TDSBLive. Open [the local editor](http://127.0.0.1:17474/editor). Choose **Open guided setup** if it is closed, then use **Next setup step** to work through it in order. The guide remembers your progress, but a completed step is not proof that a disconnected service is working today.

![Guided setup with example settings](images/guided-setup.png)

This picture shows the setup area. Read the descriptions before entering values. Your own bot ports or service details may differ from the examples.

## 2. Prepare Streamer.bot if you use it

Install and start Streamer.bot using [its official instructions](https://docs.streamer.bot/get-started/installation). Extract its entire ZIP to a local folder rather than running it in an archive. Connect your streaming accounts in Streamer.bot using its own account setup instructions.

On Linux, use [Streamer.bot's experimental Linux guide](https://docs.streamer.bot/get-started/installation/linux) first. Its requirements are different from TDSBLive's. The project's integration qualification used Streamer.bot 1.0.7; do not assume every future bot version has already been checked.

In Streamer.bot, open **Servers / Clients** and its **WebSocket Server** settings. Enable and start the server. Write down its address, port and whether authentication is enabled. A common local port is `8080`, but use the port your bot actually shows.

A server is a program waiting for a connection. Starting Streamer.bot alone is not enough if its WebSocket server is stopped.

## 3. Enter the matching connection in TDSBLive

1. In guided setup, go to the bot connections step.
2. Enter `127.0.0.1` in **Streamer.bot host** for a bot on the same computer, and enter its actual server port in **Streamer.bot port**. The host field takes an address, not a whole `ws://` URL.
3. If the bot requires authentication, enter the corresponding password or secret.
4. Choose session-only storage while you are learning. It means the secret is kept for this run and must be entered again after restarting.
5. Choose **Save Streamer.bot connection**. Use **Restart TDSBLive** under **Backup and recovery**, then reopen guided setup and enter session-only secrets again if needed.
6. Choose **Test Streamer.bot connection** and read its result.

Tests use the active saved connection. Typing a new value without saving it does not test that value. A successful connection test establishes that the service can be reached; it does not prove all accounts, events or actions are configured.

If a test fails, check that the bot server is started, the ports match, and both sides agree about authentication. Follow [Troubleshooting](Troubleshooting.md) before changing unrelated settings.

## 4. Add Speaker.bot only if you want speech

Start Speaker.bot and follow [its official installation and setup](https://speaker.bot/get-started/installation). Set up a voice that works there before asking TDSBLive to use it.

Enable its WebSocket server. A common port is `7680`; use the actual port shown in your installation. In guided setup, enter **Speaker.bot host** and **Speaker.bot port**, choose **Save Speaker.bot connection**, restart when requested, then choose **Test Speaker.bot connection**.

A working Speaker.bot connection does not guarantee a particular voice alias exists. Copy the alias from your own Speaker.bot configuration when setting up [Automation](Automation.md). If you are only setting up chat today, leave speech for later.

## 5. Import the bundled Streamer.bot actions if needed

The TDSBLive program folder includes `integrations/tdsblive-streamerbot.sb`. Use Streamer.bot's import function to import that file. Review the imported actions before using them.

Run the bundled bootstrap action once to initialize its integration and register its intended triggers. Refresh action discovery in TDSBLive afterward. The qualification probe is for checking the integration; it is not a reason to run every available action.

Grant TDSBLive permission only for actions you intend it to call. Saving the permission list does not execute an action. Do not attach the explicit event forwarder to every chat event if that chat already arrives through WebSocket events; doing both can create duplicate messages.

Rumble trigger forwarding needs the relevant bot action permission and the matching TDSBLive Rumble forwarding setting. Save and restart as instructed. Test with an isolated example before using it in production.

## 6. Connect Rumble only if you use it

Enter your private Rumble API URL in the Rumble settings. This is a connection credential, not your streaming key. Keep it out of screenshots and public support messages.

Choose session-only storage initially, connect, and wait for **Baseline established**. The baseline lets TDSBLive recognize information already present when you connect, so old data is not treated as a new alert.

Use a new test message after the connection is ready. An old message may be intentionally suppressed. Subscriber and gift behavior remains gated where it is unverified; do not assume every Rumble event is supported merely because chat connects.

## 7. Set dates and money display deliberately

Choose the financial time zone. **Use browser time zone** is convenient if the browser and your intended reporting time zone match. Weeks start on Monday. **Use current time** for the stream start sets the boundary for the stream period; do this deliberately when starting a new session.

A displayed supporter amount can be an exact amount, an estimate or an unknown value. It is a record of received events, not a bank balance or a payout statement. [Supporter totals](Supporter-Totals.md) explains the labels.

## 8. Check before moving on

The connections you use should be healthy. Unused services can remain disabled. Do not enable automation just to make all indicators green.

On Windows, persistent credentials use Windows credential protection. Compatibility environments can behave differently. If persistent storage is unavailable, use session-only credentials and enter them again after restarting.

Next: [Chat and OBS](Chat-and-OBS.md), where you will check an actual message and display it.

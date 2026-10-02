# Access from another computer

LAN access is optional. If everything runs on your streaming computer, leave it
off and use `http://127.0.0.1:17474/editor`.

> Release preparation: the new editor controls are still awaiting complete
> Windows/browser qualification. These instructions describe the current build.

## Enable authenticated access

1. Open the editor **on the streaming computer** running the Windows TDSBLive
   application. Find **Access from another computer**.
2. Choose **Create or rotate LAN access credential**. This creates the password
   used by remote editors. It also signs out existing remote sessions.
3. Copy the new credential and keep it private. **Hide credential** removes it
   from this page; it does not revoke the credential.
4. Check **Enable authenticated LAN access**.
5. In **Streaming computer addresses**, enter the IP address or hostname of
   your streaming computer, one per line. Enter an address such as
   `192.168.1.50`, not a full URL, port number or `*`.
6. Keep port **17474** unless you need another port, then click **Save access
   settings**.
7. Restart TDSBLive. On the other computer, open
   `http://YOUR-STREAMING-PC-ADDRESS:17474/editor`, replacing the address and port
   with the ones you chose. Sign in with your access credential.

Windows credential protection is required for LAN provisioning. The native Linux
host does not substitute plaintext storage. A Windows application tested in
Wine/Proton and a native Windows installation are separate qualification paths.

The streaming computer's firewall may need to allow TDSBLive on your private
network. This setup is for your LAN; do not add public router forwarding as part
of ordinary setup. HTTP is supported and HTTPS is not required. HTTP traffic is
unencrypted, so use a network you trust and keep access credentials private.

## Remote OBS or chat viewing

An OBS Browser Source on another computer cannot use `127.0.0.1` to reach your
streaming computer. Use its network address instead.

In **Combined Chat**, choose **Create private LAN viewing links** for read-only
chat links. Keep the full link, including its `#token=...` ending, when pasting
it into the viewing browser or source. Replace the host with an allowed address
of the streaming computer if the link was generated in a local editor.

Viewing links expire after 30 days. **Hide private links** hides them on the
editor page; it does not invalidate copies. Use **Manage viewing links → Revoke
viewing link** to invalidate a link. Backup restore revokes overlay viewing
tokens, so create new ones afterward when needed.

Remote editor sign-in and read-only viewing links are different permissions.
Backup, restore, configuration import/export, restart and quit remain available
only from the computer running the host, even for a signed-in remote editor.

## Turn LAN off

On the streaming computer, uncheck **Enable authenticated LAN access**, save and
restart. The application returns to local loopback binding. Update OBS URLs if
you changed the port. Rotate the access credential if a private credential was
shared accidentally.

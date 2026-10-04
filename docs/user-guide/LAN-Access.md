# Optional: use another computer on your home network

Skip this page if TDSBLive, your browser and OBS all run on the same computer. Local use is simpler and needs no HTTPS certificate.

A LAN is your local network, such as computers connected to the same home router. This page is about that trusted network, not opening TDSBLive to the public internet.

## 1. Understand addresses first

`127.0.0.1` always means the computer where the browser is running. A laptop opening that address looks for TDSBLive on the laptop, even if TDSBLive is actually on your desktop.

For another computer, you need the desktop's network address and the access mode TDSBLive provides. Copy the application's generated links rather than inventing an address or token.

## 2. Check whether your environment supports authenticated LAN access

Authenticated LAN configuration requires the application's supported persistent credential protection. Native Windows uses Windows protection. A Linux compatibility environment may not provide the required behavior.

If the application cannot establish the protected credential setup, keep access local. Do not bypass it with a plain-text password file or an unauthenticated public listener. This guide does not claim Wine or Proton LAN credentials work on every runner.

## 3. Configure only the access you need

Use the LAN controls to choose the intended listening address and authentication settings. An address field asking for an IP address is not asking for a whole `http://` URL. Listen on the specific local-network interface when possible; a wildcard address listens on more interfaces.

Save and restart when requested. If Windows Firewall asks, allow only the network profile and application access you actually need. Prefer your trusted private network. Do not add a router port-forward for this local setup.

Local HTTP is supported. Across a network, HTTP traffic is not encrypted, so use it only within the trusted-network scope you intended. A certificate is not required for ordinary same-computer operation.

## 4. Use a viewing link when someone only needs to view

Viewing links are different from giving someone editor access. Use the viewing-link controls for a limited viewing purpose. These links include a secret token and expire after 30 days.

Keep the whole generated link private. The token after `#` is part of the link; dropping it can break access. Hiding a link from your screen does not revoke it. Use the revoke control when you want to end access.

After a restore, earlier viewing tokens are revoked. Generate a new link rather than repeatedly trying an old one.

## 5. Test before depending on it

First verify the same page works locally. Then test the copied link on the second computer, check the intended access level, and confirm the actual page in OBS if you use it there.

If local access works but remote access fails, check the host computer's network address, listening configuration, authentication and firewall. Do not disable the firewall entirely as a first fix.

Next: [Troubleshooting](Troubleshooting.md), or return to [Everyday use](Everyday-Use.md).

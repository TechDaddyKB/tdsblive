# Known issues in 1.0.1

These limitations were accepted for the 1.0.1 release on 2026-10-05. They remain
open for future investigation. They are not reasons to delete your setup.

## Linux memory and CPU use

The native Linux tray adds memory use to the Windows app running through Wine or
Proton. A paired owned UMU example measured about **122 MiB additional proportional
memory** and **0.40 percentage points of one CPU core**. This comparison counts
the application processes; it excludes the browser, desktop and runner helpers.
It is an example, not a guaranteed maximum.

Longer numeric observation showed managed heap growth, then a heap reduction
between observations and nearly flat resident memory during the next ten minutes.
Three subsequent unmonitored 30-second samples were also nearly flat. This does
not prove that every workload is free of leaks. No heap dumps or private control
data were collected. The release retains this investigation for a future fix.

Before a stream, check the load on your own computer. Wine qualification has
exceeded earlier CPU targets; native Windows streaming-PC performance remains
unqualified. See the [recorded evidence](tray-release-plan.md).

## KDE panel disappearance can hide the fallback controls

KDE can continue reporting that its tray exists after only the panel has closed.
TDSBLive then cannot reliably detect the missing panel and may not display its
fallback control window. Full tray-service disappearance and restoration have
separate successful checks.

If the panel vanishes, use the editor's **Backup and recovery** controls to
restart or quit. Restore the desktop panel normally; do not kill unrelated apps
or remove TDSBLive's data. See [tray help](user-guide/Tray-and-Desktop-Controls.md).

## Compatibility and verification boundaries

- Linux uses a native launcher and the Windows backend through installed Wine
  or UMU/Proton. A fully native Linux backend is deferred. Bottles remains a
  separate manual workflow; the native launcher is not qualified with Bottles.
- Representative KDE X11/Wayland and Hyprland tray checks use isolated owned
  desktops. They do not prove support for every distribution, theme or runner.
- Actual paid Rumble Rant, Ko-fi donation and Twitch Bits delivery remains
  unverified. Synthetic tests do not claim observed human usability, physical
  touch, screen-reader speech or hearing. Native Windows OBS and streaming-PC
  performance retain the existing qualification limits.
- VTube Studio-specific work remains deferred. Use Streamer.bot's integration.

The off-screen Linux setup problem at high scaling was repaired before this
release. Actual Cancel-only checks at 125%, 200% and 400% fit the window inside
the owned 1280×900 output in both light and dark themes. That is a fixed bug,
not an accepted open issue.

Security scan findings in two owned Steam runtime copies of Python's
`urllib/request.py` were public authentication examples in the official CPython
3.13 module docstring. A bounded comparison verified them as false positives;
no credentials were printed, no vendor files were changed and no scanner
exclusion was added. New or changed findings must still be investigated.

# Platform logo artwork

Donor badges render bundled vector logos for Twitch, YouTube, Kick, Rumble, Ko-fi, Streamlabs and Patreon. No CDN request is needed. The vector paths come from [Simple Icons commit 1089fb7d2bf0e323f834c205ab76265005a6d5e8](https://github.com/simple-icons/simple-icons/tree/1089fb7d2bf0e323f834c205ab76265005a6d5e8/icons), under its [CC0-1.0 dedication](licenses/SimpleIcons-CC0.md). Source filenames match each normalized platform key in `frontend/overlay-runtime/src/platform-logos.ts`. Logos identify platforms; their inclusion does not establish integration or payload support.

The icons inherit the widget’s configured color and scale with its font size. Full platform names remain accessible and available on hover, without duplicate visible labels. Unrecognized platforms receive a generic information badge with their accessible name; no invented brand logo is used.

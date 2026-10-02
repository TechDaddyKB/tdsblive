# Widgets

A widget is one item on an overlay, such as chat, an image or a supporter total. Start with [Overlays and alerts](user-guide/Overlays-and-Alerts.md). For money displays and linked supporters, see [Supporter totals](user-guide/Supporter-Totals.md).

| Implemented kind | Purpose |
| --- | --- |
| Text, image, video, audio | Local canvas content with supported appearance/playback controls |
| Chat | Filtered combined chat, including supported native/provider emotes and Twitch GIF media |
| AlertBox | Event-driven text/media/sound with bounded group queues |
| Donor Crown | Highest eligible supporter in the selected period |
| Ranked leaderboard | Up to 25 eligible supporters |
| Latest supporter | Most recent eligible contribution |
| Current-stream leader/total | Shared configured stream period across platforms |

Exact, converted, nominal and unknown financial values remain distinct. Donor filters, linked identities, valuation reconciliation and empty states are described in [donor contracts](g08-donor-widgets.md). Unknown/gated contributions must not be displayed as proven money.

Canvas controls, assets and revision behavior are maintained in [editor contracts](g06-editor-alerts.md). Additional built-ins and advanced controls are G11 work. Custom HTML/CSS/JavaScript widgets, Monaco, manifests, sandbox permissions, mediated `SBX` APIs and package portability belong to G12; the MVP does not claim those capabilities.

# Rumble snapshots and ingestion

For connection instructions, see [First setup](user-guide/First-Setup.md#6-connect-rumble-only-if-you-use-it). The private Live API URL is a credential. Enter it locally; do not place it in Git, screenshots, logs or public support requests. Session-only storage is the default and clears on restart. Persistent Windows storage uses DPAPI.

Rumble provides snapshots rather than a durable event cursor. The same recent messages and Rants can appear in several polls. Accepting each row as a new event would duplicate chat, alerts and supporter totals. Conversely, a bounded recent window can discard activity between polls; no snapshot algorithm can recover records that were never observed.

The first trustworthy snapshot establishes a baseline and suppresses its historical contents. Subsequent snapshots compare persistent fingerprint multisets, preserving multiplicity when identical-looking messages occur more than once. Event acceptance, dedupe and polling state commit transactionally in SQLite. Financial uniqueness survives restart and baseline reset. Repeated snapshots are not treated as repeated donations.

Polls normally occur every seven seconds plus positive jitter, with configurable five-to-ten-second intervals. Failures produce health/backoff diagnostics; a failed or malformed poll does not prove a stream went offline. Capacity and observation gaps are reported honestly. Rant amounts use authoritative integer cents rather than contradictory display-dollar fields.

Full observed shapes, detection rules, ambiguity, recovery and replay contracts are maintained in [Rumble ingestion](g04-rumble-ingestion.md) and [sanitized evidence analysis](rumble-analysis.md). Subscription/gift behavior without authoritative evidence remains gated. [G10 qualification](g10-qualification.md) distinguishes actual API health from owned paid-event examples; actual paid-platform delivery remains unverified.

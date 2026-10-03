# Database and recovery

TDSBLive stores application state in `tdsblive.db` under its separate data directory, normally `%LOCALAPPDATA%\TDSBLive`. EF Core applies versioned SQLite migrations. SQLite uses WAL and foreign keys; database/schema changes require migrations rather than ad hoc file edits.

| Stored state | Integrity boundary |
| --- | --- |
| Canonical events, Rumble fingerprints/checkpoints, outbox | Transactional acceptance and persistent dedupe |
| Supporters, identities, financial contributions, FX/valuation rules | Idempotent ingestion and exact integer/decimal arithmetic |
| Overlays, revisions, asset metadata | Versioned layouts and validated content-addressed assets |
| Rules, automation inbox, executions and temporary effects | Durable planning, explicit dispatch outcomes and restart recovery |
| Non-secret configuration, setup progress and redacted logs | Validated/versioned settings and bounded logging |

Credential files are separate and Windows-DPAPI protected. They are not ordinary configuration or public fixtures. Do not publish a live database: it can contain private chat, supporter records and application history even when secrets are redacted.

Use [Backup and recovery](user-guide/Backup-and-Recovery.md), not a casual copy of a database with uncheckpointed WAL. The application creates a consistent private archive with assets and integrity metadata. Validation checks paths, schema compatibility and integrity before explicit restore confirmation. Restore shuts down consumers, retains a safety copy, pauses integrations/rules and revokes old overlay tokens. Existing encrypted local credentials stay local.

REST/process contracts are in [foundation API](foundation-api.md); financial precision, periods, identity links and reconciliation are in [financial ledger contracts](g07-financial-ledger.md). Full backup/restore and secret-free connection-settings export/import are implemented. [Portable packages](g12-custom-widgets.md) carry layouts/source/assets with fresh identities and disabled custom capabilities; they deliberately exclude private persistent widget state. Full backups retain that state, including local compatibility store keys. [Developer utilities](g13-compatibility.md) provide isolated replay, private backup and aggregate diagnostics.

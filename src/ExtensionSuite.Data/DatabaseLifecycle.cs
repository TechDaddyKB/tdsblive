using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public static class DatabaseLifecycle
{
    public static async Task InitializeAsync(FoundationDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public static async Task CheckpointAsync(FoundationDbContext db, CancellationToken cancellationToken = default) =>
        await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);
}

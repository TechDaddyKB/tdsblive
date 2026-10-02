using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ExtensionSuite.Host;

public sealed record SetupProgress(int Step = 0, bool Reviewed = false, int Version = 0);

public static class SetupEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapSetupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/setup", (ApplicationPaths paths) =>
        {
            using var db = Open(paths);
            return TypedResults.Ok(Read(db));
        });
        app.MapPut("/api/setup", (SetupProgress progress, ApplicationPaths paths) =>
        {
            if (progress.Step is < 0 or > 5 || progress.Version < 0 || progress.Version == int.MaxValue || progress.Reviewed && progress.Step != 5)
                return Results.BadRequest(new { error = "Invalid setup progress." });
            using var db = Open(paths);
            using var transaction = db.BeginTransaction();
            var current = Read(db, transaction);
            if (current.Version != progress.Version) return Results.Conflict(new { error = "Setup progress changed. Reload the guide." });
            var saved = progress with { Version = progress.Version + 1 };
            using var command = db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Configurations(Name, Json) VALUES('setup-progress', $json) ON CONFLICT(Name) DO UPDATE SET Json=$json";
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(saved, Json));
            command.ExecuteNonQuery();
            transaction.Commit();
            return Results.Ok(saved);
        }).Produces<SetupProgress>();
    }

    private static SqliteConnection Open(ApplicationPaths paths)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.Database, ForeignKeys = true }.ToString());
        db.Open();
        return db;
    }

    private static SetupProgress Read(SqliteConnection db, SqliteTransaction? transaction = null)
    {
        using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Json FROM Configurations WHERE Name='setup-progress'";
        return command.ExecuteScalar() is string json
            ? JsonSerializer.Deserialize<SetupProgress>(json, Json) ?? new() : new();
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ExtensionSuite.Data;

public sealed record FinancialSettings(string TimeZone = "UTC", DateTimeOffset? CurrentStreamStartUtc = null, int Version = 0)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TimeZone) || TimeZone.Length > 128 || Version < 0 || Version == int.MaxValue ||
            CurrentStreamStartUtc is { Offset: var offset } && offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid financial settings.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZone); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("Unknown financial timezone."); }
    }
}

public sealed class FinancialSettingsStore(IDbContextFactory<FoundationDbContext> factory)
{
    private const string Name = "financial";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<FinancialSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var json = await db.Configurations.AsNoTracking().Where(row => row.Name == Name).Select(row => row.Json).SingleOrDefaultAsync(cancellationToken);
        var settings = json is null ? new FinancialSettings() : JsonSerializer.Deserialize<FinancialSettings>(json, Options)
            ?? throw new InvalidOperationException("Financial settings are unavailable.");
        settings.Validate(); return settings;
    }

    public async Task<FinancialSettings?> SaveAsync(FinancialSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = await db.Configurations.AsNoTracking().SingleOrDefaultAsync(row => row.Name == Name, cancellationToken);
        var current = row is null ? new FinancialSettings() : JsonSerializer.Deserialize<FinancialSettings>(row.Json, Options)!;
        if (current.Version != settings.Version) return null;
        var saved = settings with { Version = checked(settings.Version + 1) };
        var json = JsonSerializer.Serialize(saved, Options);
        if (row is null) db.Configurations.Add(new StoredConfiguration { Name = Name, Json = json });
        else if (await db.Configurations.Where(item => item.Name == Name && item.Json == row.Json)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Json, json), cancellationToken) != 1) return null;
        db.FinancialAudits.Add(new() { Id = Guid.CreateVersion7(), CreatedAtTicks = DateTimeOffset.UtcNow.UtcTicks,
            Operation = "financial_settings", BeforeJson = JsonSerializer.Serialize(current, Options), AfterJson = json });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return saved;
    }
}

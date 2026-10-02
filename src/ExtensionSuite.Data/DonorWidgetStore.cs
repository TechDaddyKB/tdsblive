using System.Globalization;
using System.Numerics;
using System.Text.Json;
using ExtensionSuite.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace ExtensionSuite.Data;

public sealed class DonorWidgetStore(IDbContextFactory<FoundationDbContext> factory)
{
    public async Task<DonorWidgetSnapshot> SnapshotAsync(OverlayWidget widget, LedgerPeriodRange period,
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        widget.Validate(); period.Validate();
        var settings = widget.Donor;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        connection.CreateAggregate("donor_exact_sum", BigInteger.Zero,
            (BigInteger sum, long? amount) => sum + (amount ?? 0), sum => sum.ToString(CultureInfo.InvariantCulture), isDeterministic: true);
        await using var command = connection.CreateCommand();
        var conditions = new List<string> { "f.AccountingState <> 'excluded'" };
        if (period.StartInclusive is { } start) { conditions.Add("f.OccurredAtTicks >= $start"); command.Parameters.AddWithValue("$start", start.UtcTicks); }
        if (period.EndExclusive is { } end) { conditions.Add("f.OccurredAtTicks < $end"); command.Parameters.AddWithValue("$end", end.UtcTicks); }
        AddFilter("f.Platform", "$platform", settings.Platforms);
        AddFilter("f.Type", "$type", settings.EventTypes);
        command.CommandText = $"""
            SELECT f.SupporterId, s.Name, f.Platform,
              donor_exact_sum(CASE WHEN f.AccountingState = 'counted' THEN f.UsdAmountMinor END),
              SUM(CASE WHEN f.AccountingState = 'counted' AND f.UsdAmountMinor IS NULL THEN 1 ELSE 0 END),
              SUM(CASE WHEN f.AccountingState <> 'counted' THEN 1 ELSE 0 END),
              SUM(CASE WHEN f.AccountingState = 'counted' AND f.UsdAmountMinor IS NOT NULL AND f.Estimated = 1 THEN 1 ELSE 0 END),
              SUM(CASE WHEN f.AccountingState = 'counted' AND f.UsdAmountMinor IS NOT NULL THEN 1 ELSE 0 END),
              MAX(CASE WHEN f.AccountingState = 'counted' THEN f.OccurredAtTicks END)
            FROM FinancialEvents f JOIN Supporters s ON s.Id = f.SupporterId
            WHERE {string.Join(" AND ", conditions)}
            GROUP BY f.SupporterId, s.Name, f.Platform
            """;
        var totals = new Dictionary<Guid, Accumulator>();
        long unknown = 0, gated = 0, estimated = 0;
        // SQL groups source rows using an exact integer aggregate; application memory holds aggregates only.
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            unknown += reader.GetInt64(4); gated += reader.GetInt64(5); estimated += reader.GetInt64(6);
            if (reader.IsDBNull(8)) continue;
            var id = reader.GetGuid(0);
            if (!totals.TryGetValue(id, out var value)) totals.Add(id, value = new(id, reader.GetString(1)));
            value.Platforms.Add(reader.GetString(2)); value.Latest = Math.Max(value.Latest, reader.GetInt64(8));
            var amount = BigInteger.Parse(reader.GetString(3), CultureInfo.InvariantCulture);
            value.Amount += amount;
            value.HasKnownAmount |= reader.GetInt64(7) > 0;
            value.Unknown += reader.GetInt64(4); value.Estimated += reader.GetInt64(6);
        }
        await reader.CloseAsync();
        var minimum = BigInteger.Parse(settings.MinimumUsdMinor, CultureInfo.InvariantCulture);
        var eligible = totals.Values.Where(row => (row.HasKnownAmount || widget.Kind == "latest-supporter" && minimum == 0) && row.Amount >= minimum);
        var total = eligible.Aggregate(BigInteger.Zero, (sum, row) => sum + row.Amount);
        var ordered = widget.Kind == "latest-supporter"
            ? eligible.OrderByDescending(row => row.Latest).ThenBy(row => row.Id)
            : eligible.OrderByDescending(row => row.Amount).ThenBy(row => row.Id);
        var count = widget.Kind == "donor-leaderboard" ? settings.Count : 1;
        var winners = ordered.Take(count).ToArray();
        foreach (var winner in winners)
        {
            var metadataJson = await db.FinancialEvents.AsNoTracking().Where(row => row.SupporterId == winner.Id &&
                row.AccountingState == "counted" && row.OccurredAtTicks == winner.Latest)
                .OrderBy(row => row.Id).Select(row => row.MetadataJson).FirstOrDefaultAsync(cancellationToken);
            try
            {
                using var metadata = JsonDocument.Parse(metadataJson ?? "{}");
                if (metadata.RootElement.ValueKind == JsonValueKind.Object && metadata.RootElement.TryGetProperty("userAvatarUrl", out var avatar) && avatar.ValueKind == JsonValueKind.String)
                    winner.Avatar = FinancialStore.SafeProfileImage(avatar.GetString());
            }
            catch (JsonException) { /* Malformed legacy metadata must not prevent aggregate rendering. */ }
        }
        var rows = winners.Select(row => new DonorSupporterRow(row.Id, row.Name,
            row.Amount.ToString(CultureInfo.InvariantCulture), row.Platforms.Order(StringComparer.Ordinal).ToArray(),
            row.Unknown, row.Estimated, new DateTimeOffset(row.Latest, TimeSpan.Zero), row.HasKnownAmount, row.Avatar)).ToArray();
        var state = rows.Length > 0 ? "ready" : unknown > 0 ? "pending" : gated > 0 ? "gated" : "empty";
        return new(widget.Id, state, now, rows, total.ToString(CultureInfo.InvariantCulture), unknown, gated, estimated);

        void AddFilter(string column, string prefix, string[] values)
        {
            if (values.Length == 0) return;
            var names = values.Select((value, index) =>
            {
                var name = prefix + index.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(name, value); return name;
            });
            conditions.Add($"{column} IN ({string.Join(",", names)})");
        }
    }

    private sealed class Accumulator(Guid id, string name)
    {
        public Guid Id { get; } = id;
        public string Name { get; } = name;
        public BigInteger Amount { get; set; }
        public bool HasKnownAmount { get; set; }
        public long Unknown { get; set; }
        public long Estimated { get; set; }
        public long Latest { get; set; }
        public string? Avatar { get; set; }
        public HashSet<string> Platforms { get; } = new(StringComparer.Ordinal);
    }
}

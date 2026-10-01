using System.Globalization;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Finance;
using Microsoft.AspNetCore.Mvc;

namespace ExtensionSuite.Host;

public sealed record IdentityLinkRequest(Guid ExpectedSupporterId, Guid TargetSupporterId);
public sealed record IdentityUnlinkRequest(Guid ExpectedSupporterId);
public sealed record IdentityTransferResponse(Guid SupporterId);
public sealed record NominalRuleUpdate(string Platform, string Type, string Tier, string UsdMinorPerUnit, int ExpectedVersion);
public sealed record NominalRuleRemoval(string Platform, string Type, string Tier, int ExpectedVersion);
public sealed record RateRequest(string Currency, DateOnly Date);
public sealed record ManualRateUpdate(string Currency, DateOnly Date, string UsdPerNativeUnit);
public sealed record FinancialReconcileRequest(FinancialVersionRef[] Selected);
public sealed record FinancialTotalsResponse(string Period, string TimeZone, LedgerPeriodRange Range, FinancialTotals[] Supporters);
public sealed record RateLookupResponse(bool Available, CachedRateEntry? Rate);
public sealed record RateRefreshResponse(bool Refreshed);

public static class FinancialEndpoints
{
    public static void MapFinancialEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/financial");
        group.MapGet("/settings", async (FinancialSettingsStore store, CancellationToken ct) => TypedResults.Ok(await store.GetAsync(ct)));
        group.MapPut("/settings", async (FinancialSettings settings, FinancialSettingsStore store, CancellationToken ct) =>
        {
            try { var result = await store.SaveAsync(settings, ct); return result is null ? Results.Conflict() : Results.Ok(result); }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<FinancialSettings>();
        group.MapGet("/ledger", async (FinancialReadStore store, int? offset, int? limit, string? state, string? platform, CancellationToken ct) =>
        {
            try { return Results.Ok(await store.EntriesAsync(offset ?? 0, limit ?? 100, state ?? "all", platform, ct)); }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<FinancialLedgerPage>();
        group.MapGet("/totals", async (FinancialSettingsStore settings, FinancialStore ledger, TimeProvider clock,
            string? period, DateOnly? start, DateOnly? endExclusive, int? limit, CancellationToken ct) =>
        {
            try
            {
                var configuration = await settings.GetAsync(ct);
                var range = LedgerPeriods.Resolve(period ?? "all-time", configuration.TimeZone, clock.GetUtcNow(), start, endExclusive, configuration.CurrentStreamStartUtc);
                return Results.Ok(new FinancialTotalsResponse(period ?? "all-time", configuration.TimeZone, range, await ledger.TotalsAsync(range, limit ?? 1000, ct)));
            }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<FinancialTotalsResponse>();
        group.MapGet("/identities", async (FinancialReadStore store, string? search, int? limit, CancellationToken ct) =>
        {
            try { return Results.Ok(await store.IdentitiesAsync(search, limit ?? 500, ct)); }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<FinancialIdentity[]>();
        group.MapPost("/identities/{id:guid}/link", async (Guid id, IdentityLinkRequest request, FinancialStore store, CancellationToken ct) =>
        {
            try
            {
                var target = await store.TransferIdentityAsync(id, request.ExpectedSupporterId, request.TargetSupporterId, ct);
                return target is { } supporterId ? Results.Ok(new IdentityTransferResponse(supporterId)) : Results.Conflict();
            }
            catch (ArgumentException) { return Results.BadRequest(); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).Produces<IdentityTransferResponse>();
        group.MapPost("/identities/{id:guid}/unlink", async (Guid id, IdentityUnlinkRequest request, FinancialStore store, CancellationToken ct) =>
        {
            try
            {
                var target = await store.TransferIdentityAsync(id, request.ExpectedSupporterId, null, ct);
                return target is { } supporterId ? Results.Ok(new IdentityTransferResponse(supporterId)) : Results.Conflict();
            }
            catch (ArgumentException) { return Results.BadRequest(); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).Produces<IdentityTransferResponse>();
        group.MapGet("/rules", async (ValuationRuleStore store, CancellationToken ct) => TypedResults.Ok(await store.ListAsync(ct)));
        group.MapPut("/rules", async (NominalRuleUpdate request, ValuationRuleStore store, CancellationToken ct) =>
        {
            if (!TryDecimal(request.UsdMinorPerUnit, out var value)) return Results.BadRequest();
            try { return await store.SetAsync(request.Platform, request.Type, request.Tier, value, request.ExpectedVersion, ct) ? Results.NoContent() : Results.Conflict(); }
            catch (Exception error) when (error is ArgumentException or OverflowException) { return Results.BadRequest(); }
        });
        group.MapDelete("/rules", async ([FromBody] NominalRuleRemoval request, ValuationRuleStore store, CancellationToken ct) =>
        {
            try { return await store.RemoveAsync(request.Platform, request.Type, request.Tier, request.ExpectedVersion, ct) ? Results.NoContent() : Results.Conflict(); }
            catch (ArgumentException) { return Results.BadRequest(); }
        });
        group.MapGet("/rates", async (CachedCurrencyRates store, int? limit, CancellationToken ct) =>
        {
            try { return Results.Ok(await store.ListAsync(limit ?? 500, ct)); }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<CachedRateEntry[]>();
        group.MapPost("/rates/lookup", async (RateRequest request, CachedCurrencyRates store, CancellationToken ct) =>
        {
            try
            {
                var rate = await store.GetRateAsync(request.Currency, request.Date, ct);
                var entry = rate is null ? null : new CachedRateEntry(rate.Currency, rate.RequestedDate, rate.RateDate,
                    rate.Provider == "manual" ? "manual" : "resolved", rate.UsdPerNativeUnit.ToString(CultureInfo.InvariantCulture), rate.Provider, rate.Estimated);
                return Results.Ok(new RateLookupResponse(entry is not null, entry));
            }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<RateLookupResponse>();
        group.MapPut("/rates/override", async (ManualRateUpdate request, CachedCurrencyRates store, CancellationToken ct) =>
        {
            if (!TryDecimal(request.UsdPerNativeUnit, out var value)) return Results.BadRequest();
            try { await store.SetManualAsync(request.Currency, request.Date, value, ct); return Results.NoContent(); }
            catch (ArgumentException) { return Results.BadRequest(); }
        });
        group.MapDelete("/rates/override", async ([FromBody] RateRequest request, CachedCurrencyRates store, CancellationToken ct) =>
        {
            try { return await store.RemoveManualAsync(request.Currency, request.Date, ct) ? Results.NoContent() : Results.NotFound(); }
            catch (ArgumentException) { return Results.BadRequest(); }
        });
        group.MapPost("/rates/refresh", async (RateRequest request, CachedCurrencyRates store, CancellationToken ct) =>
        {
            try { return Results.Ok(new RateRefreshResponse(await store.RefreshAsync(request.Currency, request.Date, ct))); }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<RateRefreshResponse>();
        group.MapPost("/reconcile", async (FinancialReconcileRequest request, FinancialReconciliation service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.ReconcileAsync(request.Selected, ct)); }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).Produces<FinancialReconcileResult[]>();
        group.MapGet("/diagnostics", async (FinancialReadStore store, CancellationToken ct) => TypedResults.Ok(await store.DiagnosticsAsync(ct)));
    }

    private static bool TryDecimal(string text, out decimal value)
    {
        value = 0m;
        return text is { Length: > 0 and <= 64 } && decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);
    }
}

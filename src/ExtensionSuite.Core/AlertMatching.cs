namespace ExtensionSuite.Core;

public sealed record AlertCondition(string Unit = "quantity", string Operator = "minimum",
    [property: System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)] long Value = 1,
    [property: System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)] long? UpperExclusive = null, string? Currency = null, int? MinorUnitDigits = null)
{
    public void Validate() => new AutomationCondition("alert", "alert", Unit, Operator, Value, UpperExclusive, Currency, MinorUnitDigits).Validate();
    public bool Matches(CanonicalEvent item) => new AutomationCondition(item.Platform, item.Type, Unit, Operator, Value, UpperExclusive, Currency, MinorUnitDigits).Matches(item);
}
public sealed record AlertSet(string Id, string Name, string[] WidgetIds, string Selection = "first")
{
    public void Validate()
    {
        if (!Guid.TryParseExact(Id, "D", out _) || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 ||
            Selection is not ("first" or "all") || WidgetIds is null || WidgetIds.Length is < 1 or > 100 ||
            WidgetIds.Distinct(StringComparer.Ordinal).Count() != WidgetIds.Length)
            throw new ArgumentException("Invalid alert selection set.");
    }
}
public static class AlertMatching
{
    public static bool Matches(OverlayWidget widget, CanonicalEvent item) => !widget.Hidden && widget.Kind == "alert" &&
        (widget.Alert.EventTypes.Contains("*") || widget.Alert.EventTypes.Contains(item.Type)) && widget.Alert.Platforms.Contains(item.Platform) &&
        (widget.Alert.NativeType is null || widget.Alert.NativeType == item.NativeType) &&
        (widget.Alert.CustomTriggerKey is null || widget.Alert.CustomTriggerKey == item.AlertTriggerKey) &&
        (widget.Alert.Condition is null || widget.Alert.Condition.Matches(item));
    public static string[] Select(OverlayDefinition overlay, CanonicalEvent item)
    {
        var matching = overlay.Widgets.Where(w => Matches(w, item)).Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var set in overlay.AlertSets.Where(s => s.Selection == "first"))
        {
            var winner = set.WidgetIds.FirstOrDefault(matching.Contains);
            foreach (var id in set.WidgetIds) if (id != winner) matching.Remove(id);
        }
        return overlay.Widgets.Where(w => matching.Contains(w.Id)).Select(w => w.Id).ToArray();
    }
    public static bool RequiresV2(OverlayDefinition overlay) => overlay.AlertSets.Length > 0 ||
        overlay.Widgets.Any(w => w.Alert.Condition is not null || w.Alert.NativeType is not null || w.Alert.CustomTriggerKey is not null);
}

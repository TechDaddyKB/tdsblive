namespace ExtensionSuite.Core;

/// <summary>Amounts are native minor units, never nominal USD ledger estimates.</summary>
public sealed record AutomationCondition(string Platform, string EventType, string Unit = "quantity",
    string Operator = "minimum",
    [property: System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)] long Value = 1,
    [property: System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)] long? UpperExclusive = null,
    string? Currency = null, int? MinorUnitDigits = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Platform) || Platform.Length > 64 ||
            string.IsNullOrWhiteSpace(EventType) || EventType.Length > 128 ||
            Unit is not ("quantity" or "native-money") ||
            Operator is not ("exact" or "minimum" or "range" or "multiple") || Value < 0 ||
            (Operator == "multiple" && Value == 0) ||
            (Operator == "range" && (UpperExclusive is null || UpperExclusive <= Value)) ||
            (Operator != "range" && UpperExclusive is not null))
            throw new ArgumentException("Invalid automation condition.");
        if (Unit == "native-money")
        {
            if (!CurrencyCode.IsValid(Currency) || MinorUnitDigits is null or < 0 or > 4 ||
                (Currency == "USD" && MinorUnitDigits != 2))
                throw new ArgumentException("Native money conditions require explicit currency and scale.");
        }
        else if (Currency is not null || MinorUnitDigits is not null)
            throw new ArgumentException("Quantity conditions cannot carry currency metadata.");
    }

    public bool Matches(CanonicalEvent item)
    {
        Validate();
        if (!string.Equals(item.Platform, Platform, StringComparison.OrdinalIgnoreCase) ||
            item.Type != EventType || item.Support?.GatedReason is not null ||
            item.Support?.GiftRole == "recipient") return false;
        long? amount;
        if (Unit == "native-money")
        {
            var money = item.Support?.NativeMoney;
            if (money is null || money.Currency != Currency || money.MinorUnitDigits != MinorUnitDigits) return false;
            amount = money.AmountMinor;
        }
        else amount = item.Support?.Quantity;
        if (amount is null or < 0) return false;
        return Operator switch
        {
            "exact" => amount == Value,
            "minimum" => amount >= Value,
            "range" => amount >= Value && amount < UpperExclusive,
            "multiple" => amount > 0 && amount % Value == 0,
            _ => false
        };
    }
}

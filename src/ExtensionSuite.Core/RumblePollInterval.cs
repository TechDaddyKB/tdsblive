namespace ExtensionSuite.Core;

/// <summary>Shared scheduling constraint; does not implement polling or event detection.</summary>
public sealed record RumblePollInterval
{
    public const int DefaultSeconds = 7;
    public const int MinimumSeconds = 5;
    public const int MaximumNormalSeconds = 10;

    public RumblePollInterval(int seconds = DefaultSeconds, bool advanced = false)
    {
        if (seconds < MinimumSeconds || (!advanced && seconds > MaximumNormalSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds),
                "Use 5–10 seconds, or enable advanced settings for a slower interval.");
        }

        Seconds = seconds;
    }

    public int Seconds { get; }
    public TimeSpan Duration => TimeSpan.FromSeconds(Seconds);
}

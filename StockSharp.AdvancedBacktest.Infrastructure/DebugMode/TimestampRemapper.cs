namespace StockSharp.AdvancedBacktest.Infrastructure.DebugMode;

/// <summary>
/// Utility class to remap timestamps from auxiliary timeframes to main timeframe boundaries.
/// Events triggered by auxiliary TF should be attributed to the parent main TF candle for display.
/// </summary>
public static class TimestampRemapper
{
    public static DateTimeOffset RemapToMainTimeframe(DateTimeOffset eventTime, TimeSpan mainTimeframe)
    {
        if (mainTimeframe <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(mainTimeframe), "Timeframe must be positive");

        var ticks = eventTime.Ticks;
        var intervalTicks = mainTimeframe.Ticks;
        var flooredTicks = (ticks / intervalTicks) * intervalTicks;

        return new DateTimeOffset(flooredTicks, eventTime.Offset);
    }
}

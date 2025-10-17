namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;

public class ZigZagBreakoutConfig
{
    // Delta ZigZag depth parameter (divided by 10 for actual indicator depth)
    public decimal DzzDepth { get; set; } = 5m;

    public int JmaLength { get; set; } = 7;

    public int JmaPhase { get; set; } = 0;

    // JMA usage: -1 = bearish filter, 0 = disabled, 1 = bullish filter
    public int JmaUsage { get; set; } = -1;
}

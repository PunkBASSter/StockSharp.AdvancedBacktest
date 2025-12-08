namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;

public class ZigZagBreakoutConfig
{
    // Delta ZigZag depth parameter (divided by 10 for actual indicator depth)
    public decimal DzzDepth { get; set; } = 5m;

    public int JmaLength { get; set; } = 7;

    public int JmaPhase { get; set; } = 0;

    // JMA usage: -1 = bearish filter, 0 = disabled, 1 = bullish filter
    public int JmaUsage { get; set; } = -1;

    // Risk management: percentage of account to risk per trade (e.g., 2 = 2%)
    public decimal RiskPercentPerTrade { get; set; } = 2m;

    // Position sizing limits
    public decimal MinPositionSize { get; set; } = 0.01m;
    public decimal MaxPositionSize { get; set; } = 1000m;

    // Whether to use native StockSharp protection (StartProtection)
    public bool UseNativeProtection { get; set; } = true;
}

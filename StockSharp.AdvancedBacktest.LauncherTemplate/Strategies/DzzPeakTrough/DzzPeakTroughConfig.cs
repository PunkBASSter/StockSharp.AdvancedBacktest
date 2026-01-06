namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.DzzPeakTrough;

public class DzzPeakTroughConfig
{
    /// <summary>
    /// Delta parameter for DeltaZigZag (divided by 10 for actual indicator depth).
    /// Range: 0 &lt; value &lt;= 100
    /// </summary>
    public decimal DzzDepth { get; set; } = 5m;

    /// <summary>
    /// Risk per trade as percentage of portfolio.
    /// Range: 0 &lt; value &lt;= 10
    /// </summary>
    public decimal RiskPercentPerTrade { get; set; } = 1m;

    /// <summary>
    /// Minimum order volume.
    /// </summary>
    public decimal MinPositionSize { get; set; } = 0.01m;

    /// <summary>
    /// Maximum order volume.
    /// </summary>
    public decimal MaxPositionSize { get; set; } = 10m;

    /// <summary>
    /// Override for DeltaZigZag MinimumThreshold (optional).
    /// If null, uses security-based default.
    /// </summary>
    public decimal? MinimumThreshold { get; set; }
}

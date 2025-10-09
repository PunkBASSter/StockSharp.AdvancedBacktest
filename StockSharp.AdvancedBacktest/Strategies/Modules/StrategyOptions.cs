namespace StockSharp.AdvancedBacktest.Strategies.Modules;

/// <summary>
/// Configuration options for the PreviousWeekRangeBreakoutStrategy
/// </summary>
public class StrategyOptions
{
    // Trend Filter settings
    public IndicatorType TrendFilterType { get; set; } = IndicatorType.SMA;
    public int TrendFilterPeriod { get; set; } = 20;

    // ATR settings
    public int ATRPeriod { get; set; } = 14;

    // Position sizing
    public PositionSizingMethod SizingMethod { get; set; } = PositionSizingMethod.Fixed;
    public decimal FixedPositionSize { get; set; } = 1m;
    public decimal EquityPercentage { get; set; } = 2m;

    // Stop loss settings
    public StopLossMethod StopLossMethodValue { get; set; } = StopLossMethod.Percentage;
    public decimal StopLossPercentage { get; set; } = 2m;
    public decimal StopLossATRMultiplier { get; set; } = 2m;

    // Take profit settings
    public TakeProfitMethod TakeProfitMethodValue { get; set; } = TakeProfitMethod.RiskReward;
    public decimal TakeProfitPercentage { get; set; } = 4m;
    public decimal TakeProfitATRMultiplier { get; set; } = 3m;
    public decimal RiskRewardRatio { get; set; } = 2m;
}

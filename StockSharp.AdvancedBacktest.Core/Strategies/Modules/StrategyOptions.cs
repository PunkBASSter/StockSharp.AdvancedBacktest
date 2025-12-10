namespace StockSharp.AdvancedBacktest.Strategies.Modules;

//TODO:
//Fix wrong architecture. This is a concrete DTO class, that is used by a concrete strategy But it is located in a generic library.
//It's tightly coupled with different strategy modules that supposed to be interchangeable and suitable for other strategies.
//
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

    // Fixed risk position sizing
    public decimal RiskPercentPerTrade { get; set; } = 1m;
    public decimal MinPositionSize { get; set; } = 1m;
    public decimal MaxPositionSize { get; set; } = 1000m;

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

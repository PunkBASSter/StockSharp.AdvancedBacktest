namespace StockSharp.AdvancedBacktest.Strategies.Modules;

public enum PositionSizingMethod
{
    Fixed,
    PercentOfEquity,
    ATRBased,
    FixedRisk
}

public enum StopLossMethod
{
    Percentage,
    ATR
}

public enum TakeProfitMethod
{
    Percentage,
    ATR,
    RiskReward
}

public enum IndicatorType
{
    SMA,
    EMA
}

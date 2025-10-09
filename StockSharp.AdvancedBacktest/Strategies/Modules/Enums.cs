namespace StockSharp.AdvancedBacktest.Strategies.Modules;

public enum PositionSizingMethod
{
    Fixed,
    PercentOfEquity,
    ATRBased
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

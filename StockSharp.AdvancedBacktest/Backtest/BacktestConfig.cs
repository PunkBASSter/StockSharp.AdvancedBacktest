using StockSharp.Algo.Commissions;

namespace StockSharp.AdvancedBacktest.Backtest;

/// <summary>
/// Debug mode configuration for event capture.
/// </summary>
public class DebugModeSettings
{
    public bool Enabled { get; set; } = false;
    public string OutputDirectory { get; set; } = "debug";
    public int FlushIntervalMs { get; set; } = 500;
}

public class BacktestConfig
{
    public required PeriodConfig ValidationPeriod { get; set; }
    public required string HistoryPath { get; set; }

    /// <summary>
    /// Match order if historical price touched the limit order price.
    /// False = more strict testing (price must go through the level)
    /// </summary>
    public bool MatchOnTouch { get; set; } = false;
    public IEnumerable<ICommissionRule> CommissionRules { get; set; } = [new CommissionTradeRule { Value = 0.1m }];

    /// <summary>
    /// Debug mode configuration for real-time event capture and visualization.
    /// </summary>
    public DebugModeSettings? DebugMode { get; set; }
}

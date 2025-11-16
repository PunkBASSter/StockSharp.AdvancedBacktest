using StockSharp.Algo.Commissions;

namespace StockSharp.AdvancedBacktest.Backtest;

public class DebugModeSettings
{
    public bool Enabled { get; set; } = false;
    public string OutputDirectory { get; set; } = "debug";
    public int FlushIntervalMs { get; set; } = 500;
    public string? WebAppPath { get; set; }
    public string WebAppUrl { get; set; } = "http://localhost:3000";
    public string DebugPagePath { get; set; } = "/debug-mode";
}

public class AgenticLoggingSettings
{
    public bool Enabled { get; set; } = false;
    public string DatabasePath { get; set; } = "debug/events.db";
    public int BatchSize { get; set; } = 1000;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(30);
    public bool LogIndicators { get; set; } = true;
    public bool LogTrades { get; set; } = true;
    public bool LogMarketData { get; set; } = false;
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
    public DebugModeSettings? DebugMode { get; set; }
    public AgenticLoggingSettings? AgenticLogging { get; set; }
}

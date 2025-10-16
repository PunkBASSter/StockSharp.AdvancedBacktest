using StockSharp.Algo.Commissions;

namespace StockSharp.AdvancedBacktest.Models;

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
}

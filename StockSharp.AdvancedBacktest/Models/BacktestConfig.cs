using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.Algo.Commissions;

namespace StockSharp.AdvancedBacktest.Models;

public class BacktestConfig
{
    public required PeriodConfig ValidationPeriod { get; set; }
    public required string HistoryPath { get; set; }
    public required CustomParamsContainer ParamsContainer { get; set; }
    public decimal InitialCapital { get; set; } = 10000m;
    public IEnumerable<ICommissionRule> CommissionRules { get; set; } = [new CommissionTradeRule { Value = 0.1m }];
    public decimal TradeVolume { get; set; } = 0.01m;
}

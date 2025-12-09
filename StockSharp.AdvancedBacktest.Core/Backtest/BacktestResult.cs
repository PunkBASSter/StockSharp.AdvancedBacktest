using StockSharp.Algo.Strategies;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Backtest;

public class BacktestResult<TStrategy> where TStrategy : Strategy
{
    public required TStrategy Strategy { get; set; }
    public required PerformanceMetrics Metrics { get; set; }
    public required BacktestConfig Config { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}

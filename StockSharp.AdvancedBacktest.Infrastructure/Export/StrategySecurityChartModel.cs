using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.PerformanceValidation;

namespace StockSharp.AdvancedBacktest.Export;

public class StrategySecurityChartModel
{
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public required string HistoryPath { get; set; }
    public required Security Security { get; set; }
    public required CustomStrategyBase Strategy { get; set; }
    public required string OutputPath { get; set; }
    public required PerformanceMetrics Metrics { get; set; }
    public WalkForwardResult? WalkForwardResult { get; set; }
}

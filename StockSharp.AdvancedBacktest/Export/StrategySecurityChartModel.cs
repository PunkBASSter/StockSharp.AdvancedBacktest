using System;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;

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
}

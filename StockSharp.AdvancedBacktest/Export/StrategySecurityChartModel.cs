using System;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Export;

public class StrategySecurityChartModel
{
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
	public string HistoryPath { get; set; }
	public Security Security { get; set; }
	public CustomStrategyBase Strategy { get; set; }
	public string OutputPath { get; set; }
	public PerformanceMetrics Metrics { get; set; }
}

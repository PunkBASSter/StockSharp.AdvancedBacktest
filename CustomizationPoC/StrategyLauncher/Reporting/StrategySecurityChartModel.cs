using System;
using StockSharp.BusinessEntities;
using StockSharp.StrategyLauncher.CustomStrategy;

namespace StockSharp.Samples.MaCrossoverBacktester.Reporting;

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

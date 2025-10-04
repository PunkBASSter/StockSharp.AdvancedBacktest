using System;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Models;

public class OptimizationResult<TStrategy> where TStrategy : CustomStrategyBase, new()
{
	public DateTimeOffset StartTime { get; set; } = DateTimeOffset.Now;
	public OptimizationConfig Config { get; set; }
	public TStrategy TrainedStrategy { get; set; }
	public TStrategy ValidatedStrategy { get; set; }
	public PerformanceMetrics? TrainingMetrics { get; set; }
	public PerformanceMetrics? ValidationMetrics { get; set; }
}

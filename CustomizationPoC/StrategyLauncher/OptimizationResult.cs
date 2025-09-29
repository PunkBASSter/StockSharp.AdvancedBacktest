using System;
using StockSharp.Samples.MaCrossoverBacktester;
using StockSharp.Samples.MaCrossoverBacktester.OptimizerFactory;
using StockSharp.StrategyLauncher.CustomStrategy;

namespace StockSharp.StrategyLauncher
{
	public class OptimizationResult<TStrategy> where TStrategy : CustomStrategyBase, new()
	{
		public DateTimeOffset StartTime { get; set; } = DateTimeOffset.Now;
		public OptimizationConfig Config { get; set; }
		public TStrategy TrainedStrategy { get; set; }
		public TStrategy ValidatedStrategy { get; set; }
		public PerformanceMetrics? TrainingMetrics { get; set; }
		public PerformanceMetrics? ValidationMetrics { get; set; }
	}
}

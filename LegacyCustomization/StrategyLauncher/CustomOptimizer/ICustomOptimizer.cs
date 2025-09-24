using System.Collections.Generic;
using StockSharp.Algo.Strategies.Optimization;
using StockSharp.StrategyLauncher;
using StockSharp.StrategyLauncher.CustomStrategy;

namespace StockSharp.Samples.MaCrossoverBacktester.OptimizerFactory;

public interface ICustomOptimizer<TStrategy> where TStrategy : CustomStrategyBase, new()
{
	BaseOptimizer CreateOptimizer(OptimizationConfig config);
	Dictionary<string, OptimizationResult<TStrategy>> Optimize();
	List<TStrategy> OptimizationStrategies { get; }
	List<PerformanceMetrics> PerformanceMetrics { get; }
}
using System;
using System.Collections.Generic;
using StockSharp.Algo.Commissions;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Models;

public class OptimizationConfig
{
	public CustomParamsContainer ParamsContainer { get; set; }
	public OptimizationPeriodConfig TrainingPeriod { get; set; }
	public List<Func<PerformanceMetrics, bool>> MetricFilters { get; set; } = [];
	public decimal InitialCapital { get; set; } = 10000m;
	public IEnumerable<ICommissionRule> CommissionRules { get; set; }
		= [new CommissionTradeRule { Value = 0.1m }];
	public decimal TradeVolume { get; set; } = 0.01m;
	public bool IsBruteForce { get; set; } = true;
#if DEBUG
	public int ParallelWorkers { get; set; } = 1;
#else
	public int ParallelWorkers { get; set; } = Environment.ProcessorCount;
#endif
	public string HistoryPath { get; set; }
	public GeneticConfig GeneticSettings { get; set; } = new GeneticConfig();
}

public class GeneticConfig
{
	public int PopulationSize { get; set; } = 50;
	public int Generations { get; set; } = 100;
	public double MutationProbability { get; set; } = 0.1;
	public double CrossoverProbability { get; set; } = 0.8;
	public int EliteCount { get; set; } = 5;
}

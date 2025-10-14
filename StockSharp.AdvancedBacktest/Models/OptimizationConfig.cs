using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Models;

public class OptimizationConfig : BacktestConfig
{
    public required PeriodConfig TrainingPeriod { get; set; }
    public List<Func<PerformanceMetrics, bool>> MetricFilters { get; set; } = [];

    public bool IsBruteForce { get; set; } = true;
#if DEBUG
    public int ParallelWorkers { get; set; } = 1;
#else
	public int ParallelWorkers { get; set; } = Environment.ProcessorCount;
#endif
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

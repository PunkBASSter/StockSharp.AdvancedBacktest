using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Models;

/// <summary>
/// Configuration for optimization runs.
/// Unlike single backtests, optimization creates multiple strategy instances internally,
/// so it needs strategy-creation properties.
/// </summary>
public class OptimizationConfig : BacktestConfig
{
    public required PeriodConfig TrainingPeriod { get; set; }
    public List<Func<PerformanceMetrics, bool>> MetricFilters { get; set; } = [];

    /// <summary>
    /// Parameters container for generating optimization combinations
    /// </summary>
    public required CustomParamsContainer ParamsContainer { get; set; }

    /// <summary>
    /// Initial capital for strategy portfolios
    /// </summary>
    public decimal InitialCapital { get; set; } = 10000m;

    /// <summary>
    /// Portfolio name for created strategies
    /// </summary>
    public string PortfolioName { get; set; } = "Simulator";

    /// <summary>
    /// Trade volume for strategy orders (strategy-specific, can be overridden)
    /// </summary>
    public decimal TradeVolume { get; set; } = 0.01m;

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

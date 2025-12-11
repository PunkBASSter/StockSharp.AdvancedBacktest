using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Statistics;

/// <summary>
/// Interface for calculating performance metrics from strategy execution results
/// </summary>
public interface IPerformanceMetricsCalculator
{
    PerformanceMetrics CalculateMetrics(Strategy strategy, DateTimeOffset startDate, DateTimeOffset endDate);
}

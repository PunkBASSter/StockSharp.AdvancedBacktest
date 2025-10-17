using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Statistics;

/// <summary>
/// Interface for calculating performance metrics from strategy execution results
/// </summary>
public interface IPerformanceMetricsCalculator
{
    /// <summary>
    /// Calculate comprehensive performance metrics for a strategy
    /// </summary>
    /// <param name="strategy">The strategy to analyze</param>
    /// <param name="startDate">Start date of the trading period</param>
    /// <param name="endDate">End date of the trading period</param>
    /// <returns>Comprehensive performance metrics</returns>
    PerformanceMetrics CalculateMetrics(Strategy strategy, DateTimeOffset startDate, DateTimeOffset endDate);
}

using StockSharp.Algo.Strategies;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Models;

/// <summary>
/// Result of a single backtest run
/// </summary>
/// <typeparam name="TStrategy">The strategy type that was tested</typeparam>
public class BacktestResult<TStrategy> where TStrategy : Strategy
{
    /// <summary>
    /// The strategy instance that was executed
    /// </summary>
    public required TStrategy Strategy { get; set; }

    /// <summary>
    /// Performance metrics calculated from the backtest
    /// </summary>
    public required PerformanceMetrics Metrics { get; set; }

    /// <summary>
    /// The configuration used for this backtest
    /// </summary>
    public required BacktestConfig Config { get; set; }

    /// <summary>
    /// Indicates whether the backtest completed successfully
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Error message if the backtest failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time when the backtest started
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Time when the backtest completed
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Duration of the backtest execution
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
}

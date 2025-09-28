using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

/// <summary>
/// Interface for high-performance strategy performance tracking
/// </summary>
public interface IPerformanceTracker : IDisposable
{
    /// <summary>
    /// Current portfolio value
    /// </summary>
    decimal CurrentValue { get; }

    /// <summary>
    /// Total return percentage
    /// </summary>
    decimal TotalReturn { get; }

    /// <summary>
    /// Sharpe ratio calculation
    /// </summary>
    decimal SharpeRatio { get; }

    /// <summary>
    /// Maximum drawdown percentage
    /// </summary>
    decimal MaxDrawdown { get; }

    /// <summary>
    /// Current drawdown percentage
    /// </summary>
    decimal CurrentDrawdown { get; }

    /// <summary>
    /// Win rate percentage
    /// </summary>
    decimal WinRate { get; }

    /// <summary>
    /// Total number of trades
    /// </summary>
    int TotalTrades { get; }

    /// <summary>
    /// Number of winning trades
    /// </summary>
    int WinningTrades { get; }

    /// <summary>
    /// Record a new trade execution
    /// </summary>
    /// <param name="trade">Trade data</param>
    void RecordTrade(Trade trade);

    /// <summary>
    /// Update portfolio value
    /// </summary>
    /// <param name="value">New portfolio value</param>
    /// <param name="timestamp">Timestamp of the update</param>
    void UpdatePortfolioValue(decimal value, DateTimeOffset timestamp);

    /// <summary>
    /// Calculate volatility over specified periods
    /// </summary>
    /// <param name="periods">Number of periods (default 252 for annualized)</param>
    /// <returns>Volatility as decimal</returns>
    decimal CalculateVolatility(int periods = 252);

    /// <summary>
    /// Get performance snapshot
    /// </summary>
    /// <returns>Current performance snapshot</returns>
    PerformanceSnapshot GetSnapshot();

    /// <summary>
    /// Get performance history
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <returns>Performance snapshots in the specified range</returns>
    ImmutableArray<PerformanceSnapshot> GetHistory(DateTimeOffset? from = null, DateTimeOffset? to = null);

    /// <summary>
    /// Reset all performance metrics
    /// </summary>
    void Reset();

    /// <summary>
    /// Check if performance metrics are consistent
    /// </summary>
    bool IsConsistent { get; }
}
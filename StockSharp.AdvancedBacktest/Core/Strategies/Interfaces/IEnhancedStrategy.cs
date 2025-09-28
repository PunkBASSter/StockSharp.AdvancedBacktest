using StockSharp.Algo.Strategies;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Threading.Channels;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

/// <summary>
/// Interface for enhanced strategy contract with modern .NET patterns
/// </summary>
public interface IEnhancedStrategy : IDisposable
{
    /// <summary>
    /// Strategy parameters using modern C# patterns
    /// </summary>
    IParameterSet Parameters { get; }

    /// <summary>
    /// Performance tracking interface
    /// </summary>
    IPerformanceTracker? Performance { get; }

    /// <summary>
    /// Risk management interface
    /// </summary>
    IRiskManager? RiskManager { get; }

    /// <summary>
    /// High-performance event channel for trade execution data
    /// </summary>
    ChannelReader<TradeExecutionData> TradeEvents { get; }

    /// <summary>
    /// High-performance event channel for performance snapshots
    /// </summary>
    ChannelReader<PerformanceSnapshot> PerformanceEvents { get; }

    /// <summary>
    /// Current strategy state
    /// </summary>
    StrategyState CurrentState { get; }

    /// <summary>
    /// Initialize enhanced features with dependency injection
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    Task InitializeAsync(IServiceProvider serviceProvider);

    /// <summary>
    /// Validate strategy parameters
    /// </summary>
    /// <returns>Validation result</returns>
    ValidationResult ValidateParameters();
}
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Threading.Channels;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

/// <summary>
/// Interface for strategy event handling patterns using modern async patterns
/// </summary>
public interface IStrategyEventHandler : IAsyncDisposable
{
    /// <summary>
    /// Trade execution events channel
    /// </summary>
    ChannelReader<TradeExecutionData> TradeEvents { get; }

    /// <summary>
    /// Performance snapshot events channel
    /// </summary>
    ChannelReader<PerformanceSnapshot> PerformanceEvents { get; }

    /// <summary>
    /// Risk violation events channel
    /// </summary>
    ChannelReader<RiskViolation> RiskEvents { get; }

    /// <summary>
    /// Strategy state change events channel
    /// </summary>
    ChannelReader<StrategyStateChange> StateEvents { get; }

    /// <summary>
    /// Start event processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the event processing operation</returns>
    Task StartProcessingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop event processing
    /// </summary>
    /// <returns>Task representing the stop operation</returns>
    Task StopProcessingAsync();

    /// <summary>
    /// Publish a trade execution event
    /// </summary>
    /// <param name="tradeData">Trade execution data</param>
    /// <returns>True if event was published successfully</returns>
    bool PublishTradeEvent(TradeExecutionData tradeData);

    /// <summary>
    /// Publish a performance snapshot event
    /// </summary>
    /// <param name="snapshot">Performance snapshot</param>
    /// <returns>True if event was published successfully</returns>
    bool PublishPerformanceEvent(PerformanceSnapshot snapshot);

    /// <summary>
    /// Publish a risk violation event
    /// </summary>
    /// <param name="violation">Risk violation</param>
    /// <returns>True if event was published successfully</returns>
    bool PublishRiskEvent(RiskViolation violation);

    /// <summary>
    /// Publish a strategy state change event
    /// </summary>
    /// <param name="stateChange">Strategy state change</param>
    /// <returns>True if event was published successfully</returns>
    bool PublishStateEvent(StrategyStateChange stateChange);
}
using Microsoft.Extensions.Logging;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Threading.Channels;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

/// <summary>
/// High-performance strategy event handler using System.Threading.Channels
/// </summary>
public class StrategyEventHandler : IStrategyEventHandler
{
    private readonly Channel<TradeExecutionData> _tradeChannel;
    private readonly Channel<PerformanceSnapshot> _performanceChannel;
    private readonly Channel<RiskViolation> _riskChannel;
    private readonly Channel<StrategyStateChange> _stateChannel;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger<StrategyEventHandler> _logger;

    private readonly Task _processingTask;
    private volatile bool _isDisposed;

    /// <summary>
    /// Trade execution events channel
    /// </summary>
    public ChannelReader<TradeExecutionData> TradeEvents => _tradeChannel.Reader;

    /// <summary>
    /// Performance snapshot events channel
    /// </summary>
    public ChannelReader<PerformanceSnapshot> PerformanceEvents => _performanceChannel.Reader;

    /// <summary>
    /// Risk violation events channel
    /// </summary>
    public ChannelReader<RiskViolation> RiskEvents => _riskChannel.Reader;

    /// <summary>
    /// Strategy state change events channel
    /// </summary>
    public ChannelReader<StrategyStateChange> StateEvents => _stateChannel.Reader;

    /// <summary>
    /// Initialize the event handler with unbounded channels for maximum performance
    /// </summary>
    public StrategyEventHandler(ILogger<StrategyEventHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();

        // Create unbounded channels for high-throughput scenarios
        var channelOptions = new UnboundedChannelOptions
        {
            SingleReader = false, // Allow multiple consumers
            SingleWriter = false, // Allow multiple producers
            AllowSynchronousContinuations = false // Prevent blocking
        };

        _tradeChannel = Channel.CreateUnbounded<TradeExecutionData>(channelOptions);
        _performanceChannel = Channel.CreateUnbounded<PerformanceSnapshot>(channelOptions);
        _riskChannel = Channel.CreateUnbounded<RiskViolation>(channelOptions);
        _stateChannel = Channel.CreateUnbounded<StrategyStateChange>(channelOptions);

        // Start background processing task
        _processingTask = Task.Run(async () => await ProcessEventsAsync(_cancellationTokenSource.Token));
    }

    /// <summary>
    /// Start event processing (already started in constructor)
    /// </summary>
    public Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Strategy event processing started");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop event processing
    /// </summary>
    public async Task StopProcessingAsync()
    {
        try
        {
            _cancellationTokenSource.Cancel();

            // Complete all writers
            _tradeChannel.Writer.Complete();
            _performanceChannel.Writer.Complete();
            _riskChannel.Writer.Complete();
            _stateChannel.Writer.Complete();

            // Wait for processing task to complete
            await _processingTask;

            _logger.LogInformation("Strategy event processing stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping event processing");
        }
    }

    /// <summary>
    /// Publish a trade execution event
    /// </summary>
    public bool PublishTradeEvent(TradeExecutionData tradeData)
    {
        if (_isDisposed || _cancellationTokenSource.Token.IsCancellationRequested)
            return false;

        try
        {
            return _tradeChannel.Writer.TryWrite(tradeData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing trade event for trade {TradeId}", tradeData.TradeId);
            return false;
        }
    }

    /// <summary>
    /// Publish a performance snapshot event
    /// </summary>
    public bool PublishPerformanceEvent(PerformanceSnapshot snapshot)
    {
        if (_isDisposed || _cancellationTokenSource.Token.IsCancellationRequested)
            return false;

        try
        {
            return _performanceChannel.Writer.TryWrite(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing performance event at {Timestamp}", snapshot.Timestamp);
            return false;
        }
    }

    /// <summary>
    /// Publish a risk violation event
    /// </summary>
    public bool PublishRiskEvent(RiskViolation violation)
    {
        if (_isDisposed || _cancellationTokenSource.Token.IsCancellationRequested)
            return false;

        try
        {
            var result = _riskChannel.Writer.TryWrite(violation);
            if (violation.Severity >= RiskSeverity.Critical)
            {
                _logger.LogWarning("Risk violation published: {ViolationType} - {Message}",
                    violation.ViolationType, violation.Message);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing risk event: {ViolationType}", violation.ViolationType);
            return false;
        }
    }

    /// <summary>
    /// Publish a strategy state change event
    /// </summary>
    public bool PublishStateEvent(StrategyStateChange stateChange)
    {
        if (_isDisposed || _cancellationTokenSource.Token.IsCancellationRequested)
            return false;

        try
        {
            var result = _stateChannel.Writer.TryWrite(stateChange);
            _logger.LogDebug("Strategy state changed: {PreviousStatus} -> {NewStatus}",
                stateChange.PreviousState.Status, stateChange.NewState.Status);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing state change event");
            return false;
        }
    }

    /// <summary>
    /// Background event processing with proper error handling
    /// </summary>
    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Create tasks for processing each event type
            var tasks = new[]
            {
                ProcessTradeEventsAsync(cancellationToken),
                ProcessPerformanceEventsAsync(cancellationToken),
                ProcessRiskEventsAsync(cancellationToken),
                ProcessStateEventsAsync(cancellationToken)
            };

            // Wait for all processing tasks to complete
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogDebug("Event processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event processing");
        }
    }

    /// <summary>
    /// Process trade execution events
    /// </summary>
    private async Task ProcessTradeEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var tradeEvent in _tradeChannel.Reader.ReadAllAsync(cancellationToken))
            {
                // Process trade event - could include logging, persistence, notifications, etc.
                _logger.LogTrace("Processing trade event: {TradeId} for {SecurityCode}",
                    tradeEvent.TradeId, tradeEvent.SecurityCode);

                // Example: Persist to database, send notifications, etc.
                await ProcessTradeEventAsync(tradeEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing trade events");
        }
    }

    /// <summary>
    /// Process performance snapshot events
    /// </summary>
    private async Task ProcessPerformanceEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var snapshot in _performanceChannel.Reader.ReadAllAsync(cancellationToken))
            {
                _logger.LogTrace("Processing performance snapshot: {Timestamp} - Portfolio: {PortfolioValue:C}",
                    snapshot.Timestamp, snapshot.PortfolioValue);

                await ProcessPerformanceSnapshotAsync(snapshot, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing performance events");
        }
    }

    /// <summary>
    /// Process risk violation events
    /// </summary>
    private async Task ProcessRiskEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var violation in _riskChannel.Reader.ReadAllAsync(cancellationToken))
            {
                _logger.LogWarning("Processing risk violation: {ViolationType} - {Message}",
                    violation.ViolationType, violation.Message);

                await ProcessRiskViolationAsync(violation, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing risk events");
        }
    }

    /// <summary>
    /// Process strategy state change events
    /// </summary>
    private async Task ProcessStateEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var stateChange in _stateChannel.Reader.ReadAllAsync(cancellationToken))
            {
                _logger.LogInformation("Strategy state changed: {PreviousStatus} -> {NewStatus} - {Reason}",
                    stateChange.PreviousState.Status, stateChange.NewState.Status, stateChange.Reason);

                await ProcessStateChangeAsync(stateChange, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing state events");
        }
    }

    /// <summary>
    /// Process individual trade event (override in derived classes for custom behavior)
    /// </summary>
    protected virtual async Task ProcessTradeEventAsync(TradeExecutionData tradeEvent, CancellationToken cancellationToken)
    {
        // Default implementation - extend as needed
        await Task.Yield(); // Prevent synchronous execution
    }

    /// <summary>
    /// Process individual performance snapshot (override in derived classes for custom behavior)
    /// </summary>
    protected virtual async Task ProcessPerformanceSnapshotAsync(PerformanceSnapshot snapshot, CancellationToken cancellationToken)
    {
        // Default implementation - extend as needed
        await Task.Yield(); // Prevent synchronous execution
    }

    /// <summary>
    /// Process individual risk violation (override in derived classes for custom behavior)
    /// </summary>
    protected virtual async Task ProcessRiskViolationAsync(RiskViolation violation, CancellationToken cancellationToken)
    {
        // Default implementation - extend as needed
        await Task.Yield(); // Prevent synchronous execution
    }

    /// <summary>
    /// Process individual state change (override in derived classes for custom behavior)
    /// </summary>
    protected virtual async Task ProcessStateChangeAsync(StrategyStateChange stateChange, CancellationToken cancellationToken)
    {
        // Default implementation - extend as needed
        await Task.Yield(); // Prevent synchronous execution
    }

    /// <summary>
    /// Async dispose pattern implementation
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            await StopProcessingAsync();
            _cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disposal");
        }

        GC.SuppressFinalize(this);
    }
}
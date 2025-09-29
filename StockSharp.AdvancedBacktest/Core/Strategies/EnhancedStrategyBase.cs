using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

public abstract class EnhancedStrategyBase : Strategy, IEnhancedStrategy, IAsyncDisposable
{
    #region Private Fields

    private readonly Channel<TradeExecutionData> _tradeChannel;
    private readonly Channel<PerformanceSnapshot> _performanceChannel;
    private readonly Channel<RiskViolation> _riskChannel;
    private readonly Channel<StrategyStateChange> _stateChannel;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);

    private volatile bool _isInitialized;
    private volatile bool _isDisposed;
    private StrategyState _currentState = StrategyState.Initial;

    // Thread-safe collections for concurrent StockSharp operations
    private readonly ConcurrentDictionary<long, Order> _enhancedOrders = new();
    private readonly ConcurrentQueue<TradeExecutionData> _tradeQueue = new();

    // Object pooling for high-frequency operations
    private readonly ObjectPool<PerformanceSnapshot>? _snapshotPool;

    #endregion

    #region Protected Fields

    protected readonly ILogger<EnhancedStrategyBase> _logger;
    protected readonly IServiceProvider? _serviceProvider;

    #endregion

    #region Required Properties (C# 11+ Pattern)

    public new required IParameterSet Parameters { get; init; }

    #endregion

    #region Public Properties

    public IPerformanceTracker? Performance { get; private set; }

    public new IRiskManager? RiskManager { get; protected set; }

    public ChannelReader<TradeExecutionData> TradeEvents => _tradeChannel.Reader;

    public ChannelReader<PerformanceSnapshot> PerformanceEvents => _performanceChannel.Reader;

    public ChannelReader<RiskViolation> RiskEvents => _riskChannel.Reader;

    public ChannelReader<StrategyStateChange> StateEvents => _stateChannel.Reader;

    public StrategyState CurrentState => _currentState;

    #endregion

    #region Constructors

    protected EnhancedStrategyBase() : base()
    {
        // Initialize with service locator pattern as fallback
        _logger = InitializeLogger();
        _serviceProvider = InitializeServiceProvider();

        // Initialize high-performance channels
        _tradeChannel = Channel.CreateUnbounded<TradeExecutionData>();
        _performanceChannel = Channel.CreateUnbounded<PerformanceSnapshot>();
        _riskChannel = Channel.CreateUnbounded<RiskViolation>();
        _stateChannel = Channel.CreateUnbounded<StrategyStateChange>();

        // Initialize object pool if available
        _snapshotPool = _serviceProvider?.GetService<ObjectPool<PerformanceSnapshot>>();
    }

    protected EnhancedStrategyBase(
        ILogger<EnhancedStrategyBase> logger,
        IServiceProvider serviceProvider) : base()
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Initialize high-performance channels
        _tradeChannel = Channel.CreateUnbounded<TradeExecutionData>();
        _performanceChannel = Channel.CreateUnbounded<PerformanceSnapshot>();
        _riskChannel = Channel.CreateUnbounded<RiskViolation>();
        _stateChannel = Channel.CreateUnbounded<StrategyStateChange>();

        // Initialize object pool
        _snapshotPool = serviceProvider.GetService<ObjectPool<PerformanceSnapshot>>();
    }

    #endregion

    #region Enhanced Strategy Lifecycle

    public virtual async Task StartEnhancedAsync()
    {
        try
        {
            // Update state
            UpdateState(StrategyStatus.Starting, "Strategy starting");

            // Initialize enhanced features
            await InitializeEnhancedFeaturesAsync();

            // Update state to running
            UpdateState(StrategyStatus.Running, "Strategy started successfully");

            _logger.LogInformation("Enhanced strategy started with {ParameterCount} parameters", Parameters.Count);
        }
        catch (Exception ex)
        {
            UpdateState(StrategyStatus.Error, $"Failed to start strategy: {ex.Message}");
            _logger.LogError(ex, "Failed to start enhanced strategy");
            throw;
        }
    }

    public virtual async Task StopEnhancedAsync()
    {
        try
        {
            UpdateState(StrategyStatus.Stopping, "Strategy stopping");

            // Enhanced cleanup
            await CleanupEnhancedFeaturesAsync();

            UpdateState(StrategyStatus.Stopped, "Strategy stopped");
            _logger.LogInformation("Enhanced strategy stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during enhanced strategy cleanup");
            UpdateState(StrategyStatus.Error, $"Error stopping strategy: {ex.Message}");
        }
    }

    public virtual bool ProcessOrder(Order order)
    {
        try
        {
            // Pre-order risk validation
            if (RiskManager?.ValidateOrder(order) == false)
            {
                var violation = RiskViolation.OrderValidationFailed(order, "Order failed risk validation");
                PublishRiskEvent(violation);
                _logger.LogWarning("Order {OrderId} rejected by risk manager for security {SecurityCode}",
                    order.Id, order.Security?.Code);
                return false;
            }

            // Enhanced pre-processing
            EnhancedPreOrderProcessing(order);

            // Enhanced post-processing
            EnhancedPostOrderProcessing(order);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", order.Id);
            return false;
        }
    }

    public virtual void ProcessTrade(Trade trade)
    {
        try
        {
            // Capture enhanced trade data without allocation in hot path
            RecordTradeExecution(trade);

            UpdateState(_currentState.WithLastTrade(trade.Time));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing trade {TradeId}", trade.Id);
        }
    }

    #endregion

    #region Enhanced Functionality

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        if (_isInitialized)
            return;

        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            Performance = serviceProvider.GetService<IPerformanceTracker>();
            RiskManager = serviceProvider.GetService<IRiskManager>();

            await InitializeEnhancedFeaturesAsync();

            _isInitialized = true;
            _logger.LogInformation("Enhanced strategy features initialized for {StrategyName}", Name);
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    public ValidationResult ValidateParameters()
    {
        try
        {
            return Parameters.Validate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating parameters for strategy {StrategyName}", Name);
            return ValidationResult.Failure($"Parameter validation failed: {ex.Message}");
        }
    }

    #endregion

    #region Private Helper Methods

    private ILogger<EnhancedStrategyBase> InitializeLogger()
    {
        // Fallback to null logger if no service provider available
        return Microsoft.Extensions.Logging.Abstractions.NullLogger<EnhancedStrategyBase>.Instance;
    }

    private IServiceProvider? InitializeServiceProvider()
    {
        // Service locator pattern - would be configured by the application
        // For now, return null and rely on explicit initialization
        return null;
    }

    private async Task InitializeEnhancedFeaturesAsync()
    {
        try
        {
            // Initialize performance tracking
            Performance?.Reset();

            // Initialize risk management
            RiskManager?.ResetDaily();

            // Start background event processing
            _ = Task.Run(async () => await ProcessEventsAsync(_cancellationTokenSource.Token));

            // Give some time for initialization
            await Task.Delay(1, _cancellationTokenSource.Token);

            _logger.LogDebug("Enhanced features initialized for strategy {StrategyName}", Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize enhanced features");
            throw;
        }
    }

    private async Task CleanupEnhancedFeaturesAsync()
    {
        try
        {
            _cancellationTokenSource.Cancel();

            // Close channels
            _tradeChannel.Writer.Complete();
            _performanceChannel.Writer.Complete();
            _riskChannel.Writer.Complete();
            _stateChannel.Writer.Complete();

            // Dispose services
            Performance?.Dispose();
            RiskManager?.Dispose();

            // Brief delay to ensure cleanup completes
            await Task.Delay(1);

            _logger.LogDebug("Enhanced features cleaned up for strategy {StrategyName}", Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during enhanced cleanup");
        }
    }

    private void EnhancedPreOrderProcessing(Order order)
    {
        // Track enhanced order data
        if (order.Id.HasValue)
        {
            _enhancedOrders.TryAdd(order.Id.Value, order);
        }

        _logger.LogDebug("Processing order {OrderId} for {SecurityCode}: {Side} {Volume} @ {Price}",
            order.Id, order.Security?.Code, order.Direction, order.Volume, order.Price);
    }

    private void EnhancedPostOrderProcessing(Order order)
    {
        // Update state with pending orders
        var pendingOrders = _enhancedOrders.Count;
        UpdateState(_currentState.WithPositionsAndOrders(_currentState.ActivePositions, pendingOrders));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordTradeExecution(Trade trade)
    {
        try
        {
            // Update performance tracking
            Performance?.RecordTrade(trade);

            // Create trade execution data
            var tradeData = new TradeExecutionData(
                OriginalTrade: trade,
                StrategyParameters: Parameters.GetSnapshot(),
                Timestamp: trade.Time,
                PortfolioSnapshot: GetCurrentPortfolioSnapshot()
            );

            // Non-blocking event publishing
            _ = _tradeChannel.Writer.TryWrite(tradeData);

            // Update performance snapshot
            if (Performance != null)
            {
                var snapshot = Performance.GetSnapshot();
                _ = _performanceChannel.Writer.TryWrite(snapshot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording trade execution for trade {TradeId}", trade.Id);
        }
    }

    private PortfolioSnapshot GetCurrentPortfolioSnapshot()
    {
        var portfolio = Portfolio;
        if (portfolio == null)
        {
            return new PortfolioSnapshot(0m, 0m, 0m, 0m, DateTimeOffset.UtcNow);
        }

        return new PortfolioSnapshot(
            TotalValue: portfolio.CurrentValue ?? 0m,
            Cash: portfolio.CurrentValue ?? 0m, // Simplified for now
            UnrealizedPnL: portfolio.UnrealizedPnL ?? 0m,
            RealizedPnL: portfolio.RealizedPnL ?? 0m,
            Timestamp: DateTimeOffset.UtcNow
        );
    }

    private void UpdateState(StrategyStatus status, string? reason = null)
    {
        var previousState = _currentState;
        _currentState = _currentState.WithStatus(status, reason);

        var stateChange = new StrategyStateChange(previousState, _currentState, DateTimeOffset.UtcNow, reason);
        _ = _stateChannel.Writer.TryWrite(stateChange);
    }

    private void UpdateState(StrategyState newState)
    {
        var previousState = _currentState;
        _currentState = newState;

        var stateChange = new StrategyStateChange(previousState, _currentState, DateTimeOffset.UtcNow);
        _ = _stateChannel.Writer.TryWrite(stateChange);
    }

    private bool PublishRiskEvent(RiskViolation violation)
    {
        RiskManager?.RecordViolation(violation);
        return _riskChannel.Writer.TryWrite(violation);
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // This method would contain background processing logic
            // For now, it's a placeholder for future event processing
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in background event processing");
        }
    }

    #endregion

    #region IDisposable Implementation

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed && disposing)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _initializationSemaphore?.Dispose();

            Performance?.Dispose();
            RiskManager?.Dispose();

            _isDisposed = true;
        }
    }

    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            await CleanupEnhancedFeaturesAsync();
            Dispose(false);
            GC.SuppressFinalize(this);
        }
    }

    #endregion
}
using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Testing;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Storages;
using StockSharp.AdvancedBacktest.DebugMode;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.Integration;
using StockSharp.Messages;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Backtest;

/// <summary>
/// Single backtest runner for executing a strategy with a specific set of parameters.
/// The strategy must be pre-configured with Security and any custom parameters before passing to the runner.
/// </summary>
/// <typeparam name="TStrategy">The strategy type to backtest</typeparam>
public class BacktestRunner<TStrategy> : IDisposable where TStrategy : Strategy
{
    private readonly BacktestConfig _config;
    private readonly TStrategy _strategy;
    private readonly IPerformanceMetricsCalculator _metricsCalculator;
    private HistoryEmulationConnector? _connector;
    private TaskCompletionSource<BacktestResult<TStrategy>>? _completionSource;
    private DateTimeOffset _startTime;
    private bool _disposed;
    private DebugModeExporter? _debugExporter;
    private DebugWebAppLauncher? _webLauncher;
    private AgenticEventLogger? _agenticLogger;

    public ILogReceiver? Logger { get; set; }

    public BacktestRunner(BacktestConfig config, TStrategy strategy, IPerformanceMetricsCalculator? metricsCalculator = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _metricsCalculator = metricsCalculator ?? new PerformanceMetricsCalculator();

        if (!_config.ValidationPeriod.IsValid())
            throw new ArgumentException("Invalid validation period", nameof(config));
    }

    public async Task<BacktestResult<TStrategy>> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BacktestRunner<TStrategy>));

        _startTime = DateTimeOffset.UtcNow;
        _completionSource = new TaskCompletionSource<BacktestResult<TStrategy>>();

        try
        {
            using var registration = cancellationToken.Register(() =>
            {
                _completionSource?.TrySetCanceled(cancellationToken);
                Cleanup();
            });

            ValidateStrategy();
            ConfigureConnector();
            SubscribeToEvents();
            await LaunchDebugWebServerAsync();

            // Initialize debug mode if enabled
            InitializeDebugMode();

            // Initialize agentic logging if enabled
            await InitializeAgenticLoggingAsync();

            // Subscribe to indicators after strategy starts (they are created in OnStarted)
            if (_strategy is Strategies.CustomStrategyBase customStrategy)
            {
                if (_debugExporter != null || _agenticLogger != null)
                {
                    _strategy.ProcessStateChanged += (s) =>
                    {
                        if (s.ProcessState == ProcessStates.Started)
                        {
                            if (_debugExporter != null)
                            {
                                _debugExporter.SubscribeToIndicators(customStrategy.Indicators);
                                Logger?.AddInfoLog($"Debug mode subscribed to {customStrategy.Indicators.Count} indicators");
                            }

                            if (_agenticLogger != null)
                            {
                                _agenticLogger.SubscribeToIndicators(customStrategy.Indicators);
                                Logger?.AddInfoLog($"Agentic logging subscribed to {customStrategy.Indicators.Count} indicators");
                            }
                        }
                    };
                }
            }

            Logger?.AddInfoLog("Starting backtest...");
            _connector!.Connect();
            _connector.Start();
            _strategy.Start();

            return await _completionSource.Task;
        }
        catch (Exception ex)
        {
            Logger?.AddErrorLog(ex, "Backtest failed");
            return CreateErrorResult(ex);
        }
    }

    private void ValidateStrategy()
    {
        var workingSecurities = _strategy.GetWorkingSecurities()?.ToList();
        var hasSecurities = (workingSecurities?.Count > 0) || (_strategy.Security is not null);

        if (!hasSecurities)
        {
            throw new InvalidOperationException(
                "Strategy must have at least one security. Set Strategy.Security or override GetWorkingSecurities()");
        }

        var securityInfo = workingSecurities?.Count > 0
            ? $"{workingSecurities.Count} securities via GetWorkingSecurities()"
            : $"Security={_strategy.Security!.Id}";

        Logger?.AddInfoLog($"Strategy validated: {securityInfo}, Portfolio={_strategy.Portfolio.Name}");
    }

    private async Task LaunchDebugWebServerAsync()
    {
        // Skip web app if agentic logging is enabled (avoid startup overhead)
        if (_config.AgenticLogging?.Enabled == true)
        {
            Logger?.AddInfoLog("Agentic logging enabled. Skipping web app launch.");
            return;
        }

        if (_config.DebugMode?.Enabled != true)
            return;

        var debugConfig = _config.DebugMode;
        if (string.IsNullOrWhiteSpace(debugConfig.WebAppPath))
        {
            Logger?.AddInfoLog("Debug mode enabled but WebAppPath not configured. Skipping web server launch.");
            return;
        }

        try
        {
            _webLauncher = new DebugWebAppLauncher(
                webProjectPath: debugConfig.WebAppPath,
                serverUrl: debugConfig.WebAppUrl,
                debugPagePath: debugConfig.DebugPagePath);

            Logger?.AddInfoLog($"Launching debug web server at {debugConfig.WebAppUrl}...");

            var serverReady = await _webLauncher.EnsureServerRunningAndOpenAsync();
            if (!serverReady)
            {
                Logger?.AddWarningLog("Debug web server could not be started. Continuing without live visualization.");
            }
        }
        catch (Exception ex)
        {
            Logger?.AddWarningLog($"Failed to launch debug web server: {ex.Message}. Continuing without live visualization.");
            _webLauncher = null;
        }
    }

    private DebugModeExporter? CreateDebugExporter()
    {
        if (_config.DebugMode?.Enabled != true)
            return null;

        var debugConfig = _config.DebugMode;
        var outputPath = Path.Combine(
            debugConfig.OutputDirectory,
            "latest.jsonl"
        );

        // Create output directory if doesn't exist
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        Logger?.AddInfoLog($"Debug mode enabled. Output: {outputPath}");

        return new DebugModeExporter(
            outputPath: outputPath,
            flushIntervalMs: debugConfig.FlushIntervalMs
        );
    }

    private void InitializeDebugMode()
    {
        if (_config.DebugMode?.Enabled != true)
            return;

        // Create debug exporter
        _debugExporter = CreateDebugExporter();
        if (_debugExporter == null)
            return;

        try
        {
            // Initialize exporter with strategy
            if (_strategy is Strategies.CustomStrategyBase customStrategy)
            {
                // Extract candle interval from strategy configuration
                var candleInterval = ExtractCandleInterval(_strategy);

                // Initialize with candle interval for shift-aware indicator export
                _debugExporter.Initialize(customStrategy, candleInterval);

                // NOTE: Indicator subscription is deferred to Strategy.Started event
                // because indicators are created in OnStarted(), not yet available here

                Logger?.AddInfoLog($"Debug mode initialized with strategy (candle interval: {candleInterval?.ToString() ?? "auto-detect"})");
            }
            else
            {
                // For non-CustomStrategyBase strategies, just initialize without strategy reference
                Logger?.AddWarningLog($"Strategy type {_strategy.GetType().Name} is not CustomStrategyBase. Debug mode will have limited functionality.");
            }

            // Subscribe to connector events for candles and trades
            if (_connector != null)
            {
                _connector.CandleReceived += OnCandleReceivedForDebug;
                Logger?.AddInfoLog("Debug mode subscribed to connector candle events");
            }

            // Subscribe to strategy candle events (in addition to connector)
            // This is important because strategies may subscribe to candles independently
            _strategy.CandleReceived += OnCandleReceivedForDebug;
            Logger?.AddInfoLog("Debug mode subscribed to strategy candle events");

            // Subscribe to strategy trade events
            _strategy.OwnTradeReceived += OnOwnTradeReceivedForDebug;
            Logger?.AddInfoLog("Debug mode subscribed to trade events");
        }
        catch (Exception ex)
        {
            Logger?.AddErrorLog(ex, "Failed to initialize debug mode");
            _debugExporter?.Dispose();
            _debugExporter = null;
        }
    }

    private async Task InitializeAgenticLoggingAsync()
    {
        if (_config.AgenticLogging?.Enabled != true)
            return;

        if (_strategy is not Strategies.CustomStrategyBase customStrategy)
        {
            Logger?.AddWarningLog($"Strategy type {_strategy.GetType().Name} is not CustomStrategyBase. Agentic logging requires CustomStrategyBase.");
            return;
        }

        try
        {
            _agenticLogger = new AgenticEventLogger(customStrategy, _config.AgenticLogging);

            var strategyConfigHash = customStrategy.ParamsHash ?? "unknown";
            await _agenticLogger.StartRunAsync(
                _config.ValidationPeriod.StartDate,
                _config.ValidationPeriod.EndDate,
                strategyConfigHash);

            // Subscribe to connector events for candles
            if (_connector != null && _config.AgenticLogging.LogMarketData)
            {
                _connector.CandleReceived += OnCandleReceivedForAgentic;
                Logger?.AddInfoLog("Agentic logging subscribed to connector candle events");
            }

            // Subscribe to strategy candle events
            if (_config.AgenticLogging.LogMarketData)
            {
                _strategy.CandleReceived += OnCandleReceivedForAgentic;
                Logger?.AddInfoLog("Agentic logging subscribed to strategy candle events");
            }

            // Subscribe to strategy trade events
            if (_config.AgenticLogging.LogTrades)
            {
                _strategy.OwnTradeReceived += OnOwnTradeReceivedForAgentic;
                Logger?.AddInfoLog("Agentic logging subscribed to trade events");
            }

            Logger?.AddInfoLog("Agentic logging initialized successfully");
        }
        catch (Exception ex)
        {
            Logger?.AddErrorLog(ex, "Failed to initialize agentic logging");
            if (_agenticLogger != null)
            {
                await _agenticLogger.DisposeAsync();
                _agenticLogger = null;
            }
        }
    }

    private TimeSpan? ExtractCandleInterval(TStrategy strategy)
    {
        if (strategy is Strategies.CustomStrategyBase customStrategy)
        {
            var firstSecurity = customStrategy.Securities.FirstOrDefault();
            return firstSecurity.Value?.FirstOrDefault();
        }
        return null;
    }

    private void OnCandleReceivedForDebug(Subscription subscription, ICandleMessage candle)
    {
        if (_debugExporter == null || !_debugExporter.IsInitialized)
            return;

        try
        {
#if DEBUG
            _debugExporter.FlushBeforeCandle();
#endif

            // Get security ID from subscription or use first security from strategy
            var securityId = subscription?.SecurityId;
            if (securityId == null && _strategy is Strategies.CustomStrategyBase customStrategy)
            {
                securityId = customStrategy.Securities.Keys.FirstOrDefault()?.ToSecurityId() ?? default;
            }

            _debugExporter.CaptureCandle(candle, securityId ?? default);
        }
        catch (Exception ex)
        {
            Logger?.AddErrorLog(ex, "Error capturing candle for debug mode");
        }
    }

    private void OnOwnTradeReceivedForDebug(Subscription subscription, MyTrade trade)
    {
        if (_debugExporter == null || !_debugExporter.IsInitialized)
            return;

        try
        {
            var tradeDataPoint = new Export.TradeDataPoint
            {
                Time = new DateTimeOffset(trade.Trade.ServerTime, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                Price = (double)trade.Trade.Price,
                Volume = (double)trade.Trade.Volume,
                Side = trade.Order.Side == Sides.Buy ? "Buy" : "Sell",
                PnL = (double)(trade.PnL ?? 0m),
                OrderId = trade.Order.Id
            };

            _debugExporter.CaptureTrade(tradeDataPoint);
        }
        catch (Exception ex)
        {
            Logger?.AddErrorLog(ex, "Error capturing trade for debug mode");
        }
    }

    private void OnCandleReceivedForAgentic(Subscription subscription, ICandleMessage candle)
    {
        if (_agenticLogger == null)
            return;

        try
        {
            var securityId = subscription?.SecurityId;
            if (securityId == null && _strategy is Strategies.CustomStrategyBase customStrategy)
            {
                securityId = customStrategy.Securities.Keys.FirstOrDefault()?.ToSecurityId() ?? default;
            }

            _agenticLogger.LogCandleAsync(candle, securityId ?? default).Wait();
        }
        catch (Exception ex)
        {
            Logger?.AddErrorLog(ex, "Error logging candle for agentic debug");
        }
    }

    private void OnOwnTradeReceivedForAgentic(Subscription subscription, MyTrade trade)
    {
        if (_agenticLogger == null)
            return;

        try
        {
            var tradeDetails = new
            {
                Time = trade.Trade.ServerTime,
                Price = trade.Trade.Price,
                Volume = trade.Trade.Volume,
                Side = trade.Order.Side.ToString(),
                PnL = trade.PnL ?? 0m,
                OrderId = trade.Order.Id,
                OrderType = trade.Order.Type.ToString(),
                Commission = trade.Commission ?? 0m
            };

            _agenticLogger.LogTradeAsync(tradeDetails).Wait();
        }
        catch (Exception ex)
        {
            Logger?.AddErrorLog(ex, "Error logging trade for agentic debug");
        }
    }

    private void ConfigureConnector()
    {
        var workingSecurities = _strategy.GetWorkingSecurities()?.ToList();
        var securities = workingSecurities?.Select(s => s.sec).ToArray() ?? [];

        if (securities.Length == 0 && _strategy.Security != null)
        {
            securities = [_strategy.Security];
        }

        if (securities.Length == 0)
        {
            throw new InvalidOperationException(
                "No securities found. Either set Strategy.Security or override GetWorkingSecurities()");
        }

        Logger?.AddInfoLog($"Configuring connector with {securities.Length} security(ies): {string.Join(", ", securities.Select(s => s.Id))}");

        _strategy.Portfolio ??= Portfolio.CreateSimulator();
        if (_strategy.Portfolio.BeginValue == 0)
        {
            _strategy.Portfolio.BeginValue = 10000m;
        }

        if (string.IsNullOrEmpty(_strategy.Portfolio.Name))
        {
            _strategy.Portfolio.Name = "Simulator";
            Logger?.AddInfoLog("Portfolio Name not set, defaulting to 'Simulator'");
        }

        var innerRegistry = new StorageRegistry
        {
            DefaultDrive = new LocalMarketDataDrive(_config.HistoryPath)
        };
        // Wrap with SharedStorageRegistry to work around await using disposal bug in BasketMarketDataStorage
        var storageRegistry = new SharedStorageRegistry(innerRegistry);

        var securityProvider = new CollectionSecurityProvider(securities);
        var portfolioProvider = new CollectionPortfolioProvider([_strategy.Portfolio]);

        _connector = new HistoryEmulationConnector(
            securityProvider,
            portfolioProvider,
            storageRegistry);

        _connector.EmulationAdapter.Settings.MatchOnTouch = _config.MatchOnTouch;
        _connector.EmulationAdapter.Settings.CommissionRules = _config.CommissionRules.ToArray();

        _connector.HistoryMessageAdapter.StartDate = _config.ValidationPeriod.StartDate.UtcDateTime;
        _connector.HistoryMessageAdapter.StopDate = _config.ValidationPeriod.EndDate.UtcDateTime;
        _connector.HistoryMessageAdapter.StorageFormat = _config.StorageFormat;

        _strategy.Connector = _connector;

        if (Logger is null)
        {
            ((ILogSource)_connector).LogLevel = LogLevels.Info;
            ((ILogSource)_strategy).LogLevel = LogLevels.Info;
        }

        Logger?.AddInfoLog($"Connector configured for period {_config.ValidationPeriod.StartDate:yyyy-MM-dd} to {_config.ValidationPeriod.EndDate:yyyy-MM-dd}");
    }

    private Exception? _lastConnectorError;

    private void SubscribeToEvents()
    {
        if (_connector == null || _strategy == null)
            throw new InvalidOperationException("Connector and strategy must be configured first");

        _connector.StateChanged2 += OnConnectorStateChanged;
        _connector.Error += OnConnectorError;
        _connector.ConnectionError += OnConnectionError;
        _strategy.Error += OnStrategyError;
    }

    private void OnConnectorError(Exception error)
    {
        _lastConnectorError = error;
        Logger?.AddErrorLog(error, "Connector error occurred");
    }

    private void OnConnectionError(Exception error)
    {
        _lastConnectorError = error;
        Logger?.AddErrorLog(error, "Connection error occurred");
    }

    private void OnConnectorStateChanged(ChannelStates state)
    {
        Logger?.AddInfoLog($"Connector state changed: {state}");

        if (state == ChannelStates.Stopped)
        {
            // Backtest completed
            try
            {
                if (_connector?.IsFinished == true && _strategy != null)
                {
                    Logger?.AddInfoLog("Backtest completed successfully, calculating metrics...");
                    var result = CreateSuccessResult();
                    _completionSource?.TrySetResult(result);
                }
                else
                {
                    var errorMessage = _lastConnectorError != null
                        ? $"Backtest stopped with error: {_lastConnectorError.Message}"
                        : "Backtest stopped unexpectedly (IsFinished=false, no error captured)";
                    Logger?.AddWarningLog(errorMessage);
                    var result = CreateErrorResult(_lastConnectorError ?? new InvalidOperationException(errorMessage));
                    _completionSource?.TrySetResult(result);
                }
            }
            finally
            {
                Cleanup();
            }
        }
    }

    private void OnStrategyError(Strategy strategy, Exception error)
    {
        Logger?.AddErrorLog(error, "Strategy error occurred");
        var result = CreateErrorResult(error);
        _completionSource?.TrySetResult(result);
        Cleanup();
    }

    private BacktestResult<TStrategy> CreateSuccessResult()
    {
        if (_strategy == null)
            throw new InvalidOperationException("Strategy is null");

        var metrics = _metricsCalculator.CalculateMetrics(
            _strategy,
            _config.ValidationPeriod.StartDate,
            _config.ValidationPeriod.EndDate);

        metrics.StartTime = _config.ValidationPeriod.StartDate;
        metrics.EndTime = _config.ValidationPeriod.EndDate;

        return new BacktestResult<TStrategy>
        {
            Strategy = _strategy,
            Metrics = metrics,
            Config = _config,
            IsSuccessful = true,
            StartTime = _startTime,
            EndTime = DateTimeOffset.UtcNow
        };
    }

    private BacktestResult<TStrategy> CreateErrorResult(Exception error)
    {
        return new BacktestResult<TStrategy>
        {
            Strategy = _strategy,
            Metrics = new PerformanceMetrics(),
            Config = _config,
            IsSuccessful = false,
            ErrorMessage = error.Message,
            StartTime = _startTime,
            EndTime = DateTimeOffset.UtcNow
        };
    }

    private void Cleanup()
    {
        try
        {
            _strategy?.Stop();
            _connector?.Disconnect();

            if (_connector != null)
            {
                _connector.StateChanged2 -= OnConnectorStateChanged;
                _connector.Error -= OnConnectorError;
                _connector.ConnectionError -= OnConnectionError;

                // Unsubscribe from debug mode events
                _connector.CandleReceived -= OnCandleReceivedForDebug;

                // Unsubscribe from agentic logging events
                _connector.CandleReceived -= OnCandleReceivedForAgentic;
            }

            if (_strategy != null)
            {
                _strategy.Error -= OnStrategyError;

                // Unsubscribe from debug mode events
                _strategy.CandleReceived -= OnCandleReceivedForDebug;
                _strategy.OwnTradeReceived -= OnOwnTradeReceivedForDebug;

                // Unsubscribe from agentic logging events
                _strategy.CandleReceived -= OnCandleReceivedForAgentic;
                _strategy.OwnTradeReceived -= OnOwnTradeReceivedForAgentic;
            }

            // Cleanup debug exporter
            _debugExporter?.Cleanup();
            _debugExporter?.Dispose();
            _debugExporter = null;

            // Cleanup agentic logger
            if (_agenticLogger != null)
            {
                _agenticLogger.DisposeAsync().AsTask().Wait();
                _agenticLogger = null;
            }
        }
        catch (Exception ex)
        {
            Logger?.AddErrorLog(ex, "Error during cleanup");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Cleanup();
        _connector?.Dispose();
        _strategy?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}


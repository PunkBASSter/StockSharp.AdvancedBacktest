using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ecng.Common;
using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Testing;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Backtest;

/// <summary>
/// Single backtest runner for executing a strategy with a specific set of parameters
/// </summary>
/// <typeparam name="TStrategy">The strategy type to backtest</typeparam>
public class BacktestRunner<TStrategy> : IDisposable where TStrategy : Strategy, new()
{
    private readonly BacktestConfig _config;
    private HistoryEmulationConnector? _connector;
    private TStrategy? _strategy;
    private TaskCompletionSource<BacktestResult<TStrategy>>? _completionSource;
    private DateTimeOffset _startTime;
    private bool _disposed;

    public ILogReceiver? Logger { get; set; }

    public BacktestRunner(BacktestConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

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

            ConfigureConnector();
            _strategy = ConfigureStrategy();
            SubscribeToEvents();

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

    private void ConfigureConnector()
    {
        // Parse security ID
        var securityId = _config.SecurityId.ToSecurityId();

        // Create security
        var security = new Security
        {
            Id = _config.SecurityId,
            Code = securityId.SecurityCode,
            Board = ExchangeBoard.Associated,
        };

        // Create portfolio
        var portfolio = Portfolio.CreateSimulator();
        portfolio.Name = _config.PortfolioName;
        portfolio.BeginValue = _config.InitialCapital;

        // Setup storage registry
        var storageRegistry = new StorageRegistry
        {
            DefaultDrive = new LocalMarketDataDrive(_config.HistoryPath)
        };

        var securityProvider = new CollectionSecurityProvider(new[] { security });
        var portfolioProvider = new CollectionPortfolioProvider(new[] { portfolio });

        // Create the connector
        _connector = new HistoryEmulationConnector(
            securityProvider,
            portfolioProvider,
            storageRegistry);

        // Configure emulation settings
        _connector.EmulationAdapter.Settings.MatchOnTouch = _config.MatchOnTouch;
        _connector.EmulationAdapter.Settings.CommissionRules = _config.CommissionRules.ToArray();

        // Set date range
        _connector.HistoryMessageAdapter.StartDate = _config.ValidationPeriod.StartDate.UtcDateTime;
        _connector.HistoryMessageAdapter.StopDate = _config.ValidationPeriod.EndDate.UtcDateTime;

        // Configure logging
        if (Logger != null)
        {
            ((ILogSource)_connector).LogLevel = LogLevels.Info;
        }

        Logger?.AddInfoLog($"Connector configured: {_config.SecurityId} from {_config.ValidationPeriod.StartDate:yyyy-MM-dd} to {_config.ValidationPeriod.EndDate:yyyy-MM-dd}");
    }

    private TStrategy ConfigureStrategy()
    {
        if (_connector == null)
            throw new InvalidOperationException("Connector must be configured first");

        var strategy = new TStrategy
        {
            Security = _connector.Securities.First(),
            Portfolio = _connector.Portfolios.First(),
            Connector = _connector,
        };

        // Apply custom parameters from config
        ApplyParameters(strategy);

        if (Logger != null)
        {
            ((ILogSource)strategy).LogLevel = LogLevels.Info;
        }

        Logger?.AddInfoLog($"Strategy configured with {_config.ParamsContainer.CustomParams.Count} parameters");
        return strategy;
    }

    private void ApplyParameters(TStrategy strategy)
    {
        // Apply parameters from ParamsContainer to strategy
        // This assumes the strategy has properties matching the parameter IDs
        foreach (var param in _config.ParamsContainer.CustomParams)
        {
            var property = typeof(TStrategy).GetProperty(param.Id);
            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(strategy, Convert.ChangeType(param.Value, property.PropertyType));
                }
                catch (Exception ex)
                {
                    Logger?.AddWarningLog($"Failed to set parameter {param.Id}: {ex.Message}");
                }
            }
        }
    }

    private void SubscribeToEvents()
    {
        if (_connector == null || _strategy == null)
            throw new InvalidOperationException("Connector and strategy must be configured first");

        _connector.StateChanged2 += OnConnectorStateChanged;
        _strategy.Error += OnStrategyError;
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
                    Logger?.AddWarningLog("Backtest stopped but not finished");
                    var result = CreateErrorResult(new InvalidOperationException("Backtest stopped unexpectedly"));
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

        var metrics = MetricsCalculator.CalculateMetrics(
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
            Strategy = _strategy ?? new TStrategy(),
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
            }

            if (_strategy != null)
            {
                _strategy.Error -= OnStrategyError;
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


using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Testing;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Statistics;
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
    private HistoryEmulationConnector? _connector;
    private TaskCompletionSource<BacktestResult<TStrategy>>? _completionSource;
    private DateTimeOffset _startTime;
    private bool _disposed;

    public ILogReceiver? Logger { get; set; }

    public BacktestRunner(BacktestConfig config, TStrategy strategy)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

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

        var storageRegistry = new StorageRegistry
        {
            DefaultDrive = new LocalMarketDataDrive(_config.HistoryPath)
        };

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

        _strategy.Connector = _connector;

        if (Logger is null)
        {
            ((ILogSource)_connector).LogLevel = LogLevels.Info;
            ((ILogSource)_strategy).LogLevel = LogLevels.Info;
        }

        Logger?.AddInfoLog($"Connector configured for period {_config.ValidationPeriod.StartDate:yyyy-MM-dd} to {_config.ValidationPeriod.EndDate:yyyy-MM-dd}");
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


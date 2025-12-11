using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.Backtest;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Integration;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.Algo.Indicators;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.Integration;

public sealed class AgenticEventLogger : IAsyncDisposable
{
	private readonly AgenticLoggingSettings _settings;
	private readonly CustomStrategyBase _strategy;
	private readonly string _runId;
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly EventLogger _logger;
	private readonly List<(IIndicator indicator, Action<IIndicatorValue, IIndicatorValue> handler)> _indicatorSubscriptions = new();

	private bool _disposed;

	public AgenticEventLogger(CustomStrategyBase strategy, AgenticLoggingSettings settings)
	{
		_strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		_runId = Guid.NewGuid().ToString();

		var dbPath = Path.GetFullPath(_settings.DatabasePath);
		var dbDirectory = Path.GetDirectoryName(dbPath);
		if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
		{
			Directory.CreateDirectory(dbDirectory);
		}

		_connection = new SqliteConnection($"Data Source={dbPath}");
		_connection.Open();

		DatabaseSchema.InitializeAsync(_connection).Wait();

		_repository = new SqliteEventRepository(_connection);
		_logger = new EventLogger(_runId, _repository, _settings.BatchSize);

		_strategy.LogInfo($"Agentic debug initialized. Database: {dbPath}, RunId: {_runId}");
	}

	public async Task StartRunAsync(DateTimeOffset startTime, DateTimeOffset endTime, string strategyConfigHash)
	{
		var run = new BacktestRunEntity
		{
			Id = _runId,
			StartTime = startTime.UtcDateTime,
			EndTime = endTime.UtcDateTime,
			StrategyConfigHash = strategyConfigHash
		};

		await _repository.CreateBacktestRunAsync(run);
		_strategy.LogInfo($"Agentic debug run started: {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd}");
	}

	public async Task LogCandleAsync(ICandleMessage candle, SecurityId securityId)
	{
		if (!_settings.LogMarketData || _disposed)
			return;

		try
		{
			var candleData = new
			{
				SecurityId = securityId.ToStringId(),
				OpenTime = candle.OpenTime,
				Open = candle.OpenPrice,
				High = candle.HighPrice,
				Low = candle.LowPrice,
				Close = candle.ClosePrice,
				Volume = candle.TotalVolume
			};

			await _logger.LogEventAsync(
				EventType.MarketDataEvent,
				EventSeverity.Debug,
				EventCategory.MarketData,
				candleData);
		}
		catch (Exception ex)
		{
			_strategy.LogError($"Error logging candle: {ex.Message}");
		}
	}

	public async Task LogTradeAsync(object tradeDetails)
	{
		if (!_settings.LogTrades || _disposed)
			return;

		try
		{
			await _logger.LogTradeExecutionAsync(tradeDetails);
		}
		catch (Exception ex)
		{
			_strategy.LogError($"Error logging trade: {ex.Message}");
		}
	}

	public async Task LogOrderRejectionAsync(object rejectionDetails)
	{
		if (_disposed)
			return;

		try
		{
			await _logger.LogOrderRejectionAsync(rejectionDetails);
		}
		catch (Exception ex)
		{
			_strategy.LogError($"Error logging order rejection: {ex.Message}");
		}
	}

	public async Task LogIndicatorAsync(string indicatorName, IIndicatorValue value)
	{
		if (!_settings.LogIndicators || _disposed)
			return;

		try
		{
			var indicatorData = new
			{
				Name = indicatorName,
				Time = value.Time,
				Value = value.GetValue<object>(),
				IsFormed = value.IsFormed,
				IsFinal = value.IsFinal
			};

			await _logger.LogIndicatorCalculationAsync(indicatorData);
		}
		catch (Exception ex)
		{
			_strategy.LogError($"Error logging indicator {indicatorName}: {ex.Message}");
		}
	}

	public async Task LogPositionUpdateAsync(object positionData)
	{
		if (_disposed)
			return;

		try
		{
			await _logger.LogPositionUpdateAsync(positionData);
		}
		catch (Exception ex)
		{
			_strategy.LogError($"Error logging position update: {ex.Message}");
		}
	}

	public void SubscribeToIndicator(IIndicator indicator)
	{
		if (!_settings.LogIndicators || _disposed)
			return;

		if (indicator == null)
			throw new ArgumentNullException(nameof(indicator));

		try
		{
			Action<IIndicatorValue, IIndicatorValue> handler = (input, result) =>
			{
				if (result != null && result.IsFormed)
				{
					LogIndicatorAsync(indicator.Name, result).Wait();
				}
			};

			indicator.Changed += handler;
			_indicatorSubscriptions.Add((indicator, handler));

			_strategy.LogDebug($"Subscribed to indicator for agentic logging: {indicator.Name}");
		}
		catch (Exception ex)
		{
			_strategy.LogError($"Failed to subscribe to indicator {indicator.Name}: {ex.Message}");
		}
	}

	public void SubscribeToIndicators(IEnumerable<IIndicator> indicators)
	{
		if (!_settings.LogIndicators || _disposed)
			return;

		if (indicators == null)
			throw new ArgumentNullException(nameof(indicators));

		try
		{
			var count = 0;
			foreach (var indicator in indicators)
			{
				SubscribeToIndicator(indicator);
				count++;
			}

			_strategy.LogInfo($"Subscribed to {count} indicators for agentic logging");
		}
		catch (Exception ex)
		{
			_strategy.LogError($"Error subscribing to indicators: {ex.Message}");
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		_disposed = true;

		try
		{
			foreach (var (indicator, handler) in _indicatorSubscriptions)
			{
				try
				{
					indicator.Changed -= handler;
				}
				catch
				{
					// Ignore unsubscribe errors
				}
			}
			_indicatorSubscriptions.Clear();

			await _logger.DisposeAsync();
			await _connection.DisposeAsync();

			_strategy.LogInfo("Agentic debug cleanup completed");
		}
		catch (Exception ex)
		{
			_strategy.LogError($"Error during agentic debug cleanup: {ex.Message}");
		}
	}
}

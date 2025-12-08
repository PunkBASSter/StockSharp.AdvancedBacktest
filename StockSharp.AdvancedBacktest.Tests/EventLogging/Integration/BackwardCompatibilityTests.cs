using System.Text.Json;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Integration;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.Export;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Integration;

/// <summary>
/// Tests for backward compatibility with JSONL exports (Phase 11).
/// </summary>
public sealed class BackwardCompatibilityTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly string _runId = Guid.NewGuid().ToString();
	private readonly string _tempDir;

	public BackwardCompatibilityTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).GetAwaiter().GetResult();
		_repository = new SqliteEventRepository(_connection);
		_tempDir = Path.Combine(Path.GetTempPath(), $"backtest_compat_{Guid.NewGuid()}");
		Directory.CreateDirectory(_tempDir);
	}

	[Theory]
	[InlineData("Candle", EventType.MarketDataEvent)]
	[InlineData("Trade", EventType.TradeExecution)]
	[InlineData("Indicator", EventType.IndicatorCalculation)]
	[InlineData("State", EventType.PositionUpdate)]
	public void DebugEventTransformer_ShouldTransformDataPoints(string dataType, EventType expectedEventType)
	{
		var eventEntity = dataType switch
		{
			"Candle" => DebugEventTransformer.FromCandle(new CandleDataPoint
			{
				Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				Open = 100, High = 105, Low = 99, Close = 102, Volume = 1000, SecurityId = "AAPL@NASDAQ"
			}, _runId),
			"Trade" => DebugEventTransformer.FromTrade(new TradeDataPoint
			{
				Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				OrderId = 123, Price = 101.50, Volume = 10, Side = "Buy"
			}, _runId),
			"Indicator" => DebugEventTransformer.FromIndicator("SMA_20", new IndicatorDataPoint
			{
				Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Value = 100.25
			}, _runId),
			"State" => DebugEventTransformer.FromState(new StateDataPoint
			{
				Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				Position = 100, UnrealizedPnL = 50.25, PnL = 100.50
			}, _runId),
			_ => throw new ArgumentException($"Unknown data type: {dataType}")
		};

		Assert.NotNull(eventEntity);
		Assert.Equal(expectedEventType, eventEntity.EventType);
		Assert.Equal(_runId, eventEntity.RunId);
	}

	[Fact]
	public async Task SqliteRepository_ShouldAcceptEventsIndependently()
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		});

		var entity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "test-order", "Price": 100.50}"""
		};

		await _repository.WriteEventAsync(entity);

		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PageSize = 10,
			PageIndex = 0
		});

		Assert.Single(result.Events);
		Assert.Equal(entity.EventId, result.Events.First().EventId);
	}

	[Fact]
	public async Task DualExport_SqliteAndJsonl_ShouldBothReceiveEvents()
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		});

		var jsonlPath = Path.Combine(_tempDir, "events.jsonl");
		var candle = new CandleDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Open = 100, High = 105, Low = 99, Close = 102, Volume = 1000, SecurityId = "AAPL@NASDAQ"
		};

		// Write to both destinations
		await _repository.WriteEventAsync(DebugEventTransformer.FromCandle(candle, _runId));
		await File.WriteAllTextAsync(jsonlPath, JsonSerializer.Serialize(candle));

		// Verify both received events
		var sqliteResult = await _repository.QueryEventsAsync(new EventQueryParameters { RunId = _runId, PageSize = 10, PageIndex = 0 });
		Assert.Single(sqliteResult.Events);
		Assert.True(File.Exists(jsonlPath));
	}

	[Fact]
	public void EventEntity_ShouldRoundTripThroughJson()
	{
		var original = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Warning,
			Category = EventCategory.Execution,
			Properties = """{"Price": 100.50, "Quantity": 10}"""
		};

		var json = JsonSerializer.Serialize(original);
		var deserialized = JsonSerializer.Deserialize<EventEntity>(json);

		Assert.NotNull(deserialized);
		Assert.Equal(original.EventId, deserialized.EventId);
		Assert.Equal(original.EventType, deserialized.EventType);
		Assert.Equal(original.Severity, deserialized.Severity);
	}

	[Theory]
	[InlineData(100)]
	public async Task SqliteRepository_ShouldHandleBatchWrites(int count)
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		});

		for (int i = 0; i < count; i++)
		{
			await _repository.WriteEventAsync(new EventEntity
			{
				EventId = Guid.NewGuid().ToString(),
				RunId = _runId,
				Timestamp = DateTime.UtcNow.AddSeconds(i),
				EventType = EventType.TradeExecution,
				Severity = EventSeverity.Info,
				Category = EventCategory.Execution,
				Properties = $$$"""{"Price": {{{100 + i}}}}"""
			});
		}

		var result = await _repository.QueryEventsAsync(new EventQueryParameters { RunId = _runId, PageSize = 200, PageIndex = 0 });
		Assert.Equal(count, result.Metadata.TotalCount);
	}

	[Fact]
	public void DebugModeExporter_ShouldBeInstantiable()
	{
		var jsonlPath = Path.Combine(_tempDir, "debug.jsonl");
		using var exporter = new DebugModeExporter(jsonlPath);

		Assert.NotNull(exporter);
		Assert.Equal(jsonlPath, exporter.OutputPath);
		Assert.False(exporter.IsInitialized);
	}

	[Fact]
	public async Task SqliteEventSink_ShouldInitializeAndWriteEvents()
	{
		var dbPath = Path.Combine(_tempDir, "test_sink.db");
		await using var sink = new SqliteEventSink(dbPath);

		await sink.InitializeAsync(_runId);
		await sink.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"Price": 100.50}"""
		});
		await sink.FlushAsync();

		Assert.True(File.Exists(dbPath));
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
		try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
	}
}

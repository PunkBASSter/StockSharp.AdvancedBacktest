using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.McpServer.Tools;

public sealed class AggregateMetricsToolTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly AggregateMetricsTool _tool;
	private readonly string _runId = Guid.NewGuid().ToString();

	public AggregateMetricsToolTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).GetAwaiter().GetResult();
		_repository = new SqliteEventRepository(_connection);
		_tool = new AggregateMetricsTool(_repository);
	}

	[Fact]
	public async Task AggregateMetrics_WithValidParameters_ShouldReturnAggregations()
	{
		await CreateBacktestRun();
		await CreateTradeEvents(10);

		var result = await _tool.AggregateMetricsAsync(
			_runId,
			"TradeExecution",
			"$.Price",
			["count", "avg"]);

		Assert.NotNull(result);
		Assert.Equal(10, result.Aggregations.Count);
		Assert.NotNull(result.Aggregations.Avg);
	}

	[Fact]
	public async Task AggregateMetrics_WithTimeRange_ShouldFilterCorrectly()
	{
		await CreateBacktestRun();
		// Use fixed base time to avoid timezone issues
		var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

		for (int i = 0; i < 5; i++)
		{
			await CreateTradeEventWithFixedTime(100, baseTime.AddMinutes(i * 10));
		}

		var result = await _tool.AggregateMetricsAsync(
			_runId,
			"TradeExecution",
			"$.Price",
			["count"],
			startTime: baseTime.AddMinutes(5).ToString("o"),
			endTime: baseTime.AddMinutes(35).ToString("o"));

		Assert.Equal(3, result.Aggregations.Count);
	}

	[Fact]
	public async Task AggregateMetrics_WithInvalidRunId_ShouldReturnZeroCount()
	{
		var result = await _tool.AggregateMetricsAsync(
			Guid.NewGuid().ToString(),
			"TradeExecution",
			"$.Price",
			["count"]);

		Assert.Equal(0, result.Aggregations.Count);
	}

	[Fact]
	public async Task AggregateMetrics_WithInvalidEventType_ShouldThrow()
	{
		await Assert.ThrowsAsync<ArgumentException>(() =>
			_tool.AggregateMetricsAsync(
				_runId,
				"InvalidEventType",
				"$.Price",
				["count"]));
	}

	[Fact]
	public async Task AggregateMetrics_WithInvalidPropertyPath_ShouldThrow()
	{
		await CreateBacktestRun();

		await Assert.ThrowsAsync<ArgumentException>(() =>
			_tool.AggregateMetricsAsync(
				_runId,
				"TradeExecution",
				"invalid-path",
				["count"]));
	}

	[Fact]
	public async Task AggregateMetrics_WithEmptyAggregations_ShouldReturnCountOnly()
	{
		await CreateBacktestRun();
		await CreateTradeEvents(5);

		var result = await _tool.AggregateMetricsAsync(
			_runId,
			"TradeExecution",
			"$.Price",
			[]);

		Assert.Equal(5, result.Aggregations.Count);
	}

	[Fact]
	public async Task AggregateMetrics_AllAggregations_ShouldReturnAllValues()
	{
		await CreateBacktestRun();
		for (int i = 0; i < 5; i++)
		{
			await CreateTradeEvent(100 + i * 10);
		}

		var result = await _tool.AggregateMetricsAsync(
			_runId,
			"TradeExecution",
			"$.Price",
			["count", "sum", "avg", "min", "max", "stddev"]);

		Assert.Equal(5, result.Aggregations.Count);
		Assert.NotNull(result.Aggregations.Sum);
		Assert.NotNull(result.Aggregations.Avg);
		Assert.NotNull(result.Aggregations.Min);
		Assert.NotNull(result.Aggregations.Max);
		Assert.NotNull(result.Aggregations.StdDev);
	}

	[Fact]
	public async Task AggregateMetrics_ShouldIncludeMetadata()
	{
		await CreateBacktestRun();
		await CreateTradeEvents(5);

		var result = await _tool.AggregateMetricsAsync(
			_runId,
			"TradeExecution",
			"$.Price",
			["count"]);

		Assert.Equal("TradeExecution", result.Metadata.EventType);
		Assert.Equal("$.Price", result.Metadata.PropertyPath);
		Assert.True(result.Metadata.QueryTimeMs >= 0);
	}

	[Fact]
	public async Task AggregateMetrics_WithNullRunId_ShouldThrow()
	{
		await Assert.ThrowsAsync<ArgumentException>(() =>
			_tool.AggregateMetricsAsync(
				null!,
				"TradeExecution",
				"$.Price",
				["count"]));
	}

	[Fact]
	public async Task AggregateMetrics_WithInvalidStartTime_ShouldThrow()
	{
		await Assert.ThrowsAsync<ArgumentException>(() =>
			_tool.AggregateMetricsAsync(
				_runId,
				"TradeExecution",
				"$.Price",
				["count"],
				startTime: "not-a-valid-date"));
	}

	private async Task CreateBacktestRun()
	{
		var run = new BacktestRunEntity
		{
			Id = _runId,
			StartTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			StrategyConfigHash = new string('a', 64)
		};
		await _repository.CreateBacktestRunAsync(run);
	}

	private async Task CreateTradeEvents(int count)
	{
		for (int i = 0; i < count; i++)
		{
			await CreateTradeEvent(100 + i);
		}
	}

	private async Task CreateTradeEvent(decimal price, DateTime? timestamp = null)
	{
		var entity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = timestamp ?? DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = $$$"""{"OrderId": "{{{Guid.NewGuid()}}}", "Price": {{{price}}}, "Quantity": 10}"""
		};
		await _repository.WriteEventAsync(entity);
	}

	private async Task CreateTradeEventWithFixedTime(decimal price, DateTime timestamp)
	{
		var entity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = timestamp,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = $$$"""{"OrderId": "{{{Guid.NewGuid()}}}", "Price": {{{price}}}, "Quantity": 10}"""
		};
		await _repository.WriteEventAsync(entity);
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

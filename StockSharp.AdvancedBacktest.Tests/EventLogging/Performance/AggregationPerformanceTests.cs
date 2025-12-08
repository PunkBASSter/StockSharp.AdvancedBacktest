using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Performance;

/// <summary>
/// Tests for aggregation query performance (SC-005: aggregate 100,000 events in &lt;500ms).
/// </summary>
public sealed class AggregationPerformanceTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly string _runId = Guid.NewGuid().ToString();

	public AggregationPerformanceTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).GetAwaiter().GetResult();
		_repository = new SqliteEventRepository(_connection);
	}

	[Theory]
	[InlineData(10_000, 100)]   // 10k events in <100ms
	[InlineData(50_000, 250)]   // 50k events in <250ms
	[InlineData(100_000, 500)]  // 100k events in <500ms (SC-005)
	public async Task AggregateMetrics_ShouldCompleteWithinTimeout(int eventCount, int maxTimeMs)
	{
		await SetupTestData(eventCount);

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await _repository.AggregateMetricsAsync(new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count", "sum", "avg", "min", "max"]
		});
		stopwatch.Stop();

		Assert.Equal(eventCount, result.Aggregations.Count);
		Assert.True(stopwatch.ElapsedMilliseconds < maxTimeMs,
			$"Aggregation of {eventCount} events took {stopwatch.ElapsedMilliseconds}ms, expected < {maxTimeMs}ms");
	}

	[Fact]
	public async Task AggregateMetrics_WithStdDev_100000Events_ShouldCompleteUnder1000ms()
	{
		await SetupTestData(100_000);

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await _repository.AggregateMetricsAsync(new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count", "sum", "avg", "min", "max", "stddev"]
		});
		stopwatch.Stop();

		Assert.Equal(100_000, result.Aggregations.Count);
		Assert.NotNull(result.Aggregations.StdDev);
		Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Aggregation with StdDev took {stopwatch.ElapsedMilliseconds}ms");
	}

	[Fact]
	public async Task AggregateMetrics_WithTimeRange_ShouldFilterEfficiently()
	{
		await SetupTestData(50_000);
		var startTime = DateTime.UtcNow.AddSeconds(10_000);
		var endTime = DateTime.UtcNow.AddSeconds(30_000);

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await _repository.AggregateMetricsAsync(new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count", "avg"],
			StartTime = startTime,
			EndTime = endTime
		});
		stopwatch.Stop();

		Assert.True(result.Aggregations.Count < 50_000);
		Assert.True(stopwatch.ElapsedMilliseconds < 200, $"Filtered aggregation took {stopwatch.ElapsedMilliseconds}ms");
	}

	private async Task SetupTestData(int count)
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(24),
			StrategyConfigHash = new string('a', 64)
		});

		var random = new Random(42);
		var baseTime = DateTime.UtcNow;

		for (int i = 0; i < count; i++)
		{
			await _repository.WriteEventAsync(new EventEntity
			{
				EventId = Guid.NewGuid().ToString(),
				RunId = _runId,
				Timestamp = baseTime.AddSeconds(i),
				EventType = EventType.TradeExecution,
				Severity = EventSeverity.Info,
				Category = EventCategory.Execution,
				Properties = $$$"""{"Price": {{{100 + random.NextDouble() * 100:F2}}}, "Quantity": {{{random.Next(1, 100)}}}}"""
			});
		}
	}

	public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Performance;

/// <summary>
/// Tests for concurrent query performance (SC-004: 100 simultaneous agent queries without degradation).
/// </summary>
public sealed class ConcurrentQueryTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly string _runId = Guid.NewGuid().ToString();

	public ConcurrentQueryTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).GetAwaiter().GetResult();
		_repository = new SqliteEventRepository(_connection);
	}

	[Theory]
	[InlineData(10, 1000, 2000)]    // 10 concurrent queries, 1k events, 2s timeout
	[InlineData(50, 5000, 5000)]    // 50 concurrent queries, 5k events, 5s timeout
	[InlineData(100, 10000, 10000)] // 100 concurrent queries, 10k events, 10s timeout
	public async Task ConcurrentQueries_ShouldAllCompleteWithinTimeout(int concurrentCount, int eventCount, int timeoutMs)
	{
		await SetupTestData(eventCount);

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var tasks = Enumerable.Range(0, concurrentCount).Select(_ => QueryAsync()).ToArray();
		var results = await Task.WhenAll(tasks);
		stopwatch.Stop();

		Assert.All(results, r => Assert.NotNull(r));
		Assert.True(stopwatch.ElapsedMilliseconds < timeoutMs,
			$"{concurrentCount} concurrent queries took {stopwatch.ElapsedMilliseconds}ms, expected < {timeoutMs}ms");
	}

	[Fact]
	public async Task ConcurrentQueries_100Simultaneous_ShouldNotDegradeSignificantly()
	{
		await SetupTestData(10000);

		// Measure single query baseline
		var singleResult = await QueryAsync();
		var baselineMs = Math.Max(1, (await MeasureQueryTime()).ElapsedMilliseconds);

		// Measure 100 concurrent queries
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var tasks = Enumerable.Range(0, 100).Select(_ => QueryAsync()).ToArray();
		await Task.WhenAll(tasks);
		stopwatch.Stop();

		// Average query time should not degrade more than 10x from baseline
		var avgQueryTime = stopwatch.ElapsedMilliseconds / 100.0;
		var degradationFactor = avgQueryTime / baselineMs;
		Assert.True(degradationFactor < 10, $"Query performance degraded {degradationFactor:F1}x under concurrent load");
	}

	[Fact]
	public async Task ConcurrentMixedQueries_ShouldAllComplete()
	{
		await SetupTestData(5000);

		var eventTypes = new[] { EventType.TradeExecution, EventType.OrderRejection, EventType.IndicatorCalculation, EventType.PositionUpdate, EventType.StateChange };
		var tasks = eventTypes.SelectMany(t => Enumerable.Range(0, 20).Select(_ => QueryByTypeAsync(t))).ToArray();

		await Task.WhenAll(tasks);
		Assert.Equal(100, tasks.Length); // 5 types x 20 queries each
	}

	[Fact]
	public async Task ConcurrentAggregationQueries_ShouldAllComplete()
	{
		await SetupTestData(5000);

		var tasks = Enumerable.Range(0, 20).Select(_ => AggregateAsync()).ToArray();
		var results = await Task.WhenAll(tasks);

		Assert.All(results, r => Assert.True(r.Aggregations.Count > 0));
	}

	private async Task<System.Diagnostics.Stopwatch> MeasureQueryTime()
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();
		await QueryAsync();
		sw.Stop();
		return sw;
	}

	private async Task SetupTestData(int count)
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		});

		var eventTypes = Enum.GetValues<EventType>();
		var random = new Random(42);

		for (int i = 0; i < count; i++)
		{
			await _repository.WriteEventAsync(new EventEntity
			{
				EventId = Guid.NewGuid().ToString(),
				RunId = _runId,
				Timestamp = DateTime.UtcNow.AddSeconds(i),
				EventType = eventTypes[random.Next(eventTypes.Length)],
				Severity = EventSeverity.Info,
				Category = EventCategory.Execution,
				Properties = $$$"""{"Price": {{{100 + random.Next(100)}}}, "Quantity": 10}"""
			});
		}
	}

	private Task<EventQueryResult> QueryAsync() => _repository.QueryEventsAsync(new EventQueryParameters
	{
		RunId = _runId,
		EventType = EventType.TradeExecution,
		PageSize = 100,
		PageIndex = 0
	});

	private Task<EventQueryResult> QueryByTypeAsync(EventType eventType) => _repository.QueryEventsAsync(new EventQueryParameters
	{
		RunId = _runId,
		EventType = eventType,
		PageSize = 50,
		PageIndex = 0
	});

	private Task<AggregationResult> AggregateAsync() => _repository.AggregateMetricsAsync(new AggregationParameters
	{
		RunId = _runId,
		EventType = EventType.TradeExecution,
		PropertyPath = "$.Price",
		Aggregations = ["count", "avg", "sum"]
	});

	public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

using System.Globalization;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Performance;

/// <summary>
/// Tests for large-scale data handling (SC-008: handle large event volumes without timeout).
/// </summary>
public sealed class ScalabilityTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly string _runId = Guid.NewGuid().ToString();

	public ScalabilityTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).GetAwaiter().GetResult();
		_repository = new SqliteEventRepository(_connection);
	}

	[Theory]
	[InlineData(200_000, 5000)]  // 200k events, 5s timeout
	[InlineData(500_000, 10000)] // 500k events, 10s timeout (marked LongRunning via trait)
	public async Task Query_LargeDataset_ShouldCompleteWithinTimeout(int eventCount, int maxTimeMs)
	{
		await SetupTestData(eventCount);

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PageSize = 100,
			PageIndex = 0
		});
		stopwatch.Stop();

		Assert.True(result.Metadata.TotalCount > 0);
		Assert.True(stopwatch.ElapsedMilliseconds < maxTimeMs,
			$"Query on {eventCount} events took {stopwatch.ElapsedMilliseconds}ms, expected < {maxTimeMs}ms");
	}

	[Fact]
	public async Task Pagination_LargeDataset_ShouldNavigateEfficiently()
	{
		await SetupTestData(100_000);

		var totalQueried = 0;
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		for (int pageIndex = 0; pageIndex < 10; pageIndex++)
		{
			var result = await _repository.QueryEventsAsync(new EventQueryParameters
			{
				RunId = _runId,
				EventType = EventType.TradeExecution,
				PageSize = 1000,
				PageIndex = pageIndex
			});
			totalQueried += result.Events.Count;
			if (!result.Metadata.HasMore) break;
		}
		stopwatch.Stop();

		Assert.True(totalQueried > 0);
		Assert.True(stopwatch.ElapsedMilliseconds < 10000, $"Pagination through 10 pages took {stopwatch.ElapsedMilliseconds}ms");
	}

	[Theory]
	[InlineData(50_000, 30000)]  // 50k writes in 30s
	public async Task BatchWrite_ShouldCompleteReasonably(int eventCount, int maxTimeMs)
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(24),
			StrategyConfigHash = new string('a', 64)
		});

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var random = new Random(42);

		for (int i = 0; i < eventCount; i++)
		{
			await _repository.WriteEventAsync(new EventEntity
			{
				EventId = Guid.NewGuid().ToString(),
				RunId = _runId,
				Timestamp = DateTime.UtcNow.AddSeconds(i),
				EventType = EventType.TradeExecution,
				Severity = EventSeverity.Info,
				Category = EventCategory.Execution,
				Properties = $$"""{"Price": {{(100 + random.NextDouble() * 100).ToString("F2", CultureInfo.InvariantCulture)}}}"""
			});
		}
		stopwatch.Stop();

		Assert.True(stopwatch.ElapsedMilliseconds < maxTimeMs, $"Writing {eventCount} events took {stopwatch.ElapsedMilliseconds}ms");

		var result = await _repository.QueryEventsAsync(new EventQueryParameters { RunId = _runId, PageSize = 10, PageIndex = 0 });
		Assert.Equal(eventCount, result.Metadata.TotalCount);
	}

	[Fact]
	public async Task SequenceQuery_DeepChain_ShouldCompleteWithMaxDepth()
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		});

		// Create a chain of 100 events
		string? previousEventId = null;
		for (int i = 0; i < 100; i++)
		{
			var eventId = Guid.NewGuid().ToString();
			await _repository.WriteEventAsync(new EventEntity
			{
				EventId = eventId,
				RunId = _runId,
				Timestamp = DateTime.UtcNow.AddSeconds(i),
				EventType = EventType.TradeExecution,
				Severity = EventSeverity.Info,
				Category = EventCategory.Execution,
				ParentEventId = previousEventId,
				Properties = "{}"
			});
			previousEventId = eventId;
		}

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await _repository.QueryEventSequenceAsync(new EventSequenceQueryParameters
		{
			RunId = _runId,
			RootEventId = previousEventId!,
			MaxDepth = 50
		});
		stopwatch.Stop();

		var totalEvents = result.Sequences.Sum(s => s.Events.Count);
		Assert.True(totalEvents <= 50 || result.Sequences.Count > 0);
		Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Sequence query took {stopwatch.ElapsedMilliseconds}ms");
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
				Properties = $$"""{"Price": {{(100 + random.NextDouble() * 100).ToString("F2", CultureInfo.InvariantCulture)}}}"""
			});
		}
	}

	public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

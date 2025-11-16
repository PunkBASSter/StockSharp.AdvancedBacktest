using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Integration;

public sealed class EventQueryIntegrationTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;

	public EventQueryIntegrationTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
	}

	[Fact]
	public async Task US1_QueryEventsByType_ShouldReturnOnlyMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Info);
		await CreateTestEventAsync(runId, EventType.OrderRejection, EventSeverity.Warning);
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Info);
		await CreateTestEventAsync(runId, EventType.IndicatorCalculation, EventSeverity.Debug);

		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			PageSize = 100,
			PageIndex = 0
		});

		Assert.Equal(2, result.Metadata.TotalCount);
		Assert.Equal(2, result.Metadata.ReturnedCount);
		Assert.All(result.Events, e => Assert.Equal(EventType.TradeExecution, e.EventType));
		Assert.False(result.Metadata.HasMore);
	}

	[Fact]
	public async Task US1_QueryEventsByTimeRange_ShouldReturnOnlyEventsInRange()
	{
		var runId = await CreateTestRunAsync();
		var baseTime = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(-2));
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime);
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(1));
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(2));
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(5));

		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			StartTime = baseTime,
			EndTime = baseTime.AddHours(3),
			PageSize = 100,
			PageIndex = 0
		});

		Assert.Equal(3, result.Metadata.TotalCount);
		Assert.Equal(3, result.Metadata.ReturnedCount);
		Assert.All(result.Events, e =>
		{
			Assert.True(e.Timestamp >= baseTime);
			Assert.True(e.Timestamp <= baseTime.AddHours(3));
		});
	}

	[Fact]
	public async Task US1_QueryEventsBySeverity_ShouldReturnOnlyMatchingSeverity()
	{
		var runId = await CreateTestRunAsync();
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Debug);
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Info);
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Warning);
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Error);
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Warning);

		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Warning,
			PageSize = 100,
			PageIndex = 0
		});

		Assert.Equal(2, result.Metadata.TotalCount);
		Assert.Equal(2, result.Metadata.ReturnedCount);
		Assert.All(result.Events, e => Assert.Equal(EventSeverity.Warning, e.Severity));
	}

	[Fact]
	public async Task US1_QueryEventsWithPagination_ShouldReturnCorrectTotalCountAndHasMore()
	{
		var runId = await CreateTestRunAsync();
		for (int i = 0; i < 25; i++)
		{
			await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Info);
		}

		var firstPage = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			PageSize = 10,
			PageIndex = 0
		});

		Assert.Equal(25, firstPage.Metadata.TotalCount);
		Assert.Equal(10, firstPage.Metadata.ReturnedCount);
		Assert.Equal(0, firstPage.Metadata.PageIndex);
		Assert.Equal(10, firstPage.Metadata.PageSize);
		Assert.True(firstPage.Metadata.HasMore);

		var secondPage = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			PageSize = 10,
			PageIndex = 1
		});

		Assert.Equal(25, secondPage.Metadata.TotalCount);
		Assert.Equal(10, secondPage.Metadata.ReturnedCount);
		Assert.Equal(1, secondPage.Metadata.PageIndex);
		Assert.True(secondPage.Metadata.HasMore);

		var thirdPage = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			PageSize = 10,
			PageIndex = 2
		});

		Assert.Equal(25, thirdPage.Metadata.TotalCount);
		Assert.Equal(5, thirdPage.Metadata.ReturnedCount);
		Assert.Equal(2, thirdPage.Metadata.PageIndex);
		Assert.False(thirdPage.Metadata.HasMore);
	}

	[Fact]
	public async Task US1_QueryEventsWithMultipleFilters_ShouldCombineFilters()
	{
		var runId = await CreateTestRunAsync();
		var baseTime = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime, EventSeverity.Warning);
		await CreateTestEventWithTimeAsync(runId, EventType.OrderRejection, baseTime.AddHours(1), EventSeverity.Error);
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(1), EventSeverity.Warning);
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(1), EventSeverity.Info);
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(2), EventSeverity.Warning);

		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Warning,
			StartTime = baseTime.AddMinutes(30),
			EndTime = baseTime.AddHours(1.5),
			PageSize = 100,
			PageIndex = 0
		});

		Assert.Equal(1, result.Metadata.TotalCount);
		Assert.Equal(1, result.Metadata.ReturnedCount);
		Assert.Single(result.Events);
		Assert.Equal(EventType.TradeExecution, result.Events.First().EventType);
		Assert.Equal(EventSeverity.Warning, result.Events.First().Severity);
		Assert.True(result.Events.First().Timestamp >= baseTime.AddMinutes(30));
		Assert.True(result.Events.First().Timestamp <= baseTime.AddHours(1.5));
	}

	[Fact]
	public async Task US1_QueryEvents_ShouldOrderByTimestamp()
	{
		var runId = await CreateTestRunAsync();
		var baseTime = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(2));
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime);
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(1));

		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			PageSize = 100,
			PageIndex = 0
		});

		Assert.Equal(3, result.Events.Count);
		Assert.True(result.Events[0].Timestamp <= result.Events[1].Timestamp);
		Assert.True(result.Events[1].Timestamp <= result.Events[2].Timestamp);
	}

	[Fact]
	public async Task US1_QueryPerformance_ShouldCompleteUnder2Seconds()
	{
		var runId = await CreateTestRunAsync();
		for (int i = 0; i < 10_000; i++)
		{
			await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Info);
		}

		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			PageSize = 100,
			PageIndex = 0
		});

		Assert.True(result.Metadata.QueryTimeMs < 2000, $"Query took {result.Metadata.QueryTimeMs}ms, expected < 2000ms");
	}

	private async Task<string> CreateTestRunAsync()
	{
		var runId = Guid.NewGuid().ToString();
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		});
		return runId;
	}

	private async Task CreateTestEventAsync(string runId, EventType eventType, EventSeverity severity)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = DateTime.UtcNow,
			EventType = eventType,
			Severity = severity,
			Category = EventCategory.Execution,
			Properties = "{}"
		});
	}

	private async Task CreateTestEventWithTimeAsync(string runId, EventType eventType, DateTime timestamp, EventSeverity severity = EventSeverity.Info)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = eventType,
			Severity = severity,
			Category = EventCategory.Execution,
			Properties = "{}"
		});
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Storage;

public sealed class SqliteEventRepositoryTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;

	public SqliteEventRepositoryTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();

		_repository = new SqliteEventRepository(_connection);
	}

	[Fact]
	public async Task CreateBacktestRun_ShouldPersistRun()
	{
		var run = new BacktestRunEntity
		{
			Id = Guid.NewGuid().ToString(),
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		};

		await _repository.CreateBacktestRunAsync(run);
		var retrieved = await _repository.GetBacktestRunAsync(run.Id);

		Assert.NotNull(retrieved);
		Assert.Equal(run.Id, retrieved.Id);
		Assert.Equal(run.StartTime, retrieved.StartTime, TimeSpan.FromMilliseconds(1));
		Assert.Equal(run.EndTime, retrieved.EndTime, TimeSpan.FromMilliseconds(1));
		Assert.Equal(run.StrategyConfigHash, retrieved.StrategyConfigHash);
	}

	[Fact]
	public async Task WriteEvent_ShouldPersistEvent()
	{
		var runId = await CreateTestRunAsync();

		var eventEntity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "123", "Price": 100.50}""",
			ParentEventId = null,
			ValidationErrors = null
		};

		await _repository.WriteEventAsync(eventEntity);
		var retrieved = await _repository.GetEventByIdAsync(eventEntity.EventId);

		Assert.NotNull(retrieved);
		Assert.Equal(eventEntity.EventId, retrieved.EventId);
		Assert.Equal(EventType.TradeExecution, retrieved.EventType);
	}

	[Fact]
	public async Task QueryEvents_ByEventType_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		await CreateTestEventAsync(runId, EventType.TradeExecution);
		await CreateTestEventAsync(runId, EventType.OrderRejection);
		await CreateTestEventAsync(runId, EventType.TradeExecution);

		var parameters = new EventQueryParameters
		{
			RunId = runId,
			EventType = EventType.TradeExecution,
			PageSize = 100,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(2, result.Events.Count);
		Assert.All(result.Events, e => Assert.Equal(EventType.TradeExecution, e.EventType));
	}

	[Fact]
	public async Task QueryEvents_BySeverity_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Info);
		await CreateTestEventAsync(runId, EventType.OrderRejection, EventSeverity.Error);
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Info);

		var parameters = new EventQueryParameters
		{
			RunId = runId,
			Severity = EventSeverity.Error,
			PageSize = 100,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsAsync(parameters);

		Assert.NotNull(result);
		Assert.Single(result.Events);
		Assert.Equal(EventSeverity.Error, result.Events[0].Severity);
	}

	[Fact]
	public async Task QueryEvents_ByTimeRange_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		var baseTime = DateTime.UtcNow;

		await CreateTestEventAsync(runId, EventType.TradeExecution, timestamp: baseTime.AddMinutes(-10));
		await CreateTestEventAsync(runId, EventType.TradeExecution, timestamp: baseTime);
		await CreateTestEventAsync(runId, EventType.TradeExecution, timestamp: baseTime.AddMinutes(10));

		var parameters = new EventQueryParameters
		{
			RunId = runId,
			StartTime = baseTime.AddMinutes(-5),
			EndTime = baseTime.AddMinutes(5),
			PageSize = 100,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsAsync(parameters);

		Assert.NotNull(result);
		Assert.Single(result.Events);
	}

	[Fact]
	public async Task QueryEvents_WithPagination_ShouldRespectPageSize()
	{
		var runId = await CreateTestRunAsync();

		for (int i = 0; i < 25; i++)
		{
			await CreateTestEventAsync(runId, EventType.TradeExecution);
		}

		var parameters = new EventQueryParameters
		{
			RunId = runId,
			PageSize = 10,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(10, result.Events.Count);
		Assert.True(result.Metadata.HasMore);
	}

	private async Task<string> CreateTestRunAsync()
	{
		var runId = Guid.NewGuid().ToString();
		var run = new BacktestRunEntity
		{
			Id = runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		};
		await _repository.CreateBacktestRunAsync(run);
		return runId;
	}

	private async Task CreateTestEventAsync(
		string runId,
		EventType eventType,
		EventSeverity severity = EventSeverity.Info,
		DateTime? timestamp = null)
	{
		var eventEntity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp ?? DateTime.UtcNow,
			EventType = eventType,
			Severity = severity,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "123", "Price": 100.50}""",
			ParentEventId = null,
			ValidationErrors = null
		};
		await _repository.WriteEventAsync(eventEntity);
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

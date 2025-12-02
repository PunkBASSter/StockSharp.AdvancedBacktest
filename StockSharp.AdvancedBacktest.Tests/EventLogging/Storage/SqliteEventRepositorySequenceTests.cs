using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Storage;

public sealed class SqliteEventRepositorySequenceTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;

	public SqliteEventRepositorySequenceTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithRootEventId_ShouldReturnChain()
	{
		var runId = await CreateTestRunAsync();

		var rootEventId = Guid.NewGuid().ToString();
		var childEventId = Guid.NewGuid().ToString();
		var grandchildEventId = Guid.NewGuid().ToString();

		await CreateTestEventAsync(runId, rootEventId, EventType.TradeExecution, parentEventId: null);
		await CreateTestEventAsync(runId, childEventId, EventType.PositionUpdate, parentEventId: rootEventId);
		await CreateTestEventAsync(runId, grandchildEventId, EventType.StateChange, parentEventId: childEventId);

		var parameters = new EventSequenceQueryParameters
		{
			RunId = runId,
			RootEventId = rootEventId
		};

		var result = await _repository.QueryEventSequenceAsync(parameters);

		Assert.NotNull(result);
		Assert.Single(result.Sequences);
		Assert.Equal(3, result.Sequences[0].Events.Count);
		Assert.Equal(rootEventId, result.Sequences[0].RootEventId);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_ShouldRespectMaxDepth()
	{
		var runId = await CreateTestRunAsync();

		var eventIds = new string[10];
		for (int i = 0; i < 10; i++)
		{
			eventIds[i] = Guid.NewGuid().ToString();
		}

		await CreateTestEventAsync(runId, eventIds[0], EventType.TradeExecution, parentEventId: null);
		for (int i = 1; i < 10; i++)
		{
			await CreateTestEventAsync(runId, eventIds[i], EventType.PositionUpdate, parentEventId: eventIds[i - 1]);
		}

		var parameters = new EventSequenceQueryParameters
		{
			RunId = runId,
			RootEventId = eventIds[0],
			MaxDepth = 5
		};

		var result = await _repository.QueryEventSequenceAsync(parameters);

		Assert.NotNull(result);
		Assert.Single(result.Sequences);
		Assert.Equal(5, result.Sequences[0].Events.Count);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithSequencePattern_ShouldMatchPattern()
	{
		var runId = await CreateTestRunAsync();

		var root1 = Guid.NewGuid().ToString();
		var child1 = Guid.NewGuid().ToString();
		await CreateTestEventAsync(runId, root1, EventType.TradeExecution, parentEventId: null);
		await CreateTestEventAsync(runId, child1, EventType.PositionUpdate, parentEventId: root1);

		var root2 = Guid.NewGuid().ToString();
		var child2 = Guid.NewGuid().ToString();
		await CreateTestEventAsync(runId, root2, EventType.TradeExecution, parentEventId: null);
		await CreateTestEventAsync(runId, child2, EventType.StateChange, parentEventId: root2);

		var parameters = new EventSequenceQueryParameters
		{
			RunId = runId,
			SequencePattern = [EventType.TradeExecution, EventType.PositionUpdate]
		};

		var result = await _repository.QueryEventSequenceAsync(parameters);

		Assert.NotNull(result);
		Assert.Single(result.Sequences);
		Assert.Equal(root1, result.Sequences[0].RootEventId);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_FindIncomplete_ShouldIdentifyMissingEvents()
	{
		var runId = await CreateTestRunAsync();

		var root1 = Guid.NewGuid().ToString();
		var child1 = Guid.NewGuid().ToString();
		await CreateTestEventAsync(runId, root1, EventType.TradeExecution, parentEventId: null);
		await CreateTestEventAsync(runId, child1, EventType.PositionUpdate, parentEventId: root1);

		var root2 = Guid.NewGuid().ToString();
		await CreateTestEventAsync(runId, root2, EventType.TradeExecution, parentEventId: null);

		var parameters = new EventSequenceQueryParameters
		{
			RunId = runId,
			SequencePattern = [EventType.TradeExecution, EventType.PositionUpdate],
			FindIncomplete = true
		};

		var result = await _repository.QueryEventSequenceAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(2, result.Sequences.Count);

		var completeSequence = result.Sequences.FirstOrDefault(s => s.Complete);
		var incompleteSequence = result.Sequences.FirstOrDefault(s => !s.Complete);

		Assert.NotNull(completeSequence);
		Assert.NotNull(incompleteSequence);
		Assert.Equal(root1, completeSequence.RootEventId);
		Assert.Equal(root2, incompleteSequence.RootEventId);
		Assert.Contains(EventType.PositionUpdate, incompleteSequence.MissingEventTypes!);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithNoRootEvents_ShouldReturnEmptyResult()
	{
		var runId = await CreateTestRunAsync();

		var child = Guid.NewGuid().ToString();
		await CreateTestEventAsync(runId, child, EventType.PositionUpdate, parentEventId: "non-existent-parent");

		var parameters = new EventSequenceQueryParameters
		{
			RunId = runId,
			RootEventId = "non-existent-root"
		};

		var result = await _repository.QueryEventSequenceAsync(parameters);

		Assert.NotNull(result);
		Assert.Empty(result.Sequences);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_ShouldReturnEventsInChronologicalOrder()
	{
		var runId = await CreateTestRunAsync();
		var baseTime = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);

		var rootEventId = Guid.NewGuid().ToString();
		var childEventId = Guid.NewGuid().ToString();

		await CreateTestEventAsync(runId, childEventId, EventType.PositionUpdate,
			parentEventId: rootEventId, timestamp: baseTime.AddMinutes(5));
		await CreateTestEventAsync(runId, rootEventId, EventType.TradeExecution,
			parentEventId: null, timestamp: baseTime);

		var parameters = new EventSequenceQueryParameters
		{
			RunId = runId,
			RootEventId = rootEventId
		};

		var result = await _repository.QueryEventSequenceAsync(parameters);

		Assert.NotNull(result);
		Assert.Single(result.Sequences);
		Assert.Equal(2, result.Sequences[0].Events.Count);

		var events = result.Sequences[0].Events.ToList();
		Assert.True(events[0].Timestamp <= events[1].Timestamp);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithPagination_ShouldRespectPageSize()
	{
		var runId = await CreateTestRunAsync();

		for (int i = 0; i < 10; i++)
		{
			var rootId = Guid.NewGuid().ToString();
			var childId = Guid.NewGuid().ToString();
			await CreateTestEventAsync(runId, rootId, EventType.TradeExecution, parentEventId: null);
			await CreateTestEventAsync(runId, childId, EventType.PositionUpdate, parentEventId: rootId);
		}

		var parameters = new EventSequenceQueryParameters
		{
			RunId = runId,
			SequencePattern = [EventType.TradeExecution, EventType.PositionUpdate],
			PageSize = 3,
			PageIndex = 0
		};

		var result = await _repository.QueryEventSequenceAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(3, result.Sequences.Count);
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
		string eventId,
		EventType eventType,
		string? parentEventId,
		EventSeverity severity = EventSeverity.Info,
		DateTime? timestamp = null)
	{
		var eventEntity = new EventEntity
		{
			EventId = eventId,
			RunId = runId,
			Timestamp = timestamp ?? DateTime.UtcNow,
			EventType = eventType,
			Severity = severity,
			Category = EventCategory.Execution,
			Properties = """{"test": "data"}""",
			ParentEventId = parentEventId,
			ValidationErrors = null
		};
		await _repository.WriteEventAsync(eventEntity);
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

using System.Text.Json;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Tools;

public sealed class QueryEventSequenceToolTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly QueryEventSequenceTool _tool;

	public QueryEventSequenceToolTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
		_tool = new QueryEventSequenceTool(_repository);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithRootEventId_ShouldReturnJsonResponse()
	{
		var runId = await CreateTestRunAsync();
		var rootEventId = Guid.NewGuid().ToString();
		var childEventId = Guid.NewGuid().ToString();

		await CreateTestEventAsync(runId, rootEventId, EventType.TradeExecution, parentEventId: null);
		await CreateTestEventAsync(runId, childEventId, EventType.PositionUpdate, parentEventId: rootEventId);

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: rootEventId,
			maxDepth: 10,
			pageSize: 50,
			pageIndex: 0
		);

		Assert.NotNull(result);
		Assert.NotEmpty(result);

		using var doc = JsonDocument.Parse(result);
		Assert.True(doc.RootElement.TryGetProperty("sequences", out _));
		Assert.True(doc.RootElement.TryGetProperty("metadata", out _));
	}

	[Fact]
	public async Task QueryEventSequenceAsync_ShouldReturnEventChain()
	{
		var runId = await CreateTestRunAsync();
		var rootEventId = Guid.NewGuid().ToString();
		var childEventId = Guid.NewGuid().ToString();
		var grandchildEventId = Guid.NewGuid().ToString();

		await CreateTestEventAsync(runId, rootEventId, EventType.TradeExecution, parentEventId: null);
		await CreateTestEventAsync(runId, childEventId, EventType.PositionUpdate, parentEventId: rootEventId);
		await CreateTestEventAsync(runId, grandchildEventId, EventType.StateChange, parentEventId: childEventId);

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: rootEventId,
			maxDepth: 10,
			pageSize: 50,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var sequences = doc.RootElement.GetProperty("sequences");
		Assert.Equal(1, sequences.GetArrayLength());

		var firstSequence = sequences[0];
		var events = firstSequence.GetProperty("events");
		Assert.Equal(3, events.GetArrayLength());
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

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: eventIds[0],
			maxDepth: 5,
			pageSize: 50,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var sequences = doc.RootElement.GetProperty("sequences");
		var firstSequence = sequences[0];
		var events = firstSequence.GetProperty("events");

		Assert.Equal(5, events.GetArrayLength());
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

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			sequencePattern: "TradeExecution,PositionUpdate",
			maxDepth: 10,
			pageSize: 50,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var sequences = doc.RootElement.GetProperty("sequences");
		Assert.Equal(1, sequences.GetArrayLength());

		var firstSequence = sequences[0];
		Assert.Equal(root1, firstSequence.GetProperty("rootEventId").GetString());
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithInvalidSequencePattern_ShouldThrowArgumentException()
	{
		var runId = await CreateTestRunAsync();

		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await _tool.QueryEventSequenceAsync(
				runId: runId,
				sequencePattern: "InvalidEventType",
				maxDepth: 10,
				pageSize: 50,
				pageIndex: 0
			)
		);

		Assert.Contains("Invalid event type in sequence pattern", exception.Message);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithFindIncomplete_ShouldIdentifyMissingEvents()
	{
		var runId = await CreateTestRunAsync();

		var root1 = Guid.NewGuid().ToString();
		var child1 = Guid.NewGuid().ToString();
		await CreateTestEventAsync(runId, root1, EventType.TradeExecution, parentEventId: null);
		await CreateTestEventAsync(runId, child1, EventType.PositionUpdate, parentEventId: root1);

		var root2 = Guid.NewGuid().ToString();
		await CreateTestEventAsync(runId, root2, EventType.TradeExecution, parentEventId: null);

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			sequencePattern: "TradeExecution,PositionUpdate",
			findIncomplete: true,
			maxDepth: 10,
			pageSize: 50,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var sequences = doc.RootElement.GetProperty("sequences");
		Assert.Equal(2, sequences.GetArrayLength());

		var incompleteSequence = sequences.EnumerateArray()
			.FirstOrDefault(s => !s.GetProperty("complete").GetBoolean());

		Assert.True(incompleteSequence.ValueKind != JsonValueKind.Undefined);
		Assert.True(incompleteSequence.TryGetProperty("missingEventTypes", out var missing));
		Assert.Contains("PositionUpdate", missing.EnumerateArray().Select(m => m.GetString()));
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithMaxDepthAboveLimit_ShouldClampTo100()
	{
		var runId = await CreateTestRunAsync();
		var rootEventId = Guid.NewGuid().ToString();

		await CreateTestEventAsync(runId, rootEventId, EventType.TradeExecution, parentEventId: null);

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: rootEventId,
			maxDepth: 500,
			pageSize: 50,
			pageIndex: 0
		);

		Assert.NotNull(result);
		using var doc = JsonDocument.Parse(result);
		Assert.True(doc.RootElement.TryGetProperty("sequences", out _));
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithPageSizeAboveMax_ShouldClampTo100()
	{
		var runId = await CreateTestRunAsync();
		var rootEventId = Guid.NewGuid().ToString();

		await CreateTestEventAsync(runId, rootEventId, EventType.TradeExecution, parentEventId: null);

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: rootEventId,
			maxDepth: 10,
			pageSize: 500,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var pageSize = metadata.GetProperty("pageSize").GetInt32();

		Assert.Equal(100, pageSize);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithNegativePageIndex_ShouldClampTo0()
	{
		var runId = await CreateTestRunAsync();
		var rootEventId = Guid.NewGuid().ToString();

		await CreateTestEventAsync(runId, rootEventId, EventType.TradeExecution, parentEventId: null);

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: rootEventId,
			maxDepth: 10,
			pageSize: 50,
			pageIndex: -5
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var pageIndex = metadata.GetProperty("pageIndex").GetInt32();

		Assert.Equal(0, pageIndex);
	}

	[Fact]
	public async Task QueryEventSequenceAsync_WithNoRootEvents_ShouldReturnEmptyResult()
	{
		var runId = await CreateTestRunAsync();

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: "non-existent-root",
			maxDepth: 10,
			pageSize: 50,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var sequences = doc.RootElement.GetProperty("sequences");
		Assert.Equal(0, sequences.GetArrayLength());
	}

	[Fact]
	public async Task QueryEventSequenceAsync_ShouldReturnSequenceWithCorrectStructure()
	{
		var runId = await CreateTestRunAsync();
		var rootEventId = Guid.NewGuid().ToString();

		await CreateTestEventAsync(runId, rootEventId, EventType.TradeExecution, parentEventId: null);

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: rootEventId,
			maxDepth: 10,
			pageSize: 50,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var sequences = doc.RootElement.GetProperty("sequences");
		var firstSequence = sequences[0];

		Assert.True(firstSequence.TryGetProperty("rootEventId", out _));
		Assert.True(firstSequence.TryGetProperty("events", out _));
		Assert.True(firstSequence.TryGetProperty("complete", out _));
	}

	[Fact]
	public async Task QueryEventSequenceAsync_ShouldReturnCorrectMetadata()
	{
		var runId = await CreateTestRunAsync();

		for (int i = 0; i < 10; i++)
		{
			var rootId = Guid.NewGuid().ToString();
			var childId = Guid.NewGuid().ToString();
			await CreateTestEventAsync(runId, rootId, EventType.TradeExecution, parentEventId: null);
			await CreateTestEventAsync(runId, childId, EventType.PositionUpdate, parentEventId: rootId);
		}

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			sequencePattern: "TradeExecution,PositionUpdate",
			maxDepth: 10,
			pageSize: 3,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");

		Assert.Equal(10, metadata.GetProperty("totalSequences").GetInt32());
		Assert.Equal(3, metadata.GetProperty("returnedCount").GetInt32());
		Assert.Equal(0, metadata.GetProperty("pageIndex").GetInt32());
		Assert.Equal(3, metadata.GetProperty("pageSize").GetInt32());
		Assert.True(metadata.GetProperty("hasMore").GetBoolean());
		Assert.True(metadata.GetProperty("queryTimeMs").GetInt32() >= 0);
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

		var result = await _tool.QueryEventSequenceAsync(
			runId: runId,
			rootEventId: rootEventId,
			maxDepth: 10,
			pageSize: 50,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var sequences = doc.RootElement.GetProperty("sequences");
		var firstSequence = sequences[0];
		var events = firstSequence.GetProperty("events");

		Assert.Equal(2, events.GetArrayLength());
		var firstTimestamp = DateTime.Parse(events[0].GetProperty("timestamp").GetString()!);
		var secondTimestamp = DateTime.Parse(events[1].GetProperty("timestamp").GetString()!);

		Assert.True(firstTimestamp <= secondTimestamp);
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

	private async Task CreateTestEventAsync(
		string runId,
		string eventId,
		EventType eventType,
		string? parentEventId,
		EventSeverity severity = EventSeverity.Info,
		DateTime? timestamp = null)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = eventId,
			RunId = runId,
			Timestamp = timestamp ?? DateTime.UtcNow,
			EventType = eventType,
			Severity = severity,
			Category = EventCategory.Execution,
			Properties = """{"test": "data"}""",
			ParentEventId = parentEventId
		});
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

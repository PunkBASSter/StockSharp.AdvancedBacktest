using System.Text.Json;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Tools;

public sealed class GetEventsByTypeToolTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly GetEventsByTypeTool _tool;

	public GetEventsByTypeToolTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
		_tool = new GetEventsByTypeTool(_repository);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithValidParameters_ShouldReturnJsonResponse()
	{
		var runId = await CreateTestRunAsync();
		await CreateTestEventAsync(runId, EventType.TradeExecution);

		var result = await _tool.GetEventsByTypeAsync(
			runId: runId,
			eventType: "TradeExecution",
			pageSize: 100,
			pageIndex: 0
		);

		Assert.NotNull(result);
		Assert.NotEmpty(result);

		using var doc = JsonDocument.Parse(result);
		Assert.True(doc.RootElement.TryGetProperty("events", out _));
		Assert.True(doc.RootElement.TryGetProperty("metadata", out _));
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithInvalidEventType_ShouldThrowArgumentException()
	{
		var runId = await CreateTestRunAsync();

		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await _tool.GetEventsByTypeAsync(
				runId: runId,
				eventType: "InvalidEventType",
				pageSize: 100,
				pageIndex: 0
			)
		);

		Assert.Contains("Invalid event type", exception.Message);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithInvalidStartTime_ShouldThrowArgumentException()
	{
		var runId = await CreateTestRunAsync();

		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await _tool.GetEventsByTypeAsync(
				runId: runId,
				eventType: "TradeExecution",
				startTime: "not-a-date",
				pageSize: 100,
				pageIndex: 0
			)
		);

		Assert.Contains("Invalid start time format", exception.Message);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithInvalidEndTime_ShouldThrowArgumentException()
	{
		var runId = await CreateTestRunAsync();

		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await _tool.GetEventsByTypeAsync(
				runId: runId,
				eventType: "TradeExecution",
				endTime: "not-a-date",
				pageSize: 100,
				pageIndex: 0
			)
		);

		Assert.Contains("Invalid end time format", exception.Message);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithInvalidSeverity_ShouldThrowArgumentException()
	{
		var runId = await CreateTestRunAsync();

		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await _tool.GetEventsByTypeAsync(
				runId: runId,
				eventType: "TradeExecution",
				severity: "InvalidSeverity",
				pageSize: 100,
				pageIndex: 0
			)
		);

		Assert.Contains("Invalid severity", exception.Message);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithPageSizeAboveMax_ShouldClampTo1000()
	{
		var runId = await CreateTestRunAsync();
		for (int i = 0; i < 50; i++)
		{
			await CreateTestEventAsync(runId, EventType.TradeExecution);
		}

		var result = await _tool.GetEventsByTypeAsync(
			runId: runId,
			eventType: "TradeExecution",
			pageSize: 5000,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var pageSize = metadata.GetProperty("pageSize").GetInt32();

		Assert.Equal(1000, pageSize);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithNegativePageIndex_ShouldClampTo0()
	{
		var runId = await CreateTestRunAsync();
		await CreateTestEventAsync(runId, EventType.TradeExecution);

		var result = await _tool.GetEventsByTypeAsync(
			runId: runId,
			eventType: "TradeExecution",
			pageSize: 100,
			pageIndex: -5
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var pageIndex = metadata.GetProperty("pageIndex").GetInt32();

		Assert.Equal(0, pageIndex);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithTimeRangeFilter_ShouldReturnFilteredEvents()
	{
		var runId = await CreateTestRunAsync();
		var baseTime = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(-1));
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime);
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(1));
		await CreateTestEventWithTimeAsync(runId, EventType.TradeExecution, baseTime.AddHours(2));

		var result = await _tool.GetEventsByTypeAsync(
			runId: runId,
			eventType: "TradeExecution",
			startTime: baseTime.AddMinutes(-30).ToString("o"),
			endTime: baseTime.AddHours(1).AddMinutes(30).ToString("o"),
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var totalCount = metadata.GetProperty("totalCount").GetInt32();

		Assert.Equal(2, totalCount);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithSeverityFilter_ShouldReturnFilteredEvents()
	{
		var runId = await CreateTestRunAsync();
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Info);
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Warning);
		await CreateTestEventAsync(runId, EventType.TradeExecution, EventSeverity.Error);

		var result = await _tool.GetEventsByTypeAsync(
			runId: runId,
			eventType: "TradeExecution",
			severity: "Warning",
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var totalCount = metadata.GetProperty("totalCount").GetInt32();

		Assert.Equal(1, totalCount);
	}

	[Fact]
	public async Task GetEventsByTypeAsync_ShouldReturnCorrectMetadata()
	{
		var runId = await CreateTestRunAsync();
		for (int i = 0; i < 25; i++)
		{
			await CreateTestEventAsync(runId, EventType.TradeExecution);
		}

		var result = await _tool.GetEventsByTypeAsync(
			runId: runId,
			eventType: "TradeExecution",
			pageSize: 10,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");

		Assert.Equal(25, metadata.GetProperty("totalCount").GetInt32());
		Assert.Equal(10, metadata.GetProperty("returnedCount").GetInt32());
		Assert.Equal(0, metadata.GetProperty("pageIndex").GetInt32());
		Assert.Equal(10, metadata.GetProperty("pageSize").GetInt32());
		Assert.True(metadata.GetProperty("hasMore").GetBoolean());
		Assert.True(metadata.GetProperty("queryTimeMs").GetInt32() >= 0);
		Assert.False(metadata.GetProperty("truncated").GetBoolean());
	}

	[Fact]
	public async Task GetEventsByTypeAsync_ShouldReturnEventsWithCorrectStructure()
	{
		var runId = await CreateTestRunAsync();
		await CreateTestEventAsync(runId, EventType.TradeExecution);

		var result = await _tool.GetEventsByTypeAsync(
			runId: runId,
			eventType: "TradeExecution",
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var events = doc.RootElement.GetProperty("events");
		Assert.True(events.GetArrayLength() > 0);

		var firstEvent = events[0];
		Assert.True(firstEvent.TryGetProperty("eventId", out _));
		Assert.True(firstEvent.TryGetProperty("runId", out _));
		Assert.True(firstEvent.TryGetProperty("timestamp", out _));
		Assert.True(firstEvent.TryGetProperty("eventType", out _));
		Assert.True(firstEvent.TryGetProperty("severity", out _));
		Assert.True(firstEvent.TryGetProperty("category", out _));
		Assert.True(firstEvent.TryGetProperty("properties", out _));
	}

	[Fact]
	public async Task GetEventsByTypeAsync_WithParsedProperties_ShouldReturnJsonElement()
	{
		var runId = await CreateTestRunAsync();
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"price": 100.5, "quantity": 10}"""
		});

		var result = await _tool.GetEventsByTypeAsync(
			runId: runId,
			eventType: "TradeExecution",
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var events = doc.RootElement.GetProperty("events");
		var firstEvent = events[0];
		var properties = firstEvent.GetProperty("properties");

		Assert.Equal(JsonValueKind.Object, properties.ValueKind);
		Assert.Equal(100.5, properties.GetProperty("price").GetDouble());
		Assert.Equal(10, properties.GetProperty("quantity").GetInt32());
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

	private async Task CreateTestEventAsync(string runId, EventType eventType, EventSeverity severity = EventSeverity.Info)
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

	private async Task CreateTestEventWithTimeAsync(string runId, EventType eventType, DateTime timestamp)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = eventType,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = "{}"
		});
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

using System.Text.Json;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Tools;

public sealed class GetEventsByEntityToolTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly GetEventsByEntityTool _tool;

	public GetEventsByEntityToolTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
		_tool = new GetEventsByEntityTool(_repository);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithOrderId_ShouldReturnJsonResponse()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-123";

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}", "Price": 100.50}""");

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "OrderId",
			entityValue: orderId,
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
	public async Task GetEventsByEntityAsync_WithInvalidEntityType_ShouldThrowArgumentException()
	{
		var runId = await CreateTestRunAsync();

		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await _tool.GetEventsByEntityAsync(
				runId: runId,
				entityType: "InvalidEntity",
				entityValue: "value",
				pageSize: 100,
				pageIndex: 0
			)
		);

		Assert.Contains("Invalid entity type", exception.Message);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithSecuritySymbol_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		var symbol = "AAPL";

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"SecuritySymbol": "{{symbol}}", "Price": 150.00}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.IndicatorCalculation,
			$$"""{"SecuritySymbol": "{{symbol}}", "Value": 0.75}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			"""{"SecuritySymbol": "MSFT", "Price": 300.00}""");

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "SecuritySymbol",
			entityValue: symbol,
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var totalCount = metadata.GetProperty("totalCount").GetInt32();

		Assert.Equal(2, totalCount);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithEventTypeFilter_ShouldFilterResults()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-456";

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}", "Price": 100.50}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.OrderRejection,
			$$"""{"OrderId": "{{orderId}}", "Reason": "Cancelled"}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.PositionUpdate,
			$$"""{"OrderId": "{{orderId}}", "Quantity": 10}""");

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "OrderId",
			entityValue: orderId,
			eventTypeFilter: "TradeExecution,OrderRejection",
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var totalCount = metadata.GetProperty("totalCount").GetInt32();

		Assert.Equal(2, totalCount);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithInvalidEventTypeFilter_ShouldThrowArgumentException()
	{
		var runId = await CreateTestRunAsync();

		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await _tool.GetEventsByEntityAsync(
				runId: runId,
				entityType: "OrderId",
				entityValue: "order-123",
				eventTypeFilter: "InvalidType",
				pageSize: 100,
				pageIndex: 0
			)
		);

		Assert.Contains("Invalid event type", exception.Message);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithPageSizeAboveMax_ShouldClampTo1000()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-789";

		for (int i = 0; i < 10; i++)
		{
			await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
				$$"""{"OrderId": "{{orderId}}", "Price": {{100 + i}}.00}""");
		}

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "OrderId",
			entityValue: orderId,
			pageSize: 5000,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var pageSize = metadata.GetProperty("pageSize").GetInt32();

		Assert.Equal(1000, pageSize);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithNegativePageIndex_ShouldClampTo0()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-123";

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}"}""");

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "OrderId",
			entityValue: orderId,
			pageSize: 100,
			pageIndex: -5
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var pageIndex = metadata.GetProperty("pageIndex").GetInt32();

		Assert.Equal(0, pageIndex);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithPositionId_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		var positionId = "pos-123";

		await CreateTestEventWithPropertiesAsync(runId, EventType.PositionUpdate,
			$$"""{"PositionId": "{{positionId}}", "Quantity": 100}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.StateChange,
			$$"""{"PositionId": "{{positionId}}", "NewState": "Open"}""");

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "PositionId",
			entityValue: positionId,
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var totalCount = metadata.GetProperty("totalCount").GetInt32();

		Assert.Equal(2, totalCount);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithIndicatorName_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		var indicatorName = "SMA_20";

		await CreateTestEventWithPropertiesAsync(runId, EventType.IndicatorCalculation,
			$$"""{"IndicatorName": "{{indicatorName}}", "Value": 150.5}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.IndicatorCalculation,
			$$"""{"IndicatorName": "{{indicatorName}}", "Value": 151.0}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.IndicatorCalculation,
			"""{"IndicatorName": "EMA_10", "Value": 145.0}""");

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "IndicatorName",
			entityValue: indicatorName,
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var metadata = doc.RootElement.GetProperty("metadata");
		var totalCount = metadata.GetProperty("totalCount").GetInt32();

		Assert.Equal(2, totalCount);
	}

	[Fact]
	public async Task GetEventsByEntityAsync_ShouldReturnEventsWithCorrectStructure()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-structure-test";

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}", "Price": 100.50}""");

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "OrderId",
			entityValue: orderId,
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
	public async Task GetEventsByEntityAsync_ShouldReturnCorrectMetadata()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-metadata-test";

		for (int i = 0; i < 25; i++)
		{
			await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
				$$"""{"OrderId": "{{orderId}}", "Price": {{100 + i}}.00}""");
		}

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "OrderId",
			entityValue: orderId,
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
	}

	[Fact]
	public async Task GetEventsByEntityAsync_WithNoMatches_ShouldReturnEmptyResult()
	{
		var runId = await CreateTestRunAsync();

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			"""{"OrderId": "other-order", "Price": 100.00}""");

		var result = await _tool.GetEventsByEntityAsync(
			runId: runId,
			entityType: "OrderId",
			entityValue: "non-existent-order",
			pageSize: 100,
			pageIndex: 0
		);

		using var doc = JsonDocument.Parse(result);
		var events = doc.RootElement.GetProperty("events");
		var metadata = doc.RootElement.GetProperty("metadata");

		Assert.Equal(0, events.GetArrayLength());
		Assert.Equal(0, metadata.GetProperty("totalCount").GetInt32());
		Assert.False(metadata.GetProperty("hasMore").GetBoolean());
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

	private async Task CreateTestEventWithPropertiesAsync(
		string runId,
		EventType eventType,
		string properties,
		EventSeverity severity = EventSeverity.Info)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = DateTime.UtcNow,
			EventType = eventType,
			Severity = severity,
			Category = EventCategory.Execution,
			Properties = properties
		});
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

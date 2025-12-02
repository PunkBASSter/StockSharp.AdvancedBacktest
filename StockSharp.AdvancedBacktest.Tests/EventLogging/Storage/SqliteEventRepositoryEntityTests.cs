using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Storage;

public sealed class SqliteEventRepositoryEntityTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;

	public SqliteEventRepositoryEntityTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
	}

	[Fact]
	public async Task QueryEventsByEntityAsync_WithOrderId_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-123";

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}", "Price": 100.50}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.OrderRejection,
			$$"""{"OrderId": "{{orderId}}", "Reason": "Insufficient funds"}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			"""{"OrderId": "other-order", "Price": 200.00}""");

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = "OrderId",
			EntityValue = orderId
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(2, result.Events.Count);
		Assert.All(result.Events, e => Assert.Contains(orderId, e.Properties));
	}

	[Fact]
	public async Task QueryEventsByEntityAsync_WithSecuritySymbol_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		var symbol = "AAPL";

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"SecuritySymbol": "{{symbol}}", "Price": 150.00}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.IndicatorCalculation,
			$$"""{"SecuritySymbol": "{{symbol}}", "Value": 0.75}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			"""{"SecuritySymbol": "MSFT", "Price": 300.00}""");

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = "SecuritySymbol",
			EntityValue = symbol
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(2, result.Events.Count);
		Assert.All(result.Events, e => Assert.Contains(symbol, e.Properties));
	}

	[Fact]
	public async Task QueryEventsByEntityAsync_WithEventTypeFilter_ShouldFilterResults()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-456";

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}", "Price": 100.50}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.OrderRejection,
			$$"""{"OrderId": "{{orderId}}", "Reason": "Cancelled"}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.PositionUpdate,
			$$"""{"OrderId": "{{orderId}}", "Quantity": 10}""");

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = "OrderId",
			EntityValue = orderId,
			EventTypeFilter = [EventType.TradeExecution, EventType.OrderRejection]
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(2, result.Events.Count);
		Assert.DoesNotContain(result.Events, e => e.EventType == EventType.PositionUpdate);
	}

	[Fact]
	public async Task QueryEventsByEntityAsync_WithPagination_ShouldRespectPageSize()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-789";

		for (int i = 0; i < 15; i++)
		{
			await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
				$$"""{"OrderId": "{{orderId}}", "Price": {{100 + i}}.00}""");
		}

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = "OrderId",
			EntityValue = orderId,
			PageSize = 5,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(5, result.Events.Count);
		Assert.Equal(15, result.Metadata.TotalCount);
		Assert.True(result.Metadata.HasMore);
	}

	[Fact]
	public async Task QueryEventsByEntityAsync_WithNoMatches_ShouldReturnEmptyResult()
	{
		var runId = await CreateTestRunAsync();

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			"""{"OrderId": "other-order", "Price": 100.00}""");

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = "OrderId",
			EntityValue = "non-existent-order"
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);

		Assert.NotNull(result);
		Assert.Empty(result.Events);
		Assert.Equal(0, result.Metadata.TotalCount);
		Assert.False(result.Metadata.HasMore);
	}

	[Fact]
	public async Task QueryEventsByEntityAsync_WithPositionId_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		var positionId = "pos-123";

		await CreateTestEventWithPropertiesAsync(runId, EventType.PositionUpdate,
			$$"""{"PositionId": "{{positionId}}", "Quantity": 100}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.StateChange,
			$$"""{"PositionId": "{{positionId}}", "NewState": "Open"}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.PositionUpdate,
			"""{"PositionId": "pos-other", "Quantity": 50}""");

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = "PositionId",
			EntityValue = positionId
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(2, result.Events.Count);
	}

	[Fact]
	public async Task QueryEventsByEntityAsync_WithIndicatorName_ShouldReturnMatchingEvents()
	{
		var runId = await CreateTestRunAsync();
		var indicatorName = "SMA_20";

		await CreateTestEventWithPropertiesAsync(runId, EventType.IndicatorCalculation,
			$$"""{"IndicatorName": "{{indicatorName}}", "Value": 150.5}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.IndicatorCalculation,
			$$"""{"IndicatorName": "{{indicatorName}}", "Value": 151.0}""");
		await CreateTestEventWithPropertiesAsync(runId, EventType.IndicatorCalculation,
			"""{"IndicatorName": "EMA_10", "Value": 145.0}""");

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = "IndicatorName",
			EntityValue = indicatorName
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(2, result.Events.Count);
	}

	[Fact]
	public async Task QueryEventsByEntityAsync_ShouldReturnEventsOrderedByTimestamp()
	{
		var runId = await CreateTestRunAsync();
		var orderId = "order-time-test";
		var baseTime = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);

		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}"}""", timestamp: baseTime.AddMinutes(10));
		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}"}""", timestamp: baseTime);
		await CreateTestEventWithPropertiesAsync(runId, EventType.TradeExecution,
			$$"""{"OrderId": "{{orderId}}"}""", timestamp: baseTime.AddMinutes(5));

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = "OrderId",
			EntityValue = orderId
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(3, result.Events.Count);
		Assert.True(result.Events[0].Timestamp <= result.Events[1].Timestamp);
		Assert.True(result.Events[1].Timestamp <= result.Events[2].Timestamp);
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

	private async Task CreateTestEventWithPropertiesAsync(
		string runId,
		EventType eventType,
		string properties,
		EventSeverity severity = EventSeverity.Info,
		DateTime? timestamp = null,
		string? parentEventId = null)
	{
		var eventEntity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp ?? DateTime.UtcNow,
			EventType = eventType,
			Severity = severity,
			Category = EventCategory.Execution,
			Properties = properties,
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

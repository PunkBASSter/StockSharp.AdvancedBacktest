using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Models;

public sealed class EventEntityTests
{
	[Fact]
	public void EventEntity_ShouldStoreAllRequiredProperties()
	{
		var entity = new EventEntity
		{
			EventId = "test-123",
			RunId = "run-456",
			Timestamp = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"price": 100}"""
		};

		Assert.Equal("test-123", entity.EventId);
		Assert.Equal("run-456", entity.RunId);
		Assert.Equal(new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc), entity.Timestamp);
		Assert.Equal(EventType.TradeExecution, entity.EventType);
		Assert.Equal(EventSeverity.Info, entity.Severity);
		Assert.Equal(EventCategory.Execution, entity.Category);
		Assert.Equal("""{"price": 100}""", entity.Properties);
	}

	[Fact]
	public void EventEntity_WithOptionalProperties_ShouldStoreNullValues()
	{
		var entity = new EventEntity
		{
			EventId = "test-123",
			RunId = "run-456",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = "{}",
			ParentEventId = null,
			ValidationErrors = null
		};

		Assert.Null(entity.ParentEventId);
		Assert.Null(entity.ValidationErrors);
	}

	[Fact]
	public void EventEntity_WithParentEventId_ShouldStoreValue()
	{
		var entity = new EventEntity
		{
			EventId = "child-123",
			RunId = "run-456",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = "{}",
			ParentEventId = "parent-789"
		};

		Assert.Equal("parent-789", entity.ParentEventId);
	}

	[Fact]
	public void EventEntity_WithValidationErrors_ShouldStoreJson()
	{
		var validationJson = """[{"field":"Price","error":"Must be positive","severity":"Error"}]""";
		var entity = new EventEntity
		{
			EventId = "test-123",
			RunId = "run-456",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Error,
			Category = EventCategory.Execution,
			Properties = "{}",
			ValidationErrors = validationJson
		};

		Assert.Equal(validationJson, entity.ValidationErrors);
	}

	[Theory]
	[InlineData(EventType.TradeExecution)]
	[InlineData(EventType.OrderRejection)]
	[InlineData(EventType.IndicatorCalculation)]
	[InlineData(EventType.PositionUpdate)]
	[InlineData(EventType.StateChange)]
	[InlineData(EventType.MarketDataEvent)]
	[InlineData(EventType.RiskEvent)]
	public void EventEntity_ShouldSupportAllEventTypes(EventType eventType)
	{
		var entity = new EventEntity
		{
			EventId = "test",
			RunId = "run",
			Timestamp = DateTime.UtcNow,
			EventType = eventType,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = "{}"
		};

		Assert.Equal(eventType, entity.EventType);
	}

	[Theory]
	[InlineData(EventSeverity.Debug)]
	[InlineData(EventSeverity.Info)]
	[InlineData(EventSeverity.Warning)]
	[InlineData(EventSeverity.Error)]
	public void EventEntity_ShouldSupportAllSeverityLevels(EventSeverity severity)
	{
		var entity = new EventEntity
		{
			EventId = "test",
			RunId = "run",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = severity,
			Category = EventCategory.Execution,
			Properties = "{}"
		};

		Assert.Equal(severity, entity.Severity);
	}

	[Theory]
	[InlineData(EventCategory.Execution)]
	[InlineData(EventCategory.MarketData)]
	[InlineData(EventCategory.Indicators)]
	[InlineData(EventCategory.Risk)]
	[InlineData(EventCategory.Performance)]
	public void EventEntity_ShouldSupportAllCategories(EventCategory category)
	{
		var entity = new EventEntity
		{
			EventId = "test",
			RunId = "run",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = category,
			Properties = "{}"
		};

		Assert.Equal(category, entity.Category);
	}

	[Fact]
	public void EventEntity_WithComplexProperties_ShouldStoreJsonString()
	{
		var complexProps = """
		{
			"orderId": "12345",
			"price": 100.50,
			"quantity": 10,
			"direction": "Buy",
			"metadata": {
				"strategy": "ZigZag",
				"signal": "Breakout"
			}
		}
		""";

		var entity = new EventEntity
		{
			EventId = "test",
			RunId = "run",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = complexProps
		};

		Assert.Equal(complexProps, entity.Properties);
	}

	[Fact]
	public void BacktestRunEntity_ShouldStoreAllProperties()
	{
		var run = new BacktestRunEntity
		{
			Id = "run-123",
			StartTime = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2025, 1, 15, 16, 0, 0, DateTimeKind.Utc),
			StrategyConfigHash = new string('a', 64),
			CreatedAt = new DateTime(2025, 1, 15, 9, 55, 0, DateTimeKind.Utc)
		};

		Assert.Equal("run-123", run.Id);
		Assert.Equal(new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc), run.StartTime);
		Assert.Equal(new DateTime(2025, 1, 15, 16, 0, 0, DateTimeKind.Utc), run.EndTime);
		Assert.Equal(new string('a', 64), run.StrategyConfigHash);
		Assert.Equal(new DateTime(2025, 1, 15, 9, 55, 0, DateTimeKind.Utc), run.CreatedAt);
	}
}

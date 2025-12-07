using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Validation;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Storage;

public sealed class EventValidationTests
{
	private readonly EventValidator _validator = new();

	[Fact]
	public void ValidateEvent_WithValidTradeExecution_ShouldReturnNoErrors()
	{
		var entity = CreateValidTradeExecutionEvent();

		var result = _validator.ValidateEvent(entity);

		Assert.Empty(result.Errors);
		Assert.False(result.HasErrors);
		Assert.False(result.HasWarnings);
	}

	[Fact]
	public void ValidateEvent_TradeExecution_MissingOrderId_ShouldReturnError()
	{
		var properties = new { Price = 100.50m, Quantity = 10m, Direction = "Buy" };
		var entity = CreateEvent(EventType.TradeExecution, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties.OrderId" && e.Severity == "Error");
	}

	[Fact]
	public void ValidateEvent_TradeExecution_MissingPrice_ShouldReturnError()
	{
		var properties = new { OrderId = Guid.NewGuid().ToString(), Quantity = 10m, Direction = "Buy" };
		var entity = CreateEvent(EventType.TradeExecution, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties.Price" && e.Severity == "Error");
	}

	[Fact]
	public void ValidateEvent_TradeExecution_MissingQuantity_ShouldReturnWarning()
	{
		var properties = new { OrderId = Guid.NewGuid().ToString(), Price = 100.50m, Direction = "Buy" };
		var entity = CreateEvent(EventType.TradeExecution, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasWarnings);
		Assert.Contains(result.Errors, e => e.Field == "Properties.Quantity" && e.Severity == "Warning");
	}

	[Fact]
	public void ValidateEvent_OrderRejection_MissingRejectionReason_ShouldReturnError()
	{
		var properties = new { OrderId = Guid.NewGuid().ToString(), SecuritySymbol = "AAPL" };
		var entity = CreateEvent(EventType.OrderRejection, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties.RejectionReason" && e.Severity == "Error");
	}

	[Fact]
	public void ValidateEvent_IndicatorCalculation_MissingIndicatorName_ShouldReturnError()
	{
		var properties = new { Value = 0.75, SecuritySymbol = "AAPL" };
		var entity = CreateEvent(EventType.IndicatorCalculation, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties.IndicatorName" && e.Severity == "Error");
	}

	[Fact]
	public void ValidateEvent_IndicatorCalculation_MissingValue_ShouldReturnError()
	{
		var properties = new { IndicatorName = "SMA_20", SecuritySymbol = "AAPL" };
		var entity = CreateEvent(EventType.IndicatorCalculation, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties.Value" && e.Severity == "Error");
	}

	[Fact]
	public void ValidateEvent_PositionUpdate_MissingSecuritySymbol_ShouldReturnError()
	{
		var properties = new { Quantity = 100m, AveragePrice = 150.00m };
		var entity = CreateEvent(EventType.PositionUpdate, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties.SecuritySymbol" && e.Severity == "Error");
	}

	[Fact]
	public void ValidateEvent_WithInvalidJson_ShouldReturnError()
	{
		var entity = CreateEvent(EventType.TradeExecution, "not valid json {");

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties" && e.Error.Contains("Invalid JSON"));
	}

	[Fact]
	public void ValidateEvent_WithEmptyProperties_ShouldReturnError()
	{
		var entity = CreateEvent(EventType.TradeExecution, "{}");

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
	}

	[Fact]
	public void ValidateEvent_PropertiesExceedingMaxSize_ShouldReturnError()
	{
		var largeString = new string('x', 1024 * 1024 + 1); // 1MB + 1 byte
		var properties = new { Data = largeString };
		var entity = CreateEvent(EventType.StateChange, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties" && e.Error.Contains("exceeds maximum size"));
	}

	[Fact]
	public void ValidateEvent_PropertiesAtMaxSize_ShouldNotReturnSizeError()
	{
		var properties = new { StateType = "Position", StateBefore = new { }, StateAfter = new { } };
		var entity = CreateEvent(EventType.StateChange, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.DoesNotContain(result.Errors, e => e.Error.Contains("exceeds maximum size"));
	}

	[Fact]
	public void ValidateEvent_WithNullEntity_ShouldThrowArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => _validator.ValidateEvent(null!));
	}

	[Fact]
	public void ValidateEvent_StateChange_Valid_ShouldReturnNoErrors()
	{
		var properties = new { StateType = "Position", StateBefore = new { }, StateAfter = new { } };
		var entity = CreateEvent(EventType.StateChange, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.False(result.HasErrors);
	}

	[Fact]
	public void ValidateEvent_MarketDataEvent_Valid_ShouldReturnNoErrors()
	{
		var properties = new { SecuritySymbol = "AAPL", DataType = "Candle", Open = 100m, High = 105m, Low = 99m, Close = 104m };
		var entity = CreateEvent(EventType.MarketDataEvent, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.False(result.HasErrors);
	}

	[Fact]
	public void ValidateEvent_RiskEvent_MissingRiskType_ShouldReturnError()
	{
		var properties = new { Threshold = 0.05m, CurrentValue = 0.06m };
		var entity = CreateEvent(EventType.RiskEvent, JsonSerializer.Serialize(properties));

		var result = _validator.ValidateEvent(entity);

		Assert.True(result.HasErrors);
		Assert.Contains(result.Errors, e => e.Field == "Properties.RiskType" && e.Severity == "Error");
	}

	private static EventEntity CreateValidTradeExecutionEvent()
	{
		var properties = new
		{
			OrderId = Guid.NewGuid().ToString(),
			Price = 100.50m,
			Quantity = 10m,
			Direction = "Buy",
			SecuritySymbol = "AAPL"
		};

		return CreateEvent(EventType.TradeExecution, JsonSerializer.Serialize(properties));
	}

	private static EventEntity CreateEvent(EventType eventType, string properties) =>
		new()
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = Guid.NewGuid().ToString(),
			Timestamp = DateTime.UtcNow,
			EventType = eventType,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = properties
		};
}

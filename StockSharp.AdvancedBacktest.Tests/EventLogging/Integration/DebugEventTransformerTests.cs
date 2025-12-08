using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Integration;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.Export;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Integration;

public sealed class DebugEventTransformerTests
{
	private const string TestRunId = "test-run-123";

	[Fact]
	public void FromCandle_ShouldTransformToEventEntity()
	{
		var candle = new CandleDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Open = 100.0,
			High = 105.0,
			Low = 99.0,
			Close = 104.0,
			Volume = 1000.0,
			SecurityId = "AAPL",
			SequenceNumber = 1
		};

		var result = DebugEventTransformer.FromCandle(candle, TestRunId);

		Assert.NotNull(result);
		Assert.Equal(TestRunId, result.RunId);
		Assert.Equal(EventType.MarketDataEvent, result.EventType);
		Assert.Equal(EventSeverity.Debug, result.Severity);
		Assert.Equal(EventCategory.Data, result.Category);
		Assert.NotEmpty(result.EventId);
		Assert.Null(result.ParentEventId);

		var props = JsonDocument.Parse(result.Properties);
		Assert.Equal("AAPL", props.RootElement.GetProperty("SecuritySymbol").GetString());
		Assert.Equal(100.0, props.RootElement.GetProperty("Open").GetDouble());
		Assert.Equal(105.0, props.RootElement.GetProperty("High").GetDouble());
	}

	[Fact]
	public void FromCandle_WithParentEventId_ShouldSetParentEventId()
	{
		var candle = new CandleDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Open = 100.0,
			High = 105.0,
			Low = 99.0,
			Close = 104.0,
			Volume = 1000.0
		};

		var parentId = "parent-event-123";
		var result = DebugEventTransformer.FromCandle(candle, TestRunId, parentId);

		Assert.Equal(parentId, result.ParentEventId);
	}

	[Fact]
	public void FromTrade_ShouldTransformToEventEntity()
	{
		var trade = new TradeDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Price = 150.50,
			Volume = 100,
			Side = "Buy",
			PnL = 250.00,
			OrderId = 12345,
			SequenceNumber = 2
		};

		var result = DebugEventTransformer.FromTrade(trade, TestRunId);

		Assert.NotNull(result);
		Assert.Equal(TestRunId, result.RunId);
		Assert.Equal(EventType.TradeExecution, result.EventType);
		Assert.Equal(EventSeverity.Info, result.Severity);
		Assert.Equal(EventCategory.Execution, result.Category);

		var props = JsonDocument.Parse(result.Properties);
		Assert.Equal("12345", props.RootElement.GetProperty("OrderId").GetString());
		Assert.Equal(150.50, props.RootElement.GetProperty("Price").GetDouble());
		Assert.Equal("Buy", props.RootElement.GetProperty("Direction").GetString());
		Assert.Equal(250.00, props.RootElement.GetProperty("RealizedPnL").GetDouble());
	}

	[Fact]
	public void FromIndicator_ShouldTransformToEventEntity()
	{
		var indicator = new IndicatorDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Value = 0.75,
			SequenceNumber = 3
		};

		var result = DebugEventTransformer.FromIndicator("SMA_20", indicator, TestRunId, "AAPL");

		Assert.NotNull(result);
		Assert.Equal(TestRunId, result.RunId);
		Assert.Equal(EventType.IndicatorCalculation, result.EventType);
		Assert.Equal(EventSeverity.Debug, result.Severity);
		Assert.Equal(EventCategory.Analysis, result.Category);

		var props = JsonDocument.Parse(result.Properties);
		Assert.Equal("SMA_20", props.RootElement.GetProperty("IndicatorName").GetString());
		Assert.Equal("AAPL", props.RootElement.GetProperty("SecuritySymbol").GetString());
		Assert.Equal(0.75, props.RootElement.GetProperty("Value").GetDouble());
	}

	[Fact]
	public void FromState_WithPosition_ShouldBePositionUpdate()
	{
		var state = new StateDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Position = 100,
			PnL = 500.00,
			UnrealizedPnL = 150.00,
			ProcessState = "Normal",
			SequenceNumber = 4
		};

		var result = DebugEventTransformer.FromState(state, TestRunId, "AAPL");

		Assert.Equal(EventType.PositionUpdate, result.EventType);
		Assert.Equal(EventCategory.Portfolio, result.Category);
		Assert.Equal(EventSeverity.Info, result.Severity);

		var props = JsonDocument.Parse(result.Properties);
		Assert.Equal(100.0, props.RootElement.GetProperty("Position").GetDouble());
		Assert.Equal(500.00, props.RootElement.GetProperty("RealizedPnL").GetDouble());
	}

	[Fact]
	public void FromState_WithZeroPosition_ShouldBeStateChange()
	{
		var state = new StateDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Position = 0,
			PnL = 0,
			UnrealizedPnL = 0,
			ProcessState = "Idle",
			SequenceNumber = 5
		};

		var result = DebugEventTransformer.FromState(state, TestRunId);

		Assert.Equal(EventType.StateChange, result.EventType);
	}

	[Fact]
	public void FromState_WithErrorState_ShouldHaveErrorSeverity()
	{
		var state = new StateDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Position = 0,
			PnL = 0,
			UnrealizedPnL = 0,
			ProcessState = "Error",
			SequenceNumber = 6
		};

		var result = DebugEventTransformer.FromState(state, TestRunId);

		Assert.Equal(EventSeverity.Error, result.Severity);
	}

	[Fact]
	public void FromState_WithWarningState_ShouldHaveWarningSeverity()
	{
		var state = new StateDataPoint
		{
			Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Position = 0,
			PnL = 0,
			UnrealizedPnL = 0,
			ProcessState = "Warning",
			SequenceNumber = 7
		};

		var result = DebugEventTransformer.FromState(state, TestRunId);

		Assert.Equal(EventSeverity.Warning, result.Severity);
	}

	[Fact]
	public void FromCandle_WithNullRunId_ShouldThrowArgumentNullException()
	{
		var candle = new CandleDataPoint { Time = 0 };

		Assert.Throws<ArgumentNullException>(() => DebugEventTransformer.FromCandle(candle, null!));
	}

	[Fact]
	public void FromCandle_WithNullCandle_ShouldThrowArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => DebugEventTransformer.FromCandle(null!, TestRunId));
	}

	[Fact]
	public void FromTrade_WithNullTrade_ShouldThrowArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => DebugEventTransformer.FromTrade(null!, TestRunId));
	}

	[Fact]
	public void FromIndicator_WithNullIndicatorName_ShouldThrowArgumentNullException()
	{
		var indicator = new IndicatorDataPoint { Time = 0, Value = 1.0 };

		Assert.Throws<ArgumentNullException>(() => DebugEventTransformer.FromIndicator(null!, indicator, TestRunId));
	}
}

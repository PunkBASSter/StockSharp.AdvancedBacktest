using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.Export;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Integration;

public static class DebugEventTransformer
{
	public static EventEntity FromCandle(CandleDataPoint candle, string runId, string? parentEventId = null)
	{
		ArgumentNullException.ThrowIfNull(candle);
		ArgumentException.ThrowIfNullOrEmpty(runId);

		var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(candle.Time).UtcDateTime;

		var properties = new
		{
			SecuritySymbol = candle.SecurityId ?? "UNKNOWN",
			Open = candle.Open,
			High = candle.High,
			Low = candle.Low,
			Close = candle.Close,
			Volume = candle.Volume,
			SequenceNumber = candle.SequenceNumber
		};

		return new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.MarketDataEvent,
			Severity = EventSeverity.Debug,
			Category = EventCategory.Data,
			Properties = JsonSerializer.Serialize(properties),
			ParentEventId = parentEventId
		};
	}

	public static EventEntity FromTrade(TradeDataPoint trade, string runId, string? parentEventId = null)
	{
		ArgumentNullException.ThrowIfNull(trade);
		ArgumentException.ThrowIfNullOrEmpty(runId);

		var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.Time).UtcDateTime;

		var properties = new
		{
			OrderId = trade.OrderId?.ToString() ?? Guid.NewGuid().ToString(),
			Price = trade.Price,
			Quantity = trade.Volume,
			Direction = trade.Side,
			RealizedPnL = trade.PnL,
			SequenceNumber = trade.SequenceNumber
		};

		return new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = JsonSerializer.Serialize(properties),
			ParentEventId = parentEventId
		};
	}

	public static EventEntity FromIndicator(string indicatorName, IndicatorDataPoint indicator, string runId, string? securitySymbol = null, string? parentEventId = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(indicatorName);
		ArgumentNullException.ThrowIfNull(indicator);
		ArgumentException.ThrowIfNullOrEmpty(runId);

		var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(indicator.Time).UtcDateTime;

		var properties = new
		{
			IndicatorName = indicatorName,
			SecuritySymbol = securitySymbol ?? "UNKNOWN",
			Value = indicator.Value,
			SequenceNumber = indicator.SequenceNumber
		};

		return new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.IndicatorCalculation,
			Severity = EventSeverity.Debug,
			Category = EventCategory.Analysis,
			Properties = JsonSerializer.Serialize(properties),
			ParentEventId = parentEventId
		};
	}

	public static EventEntity FromState(StateDataPoint state, string runId, string? securitySymbol = null, string? parentEventId = null)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentException.ThrowIfNullOrEmpty(runId);

		var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(state.Time).UtcDateTime;

		var properties = new
		{
			SecuritySymbol = securitySymbol ?? "UNKNOWN",
			PositionId = $"pos-{runId}-{state.Time}",
			Position = state.Position,
			RealizedPnL = state.PnL,
			UnrealizedPnL = state.UnrealizedPnL,
			ProcessState = state.ProcessState,
			SequenceNumber = state.SequenceNumber
		};

		var severity = state.ProcessState switch
		{
			"Error" => EventSeverity.Error,
			"Warning" => EventSeverity.Warning,
			_ => EventSeverity.Info
		};

		return new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = state.Position != 0 ? EventType.PositionUpdate : EventType.StateChange,
			Severity = severity,
			Category = EventCategory.Portfolio,
			Properties = JsonSerializer.Serialize(properties),
			ParentEventId = parentEventId
		};
	}
}

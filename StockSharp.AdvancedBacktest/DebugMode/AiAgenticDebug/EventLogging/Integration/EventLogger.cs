using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Integration;

public sealed class EventLogger : IAsyncDisposable
{
	private readonly string _runId;
	private readonly BatchEventWriter _writer;

	public EventLogger(string runId, IEventRepository repository, int batchSize = 1000)
	{
		_runId = runId;
		_writer = new BatchEventWriter(repository, batchSize);
	}

	public async Task LogEventAsync(
		EventType eventType,
		EventSeverity severity,
		EventCategory category,
		object properties,
		string? parentEventId = null)
	{
		var eventEntity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = eventType,
			Severity = severity,
			Category = category,
			Properties = JsonSerializer.Serialize(properties, JsonSerializerOptionsProvider.Options),
			ParentEventId = parentEventId,
			ValidationErrors = null
		};

		await _writer.WriteEventAsync(eventEntity);
	}

	public async Task LogTradeExecutionAsync(object tradeDetails, string? parentEventId = null)
	{
		await LogEventAsync(
			EventType.TradeExecution,
			EventSeverity.Info,
			EventCategory.Execution,
			tradeDetails,
			parentEventId);
	}

	public async Task LogOrderRejectionAsync(object rejectionDetails, string? parentEventId = null)
	{
		await LogEventAsync(
			EventType.OrderRejection,
			EventSeverity.Warning,
			EventCategory.Execution,
			rejectionDetails,
			parentEventId);
	}

	public async Task LogIndicatorCalculationAsync(object indicatorData, string? parentEventId = null)
	{
		await LogEventAsync(
			EventType.IndicatorCalculation,
			EventSeverity.Debug,
			EventCategory.Indicators,
			indicatorData,
			parentEventId);
	}

	public async Task LogPositionUpdateAsync(object positionData, string? parentEventId = null)
	{
		await LogEventAsync(
			EventType.PositionUpdate,
			EventSeverity.Info,
			EventCategory.Execution,
			positionData,
			parentEventId);
	}

	public async ValueTask DisposeAsync()
	{
		await _writer.DisposeAsync();
	}
}

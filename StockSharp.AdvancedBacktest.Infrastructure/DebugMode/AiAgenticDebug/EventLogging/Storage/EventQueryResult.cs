using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class EventQueryResult
{
	public required IReadOnlyList<EventEntity> Events { get; init; }
	public required QueryResultMetadata Metadata { get; init; }
}

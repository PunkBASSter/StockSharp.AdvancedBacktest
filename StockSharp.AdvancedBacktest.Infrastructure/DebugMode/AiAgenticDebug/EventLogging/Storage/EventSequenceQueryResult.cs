using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class EventSequenceQueryResult
{
	public required IReadOnlyList<EventSequence> Sequences { get; init; }
	public required SequenceQueryMetadata Metadata { get; init; }
}

public sealed class EventSequence
{
	public required string RootEventId { get; init; }
	public required IReadOnlyList<EventEntity> Events { get; init; }
	public required bool Complete { get; init; }
	public EventType[]? MissingEventTypes { get; init; }
}

public sealed class SequenceQueryMetadata
{
	public required int TotalSequences { get; init; }
	public required int ReturnedCount { get; init; }
	public required int PageIndex { get; init; }
	public required int PageSize { get; init; }
	public required bool HasMore { get; init; }
	public int QueryTimeMs { get; set; }
}

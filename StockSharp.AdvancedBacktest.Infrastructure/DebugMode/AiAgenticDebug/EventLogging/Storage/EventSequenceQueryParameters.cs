using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class EventSequenceQueryParameters
{
	public required string RunId { get; init; }
	public string? RootEventId { get; init; }
	public EventType[]? SequencePattern { get; init; }
	public bool FindIncomplete { get; init; }
	public int MaxDepth { get; init; } = 10;
	public int PageSize { get; init; } = 50;
	public int PageIndex { get; init; } = 0;
}

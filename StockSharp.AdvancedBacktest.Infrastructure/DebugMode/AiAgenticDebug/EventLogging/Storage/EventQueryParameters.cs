using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class EventQueryParameters
{
	public required string RunId { get; init; }
	public EventType? EventType { get; init; }
	public EventSeverity? Severity { get; init; }
	public EventCategory? Category { get; init; }
	public DateTime? StartTime { get; init; }
	public DateTime? EndTime { get; init; }
	public int PageSize { get; init; } = 100;
	public int PageIndex { get; init; } = 0;
}

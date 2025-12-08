using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class EntityReferenceQueryParameters
{
	public required string RunId { get; init; }
	public required string EntityType { get; init; }
	public required string EntityValue { get; init; }
	public EventType[]? EventTypeFilter { get; init; }
	public int PageSize { get; init; } = 100;
	public int PageIndex { get; init; } = 0;
}

using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class AggregationParameters
{
	public required string RunId { get; init; }
	public required EventType EventType { get; init; }
	public required string PropertyPath { get; init; }
	public required string[] Aggregations { get; init; }
	public DateTime? StartTime { get; init; }
	public DateTime? EndTime { get; init; }
}

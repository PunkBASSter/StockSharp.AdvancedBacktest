namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

public sealed class GetEventsByTypeRequest
{
	public required string RunId { get; init; }
	public required string EventType { get; init; }
	public int? PageSize { get; init; }
	public int? PageIndex { get; init; }
}

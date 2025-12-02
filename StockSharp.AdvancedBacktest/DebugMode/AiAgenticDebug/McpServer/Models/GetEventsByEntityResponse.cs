namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

public sealed class GetEventsByEntityResponse
{
	public required EventDto[] Events { get; init; }
	public required MetadataDto Metadata { get; init; }
}

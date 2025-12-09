using System.Text.Json;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

public sealed class GetEventsByTypeResponse
{
	public required EventDto[] Events { get; init; }
	public required MetadataDto Metadata { get; init; }
}

public sealed class EventDto
{
	public required string EventId { get; init; }
	public required string RunId { get; init; }
	public required string Timestamp { get; init; }
	public required string EventType { get; init; }
	public required string Severity { get; init; }
	public required string Category { get; init; }
	public required JsonElement Properties { get; init; }
	public string? ParentEventId { get; init; }
	public JsonElement? ValidationErrors { get; init; }
}

public sealed class MetadataDto
{
	public required int TotalCount { get; init; }
	public required int ReturnedCount { get; init; }
	public required int PageIndex { get; init; }
	public required int PageSize { get; init; }
	public required bool HasMore { get; init; }
	public required int QueryTimeMs { get; init; }
	public required bool Truncated { get; init; }
}

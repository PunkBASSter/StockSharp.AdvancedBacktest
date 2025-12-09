namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

public sealed class QueryEventSequenceResponse
{
	public required EventSequenceDto[] Sequences { get; init; }
	public required SequenceMetadataDto Metadata { get; init; }
}

public sealed class EventSequenceDto
{
	public required string RootEventId { get; init; }
	public required EventDto[] Events { get; init; }
	public required bool Complete { get; init; }
	public string[]? MissingEventTypes { get; init; }
}

public sealed class SequenceMetadataDto
{
	public required int TotalSequences { get; init; }
	public required int ReturnedCount { get; init; }
	public required int PageIndex { get; init; }
	public required int PageSize { get; init; }
	public required bool HasMore { get; init; }
	public required int QueryTimeMs { get; init; }
}

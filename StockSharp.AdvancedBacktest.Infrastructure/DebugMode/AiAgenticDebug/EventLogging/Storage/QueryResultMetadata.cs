namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class QueryResultMetadata
{
	public required int TotalCount { get; init; }
	public required int ReturnedCount { get; init; }
	public required int PageIndex { get; init; }
	public required int PageSize { get; init; }
	public required bool HasMore { get; init; }
	public int QueryTimeMs { get; set; }
	public required bool Truncated { get; init; }
}

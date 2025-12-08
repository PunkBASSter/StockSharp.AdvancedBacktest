namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class ValidationErrorQueryParameters
{
	public required string RunId { get; init; }
	public string? SeverityFilter { get; init; }
	public int PageSize { get; init; } = 100;
	public int PageIndex { get; init; } = 0;
}

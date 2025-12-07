namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class StateSnapshotQueryParameters
{
	public required string RunId { get; init; }
	public required DateTime Timestamp { get; init; }
	public string? SecuritySymbol { get; init; }
	public bool IncludeIndicators { get; init; } = true;
	public bool IncludeActiveOrders { get; init; } = true;
}

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class StateDeltaQueryParameters
{
	public required string RunId { get; init; }
	public required DateTime StartTimestamp { get; init; }
	public required DateTime EndTimestamp { get; init; }
	public string? SecuritySymbol { get; init; }
}

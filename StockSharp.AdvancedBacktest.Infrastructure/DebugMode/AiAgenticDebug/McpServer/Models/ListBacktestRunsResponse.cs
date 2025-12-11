namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

public sealed class ListBacktestRunsResponse
{
	public required BacktestRunDto[] Runs { get; init; }
	public required int TotalCount { get; init; }
}

public sealed class BacktestRunDto
{
	public required string Id { get; init; }
	public required string StartTime { get; init; }
	public required string EndTime { get; init; }
	public required string StrategyConfigHash { get; init; }
	public required string CreatedAt { get; init; }
}

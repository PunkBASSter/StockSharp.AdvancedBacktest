namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

public sealed class BacktestRunEntity
{
	public required string Id { get; init; }
	public required DateTime StartTime { get; init; }
	public required DateTime EndTime { get; init; }
	public required string StrategyConfigHash { get; init; }
	public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

	public ICollection<EventEntity>? Events { get; init; }
}

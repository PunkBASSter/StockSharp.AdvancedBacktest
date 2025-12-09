namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

public sealed class EventEntity
{
	public long Id { get; init; }
	public required string EventId { get; init; }
	public required string RunId { get; init; }
	public required DateTime Timestamp { get; init; }
	public required EventType EventType { get; init; }
	public required EventSeverity Severity { get; init; }
	public required EventCategory Category { get; init; }
	public required string Properties { get; init; }
	public string? ParentEventId { get; init; }
	public string? ValidationErrors { get; init; }

	public BacktestRunEntity? Run { get; init; }
	public EventEntity? ParentEvent { get; init; }
	public ICollection<EventEntity>? ChildEvents { get; init; }
}

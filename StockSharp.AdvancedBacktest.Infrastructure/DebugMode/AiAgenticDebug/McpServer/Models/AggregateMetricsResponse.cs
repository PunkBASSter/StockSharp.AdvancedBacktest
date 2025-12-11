namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

public sealed class AggregateMetricsResponse
{
	public required AggregationsDto Aggregations { get; init; }
	public required AggregationMetadataDto Metadata { get; init; }
}

public sealed class AggregationsDto
{
	public int Count { get; init; }
	public decimal? Sum { get; init; }
	public decimal? Avg { get; init; }
	public decimal? Min { get; init; }
	public decimal? Max { get; init; }
	public decimal? StdDev { get; init; }
}

public sealed class AggregationMetadataDto
{
	public int TotalEvents { get; init; }
	public int QueryTimeMs { get; init; }
	public required string EventType { get; init; }
	public required string PropertyPath { get; init; }
}

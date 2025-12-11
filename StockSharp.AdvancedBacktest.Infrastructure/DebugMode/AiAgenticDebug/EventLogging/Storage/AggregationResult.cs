namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class AggregationResult
{
	public required AggregationValues Aggregations { get; init; }
	public required AggregationMetadata Metadata { get; init; }
}

public sealed class AggregationValues
{
	public int Count { get; init; }
	public decimal? Sum { get; init; }
	public decimal? Avg { get; init; }
	public decimal? Min { get; init; }
	public decimal? Max { get; init; }
	public decimal? StdDev { get; init; }
}

public sealed class AggregationMetadata
{
	public int TotalEvents { get; init; }
	public int QueryTimeMs { get; set; }
	public required string EventType { get; init; }
	public required string PropertyPath { get; init; }
}

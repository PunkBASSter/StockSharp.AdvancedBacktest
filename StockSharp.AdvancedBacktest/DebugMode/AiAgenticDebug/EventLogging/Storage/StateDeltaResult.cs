namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class StateDeltaResult
{
	public required DateTime StartTimestamp { get; init; }
	public required DateTime EndTimestamp { get; init; }
	public required string RunId { get; init; }
	public required IReadOnlyList<PositionChange> PositionChanges { get; init; }
	public required IReadOnlyList<IndicatorChange> IndicatorChanges { get; init; }
	public PnLChange? PnlChange { get; init; }
	public required StateDeltaMetadata Metadata { get; init; }
}

public sealed class PositionChange
{
	public required string SecuritySymbol { get; init; }
	public decimal QuantityBefore { get; init; }
	public decimal QuantityAfter { get; init; }
	public decimal QuantityChange => QuantityAfter - QuantityBefore;
	public decimal AveragePriceBefore { get; init; }
	public decimal AveragePriceAfter { get; init; }
}

public sealed class IndicatorChange
{
	public required string Name { get; init; }
	public required string SecuritySymbol { get; init; }
	public decimal? ValueBefore { get; init; }
	public decimal? ValueAfter { get; init; }
	public decimal? ValueChange => ValueAfter.HasValue ? ValueAfter.Value - (ValueBefore ?? 0m) : null;
}

public sealed class PnLChange
{
	public decimal RealizedBefore { get; init; }
	public decimal RealizedAfter { get; init; }
	public decimal RealizedChange => RealizedAfter - RealizedBefore;
	public decimal UnrealizedBefore { get; init; }
	public decimal UnrealizedAfter { get; init; }
	public decimal UnrealizedChange => UnrealizedAfter - UnrealizedBefore;
	public decimal TotalChange => RealizedChange + UnrealizedChange;
}

public sealed class StateDeltaMetadata
{
	public int QueryTimeMs { get; set; }
}

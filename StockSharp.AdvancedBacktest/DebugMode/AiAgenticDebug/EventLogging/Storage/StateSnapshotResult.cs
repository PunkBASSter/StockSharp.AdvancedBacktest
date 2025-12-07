namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class StateSnapshotResult
{
	public required DateTime Timestamp { get; init; }
	public required string RunId { get; init; }
	public required StrategyState State { get; init; }
	public required StateSnapshotMetadata Metadata { get; init; }
}

public sealed class StrategyState
{
	public required IReadOnlyList<PositionState> Positions { get; init; }
	public required IReadOnlyList<IndicatorState> Indicators { get; init; }
	public required IReadOnlyList<ActiveOrderState> ActiveOrders { get; init; }
	public required PnLState Pnl { get; init; }
}

public sealed class PositionState
{
	public required string SecuritySymbol { get; init; }
	public required decimal Quantity { get; init; }
	public required decimal AveragePrice { get; init; }
	public decimal UnrealizedPnL { get; init; }
	public decimal RealizedPnL { get; init; }
}

public sealed class IndicatorState
{
	public required string Name { get; init; }
	public required string SecuritySymbol { get; init; }
	public required decimal Value { get; init; }
	public Dictionary<string, object>? Parameters { get; init; }
}

public sealed class ActiveOrderState
{
	public required string OrderId { get; init; }
	public required string SecuritySymbol { get; init; }
	public required string Direction { get; init; }
	public required decimal Quantity { get; init; }
	public required decimal Price { get; init; }
}

public sealed class PnLState
{
	public decimal Total { get; init; }
	public decimal Realized { get; init; }
	public decimal Unrealized { get; init; }
}

public sealed class StateSnapshotMetadata
{
	public int QueryTimeMs { get; set; }
	public bool Reconstructed { get; init; } = true;
}

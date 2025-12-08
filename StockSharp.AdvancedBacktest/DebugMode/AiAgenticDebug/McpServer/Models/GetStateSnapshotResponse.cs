namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

public sealed class GetStateSnapshotResponse
{
	public required DateTime Timestamp { get; init; }
	public required string RunId { get; init; }
	public required StateDto State { get; init; }
	public required StateSnapshotMetadataDto Metadata { get; init; }
}

public sealed class StateDto
{
	public required IReadOnlyList<PositionDto> Positions { get; init; }
	public required IReadOnlyList<IndicatorDto> Indicators { get; init; }
	public required IReadOnlyList<ActiveOrderDto> ActiveOrders { get; init; }
	public required PnlDto Pnl { get; init; }
}

public sealed class PositionDto
{
	public required string SecuritySymbol { get; init; }
	public decimal Quantity { get; init; }
	public decimal AveragePrice { get; init; }
	public decimal UnrealizedPnL { get; init; }
	public decimal RealizedPnL { get; init; }
}

public sealed class IndicatorDto
{
	public required string Name { get; init; }
	public required string SecuritySymbol { get; init; }
	public decimal Value { get; init; }
	public Dictionary<string, object>? Parameters { get; init; }
}

public sealed class ActiveOrderDto
{
	public required string OrderId { get; init; }
	public required string SecuritySymbol { get; init; }
	public required string Direction { get; init; }
	public decimal Quantity { get; init; }
	public decimal Price { get; init; }
}

public sealed class PnlDto
{
	public decimal Total { get; init; }
	public decimal Realized { get; init; }
	public decimal Unrealized { get; init; }
}

public sealed class StateSnapshotMetadataDto
{
	public int QueryTimeMs { get; init; }
	public bool Reconstructed { get; init; }
}

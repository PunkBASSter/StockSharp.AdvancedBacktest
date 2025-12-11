using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

/// <summary>
/// Represents a trading signal with entry price, volume, and optional stop-loss/take-profit levels.
/// </summary>
public record TradeSignal
{
    public required Sides Direction { get; init; }
    public required decimal EntryPrice { get; init; }
    public required decimal Volume { get; init; }
    public decimal? StopLoss { get; init; }
    public decimal? TakeProfit { get; init; }
    public OrderTypes OrderType { get; init; } = OrderTypes.Limit;
    public DateTime? ExpiryTime { get; init; }
    public bool UseMarketProtectiveOrders { get; init; }

    public void Validate()
    {
        if (EntryPrice <= 0)
            throw new ArgumentException("Entry price must be positive", nameof(EntryPrice));

        if (Volume <= 0)
            throw new ArgumentException("Volume must be positive", nameof(Volume));

        if (Direction == Sides.Buy)
        {
            if (StopLoss.HasValue && StopLoss.Value >= EntryPrice)
                throw new ArgumentException($"Stop-loss ({StopLoss}) must be below entry price ({EntryPrice}) for long positions", nameof(StopLoss));

            if (TakeProfit.HasValue && TakeProfit.Value <= EntryPrice)
                throw new ArgumentException($"Take-profit ({TakeProfit}) must be above entry price ({EntryPrice}) for long positions", nameof(TakeProfit));
        }
        else if (Direction == Sides.Sell)
        {
            if (StopLoss.HasValue && StopLoss.Value <= EntryPrice)
                throw new ArgumentException($"Stop-loss ({StopLoss}) must be above entry price ({EntryPrice}) for short positions", nameof(StopLoss));

            if (TakeProfit.HasValue && TakeProfit.Value >= EntryPrice)
                throw new ArgumentException($"Take-profit ({TakeProfit}) must be below entry price ({EntryPrice}) for short positions", nameof(TakeProfit));
        }
    }
}

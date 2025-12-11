using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public sealed class ExtendedTradeSignal
{
    public Sides Direction { get; }
    public decimal EntryPrice { get; }
    public decimal EntryVolume { get; }
    public OrderTypes EntryOrderType { get; }
    public IReadOnlyList<ClosingOrderDefinition> ClosingOrders { get; }
    public decimal? StopLossPrice { get; }
    public string? GroupId { get; }
    public DateTime? ExpiryTime { get; }

    public decimal TotalClosingVolume => ClosingOrders.Sum(o => o.Volume);
    public bool IsVolumeMatched => TotalClosingVolume == EntryVolume;

    public ExtendedTradeSignal(
        Sides direction,
        decimal entryPrice,
        decimal entryVolume,
        IEnumerable<ClosingOrderDefinition> closingOrders,
        OrderTypes entryOrderType = OrderTypes.Limit,
        decimal? stopLossPrice = null,
        string? groupId = null,
        DateTime? expiryTime = null,
        bool skipValidation = false)
    {
        Direction = direction;
        EntryPrice = entryPrice;
        EntryVolume = entryVolume;
        EntryOrderType = entryOrderType;
        ClosingOrders = closingOrders.ToList().AsReadOnly();
        StopLossPrice = stopLossPrice;
        GroupId = groupId;
        ExpiryTime = expiryTime;

        if (!skipValidation)
        {
            Validate();
        }
    }

    public void Validate(bool throwIfNotMatchingVolume = true)
    {
        if (EntryPrice <= 0)
            throw new ArgumentException("Entry price must be positive", nameof(EntryPrice));

        if (EntryVolume <= 0)
            throw new ArgumentException("Entry volume must be positive", nameof(EntryVolume));

        if (ClosingOrders.Count == 0)
            throw new ArgumentException("At least one closing order is required", nameof(ClosingOrders));

        if (StopLossPrice.HasValue)
        {
            if (Direction == Sides.Buy && StopLossPrice.Value >= EntryPrice)
                throw new ArgumentException(
                    $"Stop-loss ({StopLossPrice}) must be below entry price ({EntryPrice}) for long positions",
                    nameof(StopLossPrice));

            if (Direction == Sides.Sell && StopLossPrice.Value <= EntryPrice)
                throw new ArgumentException(
                    $"Stop-loss ({StopLossPrice}) must be above entry price ({EntryPrice}) for short positions",
                    nameof(StopLossPrice));
        }

        foreach (var closingOrder in ClosingOrders)
        {
            closingOrder.Validate();
        }

        if (throwIfNotMatchingVolume && !IsVolumeMatched)
        {
            throw new ArgumentException(
                $"Total closing volume ({TotalClosingVolume}) must equal entry volume ({EntryVolume})",
                nameof(ClosingOrders));
        }
    }
}

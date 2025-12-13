using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

/// <summary>
/// Represents a trading signal with entry price, volume, and optional stop-loss/take-profit levels.
/// </summary>
public record OrderRequest
{
    public Order EntryOrder { get; init; } = null!;

    public required Sides Direction { get; init; }
    public required decimal Price { get; init; }
    public required decimal Volume { get; init; }
    public OrderTypes OrderType { get; init; } = OrderTypes.Limit;
    public DateTimeOffset? ExpiryTime { get; init; }
    public bool UseMarketProtectiveOrders { get; init; } //works correctly with candle-based backtesting only for Market orders
    public string EntryId { get; private set; } = Guid.NewGuid().ToString(); //an identifier to link related exit orders to the entry order
    public string CancellationTag { get; private set; } = null!; //marks orders to cancel when one order with the same tag is filled (TODO handle partial fills)
    public List<OrderRequest> ChildOrders { get; private set; } = [];

    private Lazy<string> _defaultTag => new(() => $"{Direction}-{Price}-{Volume}-{EntryId}");

    public void Validate()
    {
        if (Price <= 0)
            throw new ArgumentException("Entry price must be positive", nameof(Price));

        if (Volume <= 0)
            throw new ArgumentException("Volume must be positive", nameof(Volume));

        var aboveEntryOrdersVolumeSum = ChildOrders.Where(o => o.Price > Price).Sum(o => o.Volume);
        if (aboveEntryOrdersVolumeSum > 0 && aboveEntryOrdersVolumeSum != Volume)
            throw new ArgumentException("Total volume of above-entry orders must match entry volume", nameof(ChildOrders));
        
        var belowEntryOrdersVolumeSum = ChildOrders.Where(o => o.Price < Price).Sum(o => o.Volume);
        if (belowEntryOrdersVolumeSum > 0 && belowEntryOrdersVolumeSum != Volume)
            throw new ArgumentException("Total volume of below-entry orders must match entry volume", nameof(ChildOrders));
    }

    public OrderRequest AddSlTp(decimal sl, decimal? volume, string? cancellationTag)
    {
        if (sl <= 0 || (Direction == Sides.Buy && sl >= Price) || (Direction == Sides.Sell && sl <= Price))
            throw new ArgumentException("Invalid", nameof(sl));

        var slOrderRequest = new OrderRequest
        {
            Direction = Direction == Sides.Buy ? Sides.Sell : Sides.Buy,
            Price = sl,
            Volume = volume ?? Volume,
            OrderType = UseMarketProtectiveOrders ? OrderTypes.Market : OrderTypes.Limit,
            EntryId = EntryId,
            CancellationTag = cancellationTag ?? _defaultTag.Value
        };
        ChildOrders.Add(slOrderRequest);
        return this;
    }

    public OrderRequest WithTakeProfit(decimal tp, decimal? volume, string? cancellationTag)
    {
        if (tp <= 0 || (Direction == Sides.Buy && tp <= Price) || (Direction == Sides.Sell && tp >= Price))
            throw new ArgumentException("Invalid", nameof(tp));

        var tpOrderRequest = new OrderRequest
        {
            Direction = Direction == Sides.Buy ? Sides.Sell : Sides.Buy,
            Price = tp,
            Volume = volume ?? Volume,
            OrderType = UseMarketProtectiveOrders ? OrderTypes.Market : OrderTypes.Limit,
            EntryId = EntryId,
            CancellationTag = cancellationTag ?? _defaultTag.Value
        };
        ChildOrders.Add(tpOrderRequest);
        return this;
    }
}


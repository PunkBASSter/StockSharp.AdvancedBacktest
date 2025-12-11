using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public sealed class ClosingOrderDefinition
{
    public decimal Price { get; }
    public decimal Volume { get; }
    public OrderTypes OrderType { get; }

    public ClosingOrderDefinition(
        decimal price,
        decimal volume,
        OrderTypes orderType = OrderTypes.Limit)
    {
        if (volume <= 0)
            throw new ArgumentException("Volume must be positive", nameof(volume));
        if (orderType != OrderTypes.Market && price <= 0)
            throw new ArgumentException("Price must be positive for non-market orders", nameof(price));

        Price = price;
        Volume = volume;
        OrderType = orderType;
    }

    public void Validate()
    {
        if (Volume <= 0)
            throw new ArgumentException("Volume must be positive", nameof(Volume));
        if (OrderType != OrderTypes.Market && Price <= 0)
            throw new ArgumentException("Price must be positive for non-market orders", nameof(Price));
    }
}

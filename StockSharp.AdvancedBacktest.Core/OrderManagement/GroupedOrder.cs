using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public sealed class GroupedOrder
{
    public string OrderId { get; }
    public GroupedOrderRole Role { get; }
    public decimal Price { get; private set; }
    public decimal Volume { get; }
    public decimal FilledVolume { get; private set; }
    public OrderTypes OrderType { get; }
    public GroupedOrderState State { get; private set; }
    public Order? BrokerOrder { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? FilledAt { get; private set; }

    public decimal RemainingVolume => Volume - FilledVolume;
    public bool IsFilled => FilledVolume >= Volume;
    public bool IsPartiallyFilled => FilledVolume > 0 && FilledVolume < Volume;

    public GroupedOrder(
        string orderId,
        GroupedOrderRole role,
        decimal price,
        decimal volume,
        OrderTypes orderType)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        if (volume <= 0)
            throw new ArgumentException("Volume must be positive", nameof(volume));
        if (orderType != OrderTypes.Market && price <= 0)
            throw new ArgumentException("Price must be positive for non-market orders", nameof(price));

        OrderId = orderId;
        Role = role;
        Price = price;
        Volume = volume;
        OrderType = orderType;
        State = GroupedOrderState.Pending;
        FilledVolume = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetState(GroupedOrderState state)
    {
        State = state;
    }

    public void AddFilledVolume(decimal fillVolume)
    {
        if (fillVolume <= 0)
            throw new ArgumentException("Fill volume must be positive", nameof(fillVolume));
        if (FilledVolume + fillVolume > Volume)
            throw new InvalidOperationException(
                $"Fill volume {fillVolume} would exceed order volume {Volume} (currently filled: {FilledVolume})");

        FilledVolume += fillVolume;
    }

    public void MarkFilled()
    {
        State = GroupedOrderState.Filled;
        FilledAt = DateTime.UtcNow;
    }

    public void SetBrokerOrder(Order order)
    {
        BrokerOrder = order;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (OrderType != OrderTypes.Market && newPrice <= 0)
            throw new ArgumentException("Price must be positive for non-market orders", nameof(newPrice));

        Price = newPrice;
    }
}

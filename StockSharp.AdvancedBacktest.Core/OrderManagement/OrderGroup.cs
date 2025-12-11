using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public sealed class OrderGroup
{
    public string GroupId { get; }
    public string SecurityId { get; }
    public Sides Direction { get; }
    public OrderGroupState State { get; private set; }
    public GroupedOrder OpeningOrder { get; }
    public IReadOnlyList<GroupedOrder> ClosingOrders { get; }
    public DateTime CreatedAt { get; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private readonly List<GroupedOrder> _closingOrders;

    public decimal TotalClosingVolume => ClosingOrders.Sum(o => o.Volume);
    public decimal FilledClosingVolume => ClosingOrders.Sum(o => o.FilledVolume);
    public decimal RemainingVolume => OpeningOrder.FilledVolume - FilledClosingVolume;
    public bool IsVolumeMatched => TotalClosingVolume == OpeningOrder.Volume;
    public bool AllClosingOrdersFilled => ClosingOrders.All(o => o.IsFilled);

    public OrderGroup(
        string groupId,
        string securityId,
        Sides direction,
        GroupedOrder openingOrder,
        IEnumerable<GroupedOrder> closingOrders)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        ArgumentNullException.ThrowIfNull(securityId);
        ArgumentNullException.ThrowIfNull(openingOrder);
        ArgumentNullException.ThrowIfNull(closingOrders);

        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("GroupId cannot be empty", nameof(groupId));
        if (string.IsNullOrWhiteSpace(securityId))
            throw new ArgumentException("SecurityId cannot be empty", nameof(securityId));
        if (openingOrder.Role != GroupedOrderRole.Opening)
            throw new ArgumentException("Opening order must have Opening role", nameof(openingOrder));

        _closingOrders = closingOrders.ToList();

        if (_closingOrders.Count == 0)
            throw new ArgumentException("At least one closing order is required", nameof(closingOrders));

        foreach (var closingOrder in _closingOrders)
        {
            if (closingOrder.Role != GroupedOrderRole.Closing)
                throw new ArgumentException("Closing orders must have Closing role", nameof(closingOrders));
        }

        GroupId = groupId;
        SecurityId = securityId;
        Direction = direction;
        OpeningOrder = openingOrder;
        ClosingOrders = _closingOrders.AsReadOnly();
        State = OrderGroupState.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetState(OrderGroupState state)
    {
        State = state;
    }

    public void MarkActivated()
    {
        State = OrderGroupState.Active;
        ActivatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        State = OrderGroupState.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        State = OrderGroupState.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public GroupedOrder? GetOrderById(string orderId)
    {
        if (OpeningOrder.OrderId == orderId)
            return OpeningOrder;

        return _closingOrders.FirstOrDefault(o => o.OrderId == orderId);
    }

    public void AddClosingOrder(GroupedOrder closingOrder)
    {
        if (closingOrder.Role != GroupedOrderRole.Closing)
            throw new ArgumentException("Order must have Closing role", nameof(closingOrder));

        _closingOrders.Add(closingOrder);
    }

    public void RemoveClosingOrder(string orderId)
    {
        var order = _closingOrders.FirstOrDefault(o => o.OrderId == orderId);
        if (order != null)
        {
            _closingOrders.Remove(order);
        }
    }
}

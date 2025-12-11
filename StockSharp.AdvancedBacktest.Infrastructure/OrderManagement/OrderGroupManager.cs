using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Infrastructure.OrderManagement;

public sealed class OrderGroupManager : IOrderGroupManager
{
    private readonly IStrategyOrderOperations _operations;
    private readonly IOrderGroupPersistence _persistence;
    private readonly Dictionary<string, OrderGroup> _groupsById = new();
    private readonly Dictionary<string, List<OrderGroup>> _groupsBySecurityId = new();
    private readonly Dictionary<long, (OrderGroup Group, GroupedOrder Order)> _ordersByBrokerId = new();

    public OrderGroupLimits Limits { get; }

    public event Action<OrderGroup, GroupedOrder>? OrderActivated;
    public event Action<OrderGroup>? GroupCompleted;
    public event Action<OrderGroup>? GroupCancelled;
    public event Action<OrderGroup, GroupedOrder>? OrderRejected;

    public OrderGroupManager(
        IStrategyOrderOperations operations,
        OrderGroupLimits limits,
        IOrderGroupPersistence? persistence = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(limits);

        _operations = operations;
        Limits = limits;
        _persistence = persistence ?? NullOrderGroupPersistence.Instance;
    }

    public OrderGroup CreateOrderGroup(ExtendedTradeSignal signal, bool? throwIfNotMatchingVolume = null, decimal? currentEquity = null)
    {
        ArgumentNullException.ThrowIfNull(signal);

        var shouldThrowOnMismatch = throwIfNotMatchingVolume ?? Limits.ThrowIfNotMatchingVolume;

        signal.Validate(throwIfNotMatchingVolume: shouldThrowOnMismatch);

        var securityId = _operations.Security.Id;

        ValidateGroupLimits(securityId);
        ValidateRiskLimits(signal, currentEquity);

        var groupId = signal.GroupId ?? GenerateGroupId(securityId, signal.EntryPrice);

        var openingOrder = CreateOpeningGroupedOrder(signal);
        var closingOrders = CreateClosingGroupedOrders(signal);

        var group = new OrderGroup(
            groupId: groupId,
            securityId: securityId,
            direction: signal.Direction,
            openingOrder: openingOrder,
            closingOrders: closingOrders);

        var brokerOrder = PlaceOpeningOrder(signal);
        openingOrder.SetBrokerOrder(brokerOrder);
        openingOrder.SetState(GroupedOrderState.Active);

        RegisterGroup(group);

        if (brokerOrder.Id.HasValue && brokerOrder.Id.Value != 0)
        {
            _ordersByBrokerId[brokerOrder.Id.Value] = (group, openingOrder);
        }

        PersistState(securityId);

        return group;
    }

    public IReadOnlyList<OrderGroup> GetActiveGroups(string? securityId = null)
    {
        if (securityId == null)
        {
            return _groupsById.Values
                .Where(g => g.State != OrderGroupState.Completed && g.State != OrderGroupState.Cancelled)
                .ToList();
        }

        if (_groupsBySecurityId.TryGetValue(securityId, out var groups))
        {
            return groups
                .Where(g => g.State != OrderGroupState.Completed && g.State != OrderGroupState.Cancelled)
                .ToList();
        }

        return [];
    }

    public OrderGroup? GetGroupById(string groupId)
    {
        return _groupsById.TryGetValue(groupId, out var group) ? group : null;
    }

    public void AdjustOrderPrice(string groupId, string orderId, decimal newPrice)
    {
        var group = GetGroupById(groupId)
            ?? throw new InvalidOperationException($"Group {groupId} not found");

        var order = group.GetOrderById(orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found in group {groupId}");

        if (order.State != GroupedOrderState.Pending && order.State != GroupedOrderState.Active)
        {
            throw new InvalidOperationException(
                $"Cannot adjust price of order in state {order.State}. Only Pending or Active orders can be adjusted.");
        }

        if (order.BrokerOrder != null)
        {
            _operations.CancelOrder(order.BrokerOrder);
            if (order.BrokerOrder.Id.HasValue && order.BrokerOrder.Id.Value != 0)
            {
                _ordersByBrokerId.Remove(order.BrokerOrder.Id.Value);
            }
        }

        order.UpdatePrice(newPrice);

        var brokerOrder = PlaceOrderForGroupedOrder(order, group.Direction);
        order.SetBrokerOrder(brokerOrder);

        if (brokerOrder.Id.HasValue && brokerOrder.Id.Value != 0)
        {
            _ordersByBrokerId[brokerOrder.Id.Value] = (group, order);
        }

        PersistState(group.SecurityId);
    }

    public void CloseGroup(string groupId)
    {
        var group = GetGroupById(groupId)
            ?? throw new InvalidOperationException($"Group {groupId} not found");

        group.SetState(OrderGroupState.Closing);

        CancelPendingOrders(group);

        if (group.RemainingVolume > 0)
        {
            PlaceMarketCloseOrder(group);
        }

        PersistState(group.SecurityId);

        GroupCancelled?.Invoke(group);
    }

    public void CloseAllGroups(string? securityId = null)
    {
        var groups = GetActiveGroups(securityId).ToList();

        foreach (var group in groups)
        {
            CloseGroup(group.GroupId);
        }
    }

    public void OnOrderFilled(Order order, MyTrade trade)
    {
        if (!order.Id.HasValue || !_ordersByBrokerId.TryGetValue(order.Id.Value, out var entry))
            return;

        var (group, groupedOrder) = entry;

        groupedOrder.AddFilledVolume(trade.Trade.Volume);

        if (groupedOrder.IsFilled)
        {
            groupedOrder.MarkFilled();
        }
        else
        {
            groupedOrder.SetState(GroupedOrderState.PartiallyFilled);
        }

        if (groupedOrder.Role == GroupedOrderRole.Opening && group.State == OrderGroupState.Pending)
        {
            HandleOpeningOrderFilled(group, groupedOrder);
        }
        else if (groupedOrder.Role == GroupedOrderRole.Closing)
        {
            HandleClosingOrderFilled(group, groupedOrder);
        }

        PersistState(group.SecurityId);
    }

    public void OnOrderCancelled(Order order)
    {
        if (!order.Id.HasValue || !_ordersByBrokerId.TryGetValue(order.Id.Value, out var entry))
            return;

        var (group, groupedOrder) = entry;

        groupedOrder.SetState(GroupedOrderState.Cancelled);

        if (groupedOrder.Role == GroupedOrderRole.Opening && group.State == OrderGroupState.Pending)
        {
            group.MarkCancelled();
            GroupCancelled?.Invoke(group);
        }

        PersistState(group.SecurityId);
    }

    public void OnOrderRejected(Order order)
    {
        if (!order.Id.HasValue || !_ordersByBrokerId.TryGetValue(order.Id.Value, out var entry))
            return;

        var (group, groupedOrder) = entry;

        groupedOrder.SetState(GroupedOrderState.Rejected);

        OrderRejected?.Invoke(group, groupedOrder);

        if (groupedOrder.Role == GroupedOrderRole.Opening && group.State == OrderGroupState.Pending)
        {
            group.MarkCancelled();
            GroupCancelled?.Invoke(group);
        }

        PersistState(group.SecurityId);
    }

    public void Reset()
    {
        _groupsById.Clear();
        _groupsBySecurityId.Clear();
        _ordersByBrokerId.Clear();
    }

    public decimal CalculateRiskPercent(decimal entryPrice, decimal volume, decimal stopLossPrice, decimal currentEquity)
    {
        if (currentEquity <= 0)
            throw new ArgumentException("Current equity must be positive", nameof(currentEquity));

        var stopDistance = Math.Abs(entryPrice - stopLossPrice);
        var stopDistancePercent = stopDistance / entryPrice;
        var riskAmount = entryPrice * volume * stopDistancePercent;
        var riskPercent = (riskAmount / currentEquity) * 100m;

        return riskPercent;
    }

    private void ValidateGroupLimits(string securityId)
    {
        if (_groupsBySecurityId.TryGetValue(securityId, out var groups))
        {
            var activeGroupCount = groups.Count(g =>
                g.State != OrderGroupState.Completed && g.State != OrderGroupState.Cancelled);

            if (activeGroupCount >= Limits.MaxGroupsPerSecurity)
            {
                throw new InvalidOperationException(
                    $"Maximum number of order groups ({Limits.MaxGroupsPerSecurity}) reached for security {securityId}");
            }
        }
    }

    private void ValidateRiskLimits(ExtendedTradeSignal signal, decimal? currentEquity)
    {
        if (!currentEquity.HasValue || !signal.StopLossPrice.HasValue)
            return;

        var riskPercent = CalculateRiskPercent(
            signal.EntryPrice,
            signal.EntryVolume,
            signal.StopLossPrice.Value,
            currentEquity.Value);

        if (riskPercent > Limits.MaxRiskPercentPerGroup)
        {
            throw new InvalidOperationException(
                $"Risk {riskPercent:F2}% exceeds maximum allowed risk per group ({Limits.MaxRiskPercentPerGroup:F2}%)");
        }
    }

    private static string GenerateGroupId(string securityId, decimal entryPrice)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        return $"{securityId}_{timestamp}_{entryPrice}";
    }

    private static GroupedOrder CreateOpeningGroupedOrder(ExtendedTradeSignal signal)
    {
        var orderId = $"open_{Guid.NewGuid():N}";

        return new GroupedOrder(
            orderId: orderId,
            role: GroupedOrderRole.Opening,
            price: signal.EntryPrice,
            volume: signal.EntryVolume,
            orderType: signal.EntryOrderType);
    }

    private static List<GroupedOrder> CreateClosingGroupedOrders(ExtendedTradeSignal signal)
    {
        var closingOrders = new List<GroupedOrder>();
        var index = 0;

        foreach (var definition in signal.ClosingOrders)
        {
            var orderId = $"close_{index}_{Guid.NewGuid():N}";

            var order = new GroupedOrder(
                orderId: orderId,
                role: GroupedOrderRole.Closing,
                price: definition.Price,
                volume: definition.Volume,
                orderType: definition.OrderType);

            closingOrders.Add(order);
            index++;
        }

        return closingOrders;
    }

    private Order PlaceOpeningOrder(ExtendedTradeSignal signal)
    {
        return signal.Direction == Sides.Buy
            ? signal.EntryOrderType == OrderTypes.Market
                ? _operations.BuyMarket(signal.EntryVolume)
                : _operations.BuyLimit(signal.EntryPrice, signal.EntryVolume)
            : signal.EntryOrderType == OrderTypes.Market
                ? _operations.SellMarket(signal.EntryVolume)
                : _operations.SellLimit(signal.EntryPrice, signal.EntryVolume);
    }

    private Order PlaceOrderForGroupedOrder(GroupedOrder groupedOrder, Sides groupDirection)
    {
        var orderDirection = groupedOrder.Role == GroupedOrderRole.Opening
            ? groupDirection
            : groupDirection == Sides.Buy ? Sides.Sell : Sides.Buy;

        return orderDirection == Sides.Buy
            ? groupedOrder.OrderType == OrderTypes.Market
                ? _operations.BuyMarket(groupedOrder.Volume)
                : _operations.BuyLimit(groupedOrder.Price, groupedOrder.Volume)
            : groupedOrder.OrderType == OrderTypes.Market
                ? _operations.SellMarket(groupedOrder.Volume)
                : _operations.SellLimit(groupedOrder.Price, groupedOrder.Volume);
    }

    private void RegisterGroup(OrderGroup group)
    {
        _groupsById[group.GroupId] = group;

        if (!_groupsBySecurityId.TryGetValue(group.SecurityId, out var groups))
        {
            groups = [];
            _groupsBySecurityId[group.SecurityId] = groups;
        }

        groups.Add(group);
    }

    private void HandleOpeningOrderFilled(OrderGroup group, GroupedOrder openingOrder)
    {
        group.MarkActivated();

        PlaceClosingOrders(group, openingOrder.FilledVolume);

        OrderActivated?.Invoke(group, openingOrder);
    }

    private void PlaceClosingOrders(OrderGroup group, decimal filledOpeningVolume)
    {
        var closingDirection = group.Direction == Sides.Buy ? Sides.Sell : Sides.Buy;
        var scaleFactor = filledOpeningVolume / group.OpeningOrder.Volume;

        foreach (var closingOrder in group.ClosingOrders)
        {
            var scaledVolume = closingOrder.Volume * scaleFactor;

            if (scaledVolume <= 0)
                continue;

            var brokerOrder = closingDirection == Sides.Buy
                ? closingOrder.OrderType == OrderTypes.Market
                    ? _operations.BuyMarket(scaledVolume)
                    : _operations.BuyLimit(closingOrder.Price, scaledVolume)
                : closingOrder.OrderType == OrderTypes.Market
                    ? _operations.SellMarket(scaledVolume)
                    : _operations.SellLimit(closingOrder.Price, scaledVolume);

            closingOrder.SetBrokerOrder(brokerOrder);
            closingOrder.SetState(GroupedOrderState.Active);

            if (brokerOrder.Id.HasValue && brokerOrder.Id.Value != 0)
            {
                _ordersByBrokerId[brokerOrder.Id.Value] = (group, closingOrder);
            }
        }
    }

    private void HandleClosingOrderFilled(OrderGroup group, GroupedOrder closingOrder)
    {
        if (group.AllClosingOrdersFilled)
        {
            group.MarkCompleted();
            GroupCompleted?.Invoke(group);
        }
    }

    private void CancelPendingOrders(OrderGroup group)
    {
        foreach (var closingOrder in group.ClosingOrders)
        {
            if (closingOrder.State == GroupedOrderState.Pending ||
                closingOrder.State == GroupedOrderState.Active ||
                closingOrder.State == GroupedOrderState.PartiallyFilled)
            {
                if (closingOrder.BrokerOrder != null)
                {
                    _operations.CancelOrder(closingOrder.BrokerOrder);
                }

                closingOrder.SetState(GroupedOrderState.Cancelled);
            }
        }

        if (group.OpeningOrder.State == GroupedOrderState.Pending ||
            group.OpeningOrder.State == GroupedOrderState.Active)
        {
            if (group.OpeningOrder.BrokerOrder != null)
            {
                _operations.CancelOrder(group.OpeningOrder.BrokerOrder);
            }

            group.OpeningOrder.SetState(GroupedOrderState.Cancelled);
        }
    }

    private void PlaceMarketCloseOrder(OrderGroup group)
    {
        var closingDirection = group.Direction == Sides.Buy ? Sides.Sell : Sides.Buy;

        if (closingDirection == Sides.Buy)
        {
            _operations.BuyMarket(group.RemainingVolume);
        }
        else
        {
            _operations.SellMarket(group.RemainingVolume);
        }
    }

    private void PersistState(string securityId)
    {
        if (!_persistence.IsEnabled)
            return;

        var groups = GetActiveGroups(securityId);
        _persistence.Save(securityId, groups);
    }
}

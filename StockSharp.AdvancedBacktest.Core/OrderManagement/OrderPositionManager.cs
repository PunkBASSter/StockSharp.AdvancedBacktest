using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.Algo.Candles;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public class OrderPositionManager
{
    private readonly IStrategyOrderOperations _strategy;
    private readonly OrderRegistry _orderRegistry;
    private readonly Security _security;
    private readonly PartialFillRetryHandler _retryHandler;
    private ICandleMessage? _lastCandle;

    public event Action<string, object?>? OnOrderEvent;

    public OrderPositionManager(IStrategyOrderOperations strategy, Security security, string strategyName)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _orderRegistry = new OrderRegistry(strategyName) { MaxConcurrentGroups = 5 };
        _retryHandler = new PartialFillRetryHandler();
        _retryHandler.OnRetryEvent += (eventType, data) => OnOrderEvent?.Invoke(eventType, data);
    }

    public EntryOrderGroup[] ActiveOrders() => _orderRegistry.GetActiveGroups();

    public bool HasReachedRetryLimit() => _retryHandler.HasReachedRetryLimit();

    public bool RequiresManualIntervention() => _retryHandler.RequiresManualIntervention();

    public Order? HandleOrderRequest(OrderRequest? orderRequest)
    {
        if (orderRequest == null)
        {
            CancelPendingOrders();
            return null;
        }

        var priceStep = PriceStepHelper.GetPriceStep(_security);
        var existing = _orderRegistry.FindMatchingGroup(orderRequest, priceStep);

        if (existing != null && existing.State == OrderGroupState.Pending)
            return null;

        // Check if at capacity - skip new signal if max concurrent groups reached
        var activeGroups = _orderRegistry.GetActiveGroups();
        if (activeGroups.Length >= _orderRegistry.MaxConcurrentGroups)
        {
            OnOrderEvent?.Invoke("MaxConcurrentGroupsReached", new {
                ActiveCount = activeGroups.Length,
                MaxAllowed = _orderRegistry.MaxConcurrentGroups,
                SkippedOrder = orderRequest.Order.Side
            });
            return null;
        }

        _orderRegistry.RegisterGroup(orderRequest.Order, orderRequest.ProtectivePairs);
        return orderRequest.Order;
    }

    public bool CheckProtectionLevels(ICandleMessage candle)
    {
        _lastCandle = candle;

        var activeGroups = _orderRegistry.GetActiveGroups()
            .Where(g => g.State == OrderGroupState.ProtectionActive)
            .ToList();

        if (!activeGroups.Any())
            return false;

        var anyClosed = false;
        foreach (var group in activeGroups)
        {
            if (CheckGroupProtection(group, candle))
                anyClosed = true;
        }
        return anyClosed;
    }

    private bool CheckGroupProtection(EntryOrderGroup group, ICandleMessage candle)
    {
        var isLong = group.EntryOrder.Side == Sides.Buy;

        foreach (var kv in group.ProtectivePairs)
        {
            var pair = kv.Value;
            var spec = pair.Spec;

            // Always skip candle-based market order fallback when using limit order protection
            // Limit orders will fill at exact SL/TP prices - market orders fill at arbitrary candle prices
            // The candle-based check was originally for non-limit protection modes
            if (spec.OrderType == OrderTypes.Limit)
                continue;

            var slHit = isLong
                ? candle.LowPrice <= spec.StopLossPrice
                : candle.HighPrice >= spec.StopLossPrice;

            var tpHit = isLong
                ? candle.HighPrice >= spec.TakeProfitPrice
                : candle.LowPrice <= spec.TakeProfitPrice;

            if (slHit)
            {
                CloseProtectivePair(group, kv.Key);
                return true;
            }

            if (tpHit)
            {
                CloseProtectivePair(group, kv.Key);
                return true;
            }
        }
        return false;
    }

    private void CloseProtectivePair(EntryOrderGroup group, string pairId)
    {
        var pair = group.ProtectivePairs[pairId];
        var isLong = group.EntryOrder.Side == Sides.Buy;
        var volume = pair.Spec.Volume ?? group.EntryOrder.Volume;

        // Check if either protective order was already filled (prevents double exit)
        var slFilled = pair.SlOrder?.State == OrderStates.Done && pair.SlOrder?.Balance == 0;
        var tpFilled = pair.TpOrder?.State == OrderStates.Done && pair.TpOrder?.Balance == 0;

        // Cancel any still-active orders
        if (pair.SlOrder?.State == OrderStates.Active)
            _strategy.CancelOrder(pair.SlOrder);
        if (pair.TpOrder?.State == OrderStates.Active)
            _strategy.CancelOrder(pair.TpOrder);

        // Only place market order if neither protective order was filled
        // If one was filled, the position is already closed by the limit order
        if (!slFilled && !tpFilled)
            PlaceMarketOrder(isLong ? Sides.Sell : Sides.Buy, volume);

        group.ProtectivePairs.Remove(pairId);

        if (group.ProtectivePairs.Count == 0)
            group.State = OrderGroupState.Closed;
    }

    public void OnOwnTradeReceived(MyTrade trade)
    {
        var order = trade.Order;

        // Check if this is a retry order for partial fill handling
        if (_retryHandler.TryGetRetryKey(order, out var retryKey))
        {
            HandleRetryFill(retryKey!, trade);
            return;
        }

        var group = _orderRegistry.FindGroupByOrder(order);

        if (group == null)
            return;

        if (group.EntryOrder == order)
            HandleEntryFill(group, trade);
        else
            HandleProtectiveFill(group, trade);
    }

    public void OnOrderStateChanged(Order order)
    {
        var group = _orderRegistry.FindGroupByOrder(order);
        if (group == null)
            return;

        if (group.EntryOrder == order && group.State == OrderGroupState.Pending)
        {
            if (order.State == OrderStates.Done && order.Balance == order.Volume)
            {
                HandleEntryExpiration(group);
            }
            else if (order.State == OrderStates.Failed)
            {
                HandleEntryExpiration(group);
            }
        }
    }

    private void HandleEntryExpiration(EntryOrderGroup group)
    {
        foreach (var pair in group.ProtectivePairs.Values)
        {
            if (pair.SlOrder?.State == OrderStates.Active)
                _strategy.CancelOrder(pair.SlOrder);
            if (pair.TpOrder?.State == OrderStates.Active)
                _strategy.CancelOrder(pair.TpOrder);
        }

        group.ProtectivePairs.Clear();
        group.State = OrderGroupState.Closed;
    }

    private void HandleEntryFill(EntryOrderGroup group, MyTrade trade)
    {
        // Entry partial fills are not treated as errors - just wait for full fill
        // Only transition state when fully filled
        if (trade.Order.Balance > 0)
            return;

        group.State = OrderGroupState.EntryFilled;

        if (_lastCandle != null && CheckGroupProtection(group, _lastCandle))
            return;

        PlaceProtectionOrders(group);
        group.State = OrderGroupState.ProtectionActive;
    }

    private void PlaceProtectionOrders(EntryOrderGroup group)
    {
        var isLong = group.EntryOrder.Side == Sides.Buy;
        var exitSide = isLong ? Sides.Sell : Sides.Buy;

        foreach (var kv in group.ProtectivePairs)
        {
            var spec = kv.Value.Spec;
            var volume = spec.Volume ?? group.EntryOrder.Volume;

            var slOrder = PlaceProtectiveOrder(exitSide, spec.StopLossPrice, volume, spec.OrderType);
            var tpOrder = PlaceProtectiveOrder(exitSide, spec.TakeProfitPrice, volume, spec.OrderType);

            group.ProtectivePairs[kv.Key] = (slOrder, tpOrder, spec);
        }
    }

    private void HandleProtectiveFill(EntryOrderGroup group, MyTrade trade)
    {
        var order = trade.Order;
        var pairEntry = group.ProtectivePairs.FirstOrDefault(kv =>
            kv.Value.SlOrder == order || kv.Value.TpOrder == order);

        if (pairEntry.Key == null)
            return;

        var pair = pairEntry.Value;
        var isStopLoss = pair.SlOrder == order;
        var otherOrder = isStopLoss ? pair.TpOrder : pair.SlOrder;

        // Check for partial fill
        var remainingVolume = order.Balance;
        if (remainingVolume > 0)
        {
            // Partial fill detected - initiate market retry for remaining volume
            _retryHandler.InitiateRetry(group.GroupId, pairEntry.Key, remainingVolume, order.Side, PlaceMarketOrder);
            return;
        }

        // Full fill - cancel the opposing order and clean up
        if (otherOrder?.State == OrderStates.Active)
            _strategy.CancelOrder(otherOrder);

        group.ProtectivePairs.Remove(pairEntry.Key);

        if (group.ProtectivePairs.Count == 0)
            group.State = OrderGroupState.Closed;
    }

    private void HandleRetryFill(string retryKey, MyTrade trade)
    {
        var (needsMoreRetries, groupId, pairId) = _retryHandler.HandleRetryFill(retryKey, trade, PlaceMarketOrder);

        if (needsMoreRetries || groupId == null || pairId == null)
            return;

        // Successful full fill - clean up the group/pair
        var targetGroup = _orderRegistry.GetActiveGroups()
            .FirstOrDefault(g => g.GroupId == groupId);

        if (targetGroup == null)
            return;

        // Cancel the opposing order if exists
        if (targetGroup.ProtectivePairs.TryGetValue(pairId, out var pair))
        {
            if (pair.SlOrder?.State == OrderStates.Active)
                _strategy.CancelOrder(pair.SlOrder);
            if (pair.TpOrder?.State == OrderStates.Active)
                _strategy.CancelOrder(pair.TpOrder);

            targetGroup.ProtectivePairs.Remove(pairId);
        }

        if (targetGroup.ProtectivePairs.Count == 0)
            targetGroup.State = OrderGroupState.Closed;
    }

    private void CancelPendingOrders()
    {
        // Materialize the list to avoid modifying collection during enumeration
        var pendingGroups = _orderRegistry.GetActiveGroups()
            .Where(g => g.State == OrderGroupState.Pending)
            .ToList();

        foreach (var group in pendingGroups)
        {
            if (group.EntryOrder.State == OrderStates.Active)
                _strategy.CancelOrder(group.EntryOrder);
            group.State = OrderGroupState.Closed;
        }
    }

    public void CloseAllPositions()
    {
        foreach (var group in _orderRegistry.GetActiveGroups())
        {
            if (group.EntryOrder.State == OrderStates.Active)
                _strategy.CancelOrder(group.EntryOrder);

            foreach (var pair in group.ProtectivePairs.Values)
            {
                if (pair.SlOrder?.State == OrderStates.Active)
                    _strategy.CancelOrder(pair.SlOrder);
                if (pair.TpOrder?.State == OrderStates.Active)
                    _strategy.CancelOrder(pair.TpOrder);
            }

            if (group.State == OrderGroupState.ProtectionActive || group.State == OrderGroupState.EntryFilled)
            {
                var isLong = group.EntryOrder.Side == Sides.Buy;
                var totalVolume = group.ProtectivePairs.Values
                    .Sum(pp => pp.Spec.Volume ?? group.EntryOrder.Volume);

                if (totalVolume > 0)
                    PlaceMarketOrder(isLong ? Sides.Sell : Sides.Buy, totalVolume);
            }

            group.State = OrderGroupState.Closed;
        }
    }

    public void Reset()
    {
        _orderRegistry.Reset();
        _lastCandle = null;
        _retryHandler.Reset();
    }

    private Order PlaceProtectiveOrder(Sides side, decimal price, decimal volume, OrderTypes orderType)
    {
        var order = new Order
        {
            Side = side,
            Price = orderType == OrderTypes.Limit ? price : 0m,
            Volume = volume,
            Type = orderType,
            Security = _security
        };
        return _strategy.PlaceOrder(order);
    }

    private Order PlaceLimitOrder(Sides side, decimal price, decimal volume)
    {
        var order = new Order
        {
            Side = side,
            Price = price,
            Volume = volume,
            Type = OrderTypes.Limit,
            Security = _security
        };
        return _strategy.PlaceOrder(order);
    }

    private Order PlaceMarketOrder(Sides side, decimal volume)
    {
        var order = new Order
        {
            Side = side,
            Volume = volume,
            Type = OrderTypes.Market,
            Security = _security
        };
        return _strategy.PlaceOrder(order);
    }
}

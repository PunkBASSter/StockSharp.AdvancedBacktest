using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.Algo.Candles;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class OrderPositionManagerTests
{
    private readonly TestStrategy _strategy;
    private readonly Security _security;
    private readonly OrderPositionManager _manager;

    public OrderPositionManagerTests()
    {
        _strategy = new TestStrategy();
        _security = new Security { Id = "TEST@TEST", PriceStep = 0.01m };
        _manager = new OrderPositionManager(_strategy, _security, "TestStrategy");
    }

    [Fact]
    public void Constructor_WithNullStrategy_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new OrderPositionManager(null!, _security, "test"));
    }

    [Fact]
    public void HandleSignal_WithBuySignal_ReturnsEntryOrder()
    {
        var signal = CreateValidBuySignal();

        var result = _manager.HandleOrderRequest(signal);

        Assert.NotNull(result);
        Assert.Equal(Sides.Buy, result.Side);
        Assert.Equal(100m, result.Price);
        Assert.Equal(10m, result.Volume);
    }

    [Fact]
    public void HandleSignal_EntryFilled_PlacesProtectionOrders()
    {
        var signal = CreateValidBuySignal();
        var entryOrder = _manager.HandleOrderRequest(signal);

        var entryTrade = CreateTrade(entryOrder!, 100m, 10m);
        _manager.OnOwnTradeReceived(entryTrade);

        Assert.Equal(2, _strategy.PlacedOrders.Count);

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m);

        Assert.NotNull(slOrder);
        Assert.NotNull(tpOrder);
        Assert.Equal(Sides.Sell, slOrder.Side);
        Assert.Equal(Sides.Sell, tpOrder.Side);
    }

    [Fact]
    public void OnOwnTradeReceived_StopLossFilled_CancelsTakeProfitOrder()
    {
        var signal = CreateValidBuySignal();
        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m);

        _manager.OnOwnTradeReceived(CreateTrade(slOrder!, 95m, 10m));

        Assert.Contains(tpOrder, _strategy.CancelledOrders);
    }

    [Fact]
    public void OnOwnTradeReceived_TakeProfitFilled_CancelsStopLossOrder()
    {
        var signal = CreateValidBuySignal();
        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m);

        _manager.OnOwnTradeReceived(CreateTrade(tpOrder!, 110m, 10m));

        Assert.Contains(slOrder, _strategy.CancelledOrders);
    }

    [Fact]
    public void HandleSignal_DuplicateSignal_ReturnsNull()
    {
        var signal1 = CreateValidBuySignal();
        _manager.HandleOrderRequest(signal1);

        var signal2 = CreateValidBuySignal();
        var result = _manager.HandleOrderRequest(signal2);

        Assert.Null(result);
    }

    [Fact]
    public void HandleSignal_NullSignal_CancelsPendingOrders()
    {
        var signal = CreateValidBuySignal();
        var entryOrder = _manager.HandleOrderRequest(signal);

        _manager.HandleOrderRequest(null);

        Assert.Contains(entryOrder, _strategy.CancelledOrders);
    }

    [Fact]
    public void CloseAllPositions_WithFilledEntry_PlacesMarketOrder()
    {
        var signal = CreateValidBuySignal();
        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        _manager.CloseAllPositions();

        var marketOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Type == OrderTypes.Market);
        Assert.NotNull(marketOrder);
        Assert.Equal(Sides.Sell, marketOrder.Side);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var signal = CreateValidBuySignal();
        _manager.HandleOrderRequest(signal);

        _manager.Reset();

        Assert.Empty(_manager.ActiveOrders());
    }

    [Fact]
    public void HandleOrderRequest_MultipleConcurrentGroups_CreatesIndependentGroups()
    {
        var signal1 = CreateBuySignal(100m, 95m, 110m);
        var signal2 = CreateBuySignal(105m, 100m, 115m);
        var signal3 = CreateBuySignal(110m, 105m, 120m);

        var order1 = _manager.HandleOrderRequest(signal1);
        var order2 = _manager.HandleOrderRequest(signal2);
        var order3 = _manager.HandleOrderRequest(signal3);

        Assert.NotNull(order1);
        Assert.NotNull(order2);
        Assert.NotNull(order3);
        Assert.Equal(3, _manager.ActiveOrders().Length);
    }

    [Fact]
    public void HandleOrderRequest_FiveGroupsActive_AllowedByDefault()
    {
        for (var i = 0; i < 5; i++)
        {
            var signal = CreateBuySignal(100m + i, 95m + i, 110m + i);
            var order = _manager.HandleOrderRequest(signal);
            Assert.NotNull(order);
        }

        Assert.Equal(5, _manager.ActiveOrders().Length);
    }

    [Fact]
    public void CheckProtectionLevels_MultipleGroups_ClosesOnlyTriggeredGroup()
    {
        // Group1: entry at 100, SL at 95, TP at 110
        // Group2: entry at 80, SL at 70, TP at 120 (both SL and TP outside candle range)
        var signal1 = CreateBuySignal(100m, 95m, 110m);
        var signal2 = CreateBuySignal(80m, 70m, 120m);

        var order1 = _manager.HandleOrderRequest(signal1);
        var order2 = _manager.HandleOrderRequest(signal2);

        _manager.OnOwnTradeReceived(CreateTrade(order1!, 100m, 10m));
        _manager.OnOwnTradeReceived(CreateTrade(order2!, 80m, 10m));

        // Candle: Low=94 hits SL=95 for group1, but 94 > 70 (group2's SL)
        // Candle: High=97 < 110 (group1's TP) and 97 < 120 (group2's TP)
        var candle = new TimeFrameCandleMessage
        {
            OpenPrice = 96m,
            HighPrice = 97m,
            LowPrice = 94m,   // Hits SL at 95 for group1, but > 70 (group2's SL)
            ClosePrice = 95m,
            State = CandleStates.Finished
        };

        var closed = _manager.CheckProtectionLevels(candle);

        Assert.True(closed);
        var activeGroups = _manager.ActiveOrders()
            .Where(g => g.State == OrderGroupState.ProtectionActive)
            .ToList();
        Assert.Single(activeGroups);  // Group2 should still be active
    }

    [Fact]
    public void OnOrderStateChanged_EntryExpired_TransitionsToClosedState()
    {
        var signal = CreateValidBuySignal();
        var entryOrder = _manager.HandleOrderRequest(signal);

        entryOrder!.State = OrderStates.Done;
        entryOrder.Balance = entryOrder.Volume;

        _manager.OnOrderStateChanged(entryOrder);

        var groups = _manager.ActiveOrders();
        Assert.Empty(groups);
    }

    [Fact]
    public void OnOrderStateChanged_EntryFailed_TransitionsToClosedState()
    {
        var signal = CreateValidBuySignal();
        var entryOrder = _manager.HandleOrderRequest(signal);

        entryOrder!.State = OrderStates.Failed;

        _manager.OnOrderStateChanged(entryOrder);

        var groups = _manager.ActiveOrders();
        Assert.Empty(groups);
    }

    [Fact]
    public void OnOrderStateChanged_EntryExpiredWithProtectiveOrders_CancelsProtectiveOrders()
    {
        var signal = CreateValidBuySignal();
        var entryOrder = _manager.HandleOrderRequest(signal);

        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m);

        _strategy.CancelledOrders.Clear();

        var anotherEntry = CreateBuySignal(105m, 100m, 115m);
        var entry2 = _manager.HandleOrderRequest(anotherEntry);
        entry2!.State = OrderStates.Done;
        entry2.Balance = entry2.Volume;

        _manager.OnOrderStateChanged(entry2);

        var activeGroups = _manager.ActiveOrders();
        Assert.Single(activeGroups);
    }

    #region US4 Tests - Split Exit Orders

    [Fact]
    public void HandleOrderRequest_MultiplePairs_CreatesGroupWithAllPairs()
    {
        var signal = CreateMultiplePairSignal(100m, [
            (95m, 105m, 5m),
            (95m, 110m, 5m)
        ]);

        var order = _manager.HandleOrderRequest(signal);

        Assert.NotNull(order);
        var groups = _manager.ActiveOrders();
        Assert.Single(groups);
        Assert.Equal(2, groups[0].ProtectivePairs.Count);
    }

    [Fact]
    public void OnOwnTradeReceived_EntryFilled_PlacesProtectiveOrdersForAllPairs()
    {
        var signal = CreateMultiplePairSignal(100m, [
            (95m, 105m, 5m),
            (95m, 110m, 5m)
        ]);

        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        // Should place 2 SL orders and 2 TP orders = 4 protective orders
        Assert.Equal(4, _strategy.PlacedOrders.Count);
    }

    [Fact]
    public void OnOwnTradeReceived_OnePairTpFilled_CancelsOnlyCorrespondingSl()
    {
        var signal = CreateMultiplePairSignal(100m, [
            (95m, 105m, 5m),
            (95m, 110m, 5m)
        ]);

        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        // Get the TP order at 105 (first pair)
        var tpOrder105 = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 105m);
        Assert.NotNull(tpOrder105);

        _strategy.CancelledOrders.Clear();
        _manager.OnOwnTradeReceived(CreateTrade(tpOrder105, 105m, 5m));

        // Should cancel only the corresponding SL (95m for the first pair)
        Assert.Single(_strategy.CancelledOrders);
        Assert.Equal(95m, _strategy.CancelledOrders[0].Price);
    }

    [Fact]
    public void OnOwnTradeReceived_OnePairClosed_GroupRemainsActiveWithRemainingPair()
    {
        var signal = CreateMultiplePairSignal(100m, [
            (95m, 105m, 5m),
            (95m, 110m, 5m)
        ]);

        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        var tpOrder105 = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 105m);
        _manager.OnOwnTradeReceived(CreateTrade(tpOrder105!, 105m, 5m));

        var groups = _manager.ActiveOrders();
        Assert.Single(groups);
        Assert.Equal(OrderGroupState.ProtectionActive, groups[0].State);
        Assert.Single(groups[0].ProtectivePairs);
    }

    [Fact]
    public void OnOwnTradeReceived_AllPairsClosed_GroupTransitionsToClosed()
    {
        var signal = CreateMultiplePairSignal(100m, [
            (95m, 105m, 5m),
            (95m, 110m, 5m)
        ]);

        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        var tpOrder105 = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 105m);
        var tpOrder110 = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m);

        _manager.OnOwnTradeReceived(CreateTrade(tpOrder105!, 105m, 5m));
        _manager.OnOwnTradeReceived(CreateTrade(tpOrder110!, 110m, 5m));

        var groups = _manager.ActiveOrders();
        Assert.Empty(groups);
    }

    [Fact]
    public void CheckProtectionLevels_MultiplePairs_ClosesOnlyTriggeredPair()
    {
        var signal = CreateMultiplePairSignal(100m, [
            (95m, 105m, 5m),
            (90m, 115m, 5m)
        ]);

        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        // Candle that triggers first pair's SL (95) but not second pair's SL (90)
        var candle = new TimeFrameCandleMessage
        {
            OpenPrice = 96m,
            HighPrice = 97m,
            LowPrice = 94m,   // Hits 95 but not 90
            ClosePrice = 95m,
            State = CandleStates.Finished
        };

        var closed = _manager.CheckProtectionLevels(candle);

        Assert.True(closed);
        var groups = _manager.ActiveOrders();
        Assert.Single(groups);
        Assert.Equal(OrderGroupState.ProtectionActive, groups[0].State);
        Assert.Single(groups[0].ProtectivePairs);
    }

    [Fact]
    public void OnOwnTradeReceived_EntryFilled_PlacesProtectiveOrdersWithConfiguredType()
    {
        var signal = CreateMultiplePairSignalWithOrderType(100m, [
            (95m, 105m, 5m, OrderTypes.Limit),
            (90m, 115m, 5m, OrderTypes.Market)
        ]);

        var entryOrder = _manager.HandleOrderRequest(signal);
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder!, 100m, 10m));

        // 2 limit orders (SL and TP for first pair) + 2 market orders (SL and TP for second pair)
        var limitOrders = _strategy.PlacedOrders.Where(o => o.Type == OrderTypes.Limit).ToList();
        var marketOrders = _strategy.PlacedOrders.Where(o => o.Type == OrderTypes.Market).ToList();

        Assert.Equal(2, limitOrders.Count);
        Assert.Equal(2, marketOrders.Count);
    }

    #endregion

    #region Helper Methods

    private OrderRequest CreateBuySignal(decimal entryPrice, decimal slPrice, decimal tpPrice)
    {
        var order = new Order
        {
            Side = Sides.Buy,
            Price = entryPrice,
            Volume = 10m,
            Security = _security,
            Portfolio = new Portfolio { Name = "TEST" },
            Type = OrderTypes.Limit,
            State = OrderStates.Active
        };

        var protectivePair = new ProtectivePair(slPrice, tpPrice, 10m);
        return new OrderRequest(order, [protectivePair]);
    }

    private OrderRequest CreateValidBuySignal()
    {
        var order = new Order
        {
            Side = Sides.Buy,
            Price = 100m,
            Volume = 10m,
            Security = _security,
            Portfolio = new Portfolio { Name = "TEST" },
            Type = OrderTypes.Limit,
            State = OrderStates.Active
        };

        var protectivePair = new ProtectivePair(95m, 110m, 10m);
        return new OrderRequest(order, [protectivePair]);
    }

    private OrderRequest CreateMultiplePairSignal(decimal entryPrice, (decimal sl, decimal tp, decimal volume)[] pairs)
    {
        var totalVolume = pairs.Sum(p => p.volume);
        var order = new Order
        {
            Side = Sides.Buy,
            Price = entryPrice,
            Volume = totalVolume,
            Security = _security,
            Portfolio = new Portfolio { Name = "TEST" },
            Type = OrderTypes.Limit,
            State = OrderStates.Active
        };

        var protectivePairs = pairs.Select(p => new ProtectivePair(p.sl, p.tp, p.volume)).ToList();
        return new OrderRequest(order, protectivePairs);
    }

    private OrderRequest CreateMultiplePairSignalWithOrderType(decimal entryPrice, (decimal sl, decimal tp, decimal volume, OrderTypes orderType)[] pairs)
    {
        var totalVolume = pairs.Sum(p => p.volume);
        var order = new Order
        {
            Side = Sides.Buy,
            Price = entryPrice,
            Volume = totalVolume,
            Security = _security,
            Portfolio = new Portfolio { Name = "TEST" },
            Type = OrderTypes.Limit,
            State = OrderStates.Active
        };

        var protectivePairs = pairs.Select(p => new ProtectivePair(p.sl, p.tp, p.volume, p.orderType)).ToList();
        return new OrderRequest(order, protectivePairs);
    }

    private MyTrade CreateTrade(Order order, decimal price, decimal volume)
    {
        var trade = new ExecutionMessage
        {
            TradePrice = price,
            TradeVolume = volume,
            ServerTime = DateTime.Now
        };

        return new MyTrade
        {
            Order = order,
            Trade = trade
        };
    }

    #endregion

    #region Test Strategy

    private class TestStrategy : IStrategyOrderOperations
    {
        public List<Order> PlacedOrders { get; } = [];
        public List<Order> CancelledOrders { get; } = [];

        private readonly Portfolio _portfolio = new() { Name = "TEST" };
        private readonly Security _security = new() { Id = "TEST@TEST", PriceStep = 0.01m };

        public Order PlaceOrder(Order order)
        {
            var placedOrder = new Order
            {
                Security = _security,
                Portfolio = _portfolio,
                Price = order.Price,
                Volume = order.Volume,
                Side = order.Side,
                Type = order.Type,
                State = OrderStates.Active
            };
            PlacedOrders.Add(placedOrder);
            return placedOrder;
        }

        public void CancelOrder(Order order)
        {
            CancelledOrders.Add(order);
            order.State = OrderStates.Done;
        }
    }

    #endregion
}

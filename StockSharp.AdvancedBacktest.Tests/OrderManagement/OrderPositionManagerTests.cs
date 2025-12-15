using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

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

    #region Helper Methods

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

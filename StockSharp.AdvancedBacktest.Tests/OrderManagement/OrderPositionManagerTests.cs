using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class OrderPositionManagerTests
{
    private readonly TestStrategy _strategy;
    private readonly OrderPositionManager _manager;

    public OrderPositionManagerTests()
    {
        _strategy = new TestStrategy();
        _manager = new OrderPositionManager(_strategy);
    }

    [Fact]
    public void Constructor_WithNullStrategy_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new OrderPositionManager(null!));
    }

    [Fact]
    public void HandleSignal_WithBuySignal_PlacesEntryOrder()
    {
        // Arrange
        var signal = CreateValidBuySignal();

        // Act
        _manager.HandleSignal(signal);

        // Assert
        Assert.Single(_strategy.PlacedOrders);
        var order = _strategy.PlacedOrders[0];
        Assert.Equal(Sides.Buy, order.Side);
        Assert.Equal(100m, order.Price);
        Assert.Equal(10m, order.Volume);
        Assert.Equal(OrderTypes.Limit, order.Type);
    }

    [Fact]
    public void HandleSignal_EntryFilledThenTPHit_PlacesProtectionOrders()
    {
        // Arrange - Place entry order
        var signal = CreateValidBuySignal(); // Entry: 100, SL: 95, TP: 110
        _manager.HandleSignal(signal);

        var entryOrder = _strategy.PlacedOrders[0];
        _strategy.Position = 10m; // Simulate filled position

        // Act - Simulate entry fill
        var entryTrade = CreateTrade(entryOrder, 100m, 10m);
        _manager.OnOwnTradeReceived(entryTrade);

        // Assert - Protection orders should be placed
        Assert.Equal(3, _strategy.PlacedOrders.Count); // Entry + SL + TP

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m);

        Assert.NotNull(slOrder);
        Assert.NotNull(tpOrder);
        Assert.Equal(Sides.Sell, slOrder.Side);
        Assert.Equal(Sides.Sell, tpOrder.Side);
        Assert.Equal(10m, slOrder.Volume);
        Assert.Equal(10m, tpOrder.Volume);
    }

    [Fact]
    public void OnOwnTradeReceived_StopLossFilled_CancelsTakeProfitOrder()
    {
        // Arrange - Setup position with protection orders
        var signal = CreateValidBuySignal();
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];
        _strategy.Position = 10m;
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder, 100m, 10m));

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m);

        // Act - Simulate SL fill
        _strategy.Position = 0m; // Position closed
        _manager.OnOwnTradeReceived(CreateTrade(slOrder!, 95m, 10m));

        // Assert - TP order should be cancelled
        Assert.Contains(tpOrder, _strategy.CancelledOrders);
    }

    [Fact]
    public void OnOwnTradeReceived_TakeProfitFilled_CancelsStopLossOrder()
    {
        // Arrange - Setup position with protection orders
        var signal = CreateValidBuySignal();
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];
        _strategy.Position = 10m;
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder, 100m, 10m));

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m);

        // Act - Simulate TP fill
        _strategy.Position = 0m; // Position closed
        _manager.OnOwnTradeReceived(CreateTrade(tpOrder!, 110m, 10m));

        // Assert - SL order should be cancelled
        Assert.Contains(slOrder, _strategy.CancelledOrders);
    }

    [Fact]
    public void HandleSignal_SignalChangedBeforeEntryFill_CancelsOldOrderPlacesNew()
    {
        // Arrange - Place initial order
        var signal1 = CreateValidBuySignal(); // Entry: 100
        _manager.HandleSignal(signal1);
        var oldOrder = _strategy.PlacedOrders[0];

        // Act - Signal changes to new price
        var signal2 = new OrderRequest
        {
            Direction = Sides.Buy,
            Price = 105m, // Different price
            Volume = 10m,
            StopLoss = 98m,
            TakeProfit = 115m,
            OrderType = OrderTypes.Limit
        };
        _manager.HandleSignal(signal2);

        // Assert
        Assert.Contains(oldOrder, _strategy.CancelledOrders); // Old order cancelled
        Assert.Equal(2, _strategy.PlacedOrders.Count); // Old + new order
        var newOrder = _strategy.PlacedOrders[1];
        Assert.Equal(105m, newOrder.Price);
    }

    [Fact]
    public void HandleSignal_NullSignal_CancelsAllOrders()
    {
        // Arrange - Setup with active order
        var signal = CreateValidBuySignal();
        _manager.HandleSignal(signal);
        var order = _strategy.PlacedOrders[0];

        // Act
        _manager.HandleSignal(null);

        // Assert
        Assert.Contains(order, _strategy.CancelledOrders);
    }

    [Fact]
    public void CloseAllPositions_WithLongPosition_PlacesSellMarketOrder()
    {
        // Arrange
        _strategy.Position = 10m;

        // Act
        _manager.CloseAllPositions();

        // Assert
        var marketOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Type == OrderTypes.Market);
        Assert.NotNull(marketOrder);
        Assert.Equal(Sides.Sell, marketOrder.Side);
        Assert.Equal(10m, marketOrder.Volume);
    }

    [Fact]
    public void CloseAllPositions_WithShortPosition_PlacesBuyMarketOrder()
    {
        // Arrange
        _strategy.Position = -10m;

        // Act
        _manager.CloseAllPositions();

        // Assert
        var marketOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Type == OrderTypes.Market);
        Assert.NotNull(marketOrder);
        Assert.Equal(Sides.Buy, marketOrder.Side);
        Assert.Equal(10m, marketOrder.Volume);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        // Arrange - Create some state
        var signal = CreateValidBuySignal();
        _manager.HandleSignal(signal);

        // Act
        _manager.Reset();

        // Assert
        Assert.Empty(_manager.ActiveOrders());
    }

    #region Helper Methods

    private OrderRequest CreateValidBuySignal()
    {
        return new OrderRequest
        {
            Direction = Sides.Buy,
            Price = 100m,
            Volume = 10m,
            StopLoss = 95m,
            TakeProfit = 110m,
            OrderType = OrderTypes.Limit
        };
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
        public List<Order> PlacedOrders { get; } = new();
        public List<Order> CancelledOrders { get; } = new();

        public Security Security { get; } = new()
        {
            Id = "TEST@TEST",
            PriceStep = 0.01m
        };

        private readonly Portfolio _portfolio = new()
        {
            Name = "TEST"
        };

        public decimal Position { get; set; }

        private Order CreateAndRegisterOrder(decimal price, decimal volume, Sides side, OrderTypes type)
        {
            var order = new Order
            {
                Security = Security,
                Portfolio = _portfolio,
                Price = price,
                Volume = volume,
                Side = side,
                Type = type,
                State = OrderStates.Active
            };
            PlacedOrders.Add(order);
            return order;
        }

        public Order BuyLimit(decimal price, decimal volume)
            => CreateAndRegisterOrder(price, volume, Sides.Buy, OrderTypes.Limit);

        public Order SellLimit(decimal price, decimal volume)
            => CreateAndRegisterOrder(price, volume, Sides.Sell, OrderTypes.Limit);

        public Order BuyMarket(decimal volume)
            => CreateAndRegisterOrder(0, volume, Sides.Buy, OrderTypes.Market);

        public Order SellMarket(decimal volume)
            => CreateAndRegisterOrder(0, volume, Sides.Sell, OrderTypes.Market);

        public void CancelOrder(Order order)
        {
            CancelledOrders.Add(order);
            order.State = OrderStates.Done;
        }

        public void LogInfo(string format, params object[] args) { }
        public void LogWarning(string format, params object[] args) { }
    }

    #endregion
}

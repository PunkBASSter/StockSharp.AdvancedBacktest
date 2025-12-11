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
        var signal = CreateSignal(Sides.Buy, 100m, 10m, 95m, 110m);

        _manager.HandleSignal(signal);

        Assert.Single(_strategy.PlacedOrders);
        var order = _strategy.PlacedOrders[0];
        Assert.Equal(Sides.Buy, order.Side);
        Assert.Equal(100m, order.Price);
        Assert.Equal(10m, order.Volume);
        Assert.Equal(OrderTypes.Limit, order.Type);
    }

    [Theory]
    [InlineData(111, 99, true)]   // TP hit (high >= 110)
    [InlineData(109, 94, true)]   // SL hit (low <= 95)
    [InlineData(109, 96, false)]  // Neither hit
    public void CheckProtectionLevels_VariousCandles_ReturnsExpectedResult(
        decimal candleHigh, decimal candleLow, bool expectedHit)
    {
        // Setup position with protection levels (SL: 95, TP: 110)
        var signal = CreateSignal(Sides.Buy, 100m, 10m, 95m, 110m);
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];
        _strategy.Position = 10m;
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder, 100m, 10m));

        var candle = new TimeFrameCandleMessage { HighPrice = candleHigh, LowPrice = candleLow };
        var protectionHit = _manager.CheckProtectionLevels(candle);

        Assert.Equal(expectedHit, protectionHit);
        if (expectedHit)
        {
            Assert.Equal(2, _strategy.PlacedOrders.Count);
            var closeOrder = _strategy.PlacedOrders.Last();
            Assert.Equal(OrderTypes.Market, closeOrder.Type);
            Assert.Equal(Sides.Sell, closeOrder.Side);
        }
    }

    [Fact]
    public void HandleSignal_EntryFilledThenTPHit_UsesCheckProtectionLevels()
    {
        var signal = CreateSignal(Sides.Buy, 100m, 10m, 95m, 110m);
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];
        _strategy.Position = 10m;
        _manager.OnOwnTradeReceived(CreateTrade(entryOrder, 100m, 10m));

        Assert.Single(_strategy.PlacedOrders);

        var tpCandle = new TimeFrameCandleMessage { HighPrice = 111m, LowPrice = 99m };
        var protectionHit = _manager.CheckProtectionLevels(tpCandle);

        Assert.True(protectionHit);
        Assert.Equal(2, _strategy.PlacedOrders.Count);
    }

    [Fact]
    public void HandleSignal_SignalChangedBeforeEntryFill_CancelsOldOrderPlacesNew()
    {
        var signal1 = CreateSignal(Sides.Buy, 100m, 10m, 95m, 110m);
        _manager.HandleSignal(signal1);
        var oldOrder = _strategy.PlacedOrders[0];

        var signal2 = CreateSignal(Sides.Buy, 105m, 10m, 98m, 115m);
        _manager.HandleSignal(signal2);

        Assert.Contains(oldOrder, _strategy.CancelledOrders);
        Assert.Equal(2, _strategy.PlacedOrders.Count);
        Assert.Equal(105m, _strategy.PlacedOrders[1].Price);
    }

    [Fact]
    public void HandleSignal_NullSignal_CancelsAllOrders()
    {
        var signal = CreateSignal(Sides.Buy, 100m, 10m, 95m, 110m);
        _manager.HandleSignal(signal);
        var order = _strategy.PlacedOrders[0];

        _manager.HandleSignal(null);

        Assert.Contains(order, _strategy.CancelledOrders);
    }

    [Theory]
    [InlineData(10, Sides.Sell)]   // Long position -> Sell to close
    [InlineData(-10, Sides.Buy)]   // Short position -> Buy to close
    public void CloseAllPositions_PlacesCorrectMarketOrder(decimal position, Sides expectedSide)
    {
        _strategy.Position = position;

        _manager.CloseAllPositions();

        var marketOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Type == OrderTypes.Market);
        Assert.NotNull(marketOrder);
        Assert.Equal(expectedSide, marketOrder.Side);
        Assert.Equal(Math.Abs(position), marketOrder.Volume);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var signal = CreateSignal(Sides.Buy, 100m, 10m, 95m, 110m);
        _manager.HandleSignal(signal);

        _manager.Reset();

        Assert.Empty(_manager.ActiveOrders());
    }

    #region Helpers

    private static TradeSignal CreateSignal(Sides direction, decimal entry, decimal volume, decimal sl, decimal tp)
        => new()
        {
            Direction = direction,
            EntryPrice = entry,
            Volume = volume,
            StopLoss = sl,
            TakeProfit = tp,
            OrderType = OrderTypes.Limit
        };

    private static MyTrade CreateTrade(Order order, decimal price, decimal volume)
        => new()
        {
            Order = order,
            Trade = new ExecutionMessage
            {
                TradePrice = price,
                TradeVolume = volume,
                ServerTime = DateTime.Now
            }
        };

    #endregion

    #region TestStrategy

    private class TestStrategy : IStrategyOrderOperations
    {
        public List<Order> PlacedOrders { get; } = [];
        public List<Order> CancelledOrders { get; } = [];
        public Security Security { get; } = new() { Id = "TEST@TEST", PriceStep = 0.01m };
        public decimal Position { get; set; }

        private readonly Portfolio _portfolio = new() { Name = "TEST" };

        private Order CreateOrder(decimal price, decimal volume, Sides side, OrderTypes type)
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

        public Order BuyLimit(decimal price, decimal volume) => CreateOrder(price, volume, Sides.Buy, OrderTypes.Limit);
        public Order SellLimit(decimal price, decimal volume) => CreateOrder(price, volume, Sides.Sell, OrderTypes.Limit);
        public Order BuyMarket(decimal volume) => CreateOrder(0, volume, Sides.Buy, OrderTypes.Market);
        public Order SellMarket(decimal volume) => CreateOrder(0, volume, Sides.Sell, OrderTypes.Market);

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

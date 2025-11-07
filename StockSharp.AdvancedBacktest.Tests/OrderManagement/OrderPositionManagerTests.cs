using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.AdvancedBacktest.Strategies;
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
    public void CanTrade_WhenNoPositionAndNoOrders_ReturnsTrue()
    {
        _strategy.Position = 0;

        var result = _manager.CanTrade();

        Assert.True(result);
    }

    [Fact]
    public void CanTrade_WhenHasPosition_ReturnsFalse()
    {
        _strategy.Position = 10;

        var result = _manager.CanTrade();

        Assert.False(result);
    }

    [Fact]
    public void CanTrade_WhenHasActiveEntryOrder_ReturnsFalse()
    {
        var signal = CreateValidBuySignal();
        _manager.HandleSignal(signal);

        var result = _manager.CanTrade();

        Assert.False(result);
    }

    [Fact]
    public void HandleSignal_WithValidSignal_PlacesEntryOrder()
    {
        var signal = CreateValidBuySignal();

        _manager.HandleSignal(signal);

        Assert.Single(_strategy.PlacedOrders);
        var order = _strategy.PlacedOrders[0];
        Assert.Equal(signal.EntryPrice, order.Price);
        Assert.Equal(signal.Volume, order.Volume);
        Assert.Equal(Sides.Buy, order.Side);
    }

    [Fact]
    public void HandleSignal_WithNullSignal_CancelsActiveOrders()
    {
        // Place an entry order first
        var signal = CreateValidBuySignal();
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];

        // Now send null signal to cancel
        _manager.HandleSignal(null);

        Assert.Contains(entryOrder, _strategy.CancelledOrders);
    }

    [Fact]
    public void HandleSignal_WithChangedLevels_CancelsAndReplacesOrder()
    {
        // Place initial order
        var signal1 = new TradeSignal
        {
            Direction = Sides.Buy,
            EntryPrice = 100m,
            Volume = 10m,
            StopLoss = 95m,
            TakeProfit = 110m
        };
        _manager.HandleSignal(signal1);
        var firstOrder = _strategy.PlacedOrders[0];

        // Change signal levels significantly
        var signal2 = new TradeSignal
        {
            Direction = Sides.Buy,
            EntryPrice = 105m,  // Changed by 5
            Volume = 10m,
            StopLoss = 100m,
            TakeProfit = 115m
        };
        _manager.HandleSignal(signal2);

        // First order should be cancelled, second should be placed
        Assert.Contains(firstOrder, _strategy.CancelledOrders);
        Assert.Equal(2, _strategy.PlacedOrders.Count);
        Assert.Equal(105m, _strategy.PlacedOrders[1].Price);
    }

    [Fact]
    public void OnOwnTradeReceived_WhenEntryFills_PlacesProtectionOrders()
    {
        // Place entry order
        var signal = new TradeSignal
        {
            Direction = Sides.Buy,
            EntryPrice = 100m,
            Volume = 10m,
            StopLoss = 95m,
            TakeProfit = 110m
        };
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];

        // Simulate entry fill
        _strategy.Position = 10;
        var trade = CreateTrade(entryOrder, 100m, 10m);
        _manager.OnOwnTradeReceived(trade);

        // Should place SL and TP orders (total 3 orders: entry + SL + TP)
        Assert.Equal(3, _strategy.PlacedOrders.Count);

        // Find SL order (sell at 95)
        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m && o != entryOrder);
        Assert.NotNull(slOrder);
        Assert.Equal(Sides.Sell, slOrder.Side);
        Assert.Equal(10m, slOrder.Volume);

        // Find TP order (sell at 110)
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m && o != entryOrder);
        Assert.NotNull(tpOrder);
        Assert.Equal(Sides.Sell, tpOrder.Side);
        Assert.Equal(10m, tpOrder.Volume);
    }

    [Fact]
    public void OnOwnTradeReceived_WhenStopLossFills_CancelsTakeProfit()
    {
        // Setup: entry filled, protection orders placed
        var signal = new TradeSignal
        {
            Direction = Sides.Buy,
            EntryPrice = 100m,
            Volume = 10m,
            StopLoss = 95m,
            TakeProfit = 110m
        };
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];

        _strategy.Position = 10;
        var entryTrade = CreateTrade(entryOrder, 100m, 10m);
        _manager.OnOwnTradeReceived(entryTrade);

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m && o != entryOrder);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m && o != entryOrder);
        Assert.NotNull(slOrder);
        Assert.NotNull(tpOrder);

        // Simulate SL fill
        _strategy.Position = 0;
        var slTrade = CreateTrade(slOrder, 95m, 10m);
        _manager.OnOwnTradeReceived(slTrade);

        // TP order should be cancelled
        Assert.Contains(tpOrder, _strategy.CancelledOrders);
    }

    [Fact]
    public void OnOwnTradeReceived_WhenTakeProfitFills_CancelsStopLoss()
    {
        // Setup: entry filled, protection orders placed
        var signal = new TradeSignal
        {
            Direction = Sides.Buy,
            EntryPrice = 100m,
            Volume = 10m,
            StopLoss = 95m,
            TakeProfit = 110m
        };
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];

        _strategy.Position = 10;
        var entryTrade = CreateTrade(entryOrder, 100m, 10m);
        _manager.OnOwnTradeReceived(entryTrade);

        var slOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 95m && o != entryOrder);
        var tpOrder = _strategy.PlacedOrders.FirstOrDefault(o => o.Price == 110m && o != entryOrder);
        Assert.NotNull(slOrder);
        Assert.NotNull(tpOrder);

        // Simulate TP fill
        _strategy.Position = 0;
        var tpTrade = CreateTrade(tpOrder, 110m, 10m);
        _manager.OnOwnTradeReceived(tpTrade);

        // SL order should be cancelled
        Assert.Contains(slOrder, _strategy.CancelledOrders);
    }

    [Fact]
    public void Reset_ClearsAllTrackedOrders()
    {
        var signal = CreateValidBuySignal();
        _manager.HandleSignal(signal);

        _manager.Reset();

        Assert.Null(_manager.EntryOrder);
        Assert.Null(_manager.StopLossOrder);
        Assert.Null(_manager.TakeProfitOrder);
        Assert.Null(_manager.CurrentSignal);
    }

    [Fact]
    public void CloseAllPositions_WhenHasPosition_PlacesMarketOrderAndCancelsProtection()
    {
        // Setup: entry filled, protection orders placed
        var signal = new TradeSignal
        {
            Direction = Sides.Buy,
            EntryPrice = 100m,
            Volume = 10m,
            StopLoss = 95m,
            TakeProfit = 110m
        };
        _manager.HandleSignal(signal);
        var entryOrder = _strategy.PlacedOrders[0];

        _strategy.Position = 10;
        var entryTrade = CreateTrade(entryOrder, 100m, 10m);
        _manager.OnOwnTradeReceived(entryTrade);

        var protectionOrdersCount = _strategy.PlacedOrders.Count;

        // Close position
        _manager.CloseAllPositions();

        // Should have placed a market sell order
        var marketOrder = _strategy.PlacedOrders.LastOrDefault(o => o.OrderType == OrderTypes.Market);
        Assert.NotNull(marketOrder);
        Assert.Equal(Sides.Sell, marketOrder.Side);
        Assert.Equal(10m, marketOrder.Volume);
    }

    [Fact]
    public void HandleSignal_WithShortPosition_PlacesSellOrder()
    {
        var signal = new TradeSignal
        {
            Direction = Sides.Sell,
            EntryPrice = 100m,
            Volume = 10m,
            StopLoss = 105m,  // SL above entry for short
            TakeProfit = 90m   // TP below entry for short
        };

        _manager.HandleSignal(signal);

        var order = _strategy.PlacedOrders[0];
        Assert.Equal(Sides.Sell, order.Side);
        Assert.Equal(100m, order.Price);
    }

    #region Helper Methods

    private TradeSignal CreateValidBuySignal()
    {
        return new TradeSignal
        {
            Direction = Sides.Buy,
            EntryPrice = 100m,
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
            ServerTime = DateTimeOffset.Now
        };

        return new MyTrade
        {
            Order = order,
            Trade = trade
        };
    }

    #endregion

    #region Test Strategy

    private class TestStrategy : CustomStrategyBase
    {
        private decimal _position;
        public List<TestOrder> PlacedOrders { get; } = new();
        public List<TestOrder> CancelledOrders { get; } = new();

        private readonly Security _security = new()
        {
            Id = "TEST@TEST",
            PriceStep = 0.01m
        };

        private readonly Portfolio _portfolio = new()
        {
            Name = "TEST"
        };

        public TestStrategy()
        {
            // Set portfolio and security to avoid exceptions
            Portfolio = _portfolio;
            base.Security = _security;
        }

        public new decimal Position
        {
            get => _position;
            set => _position = value;
        }

        public new Security Security
        {
            get => _security;
            set { }  // No-op setter to allow assignment in constructor
        }

        public Order BuyLimit(decimal price, decimal volume)
        {
            var order = new TestOrder
            {
                Price = price,
                Volume = volume,
                Side = Sides.Buy,
                OrderType = OrderTypes.Limit,
                State = OrderStates.Active
            };
            PlacedOrders.Add(order);
            return order;
        }

        public Order SellLimit(decimal price, decimal volume)
        {
            var order = new TestOrder
            {
                Price = price,
                Volume = volume,
                Side = Sides.Sell,
                OrderType = OrderTypes.Limit,
                State = OrderStates.Active
            };
            PlacedOrders.Add(order);
            return order;
        }

        public void SellMarket(decimal volume)
        {
            var order = new TestOrder
            {
                Volume = volume,
                Side = Sides.Sell,
                OrderType = OrderTypes.Market,
                State = OrderStates.Active
            };
            PlacedOrders.Add(order);
        }

        public void BuyMarket(decimal volume)
        {
            var order = new TestOrder
            {
                Volume = volume,
                Side = Sides.Buy,
                OrderType = OrderTypes.Market,
                State = OrderStates.Active
            };
            PlacedOrders.Add(order);
        }

        public void CancelOrder(Order order)
        {
            if (order is TestOrder testOrder)
            {
                CancelledOrders.Add(testOrder);
                testOrder.State = OrderStates.Done;
            }
        }

        public void LogInfo(string message, params object[] args)
        {
            // No-op for testing
        }

        public void LogWarning(string message, params object[] args)
        {
            // No-op for testing
        }
    }

    private class TestOrder : Order
    {
        private OrderStates _state = OrderStates.Active;
        private decimal _price;
        private decimal _volume;
        private Sides _side;
        private OrderTypes _orderType = OrderTypes.Limit;

        public new OrderStates State
        {
            get => _state;
            set => _state = value;
        }

        public new decimal Price
        {
            get => _price;
            set => _price = value;
        }

        public new decimal Volume
        {
            get => _volume;
            set => _volume = value;
        }

        public new Sides Side
        {
            get => _side;
            set => _side = value;
        }

        public OrderTypes OrderType
        {
            get => _orderType;
            set => _orderType = value;
        }

        public new OrderTypes Type => _orderType;
    }

    #endregion
}

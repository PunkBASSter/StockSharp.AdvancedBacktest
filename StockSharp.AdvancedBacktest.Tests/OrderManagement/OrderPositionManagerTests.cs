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

    //TODO: Add more tests for other methods

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

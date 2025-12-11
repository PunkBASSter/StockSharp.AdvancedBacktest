using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class MockStrategyOrderOperations : IStrategyOrderOperations
{
    private long _nextOrderId = 1;
    private readonly List<Order> _orders = [];
    private readonly List<string> _logMessages = [];

    public Security Security { get; }
    public decimal Position { get; set; }
    public IReadOnlyList<Order> Orders => _orders.AsReadOnly();
    public IReadOnlyList<string> LogMessages => _logMessages.AsReadOnly();

    public MockStrategyOrderOperations(string securityId = "SBER@TQBR")
    {
        Security = new Security { Id = securityId };
    }

    public Order BuyLimit(decimal price, decimal volume)
    {
        var order = CreateOrder(Sides.Buy, OrderTypes.Limit, price, volume);
        _orders.Add(order);
        return order;
    }

    public Order SellLimit(decimal price, decimal volume)
    {
        var order = CreateOrder(Sides.Sell, OrderTypes.Limit, price, volume);
        _orders.Add(order);
        return order;
    }

    public Order BuyMarket(decimal volume)
    {
        var order = CreateOrder(Sides.Buy, OrderTypes.Market, 0, volume);
        _orders.Add(order);
        return order;
    }

    public Order SellMarket(decimal volume)
    {
        var order = CreateOrder(Sides.Sell, OrderTypes.Market, 0, volume);
        _orders.Add(order);
        return order;
    }

    public void CancelOrder(Order order)
    {
        order.State = OrderStates.Done;
    }

    public void LogInfo(string format, params object[] args)
    {
        _logMessages.Add($"[INFO] {string.Format(format, args)}");
    }

    public void LogWarning(string format, params object[] args)
    {
        _logMessages.Add($"[WARN] {string.Format(format, args)}");
    }

    private Order CreateOrder(Sides side, OrderTypes type, decimal price, decimal volume)
    {
        return new Order
        {
            Id = _nextOrderId++,
            TransactionId = _nextOrderId,
            Security = Security,
            Side = side,
            Type = type,
            Price = price,
            Volume = volume,
            State = OrderStates.Pending
        };
    }

    public void ClearOrders()
    {
        _orders.Clear();
    }

    public void ClearLogs()
    {
        _logMessages.Clear();
    }
}

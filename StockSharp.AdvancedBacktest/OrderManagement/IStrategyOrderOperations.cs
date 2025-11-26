using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public interface IStrategyOrderOperations
{
    Security Security { get; }
    decimal Position { get; }

    Order BuyLimit(decimal price, decimal volume);
    Order SellLimit(decimal price, decimal volume);
    Order BuyMarket(decimal volume);
    Order SellMarket(decimal volume);
    void CancelOrder(Order order);

    void LogInfo(string format, params object[] args);
    void LogWarning(string format, params object[] args);
}

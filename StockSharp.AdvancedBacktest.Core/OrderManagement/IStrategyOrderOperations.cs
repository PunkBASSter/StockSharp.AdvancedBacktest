using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public interface IStrategyOrderOperations
{
    Order PlaceOrder(Order order);
    void CancelOrder(Order order);
}

namespace StockSharp.AdvancedBacktest.OrderManagement;

public enum GroupedOrderState
{
    Pending,
    Active,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected
}

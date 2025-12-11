using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public interface IOrderGroupManager
{
    OrderGroupLimits Limits { get; }

    OrderGroup CreateOrderGroup(ExtendedTradeSignal signal, bool? throwIfNotMatchingVolume = null, decimal? currentEquity = null);

    IReadOnlyList<OrderGroup> GetActiveGroups(string? securityId = null);

    OrderGroup? GetGroupById(string groupId);

    void AdjustOrderPrice(string groupId, string orderId, decimal newPrice);

    void CloseGroup(string groupId);

    void CloseAllGroups(string? securityId = null);

    void OnOrderFilled(Order order, MyTrade trade);

    void OnOrderCancelled(Order order);

    void OnOrderRejected(Order order);

    void Reset();

    decimal CalculateRiskPercent(decimal entryPrice, decimal volume, decimal stopLossPrice, decimal currentEquity);

    event Action<OrderGroup, GroupedOrder>? OrderActivated;

    event Action<OrderGroup>? GroupCompleted;

    event Action<OrderGroup>? GroupCancelled;

    event Action<OrderGroup, GroupedOrder>? OrderRejected;
}

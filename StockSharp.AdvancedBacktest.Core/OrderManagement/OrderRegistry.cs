using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public record EntryOrderGroup(Order EntryOrder, Dictionary<string, (Order SlOrder, Order TpOrder)> ProtectivePairs)
{
    //TODO: make in easily comparable by volume, price levels, etc. to determine if an order already exists
}

public record GetOrderGroupRequest(); //TODO: add fields to filter by, including some hash based on volume, price levels, etc. (complarison result from above TODO)

/// <summary>
/// Registry to manage entry orders and their associated protective stop-loss and take-profit orders for a strategy.
/// </summary>
/// <param name="StrategyId"></param>
public class OrderRegistry(string StrategyId) //TODO: add methods to initialize registry from existing live orders requested from connector when strategy is reconnected in live trading
{
    private const string GroupKeyFormat = "groupV1{0}_{1}";
    private const string ProtectionPairKeyFormat = "pairV1{0}_{1}_{2}";

    private readonly Dictionary<string, EntryOrderGroup> _orderGroups = [];

    public EntryOrderGroup RegisterGroup(Order entryOrder, List<(Order SlOrder, Order TpOrder)> protectivePairs)
    {
        ArgumentNullException.ThrowIfNull(entryOrder);
        //TODO: Add validation to ensure protective pairs volumes match entry order volume, and price levels are appropriate.

        var grouping = new EntryOrderGroup(entryOrder, protectivePairs.ToDictionary(
            pp => Guid.NewGuid().ToString(),
            pp => pp));

        var groupId = Guid.NewGuid().ToString();
        foreach (var kv in grouping.ProtectivePairs)
        {
            kv.Value.SlOrder.Comment = string.Format(ProtectionPairKeyFormat, StrategyId, groupId, kv.Key);
            kv.Value.TpOrder.Comment = string.Format(ProtectionPairKeyFormat, StrategyId, groupId, kv.Key);
        }

        _orderGroups[string.Format(GroupKeyFormat, StrategyId, groupId)] = grouping;
        return grouping;
    }

    public EntryOrderGroup? GetOrderRequest(GetOrderGroupRequest request)
    {
        //TODO: implement filtering based on request fields;
        throw new NotImplementedException();
    }
}
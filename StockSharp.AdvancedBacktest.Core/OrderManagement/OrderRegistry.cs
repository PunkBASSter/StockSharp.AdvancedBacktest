using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public enum OrderGroupState { Pending, EntryFilled, ProtectionActive, Closed }

public record EntryOrderGroup(
    string GroupId,
    Order EntryOrder,
    Dictionary<string, (Order? SlOrder, Order? TpOrder, ProtectivePair Spec)> ProtectivePairs,
    OrderGroupState State = OrderGroupState.Pending)
{
    public OrderGroupState State { get; set; } = State;

    public bool Matches(OrderRequest request, decimal tolerance = 0.00000001m)
    {
        var order = request.Order;
        var pairs = request.ProtectivePairs;

        if (Math.Abs(EntryOrder.Price - order.Price) > tolerance)
            return false;
        if (EntryOrder.Side != order.Side)
            return false;
        if (EntryOrder.Volume != order.Volume)
            return false;

        if (ProtectivePairs.Count != pairs.Count)
            return false;

        var existingSpecs = ProtectivePairs.Values
            .Select(pp => pp.Spec)
            .OrderBy(s => s.StopLossPrice)
            .ThenBy(s => s.TakeProfitPrice)
            .ToList();

        var newSpecs = pairs
            .OrderBy(s => s.StopLossPrice)
            .ThenBy(s => s.TakeProfitPrice)
            .ToList();

        for (var i = 0; i < existingSpecs.Count; i++)
        {
            var existing = existingSpecs[i];
            var incoming = newSpecs[i];

            if (Math.Abs(existing.StopLossPrice - incoming.StopLossPrice) > tolerance)
                return false;
            if (Math.Abs(existing.TakeProfitPrice - incoming.TakeProfitPrice) > tolerance)
                return false;
            if (existing.Volume != incoming.Volume)
                return false;
        }

        return true;
    }
}

public class OrderRegistry(string strategyId)
{
    private readonly Dictionary<string, EntryOrderGroup> _orderGroups = [];

    public int MaxConcurrentGroups { get; set; } = 5;

    public EntryOrderGroup RegisterGroup(Order entryOrder, List<ProtectivePair> protectivePairs)
    {
        ArgumentNullException.ThrowIfNull(entryOrder);

        var activeCount = _orderGroups.Values.Count(g => g.State != OrderGroupState.Closed);
        if (activeCount >= MaxConcurrentGroups)
            throw new InvalidOperationException($"Maximum concurrent groups ({MaxConcurrentGroups}) reached");

        var totalPairVolume = protectivePairs.Sum(pp => pp.Volume ?? entryOrder.Volume);
        if (protectivePairs.Count > 1 && totalPairVolume != entryOrder.Volume)
            throw new ArgumentException($"Protective pair volumes ({totalPairVolume}) must equal entry volume ({entryOrder.Volume})");

        var groupId = Guid.NewGuid().ToString();
        var pairs = protectivePairs.ToDictionary(
            _ => Guid.NewGuid().ToString(),
            pp => ((Order?)null, (Order?)null, pp));

        var group = new EntryOrderGroup(groupId, entryOrder, pairs);
        _orderGroups[$"{strategyId}_{groupId}"] = group;

        return group;
    }

    public EntryOrderGroup[] GetActiveGroups() =>
        _orderGroups.Values.Where(g => g.State != OrderGroupState.Closed).ToArray();

    public EntryOrderGroup? FindMatchingGroup(OrderRequest request, decimal tolerance = 0.00000001m) =>
        _orderGroups.Values.FirstOrDefault(g =>
            g.State != OrderGroupState.Closed && g.Matches(request, tolerance));

    public EntryOrderGroup? FindGroupByOrder(Order order) =>
        _orderGroups.Values.FirstOrDefault(g =>
            g.EntryOrder == order ||
            g.ProtectivePairs.Values.Any(pp => pp.SlOrder == order || pp.TpOrder == order));

    public void Reset() => _orderGroups.Clear();
}

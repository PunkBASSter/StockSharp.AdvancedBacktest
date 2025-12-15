using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public class PartialFillRetryHandler
{
    private const int MaxRetryAttempts = 5;
    private readonly Dictionary<string, int> _retryCounters = [];
    private readonly Dictionary<string, Order> _retryOrders = [];
    private bool _requiresManualIntervention;

    public event Action<string, object?>? OnRetryEvent;

    public bool TryGetRetryKey(Order order, out string? retryKey)
    {
        var entry = _retryOrders.FirstOrDefault(kv => kv.Value == order);
        retryKey = entry.Key;
        return retryKey != null;
    }

    public Order? InitiateRetry(string groupId, string pairId, decimal remainingVolume, Sides side, Func<Sides, decimal, Order> placeMarketOrder)
    {
        var retryKey = $"{groupId}_{pairId}";

        if (!_retryCounters.TryGetValue(retryKey, out var retryCount))
            retryCount = 0;

        retryCount++;
        _retryCounters[retryKey] = retryCount;

        if (retryCount >= MaxRetryAttempts)
        {
            _requiresManualIntervention = true;
            OnRetryEvent?.Invoke("MaxRetryReached", new { GroupId = groupId, PairId = pairId, RetryCount = retryCount, RemainingVolume = remainingVolume });
            return null;
        }

        var marketOrder = placeMarketOrder(side, remainingVolume);
        _retryOrders[retryKey] = marketOrder;
        OnRetryEvent?.Invoke("PartialFillRetry", new { GroupId = groupId, PairId = pairId, RetryCount = retryCount, RemainingVolume = remainingVolume });

        return marketOrder;
    }

    public (bool needsMoreRetries, string? groupId, string? pairId) HandleRetryFill(string retryKey, MyTrade trade, Func<Sides, decimal, Order> placeMarketOrder)
    {
        var remainingVolume = trade.Order.Balance;

        if (remainingVolume > 0)
        {
            var parts = retryKey.Split('_');
            if (parts.Length < 2)
                return (false, null, null);

            var groupId = parts[0];
            var pairId = parts[1];

            var retryOrder = InitiateRetry(groupId, pairId, remainingVolume, trade.Order.Side, placeMarketOrder);
            return (retryOrder != null, groupId, pairId);
        }

        _retryOrders.Remove(retryKey);

        var keyParts = retryKey.Split('_');
        if (keyParts.Length < 2)
            return (false, null, null);

        return (false, keyParts[0], keyParts[1]);
    }

    public bool HasReachedRetryLimit() => _retryCounters.Values.Any(count => count >= MaxRetryAttempts);

    public bool RequiresManualIntervention() => _requiresManualIntervention;

    public void Reset()
    {
        _retryCounters.Clear();
        _retryOrders.Clear();
        _requiresManualIntervention = false;
    }
}

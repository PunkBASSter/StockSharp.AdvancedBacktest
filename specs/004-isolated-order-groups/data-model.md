# Data Model: Isolated Order Groups with Split Position Management

**Feature**: 004-isolated-order-groups
**Date**: 2025-12-15

## Entity Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        OrderRegistry                             │
│  (Central registry for all order groups in a strategy)          │
├─────────────────────────────────────────────────────────────────┤
│  - StrategyId: string                                           │
│  - MaxConcurrentGroups: int (default: 5)                        │
│  - OrderGroups: Dictionary<string, EntryOrderGroup>             │
├─────────────────────────────────────────────────────────────────┤
│  + RegisterGroup(Order, List<ProtectivePair>): EntryOrderGroup  │
│  + GetActiveGroups(): EntryOrderGroup[]                         │
│  + FindMatchingGroup(OrderRequest, tolerance?): EntryOrderGroup?│
│  + FindGroupByOrder(Order): EntryOrderGroup?                    │
│  + Reset(): void                                                │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ 1:N
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      EntryOrderGroup (record)                    │
│  (Single entry order with its protective orders)                │
├─────────────────────────────────────────────────────────────────┤
│  - GroupId: string (GUID)                                       │
│  - EntryOrder: Order                                            │
│  - State: OrderGroupState (mutable property)                    │
│  - ProtectivePairs: Dictionary<string, (Order? SlOrder,         │
│                     Order? TpOrder, ProtectivePair Spec)>       │
├─────────────────────────────────────────────────────────────────┤
│  + Matches(OrderRequest, tolerance?): bool                      │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                       OrderRequest (record)                      │
│  (Input for creating an order group)                            │
├─────────────────────────────────────────────────────────────────┤
│  - Order: Order                                                 │
│  - ProtectivePairs: List<ProtectivePair>                        │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      ProtectivePair (record)                     │
│  (Input specification for SL/TP pair)                           │
├─────────────────────────────────────────────────────────────────┤
│  - StopLossPrice: decimal                                       │
│  - TakeProfitPrice: decimal                                     │
│  - Volume: decimal?  (null = use entry volume)                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                  IStrategyOrderOperations                        │
│  (Minimal interface for order execution)                        │
├─────────────────────────────────────────────────────────────────┤
│  + PlaceOrder(Order): Order                                     │
│  + CancelOrder(Order): void                                     │
└─────────────────────────────────────────────────────────────────┘
```

## Enumerations

### OrderGroupState

```csharp
public enum OrderGroupState
{
    Pending = 0,          // Entry order placed, waiting for fill
    EntryFilled = 1,      // Entry filled, placing protective orders
    ProtectionActive = 2, // All protective orders active
    Closed = 3            // All positions closed (terminal)
}
```

**Valid Transitions**:
- `Pending → EntryFilled` (entry order fills)
- `Pending → Closed` (entry order cancelled/expired)
- `EntryFilled → ProtectionActive` (protective orders placed)
- `EntryFilled → Closed` (immediate close before protection)
- `ProtectionActive → Closed` (all protective orders filled/cancelled)


## Entity Definitions

### ProtectivePair (record)

```csharp
public record ProtectivePair(decimal StopLossPrice, decimal TakeProfitPrice, decimal? Volume);
```

### OrderRequest (record)

```csharp
public record OrderRequest(Order Order, List<ProtectivePair> ProtectivePairs);
```

**Notes**: Validation (volume sum, price direction) is performed in `OrderRegistry.RegisterGroup()`, not in the record itself.

### EntryOrderGroup (record)

```csharp
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
```

**Notes**:
- Uses record with mutable `State` property for state machine transitions
- ProtectivePairs stored as tuple `(Order? SlOrder, Order? TpOrder, ProtectivePair Spec)` - no separate ProtectivePairOrders class
- `Matches()` compares entry price, side, volume, and all protective pairs within tolerance
- Default tolerance `0.00000001m` works across forex, crypto, and stocks

### OrderRegistry (class)

```csharp
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
```

### IStrategyOrderOperations (interface)

```csharp
public interface IStrategyOrderOperations
{
    Order PlaceOrder(Order order);
    void CancelOrder(Order order);
}
```

**Notes**: Minimal interface with only two methods. Implemented by `CustomStrategyBase`.

## Relationships

| From | To | Cardinality | Description |
|------|-----|-------------|-------------|
| OrderRegistry | EntryOrderGroup | 1:N | Registry manages multiple order groups |
| EntryOrderGroup | ProtectivePair tuple | 1:N | Each entry has multiple protective pairs (as tuples) |
| OrderRequest | ProtectivePair | 1:N | Request contains protective pair specs |
| EntryOrderGroup | Order | 1:1 | Entry order reference |
| ProtectivePair tuple | Order | 0..2 | SL and TP order references (nullable until placed) |

## Validation Rules

1. **Volume Consistency**: Sum of `ProtectivePair.Volume` must equal `EntryOrder.Volume` (when multiple pairs)
2. **Concurrent Limit**: Active groups cannot exceed `MaxConcurrentGroups`
3. **State Transitions**: Only valid transitions allowed (see state machine above)
4. **Unique Group ID**: Each group has a unique GUID identifier
5. **Tolerance Default**: `0.00000001m` for price matching (works across all asset types)

## Infrastructure Entities (Infrastructure Assembly)

### TimestampRemapper

```csharp
public static class TimestampRemapper
{
    public static DateTimeOffset RemapToMainTimeframe(
        DateTimeOffset eventTime,
        TimeSpan mainTimeframe)
    {
        var ticks = eventTime.Ticks;
        var intervalTicks = mainTimeframe.Ticks;
        var flooredTicks = (ticks / intervalTicks) * intervalTicks;
        return new DateTimeOffset(flooredTicks, eventTime.Offset);
    }
}
```

### DebugModeProvider

```csharp
public class DebugModeProvider : IDisposable
{
    public bool IsHumanDebugEnabled { get; set; }
    public bool IsAiDebugEnabled { get; set; }
    public TimeSpan MainTimeframe { get; set; }

    private IDebugModeOutput? _humanOutput;
    private IDebugModeOutput? _aiOutput;

    public void SetHumanOutput(IDebugModeOutput output);
    public void SetAiOutput(IDebugModeOutput output);
    public void CaptureEvent(object eventData, DateTimeOffset timestamp, bool isAuxiliaryTimeframe);
    public void CaptureCandle(ICandleMessage candle, string securityId, bool isAuxiliaryTimeframe);
    public void CaptureIndicator(string indicatorName, decimal? value, DateTimeOffset timestamp, bool isAuxiliaryTimeframe);
    public void CaptureTrade(MyTrade trade, bool isAuxiliaryTimeframe);
    public void Flush();
    public void Dispose();
}
```

### IDebugModeOutput (interface)

```csharp
public interface IDebugModeOutput : IDisposable
{
    void Write(object eventData, DateTimeOffset displayTimestamp);
    void Flush();
}
```

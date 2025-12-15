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
│  + RegisterGroup(OrderRequest): EntryOrderGroup                 │
│  + GetActiveGroups(): EntryOrderGroup[]                         │
│  + GetGroupById(groupId): EntryOrderGroup?                      │
│  + FindMatchingGroup(entryPrice, slPrice, tpPrice, tol): EntryOrderGroup? │
│  + Reset(): void                                                │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ 1:N
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      EntryOrderGroup                             │
│  (Single entry order with its protective orders)                │
├─────────────────────────────────────────────────────────────────┤
│  - GroupId: string (GUID)                                       │
│  - EntryOrder: Order                                            │
│  - State: OrderGroupState                                       │
│  - ProtectivePairs: Dictionary<string, ProtectivePairOrders>    │
│  - CreatedAt: DateTimeOffset                                    │
│  - ClosedAt: DateTimeOffset?                                    │
├─────────────────────────────────────────────────────────────────┤
│  + TransitionTo(newState): void                                 │
│  + GetProtectivePairs(): ProtectivePairOrders[]                 │
│  + RemovePair(pairId): bool                                     │
│  + IsActive: bool                                               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ 1:N
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    ProtectivePairOrders                          │
│  (Actual orders for a protective pair)                          │
├─────────────────────────────────────────────────────────────────┤
│  - PairId: string (GUID)                                        │
│  - SlOrder: Order?                                              │
│  - TpOrder: Order?                                              │
│  - Volume: decimal                                              │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                       OrderRequest                               │
│  (Input for creating an order group)                            │
├─────────────────────────────────────────────────────────────────┤
│  - EntryOrder: Order                                            │
│  - ProtectivePairs: List<ProtectivePair>                        │
├─────────────────────────────────────────────────────────────────┤
│  + Validate(): void (throws if invalid)                         │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      ProtectivePair                              │
│  (Input specification for SL/TP pair)                           │
├─────────────────────────────────────────────────────────────────┤
│  - StopLossPrice: decimal                                       │
│  - TakeProfitPrice: decimal                                     │
│  - Volume: decimal?  (null = use entry volume)                  │
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

### OrderRequest (record)

```csharp
public record OrderRequest(Order EntryOrder, List<ProtectivePair> ProtectivePairs)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(EntryOrder);

        if (ProtectivePairs == null || ProtectivePairs.Count == 0)
            throw new ArgumentException("At least one protective pair required");

        var totalVolume = ProtectivePairs
            .Sum(p => p.Volume ?? EntryOrder.Volume);

        if (totalVolume != EntryOrder.Volume)
            throw new ArgumentException(
                $"Protective pair volumes ({totalVolume}) must equal entry volume ({EntryOrder.Volume})");

        // Validate price levels based on direction
        var direction = EntryOrder.Side;
        foreach (var pair in ProtectivePairs)
        {
            if (direction == Sides.Buy)
            {
                if (pair.StopLossPrice >= EntryOrder.Price)
                    throw new ArgumentException("SL must be below entry for buy orders");
                if (pair.TakeProfitPrice <= EntryOrder.Price)
                    throw new ArgumentException("TP must be above entry for buy orders");
            }
            else
            {
                if (pair.StopLossPrice <= EntryOrder.Price)
                    throw new ArgumentException("SL must be above entry for sell orders");
                if (pair.TakeProfitPrice >= EntryOrder.Price)
                    throw new ArgumentException("TP must be below entry for sell orders");
            }
        }
    }
}
```

### ProtectivePair (record)

```csharp
public record ProtectivePair(
    decimal StopLossPrice,
    decimal TakeProfitPrice,
    decimal? Volume = null);
```

### EntryOrderGroup (class)

```csharp
public class EntryOrderGroup
{
    public string GroupId { get; }
    public Order EntryOrder { get; }
    public OrderGroupState State { get; private set; }
    public Dictionary<string, ProtectivePairOrders> ProtectivePairs { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsActive => State != OrderGroupState.Closed;

    public void TransitionTo(OrderGroupState newState)
    {
        // Validate transition is legal
        // Update State
        // Set ClosedAt if transitioning to Closed
    }

    public ProtectivePairOrders[] GetProtectivePairs()
    {
        return ProtectivePairs.Values.ToArray();
    }

    public bool RemovePair(string pairId)
    {
        return ProtectivePairs.Remove(pairId);
    }
}
```

### ProtectivePairOrders (class)

```csharp
public class ProtectivePairOrders
{
    public string PairId { get; }
    public Order? SlOrder { get; set; }
    public Order? TpOrder { get; set; }
    public decimal Volume { get; }
}
```

### OrderRegistry (class)

```csharp
public class OrderRegistry
{
    private readonly string _strategyId;
    private readonly Dictionary<string, EntryOrderGroup> _orderGroups = new();

    public int MaxConcurrentGroups { get; set; } = 5;

    public EntryOrderGroup RegisterGroup(OrderRequest request)
    {
        request.Validate();

        var activeCount = _orderGroups.Values.Count(g => g.IsActive);
        if (activeCount >= MaxConcurrentGroups)
            throw new InvalidOperationException(
                $"Maximum concurrent groups ({MaxConcurrentGroups}) reached");

        // Create EntryOrderGroup from request
        // Add to dictionary
        // Return created group
    }

    public EntryOrderGroup[] GetActiveGroups()
    {
        return _orderGroups.Values
            .Where(g => g.IsActive)
            .ToArray();
    }

    public EntryOrderGroup? GetGroupById(string groupId)
    {
        return _orderGroups.TryGetValue(groupId, out var group) ? group : null;
    }

    public EntryOrderGroup? FindMatchingGroup(
        decimal entryPrice,
        decimal slPrice,
        decimal tpPrice,
        decimal tolerance)
    {
        return _orderGroups.Values
            .FirstOrDefault(g => g.IsActive &&
                Math.Abs(g.EntryOrder.Price - entryPrice) <= tolerance &&
                g.ProtectivePairs.Values.Any(p =>
                    p.SlOrder != null && Math.Abs(p.SlOrder.Price - slPrice) <= tolerance &&
                    p.TpOrder != null && Math.Abs(p.TpOrder.Price - tpPrice) <= tolerance));
    }

    public void Reset()
    {
        _orderGroups.Clear();
    }
}
```

## Relationships

| From | To | Cardinality | Description |
|------|-----|-------------|-------------|
| OrderRegistry | EntryOrderGroup | 1:N | Registry manages multiple order groups |
| EntryOrderGroup | ProtectivePairOrders | 1:N | Each entry has multiple protective pairs |
| OrderRequest | ProtectivePair | 1:N | Request contains protective pair specs |
| EntryOrderGroup | Order | 1:1 | Entry order reference |
| ProtectivePairOrders | Order | 1:2 | SL and TP order references |

## Validation Rules

1. **Volume Consistency**: Sum of `ProtectivePair.Volume` must equal `EntryOrder.Volume`
2. **Price Direction**: SL/TP prices must be on correct side of entry based on order direction
3. **Concurrent Limit**: Active groups cannot exceed `MaxConcurrentGroups`
4. **State Transitions**: Only valid transitions allowed (see state machine above)
5. **Unique Group ID**: Each group has a unique GUID identifier

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

    public void Initialize(CustomStrategyBase strategy, TimeSpan mainTimeframe);
    public void CaptureEvent(object eventData, DateTimeOffset timestamp, bool isAuxiliaryTimeframe);
    public void Cleanup();
}
```

### IDebugModeOutput (interface)

```csharp
public interface IDebugModeOutput : IDisposable
{
    void Initialize(CustomStrategyBase strategy);
    void Write(object eventData, DateTimeOffset displayTimestamp);
    void Flush();
}
```

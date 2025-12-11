# Research: Advanced Order Group Management

**Branch**: `003-order-group-management` | **Date**: 2025-12-11

## Research Tasks

### 1. Order State Management Pattern

**Question**: What pattern should be used for managing order and group state transitions?

**Decision**: State Machine pattern with explicit state transition validation

**Rationale**:
- Order groups have well-defined states (Pending → Active → Closing → Completed/Cancelled)
- State transitions must be validated to prevent invalid operations (e.g., cannot adjust filled order price)
- Explicit transitions enable hooks/events for each state change
- Pattern aligns with StockSharp's existing `OrderStates` enumeration approach

**Alternatives Considered**:
- **Simple enum with if/else**: Rejected - error-prone, transitions not enforced
- **Full State pattern (GoF)**: Rejected - over-engineering for 5 states; constitution mandates simplicity

### 2. Thread Safety for Multiple Groups

**Question**: How should concurrent access to order groups be handled?

**Decision**: Single-threaded execution model with event-driven updates

**Rationale**:
- StockSharp strategies execute on a single thread (message-driven)
- Order events (fills, cancellations) arrive sequentially via connector callbacks
- No locking required - simplifies implementation and eliminates deadlock risk
- Matches existing `OrderPositionManager` pattern

**Alternatives Considered**:
- **ConcurrentDictionary with locks**: Rejected - unnecessary complexity given single-threaded model
- **Immutable state with copy-on-write**: Rejected - performance overhead for frequent state updates

### 3. Pro-Rata Volume Scaling for Partial Fills

**Question**: How should fractional volumes be handled when pro-rata scaling produces non-integer results?

**Decision**: Round to security's lot size using `Math.Round` with `MidpointRounding.ToZero`, ensuring total volume doesn't exceed filled amount

**Rationale**:
- Securities have minimum lot sizes (e.g., 1 share, 0.001 BTC)
- Rounding must respect lot constraints to avoid order rejection
- Use `decimal` throughout to maintain precision
- Any remainder after rounding goes to the largest closing order

**Alternatives Considered**:
- **Always round up**: Rejected - could exceed filled volume
- **Truncate all**: Rejected - could leave unfilled volume

**Example**:
```
Opening: 100 shares, 60% filled = 60 shares
Closing orders: [30, 30, 40] → scaled [18, 18, 24]
With lot size 10: [20, 20, 20] (rounded, adjusted to sum to 60)
```

### 4. JSON Persistence Strategy

**Question**: How should order group state be persisted for live mode recovery?

**Decision**: Write-on-change to single JSON file per security with atomic file replacement

**Rationale**:
- Simple recovery: load single file on startup
- Atomic replacement prevents partial writes on crash
- Per-security files enable parallel recovery for multi-security strategies
- Source-generated `JsonSerializerContext` for performance

**Alternatives Considered**:
- **SQLite database**: Rejected - overkill for ~100 groups; adds dependency
- **Append-only log**: Rejected - requires compaction; more complex recovery
- **Write on every tick**: Rejected - I/O overhead; only state changes matter

**File Format**:
```json
{
  "securityId": "AAPL@NASDAQ",
  "lastUpdated": "2025-12-11T10:30:00.123Z",
  "groups": [
    {
      "groupId": "AAPL@NASDAQ_20251211103000123_150.50",
      "state": "Active",
      "openingOrder": { ... },
      "closingOrders": [ ... ]
    }
  ]
}
```

### 5. Risk Calculation Implementation

**Question**: How should stop distance percentage be determined when stop-loss price is provided as absolute value?

**Decision**: Calculate stop distance as `|EntryPrice - StopLossPrice| / EntryPrice`

**Rationale**:
- Spec defines risk formula: `(Entry × Volume × StopDistance%) / Equity`
- Stop distance percentage derived from absolute prices maintains consistency
- Works for both long (StopLoss < Entry) and short (StopLoss > Entry) positions

**Formula**:
```csharp
decimal stopDistancePercent = Math.Abs(entryPrice - stopLossPrice) / entryPrice;
decimal risk = (entryPrice * volume * stopDistancePercent) / currentEquity;
```

### 6. Order Type Auto-Selection

**Question**: How should order types be automatically selected for non-market orders (FR-016)?

**Decision**: Compare order price to current market price; use limit orders positioned appropriately

**Rationale**:
- If buy price ≥ current ask → order will likely fill immediately (could use market)
- If buy price < current ask → limit order waits at specified price
- For closing orders, inverse logic applies for sell orders
- Default to limit orders for predictable execution prices

**Logic**:
```
Long position closing (sell):
  - Price > Current Bid → Limit sell (take profit)
  - Price < Current Bid → Limit sell (stop loss, will wait)

Short position closing (buy):
  - Price < Current Ask → Limit buy (take profit)
  - Price > Current Ask → Limit buy (stop loss, will wait)
```

### 7. Interface Design: IOrderGroupManager

**Question**: What methods should the main abstraction expose?

**Decision**: Minimal interface following existing `OrderPositionManager` patterns

**Rationale**:
- Maintain API consistency with existing codebase
- Single responsibility: manage order groups, not strategy logic
- Event-based notifications via C# events (not callbacks)

**Proposed Interface** (detailed in contracts/):
```csharp
public interface IOrderGroupManager
{
    // Creation
    OrderGroup CreateOrderGroup(ExtendedTradeSignal signal, bool throwIfNotMatchingVolume = true);

    // Queries
    IReadOnlyList<OrderGroup> GetActiveGroups(string? securityId = null);
    OrderGroup? GetGroupById(string groupId);

    // Modifications
    void AdjustOrderPrice(string groupId, string orderId, decimal newPrice);
    void CloseGroup(string groupId);
    void CloseAllGroups(string? securityId = null);

    // Event handling (called by strategy)
    void OnOrderFilled(Order order, MyTrade trade);
    void OnOrderCancelled(Order order);
    void OnOrderRejected(Order order);

    // Lifecycle
    void Reset();

    // Events
    event Action<OrderGroup, GroupedOrder> OrderActivated;
    event Action<OrderGroup> GroupCompleted;
    event Action<OrderGroup> GroupCancelled;
}
```

## Summary

All technical decisions align with:
- Constitution principles (separation of concerns, financial precision, composition)
- Existing codebase patterns (single-threaded, event-driven, decimal types)
- Spec requirements (pro-rata scaling, JSON persistence, state tracking)

No NEEDS CLARIFICATION items remain. Ready for Phase 1: Design & Contracts.

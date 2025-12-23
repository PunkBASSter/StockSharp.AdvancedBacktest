# Research: Isolated Order Groups with Split Position Management

**Feature**: 004-isolated-order-groups
**Date**: 2025-12-15

## Research Areas

### 1. Order Group State Machine Design

**Decision**: Use 4-state model: `Pending → EntryFilled → ProtectionActive → Closed`

**Rationale**:
- **Pending**: Entry order placed but not yet filled - allows cancellation/modification
- **EntryFilled**: Entry filled, protective orders being placed - transitional state
- **ProtectionActive**: All protective orders placed and active - normal operating state
- **Closed**: All positions closed (via SL/TP/manual) - terminal state

**Alternatives Considered**:
- 3-state (Pending → Active → Closed): Insufficient - can't distinguish between entry pending and protection active
- 6-state with partial fills: Over-engineering - partial fills handled via volume tracking within states

**Implementation Notes**:
- Use enum `OrderGroupState` with explicit values
- State transitions validated in `EntryOrderGroup.TransitionTo()` method
- Invalid transitions throw `InvalidOperationException`

### 2. Multiple Protective Pairs Per Entry

**Decision**: Use `List<ProtectivePair>` where each pair contains SL price, TP price, and volume

**Rationale**:
- Enables partial exit strategies (e.g., 50% at TP1, 50% at TP2)
- Volume validation ensures pairs sum to entry volume
- Each pair tracked independently for cancellation logic

**Alternatives Considered**:
- Single SL/TP per entry: Too restrictive for sophisticated strategies
- Separate SL and TP lists: Harder to maintain pairing relationship for cancellation

**Implementation Notes**:
- `ProtectivePair` is immutable record: `record ProtectivePair(decimal StopLossPrice, decimal TakeProfitPrice, decimal Volume)`
- Validation in `OrderRequest.Validate()` checks `ProtectivePairs.Sum(p => p.Volume) == EntryVolume`
- When TP fills, corresponding SL is cancelled (tracked by pair ID)

### 3. Auxiliary Timeframe Subscription Pattern

**Decision**: Create separate internal candle subscription for order maintenance at 5-minute intervals

**Rationale**:
- StockSharp supports multiple concurrent subscriptions per security
- 5-minute candles provide 12x more granularity than hourly for SL/TP checks
- Keeps main strategy logic clean - auxiliary subscription handled by `OrderPositionManager`

**Alternatives Considered**:
- Tick-level subscription: Too much overhead for backtesting; 5-min sufficient
- Timer-based checks: Doesn't align with candle data availability in backtests

**Implementation Notes**:
- `AuxiliarySubscriptionManager` creates subscription in `CustomStrategyBase.OnStarted2()`
- Handler only calls `OrderPositionManager.CheckProtectionLevels(candle)`
- Candle data not stored or exported - purely for internal logic

### 4. Timestamp Remapping for Events

**Decision**: Store actual timestamp internally; remap to parent main TF candle for display

**Rationale**:
- Internal chronological order preserved for correct event sequencing
- User sees events attributed to the main TF candle they belong to
- Prevents confusion from auxiliary TF timestamps appearing in outputs

**Alternatives Considered**:
- Truncate timestamps to main TF: Loses precision for internal ordering
- Display auxiliary TF timestamps: Violates invisibility requirement

**Implementation Notes**:
- `TimestampRemapper` class with `RemapToMainTimeframe(DateTimeOffset eventTime, TimeSpan mainTimeframe)` method
- Returns floor of event time to nearest main TF boundary
- Example: 1:15 with 1-hour main TF → 1:00

### 5. Unified Debug Mode Abstraction

**Decision**: Create `DebugModeProvider` that manages both AI and human debug outputs

**Rationale**:
- Single point of configuration for enabling/disabling modes
- Consistent filtering of auxiliary TF events across both modes
- Enables simultaneous operation for testing and validation

**Alternatives Considered**:
- Separate provider classes: Duplicates filtering logic
- Combine into existing `DebugModeExporter`: Violates single responsibility

**Implementation Notes**:
```csharp
public class DebugModeProvider
{
    public bool IsHumanDebugEnabled { get; set; }
    public bool IsAiDebugEnabled { get; set; }

    public void CaptureEvent(DebugEvent event, bool isAuxiliaryTimeframe)
    {
        if (isAuxiliaryTimeframe) return; // Filter out

        if (IsHumanDebugEnabled) _humanOutput.Write(event);
        if (IsAiDebugEnabled) _aiOutput.Write(event);
    }
}
```

### 6. Concurrent Order Group Limit

**Decision**: Configurable limit with default of 5

**Rationale**:
- Prevents runaway position accumulation
- 5 provides flexibility while maintaining reasonable risk exposure
- Configurable allows strategy-specific tuning

**Implementation Notes**:
- Property `MaxConcurrentGroups` on `OrderPositionManager` or `OrderRegistry`
- Default value: 5
- `RegisterGroup()` throws `InvalidOperationException` when limit reached

### 7. Pessimistic SL/TP Trigger Order

**Decision**: When both SL and TP could trigger in same auxiliary candle, trigger SL first

**Rationale**:
- Conservative assumption prevents overly optimistic backtest results
- Real markets typically see adverse moves hit stops before favorable moves hit targets
- Reduces risk of false confidence in strategy performance

**Implementation Notes**:
- In `CheckProtectionLevels()`, check SL condition before TP
- If both would trigger, execute SL path only
- Log warning when this fallback is used for debugging

## Dependencies & Integration

### StockSharp Integration Points

1. **Candle Subscription**: Use `SubscribeCandles()` with separate `Subscription` object for auxiliary TF
2. **Order Placement**: Continue using `BuyLimit()`, `SellLimit()`, `BuyMarket()`, `SellMarket()` from Strategy base
3. **Order Events**: Hook into `OnOwnTradeReceived()` for fill notifications
4. **Order Cancellation**: Use `CancelOrder()` for protective order management

### Existing Code Reuse

- `PriceStepHelper`: For price level comparisons (tolerance matching)
- `DebugModeExporter`: Extract human debug output logic
- `AgenticEventLogger`: Reuse for AI debug output
- `IDebugEventSink`: Existing interface on `CustomStrategyBase`

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Auxiliary TF events leaking to outputs | Comprehensive filtering tests; boolean flag on all event sources |
| State machine bugs | Thorough unit tests for all valid/invalid transitions |
| Volume mismatch in protective pairs | Validation at registration time; runtime assertions |
| Performance degradation with multiple groups | Benchmark tests; O(n) operations where n ≤ 5 |
| Breaking existing tests | Run full test suite before PR; maintain backward compatibility |

### 8. Duplicate Signal Detection Criteria

**Decision**: Match by entry price + SL + TP (all three values must match within price step tolerances)

**Rationale**:
- A signal is only truly duplicate if all three price levels match
- Different SL or TP for same entry price represents a legitimately different trading setup
- Enables strategies like ZigZagBreakout to adjust SL/TP while keeping entry price

**Alternatives Considered**:
- Entry price only: Too permissive - would block legitimate different setups at same entry
- Entry price + direction: Doesn't account for SL/TP changes

**Implementation Notes**:
- `FindMatchingGroup(entryPrice, slPrice, tpPrice, tolerance)` replaces `FindSimilarGroup(price, tolerance)`
- All three comparisons use same price step tolerance
- Returns first matching active group or null

### 9. API Reusability Design

**Decision**: All position management components must be strategy-agnostic and reusable

**Rationale**:
- Avoids code duplication across multiple strategy implementations
- Enables consistent order management behavior
- Simplifies testing and maintenance

**Implementation Notes**:
- No strategy-specific code in OrderRegistry, OrderPositionManager, OrderRequest, ProtectivePair
- Generic interfaces for strategy interaction (IStrategyOrderOperations)
- Configuration via constructor parameters or properties, not strategy inheritance

### 10. Deletion-Based Tracking

**Decision**: Registry only contains active orders; closed pairs are deleted, not flagged

**Rationale**:
- Simpler state model - no need to track `IsClosed` flags
- Natural cleanup - dictionary size reflects active state
- Queries automatically return only active items

**Implementation Notes**:
- `RemovePair(pairId)` instead of `MarkPairClosed(pairId)`
- `GetProtectivePairs()` returns all pairs (all are active by definition)
- Group transitions to Closed state when all pairs removed

## Open Questions (Resolved)

All clarifications resolved in spec. No remaining open questions.

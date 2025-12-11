# Bug Report: Position Not Closed Until End of Backtest

**Date:** 2025-12-10
**Status:** Under Investigation
**Affected Component:** `OrderPositionManager.cs`
**Severity:** High

## Summary

A trade opened in July 2020 remained open until the end of the test period (December 2023), blocking all subsequent trades. Despite code changes attempting to fix SL/TP handling, the issue persists.

## Observed Behavior

- Entry order fills correctly
- Position is created (Position > 0)
- SL/TP protection levels are set from the signal
- Position is never closed by SL or TP
- Position blocks new trades (strategy checks `if (Position > 0) return;`)
- Position remains open for ~3.5 years until backtest ends

## Root Cause Analysis

### Primary Bug: Premature Clearing of Protection Levels

The bug is in `CheckProtectionLevelsInternal` (lines 95-120 in `OrderPositionManager.cs`):

```csharp
if (_currentStopLoss.HasValue && candle.LowPrice <= _currentStopLoss.Value)
{
    CloseAllPositions();        // Market order PLACED (async)
    _currentStopLoss = null;     // Cleared IMMEDIATELY - BUG!
    _currentTakeProfit = null;   // Cleared IMMEDIATELY - BUG!
    return true;
}
```

### The Problem Sequence

1. **Candle N**: Entry order fills, creates Position > 0
2. **Same Candle N**: `HandleEntryFill` restores `_currentStopLoss` and `_currentTakeProfit`
3. **Same Candle N**: `CheckProtectionLevelsInternal(_lastCandle)` detects SL hit
4. `CloseAllPositions()` is called → market order **placed but not filled yet**
5. `_currentStopLoss` and `_currentTakeProfit` are **immediately set to null**
6. **Candle N+1**: `CheckProtectionLevels` is called with:
   - `Position` still > 0 (market close hasn't filled)
   - `_currentStopLoss == null && _currentTakeProfit == null`
7. Early-exit condition fires:
   ```csharp
   if (_strategy.Position == 0 || (_currentStopLoss == null && _currentTakeProfit == null))
       return false;
   ```
8. **Position is orphaned** - no protection levels, no way to close

### Why the Market Close Order Fails

In StockSharp backtesting, order execution is asynchronous. The market order placed by `CloseAllPositions()` may:
- Be queued for execution on the next candle
- Fail silently if market conditions prevent fill
- Be processed after the protection levels are already cleared

### Why Most Trades Work

For typical trades, the market order fills quickly within the same processing cycle. The bug manifests only in edge cases:
- Extreme price movements (gaps, crashes)
- Entry and SL/TP hit in the same candle
- Specific timing conditions in the backtest engine

## Code Flow Trace

```
OnProcessCandle(candle)
  └── CheckProtectionLevels(candle)
        └── _lastCandle = candle
        └── if Position == 0 → return false
        └── CheckProtectionLevelsInternal(candle)
              └── if SL hit → CloseAllPositions() + clear levels + return true

OnOwnTradeReceived(trade)  [Entry fills]
  └── HandleEntryFill(trade)
        └── Restore _currentStopLoss, _currentTakeProfit from signal
        └── CheckProtectionLevelsInternal(_lastCandle)
              └── if SL hit → CloseAllPositions() + clear levels
        └── PlaceProtectionOrders(signal)  [if not closed]

OnOwnTradeReceived(trade)  [Close fills]
  └── _pendingCloseOrder matched → Position now 0
```

## Secondary Issues Identified

### 1. Protection Orders Use Wrong Order Type

In `PlaceProtectionOrders` (lines 234-237):

```csharp
if (exitDirection == Sides.Sell)
    slOrder = _strategy.SellLimit(signal.StopLoss.Value, volume);
```

For a LONG position, SL should be a **Sell Stop** order (triggers when price drops), not a **Sell Limit** (triggers when price rises). The limit orders will never fill for their intended purpose.

However, this is mitigated by the manual `CheckProtectionLevels` candle-based checking.

### 2. No Validation of Close Order Success

`CloseAllPositions()` places a market order but doesn't verify it was accepted or track its state beyond storing it in `_pendingCloseOrder`.

## Recommended Fix

### Option A: Don't Clear Levels Until Close Confirmed

Only clear protection levels when the close order fills:

```csharp
// In CheckProtectionLevelsInternal - DON'T clear levels here
if (_currentStopLoss.HasValue && candle.LowPrice <= _currentStopLoss.Value)
{
    CloseAllPositions();
    // Remove: _currentStopLoss = null;
    // Remove: _currentTakeProfit = null;
    return true;
}

// In OnOwnTradeReceived, when close order fills:
if (_pendingCloseOrder != null && order == _pendingCloseOrder)
{
    _pendingCloseOrder = null;
    _currentStopLoss = null;      // Clear here instead
    _currentTakeProfit = null;
    return;
}
```

### Option B: Track Close-Pending State

Add a flag to track that a close is in progress:

```csharp
private bool _closePending;

// In CloseAllPositions:
_closePending = true;

// In CheckProtectionLevels:
if (_closePending) return false;  // Already closing

// In OnOwnTradeReceived (close fill):
_closePending = false;
_currentStopLoss = null;
_currentTakeProfit = null;
```

### Option C: Synchronous Position Check

After calling `CloseAllPositions`, immediately verify position state:

```csharp
CloseAllPositions();
// If position is somehow still open after this, keep protection levels
if (_strategy.Position != 0)
{
    // Don't clear levels - something went wrong
    _strategy.LogWarning("Close order placed but position still open");
}
else
{
    _currentStopLoss = null;
    _currentTakeProfit = null;
}
```

## Test Scenarios to Verify Fix

1. **Same-candle entry and SL**: Entry and SL triggered in single candle
2. **Gap through SL**: Price gaps below SL without touching it
3. **Rapid successive candles**: Multiple candles processed before order fills
4. **Orphan order handling**: Entry fills after position closed

## Related Code Locations

- `OrderPositionManager.cs:80-120` - CheckProtectionLevels
- `OrderPositionManager.cs:122-143` - CloseAllPositions
- `OrderPositionManager.cs:145-184` - OnOwnTradeReceived
- `OrderPositionManager.cs:255-284` - HandleEntryFill
- `ZigZagBreakoutStrategy.cs:104-110` - Protection check call site

## Debug Markers

Existing debug markers in `ZigZagBreakoutStrategy.cs`:
```csharp
// Line 82-83: 2020-04-29 - "Stops from StandardProtection were placed but never triggered"
// Line 86-87: 2020-02-06 - "Stops were not placed here - custom order management was used"
```

These indicate known problematic dates to focus investigation on.

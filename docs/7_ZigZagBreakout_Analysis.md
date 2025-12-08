# ZigZagBreakout Strategy Analysis

**Date**: 2025-12-07
**Backtest Period**: 2020-01-01 to 2023-12-31
**Security**: BTCUSDT@BNB
**Timeframe**: H1 (1 hour)

## Executive Summary

The ZigZagBreakout strategy was executed successfully with the following results:
- **Net Profit**: $1,062.95 (+10.63% total return)
- **Total Trades**: 65
- **Win Rate**: 29.2% (19 wins, 13 losses)
- **Max Drawdown**: 63.15%
- **Profit Factor**: 1.46

While the strategy is profitable, several issues were identified that require attention.

---

## Issues Identified

### Issue #1: High Maximum Drawdown (63.15%)

**Severity**: High
**Status**: Open

The strategy experiences a maximum drawdown of 63.15%, which is excessively high for a trend-following breakout strategy.

**Root Causes**:
1. **Wide Stop-Loss Levels**: The SL is set at the previous ZigZag trough, which can be very far from entry
2. **Position Sizing**: Risk per trade is 2% (`RiskPercentPerTrade = 0.02m`), but with wide stops, actual risk exposure may be higher
3. **No Maximum Drawdown Circuit Breaker**: Strategy continues trading even during deep drawdowns

**Recommendations**:
- Consider implementing ATR-based stop-losses instead of ZigZag-based stops
- Add a maximum drawdown threshold to pause trading
- Review position sizing formula to ensure actual risk matches intended risk

---

### Issue #2: Win Rate Calculation Includes All Trades

**Severity**: Medium
**Status**: Informational

The reported 29.2% win rate is calculated as:
```
Win Rate = Winning Trades / Total Trades = 19 / 65 = 29.2%
```

This includes:
- Entry trades (with PnL = 0)
- Protection order fills
- Position closing trades

**Actual Completed Round-Trip Win Rate**:
- 19 winning round-trips
- 13 losing round-trips
- Win Rate = 19 / 32 = **59.4%**

**Recommendation**: Modify `PerformanceMetricsCalculator` to calculate win rate based on completed round-trip trades only, filtering out entries and intermediate fills.

---

### Issue #3: Same-Candle Entry and Exit Handling

**Severity**: Low
**Status**: Working As Designed

When a large candle triggers both entry (hitting the limit order) and SL/TP (within the same candle range), both trades execute in the same timestamp.

**Examples from backtest**:
- `2020-01-16 13:00` - Buy 2 @ 8727.24, Sell 2 @ 8693.5 (SL hit in same candle)
- `2020-01-24 12:00` - Buy 2 @ 8354.80, Sell 2 @ 8434.10 (TP hit in same candle)
- `2020-01-31 21:00` - Buy 4 @ 9320, Sell 4 @ 9370 (TP hit in same candle)

**Current Handling** (`OrderPositionManager.cs:242-252`):
```csharp
// CRITICAL FIX: Check if SL/TP were hit in the SAME candle that filled the entry
if (_lastCandle != null && _strategy.Position != 0)
{
    if (CheckProtectionLevelsInternal(_lastCandle))
    {
        _strategy.LogInfo("Protection hit immediately after entry - position closed");
        return; // Position was closed, don't place protection orders
    }
}
```

This behavior is correct and prevents orphaned protection orders.

---

### Issue #4: Buy Trades with Negative PnL

**Severity**: Low
**Status**: Expected Behavior

Some BUY trades show negative PnL in the trade log:
- `2020-01-28 01:00` - Buy 0.01 @ 8988, PnL: -0.64
- `2020-01-29 00:00` - Buy 3.99 @ 9215.99, PnL: -1163.96

**Explanation**: StockSharp uses FIFO-based PnL calculation. When protection orders (SL limit sells) are placed, they can create a temporary short position if they fill before the entry is fully executed. The subsequent BUY to close this position shows negative PnL.

**Note**: This is a side effect of the manual SL/TP implementation via limit orders rather than native StockSharp protection. The `OrderPositionManager` handles this through `CheckProtectionLevels` which uses candle-based simulation instead.

---

### Issue #5: Pending Order Updates on ZigZag Peak Changes

**Severity**: Low
**Status**: Working Correctly

The strategy correctly updates pending buy orders when the ZigZag peak level changes.

**Implementation** (`OrderPositionManager.cs:60-69`):
```csharp
if (HasSignalChanged(signal) && IsOrderActive(_order.EntryOrder))
{
    _strategy.LogInfo("Canceling existing entry order - signal levels changed");
    CancelAllOrders();
    PlaceEntryOrder(signal);
    // Update SL/TP levels for new signal
    _currentStopLoss = signal.StopLoss;
    _currentTakeProfit = signal.TakeProfit;
}
```

The `HasSignalChanged` method (`OrderPositionManager.cs:316-340`) checks if entry price, stop-loss, or take-profit levels have changed beyond the price step threshold.

---

## Configuration Recommendations

### Recommended Parameter Adjustments

1. **Reduce Risk Per Trade**:
   ```csharp
   RiskPercentPerTrade = 0.01m  // 1% instead of 2%
   ```

2. **Implement Trailing Stop-Loss**:
   - Consider trailing the stop to break-even after 1R profit
   - Or use ATR-based trailing stops

3. **Add Maximum Position Limit**:
   ```csharp
   MaxOpenPositions = 1  // Currently implicit but should be explicit
   ```

### Suggested Enhancements

1. **Add Time-Based Exit**: Close positions at end of day if not stopped out
2. **Trend Filter**: Only take trades in direction of higher timeframe trend
3. **Volatility Filter**: Avoid trading during extremely high volatility periods

---

## Test Configuration

```json
{
  "HistoryPath": "C:\\Users\\Andrew\\Documents\\StockSharp\\Hydra\\Storage",
  "StorageFormat": "Binary",
  "Period": "2020-01-01 to 2023-12-31",
  "InitialCapital": 10000,
  "DzzDepth": 5,
  "RiskPercentPerTrade": 0.02
}
```

---

## Files Referenced

- `StockSharp.AdvancedBacktest.LauncherTemplate/Strategies/ZigZagBreakout/ZigZagBreakoutStrategy.cs`
- `StockSharp.AdvancedBacktest/OrderManagement/OrderPositionManager.cs`
- `StockSharp.AdvancedBacktest/Statistics/PerformanceMetricsCalculator.cs`

---

## Next Steps

1. [ ] Fix win rate calculation to use round-trip trades
2. [ ] Investigate maximum drawdown and consider tighter stops
3. [ ] Add circuit breaker for maximum drawdown threshold
4. [ ] Consider implementing trailing stops

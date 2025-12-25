# PRD: DeltaZigZag Indicator Port from MQL5

## Overview

Port the MQL5 DeltaZigZag indicator to the StockSharp.AdvancedBacktest platform as a reusable indicator for algorithmic trading strategies.

**Source:** https://www.mql5.com/en/code/1321

## Business Context

### Problem Statement

The trading platform needs a ZigZag-based indicator that:
1. Identifies significant price reversals (peaks and troughs)
2. Uses adaptive thresholds based on recent volatility
3. Provides a foundation for pattern-based trading strategies

### Value Proposition

- **For Strategy Developers:** Reusable indicator for breakout and reversal strategies
- **For Backtesting:** Consistent peak/trough detection across different instruments and timeframes
- **For Visualization:** Clear zigzag line on charts showing market structure

## Functional Requirements

### FR-1: Core ZigZag Calculation

The indicator shall detect price reversals using the following logic:
- During an uptrend: track the highest high as a potential peak
- During a downtrend: track the lowest low as a potential trough
- Confirm a reversal when price moves beyond the threshold from the current extremum

### FR-2: Dynamic Threshold (Delta)

The reversal threshold shall be calculated as a **percentage of the previous swing size**:
- Parameter: `Delta` (e.g., 0.5 = 50% retracement of last swing)
- This adapts the indicator to current market volatility
- Larger swings require larger reversals to confirm

### FR-3: Minimum Threshold Fallback

For the initial swing (when no swing history exists):
- Parameter: `MinimumThreshold` provides an absolute fallback value
- Should be calibrated based on the instrument's price step

### FR-4: Applied Prices

The indicator shall use:
- **High price** for peak detection (tracking maximum during uptrend)
- **Low price** for trough detection (tracking minimum during downtrend)

### FR-5: Output Values

For each confirmed reversal, the indicator shall output:
- **Extremum price:** The peak or trough value
- **Direction:** Whether this is a peak (up) or trough (down)
- **Shift:** Number of bars back to the actual extremum (for proper chart placement)

## Non-Functional Requirements

### NFR-1: Reusability

The indicator shall be placed in the Core assembly to be reusable across:
- Multiple trading strategies
- Backtesting and live trading modes
- Different visualization components

### NFR-2: Integration with S# Ecosystem

The indicator shall:
- Follow S# indicator patterns (BaseIndicator, IIndicatorValue)
- Work with standard candle subscriptions
- Integrate with existing export/visualization pipelines

### NFR-3: Testability

The indicator shall be unit-testable with:
- Synthetic price data for deterministic testing
- Verification of peak/trough detection accuracy
- Shift calculation accuracy

## Architectural Considerations

### Multi-Buffer Architecture

The MQL5 DeltaZigZag uses separate buffers for peaks (`ZzTopBuffer`) and troughs (`ZzBtmBuffer`). This enables:
- Independent tracking of peak and trough candidates
- Potential for separate visualization of peaks vs troughs
- Foundation for derived indicators (DeltaPeaks, DeltaTroughs)

**Current approach:** Single-value output per candle (simpler, sufficient for MVP)

**Future consideration:** Multi-buffer output for scenarios where both peak and trough confirm on the same candle

### Derived Indicators (Future)

Building on the core DeltaZigZag:
- **DeltaPeaks:** Filter to show only peaks (similar to S# Peak indicator)
- **DeltaTroughs:** Filter to show only troughs (similar to S# Trough indicator)
- These provide single-value outputs friendly to charting libraries

## Out of Scope

### N-Level Breakthrough

The original MQL5 indicator supports "trend confirmation via N-level breakthrough" where price must break N previous extremums to confirm a trend change. This is **not included in MVP**.

### Pips Mode

The original supports both percentage-based and absolute (pips) reversal modes. MVP implements **percentage-only**.

### Horizontal Level Extrapolation

Holding peak/trough values until the next extremum appears (for level visualization). **Not included in MVP**.

## Success Criteria

1. Indicator correctly identifies peaks and troughs matching the Delta algorithm
2. Dynamic threshold adapts based on last swing size
3. Shift values correctly place extremums on their original bars
4. Unit tests pass for all core scenarios
5. Existing ZigZagBreakoutStrategy works with the ported indicator

## References

- MQL5 Source: https://www.mql5.com/en/code/viewcode/1321/128697/deltazigzag.mq5
- S# ZigZag: `StockSharp/Algo/Indicators/ZigZag.cs`
- S# Peak/Trough: `StockSharp/Algo/Indicators/Peak.cs`, `Trough.cs`

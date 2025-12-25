# Research: DeltaZigZag Indicator Port

**Feature**: 001-delta-zigzag-indicators
**Date**: 2025-12-25

## Design Decisions

### D1: Indicator Base Class Pattern

**Decision**: Extend `BaseIndicator` directly (not `ZigZag`)

**Rationale**:
- The existing S# `ZigZag` uses a fixed percentage threshold (`Deviation`) applied to the current extremum price
- DeltaZigZag requires a dynamic threshold based on the previous swing size, which is fundamentally different logic
- Extending `ZigZag` would require overriding most of its calculation logic, defeating the purpose of inheritance
- Composition over inheritance: DeltaZigZag can reuse `ZigZagIndicatorValue` for output compatibility

**Alternatives considered**:
- Extend `ZigZag` and override `CalcZigZag()` - Rejected: would still use parent's state management incorrectly
- Create entirely new value type - Rejected: unnecessary when `ZigZagIndicatorValue` already provides IsUp, shift, and value

### D2: Dynamic Threshold Calculation

**Decision**: Threshold = `Delta * lastSwingSize` where `lastSwingSize = |lastPeak - lastTrough|`

**Rationale**:
- This matches the MQL5 DeltaZigZag algorithm where larger swings require larger reversals to confirm
- Adapts automatically to market volatility without manual parameter adjustment per instrument
- The `MinimumThreshold` provides a floor for the initial swing when no history exists

**Alternatives considered**:
- ATR-based threshold - Rejected: requires additional indicator dependency, MQL5 source uses swing-based
- Fixed percentage like standard S# ZigZag - Rejected: doesn't adapt to volatility changes

### D3: Derived Indicators via Composition

**Decision**: `DeltaZzPeak` and `DeltaZzTrough` wrap `DeltaZigZag` internally

**Rationale**:
- Follows the exact pattern of S# `Peak` and `Trough` which extend `ZigZag`
- However, we use composition instead of inheritance to align with Constitution Principle IV
- Each derived indicator creates its own `DeltaZigZag` instance and filters output by `IsUp` property

**Alternatives considered**:
- Extend `DeltaZigZag` class - Rejected: would require making internal state protected, violates encapsulation
- Single indicator with output mode parameter - Rejected: existing S# pattern separates Peak/Trough into distinct classes

### D4: Initial Trend Direction Algorithm

**Decision**: Derive from first candle structure per spec clarification

**Rationale**:
- Per clarification session: uptrend if `close > open`, downtrend if `close < open`
- Doji tie-breaker: uptrend if `(high - open) > (open - low)`, downtrend otherwise
- This provides a deterministic starting point without waiting for MinimumThreshold breach

**Alternatives considered**:
- Wait for first MinimumThreshold move - Rejected: delays indicator formation unnecessarily
- Always start uptrend - Rejected: arbitrary and loses information from first candle

### D5: Output Value Type

**Decision**: Reuse `ZigZagIndicatorValue` from StockSharp

**Rationale**:
- Already provides `IsUp`, `Value` (extremum price), `Shift`, and `Time` properties
- Ensures compatibility with existing export and visualization pipelines
- `ShiftedIndicatorValue` base class enables proper chart placement

**Alternatives considered**:
- Create `DeltaZigZagIndicatorValue` - Rejected: no additional data needed beyond what ZigZagIndicatorValue provides
- Return simple `DecimalIndicatorValue` - Rejected: loses IsUp and Shift information critical for charting

### D6: State Management

**Decision**: Track `lastPeakPrice`, `lastTroughPrice`, `lastSwingSize`, `isUpTrend`, `currentExtremum`, `shift`

**Rationale**:
- `lastSwingSize` enables dynamic threshold calculation
- Must track both last peak and trough to compute swing size on reversal
- `currentExtremum` tracks the candidate peak/trough being formed
- `shift` counts bars since extremum was set (for chart placement)

**Alternatives considered**:
- Circular buffer approach like S# ZigZag - Rejected: DeltaZigZag needs more state (swing history)
- Store full swing history - Rejected: only last swing needed for threshold calculation (MVP scope)

## Integration Patterns

### Candle Input

The indicator accepts `CandleIndicatorValue` (via `[IndicatorIn]` attribute):
- Uses `HighPrice` for peak detection during uptrends
- Uses `LowPrice` for trough detection during downtrends
- Uses `OpenPrice` and `ClosePrice` for initial direction determination

### Pipeline Compatibility

Indicators will integrate with existing pipelines:
- `IndicatorExporter` for JSON export
- Web visualization charts
- Strategy subscription via `indicator.Process(candle)`

### Testing Strategy

- Create synthetic candle sequences with known peaks/troughs
- Verify shift values correctly point to extremum bars
- Test edge cases: Delta=0, Delta=1.0, doji candles, price gaps
- Compare output format compatibility with existing ZigZag consumers

## References

- S# ZigZag implementation: `StockSharp/Algo/Indicators/ZigZag.cs`
- S# Peak implementation: `StockSharp/Algo/Indicators/Peak.cs`
- S# Trough implementation: `StockSharp/Algo/Indicators/Trough.cs`
- MQL5 DeltaZigZag source: https://www.mql5.com/en/code/1321

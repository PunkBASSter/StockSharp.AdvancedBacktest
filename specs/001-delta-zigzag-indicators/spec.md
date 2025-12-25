# Feature Specification: DeltaZigZag Indicator Port with Derived Indicators

**Feature Branch**: `001-delta-zigzag-indicators`
**Created**: 2025-12-25
**Status**: Draft
**Input**: User description: "Port DeltaZigZag indicator from MQL5 with derived DeltaZzPeak and DeltaZzTrough indicators for frontend-friendly visualization"

## Clarifications

### Session 2025-12-25

- Q: How does the indicator determine the initial trend direction when starting from cold (no history)? → A: Derive from first candle: uptrend if close > open, downtrend otherwise.
- Q: When the first candle has close == open (doji), how should initial trend direction be determined? → A: Compare high vs low: uptrend if high-open > open-low, downtrend otherwise.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Core DeltaZigZag Indicator for Strategy Development (Priority: P1)

As a strategy developer, I need a ZigZag indicator that detects price reversals using dynamic thresholds based on recent volatility, so that I can build breakout and reversal trading strategies.

**Why this priority**: This is the foundation indicator. Without the core DeltaZigZag, the derived indicators cannot be built. It enables pattern-based trading strategies and provides consistent peak/trough detection across instruments.

**Independent Test**: Can be fully tested by feeding synthetic candle data and verifying that peaks and troughs are detected at correct price levels with accurate bar shifts.

**Acceptance Scenarios**:

1. **Given** an uptrend where price makes a higher high, **When** price retraces by more than the dynamic threshold (Delta percentage of last swing), **Then** the indicator outputs a peak at the previous high price with the correct bar shift.

2. **Given** a downtrend where price makes a lower low, **When** price rallies by more than the dynamic threshold (Delta percentage of last swing), **Then** the indicator outputs a trough at the previous low price with the correct bar shift.

3. **Given** no previous swing exists (initial state), **When** price moves beyond the MinimumThreshold from the starting point, **Then** the indicator uses the MinimumThreshold as the fallback reversal requirement.

4. **Given** the indicator is processing candles, **When** a reversal is confirmed, **Then** the output includes the extremum price, direction (peak/trough), and bar shift for proper chart placement.

---

### User Story 2 - DeltaZzPeak Indicator for Frontend Visualization (Priority: P2)

As a frontend developer displaying trading charts, I need an indicator that outputs only peaks (one value per timestamp, no double values), so that I can render clean visualizations without handling multi-buffer edge cases.

**Why this priority**: Frontend visualization is a key value proposition. DeltaZzPeak filters the core indicator to show only peaks, following the same pattern as the existing S# Peak indicator.

**Independent Test**: Can be fully tested by feeding the same synthetic data as the core indicator and verifying that only peak values are output, with empty/null values for non-peak candles.

**Acceptance Scenarios**:

1. **Given** the core DeltaZigZag indicator outputs a peak, **When** DeltaZzPeak processes the same candle, **Then** it outputs the peak value.

2. **Given** the core DeltaZigZag indicator outputs a trough, **When** DeltaZzPeak processes the same candle, **Then** it outputs an empty/null value (no value).

3. **Given** multiple candles in a row, **When** DeltaZzPeak processes them, **Then** each timestamp has at most one value (never double values on same timestamp).

---

### User Story 3 - DeltaZzTrough Indicator for Frontend Visualization (Priority: P2)

As a frontend developer displaying trading charts, I need an indicator that outputs only troughs (one value per timestamp, no double values), so that I can render clean visualizations without handling multi-buffer edge cases.

**Why this priority**: Same priority as DeltaZzPeak; together they provide the complete filtered view of the DeltaZigZag for charting libraries.

**Independent Test**: Can be fully tested by feeding the same synthetic data as the core indicator and verifying that only trough values are output.

**Acceptance Scenarios**:

1. **Given** the core DeltaZigZag indicator outputs a trough, **When** DeltaZzTrough processes the same candle, **Then** it outputs the trough value.

2. **Given** the core DeltaZigZag indicator outputs a peak, **When** DeltaZzTrough processes the same candle, **Then** it outputs an empty/null value (no value).

3. **Given** multiple candles in a row, **When** DeltaZzTrough processes them, **Then** each timestamp has at most one value (never double values on same timestamp).

---

### User Story 4 - Integration with Existing Strategies (Priority: P3)

As a strategy developer with existing ZigZag-based strategies, I need the DeltaZigZag indicator to work as a drop-in replacement for strategy backtesting, so that I can test strategies with adaptive volatility-based thresholds.

**Why this priority**: Enables existing ZigZagBreakoutStrategy to use the new indicator without major modifications.

**Independent Test**: Can be tested by running an existing ZigZag-based strategy with the new DeltaZigZag indicator and verifying trade signals are generated correctly.

**Acceptance Scenarios**:

1. **Given** an existing strategy that uses ZigZag peak/trough signals, **When** the strategy subscribes to DeltaZigZag outputs, **Then** the strategy receives reversal signals in the expected format.

2. **Given** the DeltaZigZag is used in backtesting mode, **When** historical candles are processed, **Then** the indicator produces consistent results across multiple backtest runs with the same parameters.

---

### Edge Cases

- What happens when Delta is set to 0? The indicator should use MinimumThreshold exclusively.
- What happens when Delta is set to 1.0 (100%)? Reversals require a full retracement of the previous swing.
- What happens when price gaps through the threshold? The reversal should still be detected on the first candle that exceeds the threshold.
- How does the indicator handle the first few candles before any swing is established? It uses MinimumThreshold until the first reversal is confirmed.
- What happens when high and low prices are equal (doji candle)? The indicator should continue tracking the current trend direction.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST detect price reversals by tracking highest highs during uptrends and lowest lows during downtrends.
- **FR-002**: System MUST confirm a reversal when price moves beyond the dynamic threshold from the current extremum.
- **FR-003**: System MUST calculate the dynamic threshold as a percentage (Delta parameter) of the previous swing size.
- **FR-004**: System MUST use a MinimumThreshold parameter as a fallback when no swing history exists.
- **FR-005**: System MUST use high prices for peak detection and low prices for trough detection.
- **FR-006**: System MUST output for each confirmed reversal: the extremum price, direction (peak or trough), and bar shift (number of bars back to the actual extremum).
- **FR-007**: DeltaZzPeak indicator MUST filter the core DeltaZigZag to output only peak values, returning empty values for trough candles.
- **FR-008**: DeltaZzTrough indicator MUST filter the core DeltaZigZag to output only trough values, returning empty values for peak candles.
- **FR-009**: Both derived indicators MUST produce at most one value per timestamp (no double values on same candle).
- **FR-010**: All three indicators MUST be reusable across multiple trading strategies and work in both backtesting and live trading modes.
- **FR-011**: System MUST determine initial trend direction from the first candle: uptrend if close > open, downtrend if close < open. When close == open (doji), use high/low comparison: uptrend if (high - open) > (open - low), downtrend otherwise.

### Key Entities

- **DeltaZigZag**: Core indicator that detects price reversals using dynamic volatility-based thresholds. Key attributes: Delta (percentage), MinimumThreshold (absolute), current trend direction, current extremum tracking.
- **DeltaZzPeak**: Derived indicator filtering DeltaZigZag to output only peaks. Wraps DeltaZigZag and filters by direction.
- **DeltaZzTrough**: Derived indicator filtering DeltaZigZag to output only troughs. Wraps DeltaZigZag and filters by direction.
- **Reversal Output**: Data structure containing extremum price, direction (peak/trough), and bar shift.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: DeltaZigZag correctly identifies peaks and troughs matching the Delta algorithm with 100% accuracy on synthetic test datasets.
- **SC-002**: Bar shift values correctly place extremums on their original bars in all test scenarios.
- **SC-003**: DeltaZzPeak and DeltaZzTrough produce exactly one value per timestamp, with zero instances of double values on the same candle.
- **SC-004**: All three indicators integrate seamlessly with existing export and visualization pipelines without requiring pipeline modifications.
- **SC-005**: Existing ZigZagBreakoutStrategy can use DeltaZigZag without code changes beyond indicator substitution.
- **SC-006**: All unit tests pass for core indicator logic, derived indicator filtering, and edge cases.

## Assumptions

- The Delta parameter represents a percentage (0.0 to 1.0 range, where 0.5 = 50% retracement).
- MinimumThreshold is an absolute price value, not a percentage.
- The existing S# Peak and Trough indicators serve as the reference pattern for DeltaZzPeak and DeltaZzTrough implementation.
- Single-value output per candle is sufficient for MVP (multi-buffer output for simultaneous peak/trough is out of scope).

## Out of Scope

- **N-Level Breakthrough**: Trend confirmation via breaking N previous extremums.
- **Pips Mode**: Absolute (pips) reversal mode; MVP implements percentage-only.
- **Horizontal Level Extrapolation**: Holding peak/trough values until next extremum appears.

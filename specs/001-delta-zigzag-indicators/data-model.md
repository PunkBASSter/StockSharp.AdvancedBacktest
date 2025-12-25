# Data Model: DeltaZigZag Indicators

**Feature**: 001-delta-zigzag-indicators
**Date**: 2025-12-25

## Entities

### DeltaZigZag

Core indicator that detects price reversals using dynamic volatility-based thresholds.

**Properties (Parameters)**:

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Delta | decimal | 0.0 to 1.0 | Percentage of last swing size for reversal threshold |
| MinimumThreshold | decimal | > 0 | Absolute fallback threshold when no swing history exists |

**Internal State**:

| Field | Type | Description |
|-------|------|-------------|
| _isUpTrend | bool? | Current trend direction (null = not initialized) |
| _currentExtremum | decimal? | Current peak/trough being tracked |
| _lastPeakPrice | decimal? | Price of most recent confirmed peak |
| _lastTroughPrice | decimal? | Price of most recent confirmed trough |
| _lastSwingSize | decimal? | Size of last completed swing (|peak - trough|) |
| _shift | int | Bars since current extremum was set |

**Lifecycle**:

```
[Uninitialized] → (first candle) → [Trending]
     ↓                                  ↓
   Reset()                    (reversal confirmed)
     ↓                                  ↓
[Uninitialized]              [Output ZigZagIndicatorValue]
                                        ↓
                             (continue tracking new direction)
```

**State Transitions**:

1. **Uninitialized → Trending**: First candle processed, initial direction determined from close/open
2. **Trending (Up) → Peak Confirmed**: Low price drops below (currentExtremum - threshold)
3. **Trending (Down) → Trough Confirmed**: High price rises above (currentExtremum + threshold)
4. **Reset**: All state cleared, returns to Uninitialized

### DeltaZzPeak

Derived indicator filtering DeltaZigZag to output only peaks.

**Properties**:

| Field | Type | Description |
|-------|------|-------------|
| Delta | decimal | Delegated to internal DeltaZigZag |
| MinimumThreshold | decimal | Delegated to internal DeltaZigZag |

**Internal State**:

| Field | Type | Description |
|-------|------|-------------|
| _deltaZigZag | DeltaZigZag | Internal indicator instance |

**Behavior**:
- Processes candle through internal DeltaZigZag
- If result `IsUp == true` (peak), returns the value
- Otherwise, returns empty ZigZagIndicatorValue

### DeltaZzTrough

Derived indicator filtering DeltaZigZag to output only troughs.

**Properties**:

| Field | Type | Description |
|-------|------|-------------|
| Delta | decimal | Delegated to internal DeltaZigZag |
| MinimumThreshold | decimal | Delegated to internal DeltaZigZag |

**Internal State**:

| Field | Type | Description |
|-------|------|-------------|
| _deltaZigZag | DeltaZigZag | Internal indicator instance |

**Behavior**:
- Processes candle through internal DeltaZigZag
- If result `IsUp == false` (trough) and not empty, returns the value
- Otherwise, returns empty ZigZagIndicatorValue

## Output Value

Uses existing `ZigZagIndicatorValue` from StockSharp:

| Field | Type | Description |
|-------|------|-------------|
| Value | decimal | Extremum price (peak or trough) |
| Shift | int | Bars back to actual extremum |
| Time | DateTime | Timestamp of the candle being processed |
| IsUp | bool | True = peak, False = trough |
| IsEmpty | bool | True when no reversal detected on this candle |

## Validation Rules

| Rule | Entity | Field | Constraint |
|------|--------|-------|------------|
| VR-001 | DeltaZigZag | Delta | Must be >= 0 and <= 1.0 |
| VR-002 | DeltaZigZag | MinimumThreshold | Must be > 0 |
| VR-003 | All | Input | Must be CandleIndicatorValue |

## Relationships

```
┌─────────────────┐
│  DeltaZigZag    │
│  (Core)         │
└────────┬────────┘
         │ composes
    ┌────┴────┐
    ▼         ▼
┌────────┐ ┌──────────┐
│DeltaZz │ │DeltaZz   │
│Peak    │ │Trough    │
└────────┘ └──────────┘
    │           │
    └─────┬─────┘
          ▼
┌─────────────────────┐
│ ZigZagIndicatorValue│
│ (S# existing type)  │
└─────────────────────┘
```

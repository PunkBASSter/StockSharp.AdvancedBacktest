# Internal API Contract: Debug Mode

**Feature**: 004-isolated-order-groups
**Assembly**: StockSharp.AdvancedBacktest.Infrastructure

## DebugModeProvider API

### Initialize

Initializes the debug mode provider for a strategy.

```csharp
void Initialize(CustomStrategyBase strategy, TimeSpan mainTimeframe)
```

**Parameters**:
- `strategy`: The strategy to attach debug mode to
- `mainTimeframe`: The main candle timeframe (e.g., 1 hour) for timestamp remapping

**Behavior**:
- Creates output instances based on enabled flags
- Stores main timeframe for timestamp remapping
- Hooks into strategy events if needed

---

### CaptureEvent

Captures a debug event with auxiliary TF filtering.

```csharp
void CaptureEvent(object eventData, DateTimeOffset timestamp, bool isAuxiliaryTimeframe)
```

**Parameters**:
- `eventData`: The event data to capture (candle, indicator, trade, etc.)
- `timestamp`: Actual timestamp of the event
- `isAuxiliaryTimeframe`: True if event originated from auxiliary TF

**Behavior**:
- If `isAuxiliaryTimeframe == true`: Event is discarded (not captured)
- If `isAuxiliaryTimeframe == false`:
  - Timestamp is remapped to main TF boundary if needed
  - Event is written to all enabled outputs

**Example**:
```csharp
// Main TF candle - captured
provider.CaptureEvent(candleData, candle.OpenTime, isAuxiliaryTimeframe: false);

// Auxiliary TF event - filtered out
provider.CaptureEvent(slTriggerEvent, DateTimeOffset.Now, isAuxiliaryTimeframe: true);

// SL trigger from auxiliary TF that should appear under main TF candle
var displayTime = TimestampRemapper.RemapToMainTimeframe(actualTime, mainTimeframe);
provider.CaptureEvent(tradeData, displayTime, isAuxiliaryTimeframe: false);
```

---

### CaptureCandle

Convenience method for capturing candle events.

```csharp
void CaptureCandle(ICandleMessage candle, SecurityId securityId, bool isAuxiliaryTimeframe)
```

**Parameters**:
- `candle`: StockSharp candle message
- `securityId`: Security identifier
- `isAuxiliaryTimeframe`: True if from auxiliary subscription

**Behavior**:
- Auxiliary TF candles are always filtered out
- Main TF candles are captured to enabled outputs

---

### CaptureIndicator

Captures indicator value updates.

```csharp
void CaptureIndicator(string indicatorName, IndicatorDataPoint dataPoint, bool isAuxiliaryTimeframe)
```

**Behavior**: Same filtering rules as CaptureEvent

---

### CaptureTrade

Captures trade execution events.

```csharp
void CaptureTrade(TradeDataPoint trade, DateTimeOffset actualTime, bool isAuxiliaryTimeframe)
```

**Parameters**:
- `trade`: Trade data point
- `actualTime`: Actual execution time
- `isAuxiliaryTimeframe`: True if triggered by auxiliary TF check

**Behavior**:
- Trade is captured with remapped timestamp
- Even if triggered by auxiliary TF, trade events are NOT filtered
- Timestamp is remapped to parent main TF candle

**Important**: Trade events triggered by auxiliary TF checks are special:
- `isAuxiliaryTimeframe` should be `false` for the trade itself
- The trade's display timestamp should be remapped to main TF
- This ensures trades appear under the correct candle while preserving execution

---

### Cleanup

Performs cleanup and flushes remaining events.

```csharp
void Cleanup()
```

**Behavior**:
- Flushes all pending events
- Disposes output instances
- Should be called when strategy stops

---

## IDebugModeOutput API

Interface for debug output implementations.

### Initialize

```csharp
void Initialize(CustomStrategyBase strategy)
```

### Write

```csharp
void Write(object eventData, DateTimeOffset displayTimestamp)
```

**Parameters**:
- `eventData`: Event data to write
- `displayTimestamp`: Timestamp to display (already remapped if needed)

### Flush

```csharp
void Flush()
```

---

## TimestampRemapper API

### RemapToMainTimeframe

Maps a timestamp to its parent main timeframe candle boundary.

```csharp
static DateTimeOffset RemapToMainTimeframe(DateTimeOffset eventTime, TimeSpan mainTimeframe)
```

**Parameters**:
- `eventTime`: Actual event timestamp
- `mainTimeframe`: Main candle timeframe (e.g., 1 hour)

**Returns**: Floor of event time to nearest main TF boundary

**Examples**:
```csharp
var mainTF = TimeSpan.FromHours(1);

// 1:15 → 1:00
TimestampRemapper.RemapToMainTimeframe(new DateTimeOffset(2020, 1, 1, 1, 15, 0, TimeSpan.Zero), mainTF)
// Returns: 2020-01-01 01:00:00

// 2:59 → 2:00
TimestampRemapper.RemapToMainTimeframe(new DateTimeOffset(2020, 1, 1, 2, 59, 0, TimeSpan.Zero), mainTF)
// Returns: 2020-01-01 02:00:00

// Exact boundary unchanged
TimestampRemapper.RemapToMainTimeframe(new DateTimeOffset(2020, 1, 1, 3, 0, 0, TimeSpan.Zero), mainTF)
// Returns: 2020-01-01 03:00:00
```

---

## Configuration Properties

### DebugModeProvider Properties

```csharp
public bool IsHumanDebugEnabled { get; set; }  // Default: false
public bool IsAiDebugEnabled { get; set; }     // Default: false
public TimeSpan MainTimeframe { get; set; }    // Set during Initialize
```

**Usage**:
```csharp
var provider = new DebugModeProvider
{
    IsHumanDebugEnabled = true,
    IsAiDebugEnabled = true  // Both can be enabled simultaneously
};
provider.Initialize(strategy, TimeSpan.FromHours(1));
```

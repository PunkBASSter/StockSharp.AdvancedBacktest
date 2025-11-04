# Debug Mode

## Overview

The Debug Mode feature enables real-time visualization of strategy execution during backtesting. Instead of waiting for backtest completion to view charts, developers can watch candles, indicators, and trades update live as the strategy processes historical data.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Strategy Execution                        │
│  (CustomStrategyBase with DebugExporter)                    │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Events: OnCandle, OnIndicator, OnTrade
                 ▼
┌─────────────────────────────────────────────────────────────┐
│              DebugModeExporter                               │
│  - Captures strategy events                                  │
│  - Time-based buffering (500ms window)                      │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│              DebugEventBuffer                                │
│  - Time-based buffering (500ms default)                     │
│  - Thread-safe event collection                             │
│  - Immediate flush on disposal                              │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│              FileBasedWriter                                 │
│  - Writes JSONL to disk                                      │
│  - File rotation at 10MB                                     │
│  - Concurrent read support                                   │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
        ┌────────────────┐            ┌──────────────────┐
        │ candles.jsonl  │            │ indicators.jsonl │
        │ trades.jsonl   │            │ state.jsonl      │
        └────────┬───────┘            └────────┬─────────┘
                 │                             │
                 │ Polling (500ms)             │
                 │                             │
                 ▼                             ▼
        ┌─────────────────────────────────────────────┐
        │         Browser (Next.js App)               │
        │  - Polls files via fetch()                  │
        │  - Parses JSONL incrementally               │
        │  - Updates Lightweight Charts               │
        └─────────────────────────────────────────────┘
```

## Components

### DebugEventBuffer

Time-based event buffering system that collects events during strategy execution and flushes them periodically.

**Key Features:**
- **Time-based flushing**: Flushes every 500ms (configurable)
- **Thread-safe**: Safe for concurrent strategy execution
- **Immediate flush**: On disposal or manual trigger
- **Event grouping**: Events organized by type (candles, indicators, trades, state)

**Usage:**
```csharp
var buffer = new DebugEventBuffer(flushIntervalMs: 500);

buffer.OnFlush += events =>
{
    // Handle flushed events
    foreach (var (eventType, eventList) in events)
    {
        Console.WriteLine($"Flushed {eventList.Count} events of type {eventType}");
    }
};

// Add events
buffer.Add("candle", candleDataPoint);
buffer.Add("trade", tradeDataPoint);

// Manual flush (optional)
buffer.Flush();

// Cleanup (triggers final flush)
buffer.Dispose();
```

### DebugModeExporter

Main orchestrator that captures strategy events and coordinates export.

**Features:**
- Composition-based design (attach to any CustomStrategyBase)
- Subscribes to indicator.Changed events
- Captures trade executions
- Manages sequence numbering

### FileBasedWriter

JSONL file writer with concurrent read support.

**Features:**
- Append-only JSONL format
- File rotation at 10MB threshold
- Windows file sharing support (FileShare.Read)
- Async I/O for performance

## Configuration

Debug mode is configured at strategy launch:

```csharp
var strategy = new MyStrategy();

// Optional: Enable debug mode
if (enableDebugMode)
{
    var debugExporter = new DebugModeExporter(
        outputPath: "debug-output",
        flushIntervalMs: 500
    );

    strategy.DebugExporter = debugExporter;
}

// Run strategy
strategy.Start();
```

## Data Format

### JSONL (JSON Lines)

All debug data is exported in JSONL format - one JSON object per line:

```jsonl
{"time":1729555200,"open":50000.0,"high":50100.0,"low":49950.0,"close":50050.0,"volume":1250.5,"sequenceNumber":1,"securityId":"BTCUSDT@BINANCE"}
{"time":1729555260,"open":50050.0,"high":50150.0,"low":50000.0,"close":50100.0,"volume":1300.2,"sequenceNumber":2,"securityId":"BTCUSDT@BINANCE"}
```

### File Organization

```
debug-output/
├── candles.jsonl              # All candle updates
├── indicators_SMA_20.jsonl    # Per-indicator files
├── indicators_RSI_14.jsonl
├── trades.jsonl               # All trade executions
└── state.jsonl                # Strategy state updates
```

## Performance Considerations

### Buffer Performance
- **Add operation**: <1μs per event
- **Flush operation**: <10ms for 1000 events
- **Memory overhead**: <10MB for typical buffering

### Strategy Impact
- Target: <5% slowdown with debug mode enabled
- Time-based buffering minimizes strategy thread blocking
- Async event firing prevents I/O blocking

### Breakpoint Debugging
When debugging with breakpoints:
- Timer continues to run
- Events accumulate in buffer
- Automatic flush every 500ms (even when paused)
- Manual flush on disposal

## Design Principles

1. **Vertical Slicing**: All debug mode logic in `DebugMode/` namespace
2. **Reuse Export Infrastructure**: Extend existing ChartDataModels
3. **Composition over Inheritance**: DebugModeExporter as property, not base class
4. **Minimal Surface Area**: Only DebugExporter property added to CustomStrategyBase
5. **Backward Compatibility**: All debug fields are nullable

## Testing

Run unit tests:
```bash
dotnet test --filter "FullyQualifiedName~DebugMode"
```

Target coverage: >80%

## Related Documents

- [5_TRD_DebugMode.md](../../docs/5_TRD_DebugMode.md) - Technical Requirements Document
- [DM-01 Task](../../../Docs/DebugMode/DM-01%20Extend%20Export%20Infrastructure.md) - Implementation task
- [ChartDataModels.cs](../Export/ChartDataModels.cs) - Extended data models

## Future Enhancements

**Phase 2** (Future):
- WebSocket-based real-time streaming
- SignalR server integration
- Sub-100ms latency
- Bidirectional communication (pause/resume from UI)

**Phase 3** (Future):
- Historical replay mode
- Step-through debugging from UI
- State snapshot/restore

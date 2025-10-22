# StockSharp Advanced Backtesting Library - Debug Mode Technical Requirements Document (TRD)

**Version**: 1.0
**Date**: 2025-10-21
**Status**: Design Phase
**Target Framework**: .NET 10
**Related PRDs**: 1_PRD_MVP.md, 3_PRD_LauncherTemplate.md

---

## Executive Summary

### Purpose

Enable real-time visualization of strategy execution during backtesting for debugging and development purposes. Instead of waiting for backtest completion to view charts, developers can watch candles, indicators, and trades update live as the strategy processes historical data.

### Approach: File-Based MVP

**MVP**: File-based incremental export with browser polling (1 week)

This approach provides:

- ✅ Quick proof-of-concept with minimal dependencies
- ✅ Works with existing static HTML report infrastructure
- ✅ Simple, debuggable implementation
- ✅ No server infrastructure required

### Key Deliverables

1. **Real-time data export** from C# strategy execution
2. **Incremental file writing** (JSONL format for append-only)
3. **Live chart updates** in browser using Lightweight Charts v4
4. **Debug UI** with connection status
5. **Reuse existing Export infrastructure** (extend ChartDataModels, IndicatorExporter, ReportBuilder)

---

## 1. Requirements Analysis

### 1.1 Functional Requirements

#### FR-1: Debug Mode Toggle

- **FR-1.1**: Add `DebugModeExporter` via composition to `CustomStrategyBase`
- **FR-1.2**: Debug mode should be opt-in (default: false)
- **FR-1.3**: Debug mode should not affect strategy execution logic

#### FR-2: Real-Time Data Capture

- **FR-2.1**: Capture candle data as strategy processes each bar
- **FR-2.2**: Capture indicator values immediately after calculation
- **FR-2.3**: Capture trade execution events (entry, exit, rejection)
- **FR-2.4**: Capture strategy state changes (position, PnL)
- **FR-2.5**: Include timestamps for all events

#### FR-3: Incremental Data Export

- **FR-3.1**: Write data to append-only JSONL files (one JSON per line)
- **FR-3.2**: Support concurrent read/write (browser + strategy)
- **FR-3.3**: Maintain export performance (<1ms per update)

#### FR-4: Live Chart Updates

- **FR-4.1**: Poll data files every 500ms (Phase 1)
- **FR-4.2**: Update charts incrementally using `series.update()`

### 1.2 Non-Functional Requirements

#### NFR-1: Performance

- Strategy execution should not be slowed by >5% when debug mode enabled
- File writes should be time-buffered (default: 500ms window to match polling interval)
- Buffer flushes immediately when breakpoint hit or strategy pauses
- Memory overhead <100MB for typical backtest

#### NFR-2: Reliability

- Handle file locking gracefully on Windows

#### NFR-3: Usability

- Clear visual indicator when debug mode is active
- Show real-time progress (optional)

#### NFR-4: Compatibility

- Work with existing `ReportBuilder` infrastructure
- Support all indicator types (simple, complex, composite)
- Compatible with walk-forward optimization
- No breaking changes to existing strategies

---

## 2. Architecture Design

### 2.1 System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Strategy Execution                        │
│  (CustomStrategyBase with DebugMode=true)                   │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Events: OnCandle, OnIndicator, OnTrade
                 ▼
┌─────────────────────────────────────────────────────────────┐
│              RealtimeDataExporter                            │
│  - Time-based buffering (500ms window)                      │
│  - Writes JSONL to disk                                      │
│  - Immediate flush on breakpoint/pause                      │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ├─── Phase 1: File System ─────┐
                 │                               │
                 ▼                               ▼
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

### 2.2 Data Flow Diagram

```text
Strategy Processing:
[Candle Received] → [Indicators Calculate] → [Trading Logic] → [Order Execution]
      ↓                      ↓                      ↓                  ↓
  [Export Event]        [Export Event]         [Export Event]    [Export Event]
      ↓                      ↓                      ↓                  ↓
                    [RealtimeDataExporter Buffer]
                                 ↓
                        [Time-Based Flush (500ms)]
                                 ↓
                         [File System Write]
                                 ↓
                           [JSONL Files]
                                 ↓
                          [Browser Poll (500ms)]
                                 ↓
                    [Parse & Validate Update]
                                 ↓
                    [Chart Update Animation]
```

### 2.3 Component Architecture

#### C# Backend Components (Vertical Slicing)

```text
StockSharp.AdvancedBacktest/
├── Export/
│   ├── ChartDataModels.cs              (EXTEND: add debug event models)
│   ├── IIndicatorExporter.cs           (REUSE as-is)
│   ├── IndicatorExporter.cs            (REUSE as-is)
│   └── ReportBuilder.cs                (EXTEND: add debug mode support)
├── Strategies/
│   └── CustomStrategyBase.cs           (MODIFY: add DebugModeExporter property)
└── DebugMode/                          (NEW: vertical slice for debug mode)
    ├── DebugModeExporter.cs            Main exporter class (encapsulates debug logic)
    ├── DebugEventBuffer.cs             Time-based event buffering
    ├── FileBasedWriter.cs              JSONL file writer
    ├── IndicatorInterception.cs        Strategy for capturing indicator values
    └── README.md                       Debug mode documentation
```

**Design Principles:**

- **Vertical Slicing**: All debug mode logic in `DebugMode/` folder
- **Reuse Export Infrastructure**: Extend existing models instead of duplicating
- **Composition over Inheritance**: Use `DebugModeExporter` as property, not base class
- **Minimal Surface Area**: Only add `DebugModeExporter` property to `CustomStrategyBase`

#### Frontend Components (Simplified Organization)

```text
StockSharp.AdvancedBacktest.Web/
├── components/
│   ├── charts/
│   │   ├── CandlestickChart.tsx        (EXTEND: add debugMode prop)
│   │   └── ChartContainer.tsx          (EXTEND: add debugMode prop)
│   └── debug/                          (NEW: all debug-specific components)
│       ├── DebugControls.tsx           Connection status, pause/resume
│       ├── DebugToggle.tsx             Enable/disable debug mode
│       └── DebugStats.tsx              Statistics display
├── hooks/
│   └── debug/                          (NEW: debug-specific hooks)
│       ├── useRealtimeUpdates.ts       Main hook for file polling
│       ├── useFilePolling.ts           File polling logic
│       └── useChartUpdater.ts          Throttled chart updates
└── lib/
    └── debug/                          (NEW: debug utilities)
        ├── file-poller.ts              JSONL file polling
        ├── event-parser.ts             Parse debug events
        └── update-buffer.ts            Buffer & throttle updates
```

---

## 3. Detailed Component Specifications

### 3.1 CustomStrategyBase Extensions (Composition Pattern)

#### Current State

```csharp
public abstract class CustomStrategyBase : Strategy, IIndicatorExportable
{
    public string Hash => $"{GetType().Name}V{Version}_{SecuritiesHash}_{ParamsHash}";
    public PerformanceMetrics? PerformanceMetrics { get; protected set; }
    public IIndicatorExporter? IndicatorExporter { get; set; }

    public virtual List<IndicatorDataSeries> GetIndicatorSeries() { /* ... */ }
}
```

#### Proposed Changes (Minimal Surface Area)

```csharp
public abstract class CustomStrategyBase : Strategy, IIndicatorExportable
{
    // Existing properties...

    /// <summary>
    /// Debug mode exporter for real-time data streaming (optional, null when disabled)
    /// Encapsulates all debug mode logic to keep strategy code clean
    /// </summary>
    public DebugModeExporter? DebugExporter { get; set; }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);
        DebugExporter?.Initialize(this);
    }

    protected override void OnStopped()
    {
        DebugExporter?.Cleanup();
        base.OnStopped();
    }
}
```

**Design Notes:**

- **Single Property**: Only add `DebugExporter` property to `CustomStrategyBase`
- **Composition over Inheritance**: All debug logic in `DebugModeExporter` class
- **Optional**: Null when debug mode disabled (no overhead)
- **Encapsulation**: Strategy doesn't know about buffering, files, or event hooks
- **Configuration**: Created and configured in LauncherTemplate, not in strategy

### 3.2 IndicatorContainer Interception Strategies

**Problem**: `IndicatorContainer` is part of StockSharp framework and cannot be modified directly.

**Goal**: Capture indicator values as they're calculated without modifying StockSharp code.

#### Solution: Subscribe to `Changed` Event (Recommended)

**Approach**: Every `IIndicator` already fires `Changed` event when value is calculated. Subscribe to this event.

**Pros:**

- **No StockSharp modifications needed**
- **Event already exists in framework**
- Clean, idiomatic C#
- Zero performance overhead when not debugging
- Works with all indicator types

**Cons:**

- None significant

**Implementation Complexity**: Very Low

```csharp
// From BaseIndicator.cs:
public event Action<IIndicatorValue, IIndicatorValue> Changed;

// In DebugModeExporter.Initialize():
foreach (var indicator in strategy.Indicators)
{
    indicator.Changed += (input, result) =>
    {
        if (result.IsFinal) // Only export final values
        {
            CaptureIndicatorValue(indicator.Name, result);
        }
    };
}
```

#### Recommendation

**Use Option 4** (Subscribe to `Changed` event). This is the cleanest, most performant, and requires zero StockSharp modifications.

### 3.3 Debug Event Models (Extend Existing Export Models)

**Strategy**: Extend `ChartDataModels.cs` instead of creating new models.

#### Modifications to Export/ChartDataModels.cs

```csharp
namespace StockSharp.AdvancedBacktest.Export;

// EXTEND EXISTING MODELS - Add sequence numbers and type fields for debug mode

// Extend CandleDataPoint
public class CandleDataPoint
{
    public long Time { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }

    // NEW: Add for debug mode support
    public long? SequenceNumber { get; set; }
    public string? SecurityId { get; set; }
}

// Extend IndicatorDataPoint
public class IndicatorDataPoint
{
    public long Time { get; set; }
    public double Value { get; set; }

    // NEW: Add for debug mode support
    public long? SequenceNumber { get; set; }
}

// Extend TradeDataPoint
public class TradeDataPoint
{
    public long Time { get; set; }
    public double Price { get; set; }
    public double Volume { get; set; }
    public string Side { get; set; } = string.Empty;
    public double PnL { get; set; }

    // NEW: Add for debug mode support
    public long? SequenceNumber { get; set; }
    public long? OrderId { get; set; }
}

// NEW: Add state tracking model
public class StateDataPoint
{
    public long Time { get; set; }
    public double Position { get; set; }
    public double PnL { get; set; }
    public double UnrealizedPnL { get; set; }
    public string ProcessState { get; set; } = string.Empty;
    public long? SequenceNumber { get; set; }
}
```

**Benefits:**

- Reuses existing models (backward compatible)
- Optional sequence numbers (null when not in debug mode)
- JSONL format matches existing JSON structure
- Browser code can use same parsing logic

### 3.4 DebugModeExporter Class (Encapsulation)

**Purpose**: Encapsulate all debug mode logic in a single, composable class that can be attached to any strategy.

```csharp
namespace StockSharp.AdvancedBacktest.DebugMode;

/// <summary>
/// Manages real-time export of strategy execution data for debugging
/// Encapsulates all debug mode logic to keep CustomStrategyBase clean
/// </summary>
public class DebugModeExporter : IDisposable
{
    private readonly string _outputPath;
    private readonly int _flushIntervalMs;
    private readonly DebugEventBuffer _buffer;
    private readonly FileBasedWriter _writer;
    private CustomStrategyBase? _strategy;
    private long _sequenceNumber = 0;

    public DebugModeExporter(string outputPath, int flushIntervalMs = 500)
    {
        _outputPath = outputPath;
        _flushIntervalMs = flushIntervalMs;

        Directory.CreateDirectory(_outputPath);

        // Time-based buffer (matches polling interval)
        _buffer = new DebugEventBuffer(flushIntervalMs);
        _writer = new FileBasedWriter(_outputPath);

        _buffer.OnFlush += events => _writer.WriteEvents(events);
    }

    /// <summary>
    /// Initialize debug mode hooks for the given strategy
    /// Called from CustomStrategyBase.OnStarted()
    /// </summary>
    public void Initialize(CustomStrategyBase strategy)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

        // Hook trade events
        _strategy.NewMyTrade += OnTradeExecuted;

        // Hook indicator events using Changed event (no StockSharp modifications needed!)
        foreach (var indicator in _strategy.Indicators)
        {
            indicator.Changed += (input, result) =>
            {
                if (result.IsFinal)
                {
                    CaptureIndicatorValue(indicator.Name, result);
                }
            };
        }

        // TODO: Hook candle processing (requires ProcessMessage override in strategy)

        _strategy.LogInfo($"Debug mode initialized. Output: {_outputPath}");
    }

    /// <summary>
    /// Cleanup and flush remaining events
    /// Called from CustomStrategyBase.OnStopped()
    /// </summary>
    public void Cleanup()
    {
        if (_strategy != null)
        {
            _strategy.NewMyTrade -= OnTradeExecuted;
            _strategy.LogInfo("Debug mode cleanup completed");
        }

        _buffer.Flush();
    }

    private void OnTradeExecuted(MyTrade myTrade)
    {
        var tradePoint = new TradeDataPoint
        {
            Time = myTrade.Trade.Time.ToUnixTimeSeconds(),
            Price = (double)myTrade.Trade.Price,
            Volume = (double)myTrade.Trade.Volume,
            Side = myTrade.Order.Side == Sides.Buy ? "buy" : "sell",
            SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
            OrderId = myTrade.Order.TransactionId
        };

        _buffer.Add("trade", tradePoint);
    }

    private void CaptureIndicatorValue(string indicatorName, IIndicatorValue result)
    {
        var indicatorPoint = new IndicatorDataPoint
        {
            Time = result.Time.ToUnixTimeSeconds(),
            Value = (double)result.GetValue<decimal>(),
            SequenceNumber = Interlocked.Increment(ref _sequenceNumber)
        };

        _buffer.Add($"indicator_{indicatorName}", indicatorPoint);
    }

    public void CaptureCandle(Security security, ICandleMessage candle)
    {
        var candlePoint = new CandleDataPoint
        {
            Time = candle.OpenTime.ToUnixTimeSeconds(),
            Open = (double)candle.OpenPrice,
            High = (double)candle.HighPrice,
            Low = (double)candle.LowPrice,
            Close = (double)candle.ClosePrice,
            Volume = (double)candle.TotalVolume,
            SecurityId = security.Id,
            SequenceNumber = Interlocked.Increment(ref _sequenceNumber)
        };

        _buffer.Add("candle", candlePoint);
    }

    public void Dispose()
    {
        _buffer.Dispose();
        _writer.Dispose();
    }
}
```

**Key Features:**

- **Self-contained**: All debug logic in one class
- **No StockSharp modifications**: Uses existing `Changed` event
- **Time-based buffering**: Matches polling interval (500ms default)
- **Thread-safe**: Uses `Interlocked` for sequence numbers
- **Composable**: Attach to any strategy via property

### 3.5 Time-Based Event Buffer

```csharp
namespace StockSharp.AdvancedBacktest.DebugMode;

/// <summary>
/// Buffers debug events using time-based flushing instead of count-based
/// Ensures all events during polling interval are captured, even when debugging with breakpoints
/// </summary>
public class DebugEventBuffer : IDisposable
{
    private readonly Dictionary<string, List<object>> _buffers = new();
    private readonly Timer _flushTimer;
    private readonly object _lock = new();

    public event Action<Dictionary<string, List<object>>>? OnFlush;

    public DebugEventBuffer(int flushIntervalMs = 500)
    {
        // Time-based flush (not count-based!)
        _flushTimer = new Timer(
            _ => Flush(),
            null,
            flushIntervalMs,
            flushIntervalMs);
    }

    public void Add(string eventType, object eventData)
    {
        lock (_lock)
        {
            if (!_buffers.ContainsKey(eventType))
            {
                _buffers[eventType] = new List<object>();
            }

            _buffers[eventType].Add(eventData);
        }
    }

    public void Flush()
    {
        Dictionary<string, List<object>> eventsToFlush;

        lock (_lock)
        {
            if (_buffers.Count == 0 || _buffers.All(kvp => kvp.Value.Count == 0))
                return;

            eventsToFlush = new Dictionary<string, List<object>>(_buffers);
            _buffers.Clear();
        }

        // Fire event asynchronously to not block strategy
        Task.Run(() => OnFlush?.Invoke(eventsToFlush));
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        Flush();
    }
}
```

**Key Change**: Time-based flushing instead of count-based. When breakpoint is hit, timer flush ensures all accumulated events are written.

### 3.6 File-Based Writer

```csharp
namespace StockSharp.AdvancedBacktest.Debug.Export;

/// <summary>
/// Phase 1: Exports debug events to JSONL files for browser polling
/// </summary>
public class FileBasedExporter : IRealtimeExporter, IDisposable
{
    private readonly string _outputPath;
    private readonly IDebugEventBuffer _buffer;
    private readonly Dictionary<string, StreamWriter> _writers = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private const int MAX_FILE_SIZE_MB = 10;
    private int _fileRotationCounter = 0;

    public FileBasedExporter(string outputPath, IDebugEventBuffer buffer)
    {
        _outputPath = outputPath;
        _buffer = buffer;

        // Subscribe to buffer flush events
        _buffer.OnFlush += WriteBufferedEvents;

        InitializeWriters();
    }

    private void InitializeWriters()
    {
        // Create separate files for each event type
        var eventTypes = new[] { "candles", "indicators", "trades", "state" };

        foreach (var eventType in eventTypes)
        {
            var filePath = GetFilePath(eventType);

            // Open with FileShare.Read to allow concurrent browser access
            var fileStream = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            _writers[eventType] = new StreamWriter(fileStream)
            {
                AutoFlush = false // Batch writes
            };
        }
    }

    private string GetFilePath(string eventType)
    {
        var fileName = _fileRotationCounter > 0
            ? $"{eventType}_{_fileRotationCounter}.jsonl"
            : $"{eventType}.jsonl";

        return Path.Combine(_outputPath, fileName);
    }

    private async void WriteBufferedEvents(List<DebugEvent> events)
    {
        await _writeLock.WaitAsync();

        try
        {
            // Group events by type
            var grouped = events.GroupBy(e => e.Type);

            foreach (var group in grouped)
            {
                if (!_writers.TryGetValue(group.Key, out var writer))
                    continue;

                foreach (var evt in group)
                {
                    var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await writer.WriteLineAsync(json);
                }

                // Flush after batch
                await writer.FlushAsync();

                // Check file size for rotation
                await CheckAndRotateFile(group.Key, writer);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task CheckAndRotateFile(string eventType, StreamWriter writer)
    {
        var fileInfo = new FileInfo(GetFilePath(eventType));

        if (fileInfo.Length > MAX_FILE_SIZE_MB * 1024 * 1024)
        {
            // Close current file
            await writer.DisposeAsync();
            _writers.Remove(eventType);

            // Increment rotation counter
            _fileRotationCounter++;

            // Create new file
            var newPath = GetFilePath(eventType);
            var fileStream = new FileStream(
                newPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);

            _writers[eventType] = new StreamWriter(fileStream) { AutoFlush = false };
        }
    }

    public void Dispose()
    {
        _buffer.OnFlush -= WriteBufferedEvents;
        _buffer.Flush();

        foreach (var writer in _writers.Values)
        {
            writer.Dispose();
        }

        _writers.Clear();
        _writeLock.Dispose();
    }
}
```

### 3.6 Frontend: useRealtimeUpdates Hook

```typescript
// hooks/useRealtimeUpdates.ts

import { useEffect, useState, useRef, useCallback } from 'react';
import { IChartApi, ISeriesApi } from 'lightweight-charts';
import { DebugEvent, CandleEvent, IndicatorEvent, TradeEvent } from '@/types/debug-events';

interface UseRealtimeUpdatesOptions {
  debugDataPath: string;
  enabled: boolean;
  pollingInterval?: number; // Default: 500ms
  maxEventsPerUpdate?: number; // Default: 100
}

interface RealtimeUpdateState {
  isConnected: boolean;
  lastUpdate: number;
  eventsProcessed: number;
  currentSequence: number;
  error?: string;
}

export function useRealtimeUpdates(
  chart: IChartApi | null,
  candleSeries: ISeriesApi<'Candlestick'> | null,
  indicatorSeries: Map<string, ISeriesApi<'Line'>>,
  options: UseRealtimeUpdatesOptions
) {
  const [state, setState] = useState<RealtimeUpdateState>({
    isConnected: false,
    lastUpdate: 0,
    eventsProcessed: 0,
    currentSequence: 0,
  });

  const pollTimerRef = useRef<NodeJS.Timeout>();
  const filePositionsRef = useRef<Record<string, number>>({
    candles: 0,
    indicators: 0,
    trades: 0,
    state: 0,
  });

  const processEvents = useCallback(async () => {
    if (!options.enabled || !chart || !candleSeries) return;

    try {
      // Fetch updates from each file type
      const eventTypes = ['candles', 'indicators', 'trades', 'state'];
      let totalEvents = 0;

      for (const eventType of eventTypes) {
        const filePath = `${options.debugDataPath}/${eventType}.jsonl`;
        const startPos = filePositionsRef.current[eventType] || 0;

        // Fetch with Range header to get only new data
        const response = await fetch(filePath, {
          headers: {
            Range: `bytes=${startPos}-`,
          },
        });

        if (response.status === 206 || response.status === 200) {
          const text = await response.text();
          const lines = text.trim().split('\n');

          // Process each line (JSONL format)
          for (const line of lines) {
            if (!line.trim()) continue;

            try {
              const event: DebugEvent = JSON.parse(line);
              processEvent(event, candleSeries, indicatorSeries);
              totalEvents++;

              // Track sequence for gap detection
              if (event.sequenceNumber > state.currentSequence) {
                setState(prev => ({ ...prev, currentSequence: event.sequenceNumber }));
              }
            } catch (e) {
              console.warn('Failed to parse debug event:', e);
            }
          }

          // Update file position
          filePositionsRef.current[eventType] = startPos + text.length;
        }
      }

      if (totalEvents > 0) {
        setState(prev => ({
          ...prev,
          isConnected: true,
          lastUpdate: Date.now(),
          eventsProcessed: prev.eventsProcessed + totalEvents,
        }));
      }
    } catch (error) {
      console.error('Error processing debug events:', error);
      setState(prev => ({
        ...prev,
        isConnected: false,
        error: error instanceof Error ? error.message : 'Unknown error',
      }));
    }
  }, [options, chart, candleSeries, indicatorSeries, state.currentSequence]);

  const processEvent = useCallback(
    (
      event: DebugEvent,
      candleSeries: ISeriesApi<'Candlestick'>,
      indicatorSeries: Map<string, ISeriesApi<'Line'>>
    ) => {
      switch (event.type) {
        case 'candle': {
          const candle = event as CandleEvent;
          candleSeries.update({
            time: candle.time as any,
            open: candle.open,
            high: candle.high,
            low: candle.low,
            close: candle.close,
          });
          break;
        }

        case 'indicator': {
          const indicator = event as IndicatorEvent;
          const series = indicatorSeries.get(indicator.indicatorName);
          if (series) {
            series.update({
              time: indicator.time as any,
              value: indicator.value,
            });
          }
          break;
        }

        case 'trade': {
          const trade = event as TradeEvent;
          // Add marker to candlestick series
          const markers = candleSeries.markers?.() || [];
          markers.push({
            time: trade.time as any,
            position: trade.side === 'buy' ? 'belowBar' : 'aboveBar',
            color: trade.side === 'buy' ? '#2196F3' : '#F44336',
            shape: trade.side === 'buy' ? 'arrowUp' : 'arrowDown',
            text: `${trade.side.toUpperCase()} @ ${trade.price.toFixed(2)}`,
          });
          candleSeries.setMarkers(markers);
          break;
        }
      }
    },
    []
  );

  // Start/stop polling
  useEffect(() => {
    if (!options.enabled) {
      if (pollTimerRef.current) {
        clearInterval(pollTimerRef.current);
        pollTimerRef.current = undefined;
      }
      return;
    }

    // Start polling
    pollTimerRef.current = setInterval(
      processEvents,
      options.pollingInterval || 500
    );

    // Initial fetch
    processEvents();

    return () => {
      if (pollTimerRef.current) {
        clearInterval(pollTimerRef.current);
      }
    };
  }, [options.enabled, options.pollingInterval, processEvents]);

  return state;
}
```

### 3.7 Frontend: Debug Controls Component

```typescript
// components/debug/DebugControls.tsx

'use client';
import { useState } from 'react';

interface DebugControlsProps {
  isConnected: boolean;
  eventsProcessed: number;
  lastUpdate: number;
  error?: string;
}

export default function DebugControls({
  isConnected,
  eventsProcessed,
  lastUpdate,
  error,
}: DebugControlsProps) {
  const [isPaused, setIsPaused] = useState(false);

  const getStatusColor = () => {
    if (error) return 'bg-red-500';
    if (!isConnected) return 'bg-gray-400';
    return 'bg-green-500';
  };

  const getStatusText = () => {
    if (error) return 'Error';
    if (!isConnected) return 'Disconnected';
    return 'Live';
  };

  const timeSinceUpdate = lastUpdate ? Date.now() - lastUpdate : 0;
  const isStale = timeSinceUpdate > 5000; // 5 seconds

  return (
    <div className="fixed top-4 right-4 bg-white rounded-lg shadow-lg p-4 min-w-64">
      <div className="flex items-center gap-2 mb-3">
        <div className={`w-3 h-3 rounded-full ${getStatusColor()} ${isConnected ? 'animate-pulse' : ''}`} />
        <span className="font-semibold">{getStatusText()}</span>
      </div>

      <div className="space-y-2 text-sm">
        <div className="flex justify-between">
          <span className="text-gray-600">Events processed:</span>
          <span className="font-mono">{eventsProcessed.toLocaleString()}</span>
        </div>

        {lastUpdate > 0 && (
          <div className="flex justify-between">
            <span className="text-gray-600">Last update:</span>
            <span className={`font-mono ${isStale ? 'text-yellow-600' : ''}`}>
              {Math.round(timeSinceUpdate / 1000)}s ago
            </span>
          </div>
        )}

        {error && (
          <div className="text-red-600 text-xs mt-2 p-2 bg-red-50 rounded">
            {error}
          </div>
        )}
      </div>

      <div className="mt-4 flex gap-2">
        <button
          onClick={() => setIsPaused(!isPaused)}
          className="flex-1 px-3 py-1.5 bg-blue-500 text-white rounded hover:bg-blue-600 transition-colors text-sm"
        >
          {isPaused ? 'Resume' : 'Pause'}
        </button>
        <button
          onClick={() => window.location.reload()}
          className="flex-1 px-3 py-1.5 bg-gray-500 text-white rounded hover:bg-gray-600 transition-colors text-sm"
        >
          Reset
        </button>
      </div>
    </div>
  );
}
```

## 5. Implementation Plan (Revised)

### Phase 1: File-Based MVP (5 Days)

#### Day 1: Extend Export Infrastructure

- [ ] **Extend ChartDataModels.cs**
  - Add `SequenceNumber`, `SecurityId`, `OrderId` fields to existing models
  - Add new `StateDataPoint` class
- [ ] **Create DebugMode folder structure**
  - `StockSharp.AdvancedBacktest/DebugMode/` with README
- [ ] **Implement DebugEventBuffer**
  - Time-based flushing (500ms default)
  - Immediate flush on disposal
  - Thread-safe buffering
- [ ] **Write unit tests**
  - Buffer time-based flushing
  - Sequence number generation
  - Thread safety

#### Day 2: File Writer & Exporter Core

- [ ] **Implement FileBasedWriter**
  - JSONL writing with FileShare.Read
  - File rotation at 10MB
  - Async I/O for performance
- [ ] **Implement DebugModeExporter class**
  - Constructor with output path configuration
  - Initialize/Cleanup lifecycle methods
  - Trade event hooking
- [ ] **Write unit tests**
  - JSONL format validation
  - File rotation behavior
  - Concurrent read/write on Windows

#### Day 3: Indicator & Candle Capture

- [ ] **Implement indicator capture in DebugModeExporter**
  - Subscribe to indicator.Changed event
  - Filter for IsFinal values only
  - Handle complex indicators
- [ ] **Implement candle capture**
  - Add CaptureCandle() method
  - Document ProcessMessage override pattern for strategies
- [ ] **Add DebugExporter property to CustomStrategyBase**
  - Single property: `DebugModeExporter? DebugExporter`
  - Call Initialize() in OnStarted()
  - Call Cleanup() in OnStopped()
- [ ] **Write integration tests**
  - End-to-end: Strategy with mock indicators
  - Validate all event types captured

#### Day 4: Frontend Implementation

- [ ] **Create debug folder structure**
  - `components/debug/`, `hooks/debug/`, `lib/debug/`
- [ ] **Implement file polling logic**
  - `useFilePolling.ts` hook
  - JSONL incremental parsing
  - Range header support for partial reads
- [ ] **Implement chart updates**
  - `useRealtimeUpdates.ts` main hook
  - Throttling to 30 FPS
  - Sequence number tracking
- [ ] **Create DebugControls component**
  - Connection status indicator
  - Events processed counter
  - Error display

#### Day 5: Integration & Testing

- [ ] **LauncherTemplate integration**
  - Add debug mode configuration Program.cs directly on strategy creation/launch
  - Create DebugModeExporter instance based on config
  - Attach to strategy before start
- [ ] **End-to-end testing**
  - Run sample strategy with debug mode enabled
  - Verify browser updates in real-time
  - Test with breakpoints (verify flush behavior)
  - Validate no errors in browser console

### Testing Strategy (Simplified)

#### Unit Tests

- `DebugEventBuffer`: Time-based flushing, thread safety
- `FileBasedWriter`: JSONL format, file rotation, concurrent access
- Event serialization: JSON format validation
- Sequence numbers: Increment, uniqueness

#### Integration Tests

- End-to-end: Strategy → Files → Browser
- File locking: Windows concurrent access
- Performance: Overhead measurement
- Breakpoint behavior: Flush on pause

#### Acceptance Criteria

- [ ] Strategy can export candles, indicators, trades to JSONL
- [ ] Browser polls files and updates charts in real-time
- [ ] No frontend errors in Chrome DevTools
- [ ] Unit test coverage of DebugModeExporter, FileBasedWriter, DebugEventBuffer >80%
- [ ] Works with breakpoints (immediate flush)

## 6. Acceptance Criteria

### MVP Completion Checklist

- [ ] **Backend Implementation**
  - [ ] DebugModeExporter class with Initialize/Cleanup lifecycle
  - [ ] Time-based DebugEventBuffer (500ms flush interval)
  - [ ] FileBasedWriter with JSONL format and file rotation
  - [ ] Extended ChartDataModels with sequence numbers
  - [ ] Indicator capture via Changed event subscription

- [ ] **Frontend Implementation**
  - [ ] useRealtimeUpdates hook with file polling
  - [ ] JSONL incremental parsing with Range headers
  - [ ] DebugControls component with connection status
  - [ ] Chart update throttling (30 FPS)

- [ ] **Integration**
  - [ ] CustomStrategyBase with DebugExporter property
  - [ ] LauncherTemplate configuration support
  - [ ] End-to-end: Strategy → Files → Browser visualization

- [ ] **Quality & Performance**
  - [ ] Unit test coverage of DebugModeExporter, FileBasedWriter, DebugEventBuffer >80%
  - [ ] No browser console errors
  - [ ] Works correctly with debugger breakpoints
  - [ ] File locking handled gracefully on Windows

- [ ] **Documentation**
  - [ ] Debug mode usage guide in README
  - [ ] Configuration examples

---

## Appendix A: JSONL Format Examples (Using Extended Existing Models)

### Candle Event (Extended CandleDataPoint)

```json
{"time":1729555200,"open":50000.0,"high":50100.0,"low":49950.0,"close":50050.0,"volume":1250.5,"sequenceNumber":1,"securityId":"BTCUSDT@BINANCE"}
```

### Indicator Event (Extended IndicatorDataPoint)

```json
{"time":1729555200,"value":49980.5,"sequenceNumber":2}
```

**Note**: Indicator name is embedded in filename `indicators_SMA_20.jsonl`

### Trade Event (Extended TradeDataPoint)

```json
{"time":1729555200,"price":50050.0,"volume":0.01,"side":"buy","pnL":0.0,"sequenceNumber":3,"orderId":12345}
```

### State Event (New StateDataPoint)

```json
{"time":1729555200,"position":0.01,"pnL":50.0,"unrealizedPnL":0.0,"processState":"Started","sequenceNumber":4}
```

**File Organization:**

```text
debug-output/
├── candles.jsonl              # All candle updates
├── indicators_SMA_20.jsonl    # Per-indicator files
├── indicators_RSI_14.jsonl
├── trades.jsonl               # All trade executions
└── state.jsonl                # Strategy state updates
```

---

## Appendix B: Lightweight Charts Update API

```typescript
// Batch load (current approach)
candlestickSeries.setData([
  { time: 1729555200, open: 50000, high: 50100, low: 49950, close: 50050 },
  // ... more candles
]);

// Incremental update (debug mode)
candlestickSeries.update({
  time: 1729555260,
  open: 50050,
  high: 50150,
  low: 50000,
  close: 50100
});

// Add trade marker
candlestickSeries.setMarkers([
  ...existingMarkers,
  {
    time: 1729555260,
    position: 'belowBar',
    color: '#2196F3',
    shape: 'arrowUp',
    text: 'BUY @ 50050'
  }
]);

// Update indicator line
lineSeries.update({ time: 1729555260, value: 49990 });
```

---

**End of Document**

**Version**: 1.0
**Status**: Ready for Review
**Next Step**: Review with team, finalize Phase 1 scope, begin implementation

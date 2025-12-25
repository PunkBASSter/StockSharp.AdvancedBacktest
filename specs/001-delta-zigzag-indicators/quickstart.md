# Quickstart: DeltaZigZag Indicators

**Feature**: 001-delta-zigzag-indicators
**Date**: 2025-12-25

## Overview

The DeltaZigZag indicator suite provides volatility-adaptive peak/trough detection for trading strategies. Unlike the standard S# ZigZag which uses a fixed percentage threshold, DeltaZigZag calculates the reversal threshold dynamically based on the previous swing size.

## Installation

The indicators are part of the `StockSharp.AdvancedBacktest.Core` assembly. No additional NuGet packages required.

```csharp
using StockSharp.AdvancedBacktest.Core.Indicators;
```

## Basic Usage

### Core DeltaZigZag Indicator

```csharp
// Create indicator with 50% retracement threshold
var deltaZZ = new DeltaZigZag
{
    Delta = 0.5m,              // 50% of last swing required for reversal
    MinimumThreshold = 10m     // Absolute fallback for initial swing
};

// Subscribe to candles
foreach (var candle in candles)
{
    var result = deltaZZ.Process(candle);

    if (!result.IsEmpty)
    {
        // Reversal detected
        var price = result.GetValue<decimal>();
        var shift = ((ZigZagIndicatorValue)result).Shift;
        var isUp = ((ZigZagIndicatorValue)result).IsUp;

        Console.WriteLine($"{(isUp ? "Peak" : "Trough")} at {price}, {shift} bars ago");
    }
}
```

### DeltaZzPeak (Peaks Only)

```csharp
// Create peak-only indicator for charting
var peaks = new DeltaZzPeak
{
    Delta = 0.5m,
    MinimumThreshold = 10m
};

foreach (var candle in candles)
{
    var result = peaks.Process(candle);

    if (!result.IsEmpty)
    {
        // Only peaks are returned
        var peakPrice = result.GetValue<decimal>();
        // Use for resistance level visualization
    }
}
```

### DeltaZzTrough (Troughs Only)

```csharp
// Create trough-only indicator for charting
var troughs = new DeltaZzTrough
{
    Delta = 0.5m,
    MinimumThreshold = 10m
};

foreach (var candle in candles)
{
    var result = troughs.Process(candle);

    if (!result.IsEmpty)
    {
        // Only troughs are returned
        var troughPrice = result.GetValue<decimal>();
        // Use for support level visualization
    }
}
```

## Strategy Integration

```csharp
public class BreakoutStrategy : CustomStrategyBase
{
    private DeltaZigZag _deltaZZ;
    private decimal _lastPeak;
    private decimal _lastTrough;

    protected override void OnStarted()
    {
        _deltaZZ = new DeltaZigZag
        {
            Delta = 0.5m,
            MinimumThreshold = Security.PriceStep * 100
        };

        // Subscribe to candles
        this.WhenCandlesFinished(CandleSeries)
            .Do(ProcessCandle)
            .Apply(this);
    }

    private void ProcessCandle(ICandleMessage candle)
    {
        var result = _deltaZZ.Process(candle);

        if (!result.IsEmpty)
        {
            var zzValue = (ZigZagIndicatorValue)result;

            if (zzValue.IsUp)
                _lastPeak = zzValue.GetValue<decimal>();
            else
                _lastTrough = zzValue.GetValue<decimal>();
        }

        // Trading logic using _lastPeak and _lastTrough
    }
}
```

## Parameter Guidelines

| Parameter | Typical Range | Description |
|-----------|---------------|-------------|
| Delta = 0.3 | Sensitive | Detects smaller reversals, more signals |
| Delta = 0.5 | Balanced | Standard setting for most instruments |
| Delta = 0.7 | Conservative | Only major reversals, fewer signals |
| MinimumThreshold | 50-200 × PriceStep | Instrument-specific fallback |

## Export and Visualization

The indicators integrate with existing pipelines:

```csharp
// Export to JSON for web visualization
var exporter = new IndicatorExporter();
exporter.AddIndicator("DeltaZZ_Peaks", peaksIndicator);
exporter.AddIndicator("DeltaZZ_Troughs", troughsIndicator);
await exporter.ExportAsync(outputPath);
```

## Common Patterns

### Breakout Detection

```csharp
// Buy when price breaks above last peak
if (candle.HighPrice > _lastPeak && Position <= 0)
{
    Buy(volume);
}

// Sell when price breaks below last trough
if (candle.LowPrice < _lastTrough && Position >= 0)
{
    Sell(volume);
}
```

### Dynamic Stop-Loss

```csharp
// Use last trough as stop for long positions
var stopPrice = _lastTrough;

// Use last peak as stop for short positions
var stopPrice = _lastPeak;
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No signals generated | Increase Delta or decrease MinimumThreshold |
| Too many signals | Decrease Delta or increase MinimumThreshold |
| Signals delayed | Normal behavior - confirmation requires reversal |
| Shift always 0 | Ensure using IsFinal candles (completed bars) |

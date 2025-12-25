# Implementation Plan: DeltaZigZag Indicator Port

**Branch**: `001-delta-zigzag-indicators` | **Date**: 2025-12-25 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-delta-zigzag-indicators/spec.md`

## Summary

Port the MQL5 DeltaZigZag indicator to StockSharp as a Core assembly indicator with dynamic volatility-based thresholds. Create two derived indicators (DeltaZzPeak, DeltaZzTrough) following the S# Peak/Trough pattern for frontend-friendly single-value output per timestamp.

## Technical Context

**Language/Version**: C# / .NET 8
**Primary Dependencies**: StockSharp.Algo.Indicators (BaseIndicator, IIndicatorValue, ZigZagIndicatorValue, ShiftedIndicatorValue)
**Storage**: N/A (stateless indicator, in-memory state only)
**Testing**: xUnit v3 with Microsoft.NET.Test.Sdk
**Target Platform**: Windows/Linux (.NET 8 runtime)
**Project Type**: Library (Core assembly)
**Performance Goals**: Process candles in real-time without blocking strategy execution
**Constraints**: Must follow S# indicator patterns for compatibility with existing pipelines
**Scale/Scope**: 3 new indicator classes, integration with existing ZigZag-based strategies

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Separation of Concerns | ✅ PASS | Indicators are business logic → Core assembly |
| II. Test-First Development | ✅ PASS | Unit tests will be written first using synthetic candle data |
| III. Financial Precision | ✅ PASS | All price calculations use `decimal` (matching S# ZigZag) |
| IV. Composition Over Inheritance | ✅ PASS | DeltaZzPeak/DeltaZzTrough wrap DeltaZigZag via composition |
| V. Explicit Visibility | ✅ PASS | All classes/members will have explicit access modifiers |
| VI. System.Text.Json Standard | ✅ N/A | No JSON serialization in indicators |
| VII. End-to-End Testability | ✅ PASS | Synthetic candle data enables isolated testing |

**Gate Result**: PASS - No violations requiring justification

## Project Structure

### Documentation (this feature)

```text
specs/001-delta-zigzag-indicators/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
StockSharp.AdvancedBacktest.Core/
├── Indicators/
│   ├── DeltaZigZag.cs           # Core indicator with dynamic threshold
│   ├── DeltaZzPeak.cs           # Derived peak-only indicator
│   └── DeltaZzTrough.cs         # Derived trough-only indicator

StockSharp.AdvancedBacktest.Core.Tests/
├── Indicators/
│   ├── DeltaZigZagTests.cs      # Unit tests for core indicator
│   ├── DeltaZzPeakTests.cs      # Unit tests for peak filter
│   └── DeltaZzTroughTests.cs    # Unit tests for trough filter
```

**Structure Decision**: Indicators belong in Core assembly under `Indicators/` namespace. Tests follow assembly separation pattern in Core.Tests.

## Complexity Tracking

> No violations requiring justification. All gates pass.

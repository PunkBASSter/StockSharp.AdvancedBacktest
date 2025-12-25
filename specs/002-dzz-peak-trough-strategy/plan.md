# Implementation Plan: DeltaZz Peak/Trough Breakout Strategy

**Branch**: `002-dzz-peak-trough-strategy` | **Date**: 2025-12-25 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-dzz-peak-trough-strategy/spec.md`

## Summary

Implement a ZigZagBreakout strategy variant using separate DeltaZzPeak and DeltaZzTrough indicators for frontend visualization compatibility. Add a signal deduplication system based on (Entry, SL, TP) tuples to prevent duplicate orders when indicator values persist. Extract the existing ZigZagBreakoutStrategy launching code into a dedicated launcher class and implement a similar launcher for the new strategy with DI container integration.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0
**Primary Dependencies**: StockSharp.Algo, StockSharp.BusinessEntities, Microsoft.Extensions.DependencyInjection 8.0.1
**Storage**: N/A (uses existing StockSharp storage infrastructure)
**Testing**: xUnit v3 with Microsoft.NET.Test.Sdk
**Target Platform**: Windows/Linux (cross-platform .NET 8)
**Project Type**: Multi-project solution (Core, Infrastructure, LauncherTemplate)
**Performance Goals**: N/A (offline backtesting, not real-time)
**Constraints**: Decimal precision for all financial calculations, one-way dependency (Infrastructure → Core)
**Scale/Scope**: Single strategy implementation with launcher infrastructure refactoring

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Separation of Concerns | PASS | Strategy logic in LauncherTemplate, signal deduplication can be in Core if reusable. Launcher infrastructure in LauncherTemplate (application layer). |
| II. Test-First Development | PASS | Tests will be written first per TDD workflow. Test projects exist for Core and Infrastructure. |
| III. Financial Precision | PASS | All prices (Entry, SL, TP) use decimal type. Deduplication keys use decimal comparison. |
| IV. Composition Over Inheritance | PASS | SignalDeduplicator is a composed component, not an inheritance hierarchy. Launchers implement IStrategyLauncher interface. |
| V. Explicit Visibility | PASS | All new classes will have explicit access modifiers. |
| VI. System.Text.Json Standard | N/A | No new JSON serialization required for this feature. |
| VII. End-to-End Testability | PASS | Strategy testable with mock candle data. Launchers testable with DI container mocking. |

**Gate Result**: PASS - All applicable principles satisfied.

## Project Structure

### Documentation (this feature)

```text
specs/002-dzz-peak-trough-strategy/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (N/A - no external APIs)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
StockSharp.AdvancedBacktest.Core/
├── Indicators/
│   ├── DeltaZigZag.cs         # Existing
│   ├── DeltaZzPeak.cs         # Existing
│   └── DeltaZzTrough.cs       # Existing
├── OrderManagement/
│   ├── OrderRequest.cs        # Existing - contains ProtectivePair
│   ├── OrderPositionManager.cs # Existing
│   └── SignalDeduplicator.cs  # NEW - reusable signal deduplication

StockSharp.AdvancedBacktest.LauncherTemplate/
├── Program.cs                  # MODIFIED - thin orchestrator with DI
├── Launchers/                  # NEW directory
│   ├── IStrategyLauncher.cs   # NEW - launcher abstraction
│   ├── ZigZagBreakoutLauncher.cs # NEW - extracted from Program.cs
│   └── DzzPeakTroughLauncher.cs  # NEW - new strategy launcher
└── Strategies/
    ├── ZigZagBreakout/
    │   ├── ZigZagBreakoutStrategy.cs  # Existing
    │   └── ZigZagBreakoutConfig.cs    # Existing
    └── DzzPeakTrough/                 # NEW directory
        ├── DzzPeakTroughStrategy.cs   # NEW - main strategy
        └── DzzPeakTroughConfig.cs     # NEW - configuration

StockSharp.AdvancedBacktest.Core.Tests/
├── OrderManagement/
│   └── SignalDeduplicatorTests.cs  # NEW

StockSharp.AdvancedBacktest.Tests/
└── Strategies/
    └── DzzPeakTroughStrategyTests.cs  # NEW - integration tests
```

**Structure Decision**: Follows existing assembly decomposition pattern. SignalDeduplicator goes in Core (reusable business logic). Launchers and strategy implementations stay in LauncherTemplate (application layer).

## Complexity Tracking

> No violations to justify. All changes align with existing patterns.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | - | - |

# Implementation Plan: Core-Infrastructure Assembly Decomposition

**Branch**: `002-core-infra-decomposition` | **Date**: 2025-12-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-core-infra-decomposition/spec.md`

## Summary

Decompose `StockSharp.AdvancedBacktest` monolithic assembly into two assemblies with clean separation:
- **Core**: Business logic (strategies, order management, parameters, metrics, validation)
- **Infrastructure**: Implementation details (export, debug, optimization orchestration, storage, serialization)

Technical approach: Test-first migration with separate test projects per assembly. Create debug logging abstraction in Core implemented by Infrastructure. One-way dependency: Infrastructure → Core → StockSharp.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: StockSharp (submodule), Ecng, System.Text.Json, Microsoft.Data.Sqlite, Microsoft.Extensions.Logging
**Storage**: File-based (JSON export), SQLite (debug event storage) - no changes
**Testing**: xUnit v3 with Microsoft.NET.Test.Sdk targeting .NET 10
**Target Platform**: Windows, cross-platform .NET
**Project Type**: Library assemblies (Core + Infrastructure) with test projects
**Performance Goals**: No degradation from current implementation
**Constraints**: Preserve existing API surface, maintain backward compatibility with LegacyCustomization/StrategyLauncher
**Scale/Scope**: ~5,600 lines across 125 files; Core ~1,700 lines, Infrastructure ~3,900 lines

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Separation of Concerns | ✅ ALIGNED | This feature enforces separation by splitting Core (business) from Infrastructure (operational) |
| II. Test-First Development | ✅ ALIGNED | FR-019 mandates test-first migration; separate test projects per assembly |
| III. Financial Precision | ✅ N/A | No changes to financial calculations; existing decimal usage preserved |
| IV. Composition Over Inheritance | ✅ ALIGNED | Debug logging abstraction uses composition (interface injection) |
| V. Explicit Visibility | ✅ ALIGNED | All moved classes retain explicit modifiers |
| VI. System.Text.Json Standard | ✅ N/A | Serialization moves to Infrastructure; no new serialization code |
| VII. End-to-End Testability | ✅ ALIGNED | Separate test projects enable isolated testing; Core testable without Infrastructure |

**Gate Status**: ✅ PASS - All applicable principles aligned or not affected.

## Project Structure

### Documentation (this feature)

```text
specs/002-core-infra-decomposition/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (debug abstraction interface)
├── checklists/          # Quality checklists
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (from /speckit.tasks)
```

### Source Code (repository root)

```text
StockSharp.AdvancedBacktest/
├── StockSharp.AdvancedBacktest.Core/           # NEW: Core assembly
│   ├── Strategies/                              # CustomStrategyBase, Modules/
│   ├── OrderManagement/                         # TradeSignal, OrderPositionManager
│   ├── Parameters/                              # ICustomParam, NumberParam, etc.
│   ├── Statistics/                              # PerformanceMetrics, Calculator
│   ├── PerformanceValidation/                   # WalkForward validation
│   ├── Models/                                  # OptimizationConfig, BacktestConfig
│   ├── Backtest/                                # BacktestResult, PeriodConfig
│   └── StockSharp.AdvancedBacktest.Core.csproj
│
├── StockSharp.AdvancedBacktest.Infrastructure/  # NEW: Infrastructure assembly
│   ├── Export/                                  # ReportBuilder, IndicatorExporter
│   ├── DebugMode/                               # DebugModeExporter, AiAgenticDebug/
│   ├── Optimization/                            # OptimizerRunner, LauncherBase
│   ├── Storages/                                # SharedStorageRegistry
│   ├── Serialization/                           # JSON options, converters
│   ├── Utilities/                               # CartesianProductGenerator, helpers
│   └── StockSharp.AdvancedBacktest.Infrastructure.csproj
│
├── StockSharp.AdvancedBacktest.Core.Tests/      # NEW: Core tests
│   ├── Strategies/
│   ├── OrderManagement/
│   ├── Parameters/
│   └── Statistics/
│
├── StockSharp.AdvancedBacktest.Infrastructure.Tests/  # NEW: Infrastructure tests
│   ├── Export/
│   ├── DebugMode/
│   ├── Optimization/
│   └── Storages/
│
├── StockSharp.AdvancedBacktest/                 # DEPRECATED: Original assembly (to be removed)
│
└── LegacyCustomization/
    └── StrategyLauncher/                        # Updated to reference Core + Infrastructure
```

**Structure Decision**: Multi-project library structure with separate test assemblies. Core contains all trading business logic; Infrastructure contains all operational/export/debug functionality. Test projects mirror assembly structure for isolated testing.

## Complexity Tracking

> No constitution violations identified. The assembly split aligns with Separation of Concerns principle.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |

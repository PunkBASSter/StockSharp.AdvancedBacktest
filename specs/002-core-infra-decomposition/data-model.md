# Data Model: Core-Infrastructure Assembly Decomposition

**Date**: 2025-12-09
**Feature**: 002-core-infra-decomposition

## Overview

This document defines the data model for the assembly decomposition. Since this is a refactoring feature (not new functionality), the data model focuses on:
1. Assembly structure and relationships
2. New debug logging abstraction interface
3. Component categorization for migration

## Assembly Entities

### Core Assembly

**Name**: `StockSharp.AdvancedBacktest.Core`
**Responsibility**: Trading business logic, strategy abstractions, order management, metrics

| Namespace | Components | Lines (approx) |
|-----------|------------|----------------|
| Strategies | CustomStrategyBase, Modules/* | ~400 |
| OrderManagement | TradeSignal, OrderPositionManager, IStrategyOrderOperations | ~400 |
| Parameters | ICustomParam, CustomParam<T>, NumberParam, SecurityParam, etc. | ~260 |
| Statistics | PerformanceMetrics, PerformanceMetricsCalculator, IPerformanceMetricsCalculator | ~290 |
| PerformanceValidation | WalkForwardConfig, WalkForwardValidator, WalkForwardResult, WindowResult | ~280 |
| Models | OptimizationConfig, OptimizationResult, GeneticConfig | ~80 |
| Backtest | BacktestConfig, BacktestResult, PeriodConfig | ~60 |
| (root) | IDebugEventSink, NullDebugEventSink | ~30 |

**Dependencies**: StockSharp, Ecng

---

### Infrastructure Assembly

**Name**: `StockSharp.AdvancedBacktest.Infrastructure`
**Responsibility**: Export, debug implementation, optimization orchestration, storage, serialization

| Namespace | Components | Lines (approx) |
|-----------|------------|----------------|
| Export | ReportBuilder, BacktestExporter, IndicatorExporter, ChartDataModels, etc. | ~700 |
| DebugMode | DebugModeExporter, DebugEventBuffer, FileBasedWriter | ~900 |
| DebugMode.AiAgenticDebug | EventLogger, SqliteEventSink, AgenticEventLogger, MCP tools | ~1300 |
| Optimization | OptimizerRunner, LauncherBase, OptimizationLauncher | ~400 |
| Storages | SharedStorageRegistry, SharedMarketDataStorage | ~400 |
| Serialization | StrategyConfigJsonOptions, CustomParamJsonConverter | ~120 |
| Utilities | CartesianProductGenerator, IndicatorValueHelper, PriceStepHelper, SecurityIdComparer | ~220 |

**Dependencies**: Core, StockSharp, Ecng, System.Text.Json, Microsoft.Data.Sqlite, Microsoft.Extensions.Logging

---

## Debug Logging Abstraction

### IDebugEventSink (Core)

The new abstraction interface defined in Core, implemented by Infrastructure.

```
┌─────────────────────────────────────────────────────────────┐
│                    CORE ASSEMBLY                             │
├─────────────────────────────────────────────────────────────┤
│  <<interface>>                                               │
│  IDebugEventSink                                             │
│  ─────────────────                                           │
│  + LogEvent(category: string, eventType: string, data: obj)  │
│  + Flush()                                                   │
├─────────────────────────────────────────────────────────────┤
│  NullDebugEventSink : IDebugEventSink                        │
│  ─────────────────────────────────────                       │
│  + Instance : NullDebugEventSink [static, readonly]          │
│  - NullDebugEventSink() [private]                            │
│  + LogEvent(...) { /* no-op */ }                             │
│  + Flush() { /* no-op */ }                                   │
└─────────────────────────────────────────────────────────────┘
                           △
                           │ implements
                           │
┌─────────────────────────────────────────────────────────────┐
│                 INFRASTRUCTURE ASSEMBLY                      │
├─────────────────────────────────────────────────────────────┤
│  FileDebugEventSink : IDebugEventSink                        │
│  ───────────────────────────────────                         │
│  - _writer : FileBasedWriter                                 │
│  + LogEvent(...) { writes to JSONL file }                    │
│  + Flush() { flushes buffer }                                │
├─────────────────────────────────────────────────────────────┤
│  SqliteDebugEventSink : IDebugEventSink                      │
│  ─────────────────────────────────────                       │
│  - _eventSink : SqliteEventSink                              │
│  + LogEvent(...) { persists to SQLite }                      │
│  + Flush() { commits transaction }                           │
└─────────────────────────────────────────────────────────────┘
```

### Usage Pattern

```
CustomStrategyBase (Core)
    │
    │ has property
    ▼
IDebugEventSink DebugSink { get; set; } = NullDebugEventSink.Instance
    │
    │ when configured by Infrastructure
    ▼
Infrastructure sets: strategy.DebugSink = new FileDebugEventSink(...)
```

---

## Test Project Mapping

### Core.Tests

| Test Namespace | Tests For |
|----------------|-----------|
| Strategies | CustomStrategyBase, Modules |
| OrderManagement | TradeSignal, OrderPositionManager |
| Parameters | Parameter types, CustomParamsContainer |
| Statistics | PerformanceMetrics calculation |

### Infrastructure.Tests

| Test Namespace | Tests For |
|----------------|-----------|
| Export | ReportBuilder, Exporters |
| DebugMode | DebugModeExporter, EventBuffer, FileBasedWriter |
| DebugMode.AiAgenticDebug | EventLogger, SqliteEventSink |
| Optimization | OptimizerRunner |
| Storages | SharedStorageRegistry |

---

## Migration State Tracking

### Component Migration States

| State | Description |
|-------|-------------|
| NOT_STARTED | Component in original assembly, no migration work done |
| TESTS_MIGRATED | Tests moved to new test project, failing (RED) |
| STUB_CREATED | Minimal stub in target assembly, tests compile (GREEN) |
| CODE_MIGRATED | Full implementation moved, tests passing |
| VERIFIED | Build + all tests pass, reviewed |

### Migration Order (Dependencies)

```
Phase 1: Core Assembly (no dependencies on Infrastructure)
  1. Parameters namespace (no internal deps)
  2. Models namespace (depends on Parameters)
  3. Statistics namespace (no internal deps)
  4. Backtest namespace (depends on Models)
  5. OrderManagement namespace (no internal deps)
  6. PerformanceValidation namespace (depends on Statistics, Models)
  7. Strategies namespace (depends on Parameters, Statistics, OrderManagement)
  8. IDebugEventSink interface (new, no deps)

Phase 2: Infrastructure Assembly (depends on Core)
  9. Utilities namespace (no internal deps)
  10. Serialization namespace (no internal deps)
  11. Storages namespace (depends on StockSharp)
  12. Export namespace (depends on Core types)
  13. Optimization namespace (depends on Core, Storages)
  14. DebugMode namespace (depends on Core IDebugEventSink)
```

---

## Validation Rules

### Dependency Direction

- Core assembly MUST NOT reference Infrastructure assembly
- Infrastructure assembly MUST reference Core assembly
- Both test projects reference their corresponding assembly

### Namespace Preservation

- `StockSharp.AdvancedBacktest.Strategies` → `StockSharp.AdvancedBacktest.Strategies` (unchanged)
- `StockSharp.AdvancedBacktest.Export` → `StockSharp.AdvancedBacktest.Export` (unchanged)
- New: `StockSharp.AdvancedBacktest.Core.IDebugEventSink`

### Access Modifiers

- All public types remain public
- Internal types remain internal within their new assembly
- InternalsVisibleTo configured for test projects

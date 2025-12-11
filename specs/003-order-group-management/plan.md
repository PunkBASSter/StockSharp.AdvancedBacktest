# Implementation Plan: Advanced Order Group Management

**Branch**: `003-order-group-management` | **Date**: 2025-12-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-order-group-management/spec.md`

## Summary

Implement an advanced order group management system that tracks multiple order groups per security, each containing one opening order and multiple closing orders with fractional volumes. The system enables smoother equity curves by scaling out of positions at multiple price levels. Core abstractions reside in `StockSharp.AdvancedBacktest.Core`, with Infrastructure providing JSON persistence for live trading mode.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0
**Primary Dependencies**: StockSharp (Algo, BusinessEntities, Messages), Microsoft.Extensions.Options
**Storage**: JSON files for live mode persistence (System.Text.Json)
**Testing**: xUnit v3 with Microsoft.NET.Test.Sdk
**Target Platform**: Windows/Linux (.NET 8 runtime)
**Project Type**: Library (Core + Infrastructure assemblies)
**Performance Goals**: ≤10% backtest performance degradation vs single-position system; support 100 simultaneous order groups per security
**Constraints**: All financial calculations MUST use `decimal` type; no Newtonsoft.Json for new code
**Scale/Scope**: Single strategy instance, multiple securities, up to 100 order groups per security

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Separation of Concerns | ✅ PASS | Core: abstractions, models, state logic. Infrastructure: JSON persistence, file I/O |
| II. Test-First Development | ✅ REQUIRED | Tests must be written first for all new types |
| III. Financial Precision | ✅ REQUIRED | All prices, volumes use `decimal`; custom JSON converters for serialization |
| IV. Composition Over Inheritance | ✅ PASS | OrderGroup composes GroupedOrders; no deep inheritance |
| V. Explicit Visibility | ✅ REQUIRED | All classes/members have explicit access modifiers |
| VI. System.Text.Json Standard | ✅ REQUIRED | Use System.Text.Json with source generation for persistence |
| VII. End-to-End Testability | ✅ REQUIRED | Mock IStrategyOrderOperations for isolated testing |

**Gate Result**: PASS - No violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/003-order-group-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (internal C# interfaces)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
StockSharp.AdvancedBacktest.Core/
├── OrderManagement/
│   ├── IStrategyOrderOperations.cs      # Existing interface
│   ├── TradeSignal.cs                   # Existing - to be extended
│   ├── OrderPositionManager.cs          # Existing - remains for simple cases
│   ├── OrderGroup.cs                    # NEW: Order group model
│   ├── GroupedOrder.cs                  # NEW: Individual order within group
│   ├── OrderGroupState.cs               # NEW: Group state enumeration
│   ├── GroupedOrderState.cs             # NEW: Order state enumeration
│   ├── OrderGroupLimits.cs              # NEW: Configuration model
│   ├── ExtendedTradeSignal.cs           # NEW: Signal with multiple closing orders
│   ├── IOrderGroupManager.cs            # NEW: Abstraction interface
│   └── IOrderGroupPersistence.cs        # NEW: Persistence abstraction

StockSharp.AdvancedBacktest.Infrastructure/
├── OrderManagement/                      # NEW directory
│   ├── OrderGroupManager.cs             # NEW: Main implementation
│   ├── OrderGroupJsonPersistence.cs     # NEW: JSON file persistence
│   └── OrderGroupJsonContext.cs         # NEW: Source-generated JSON context

StockSharp.AdvancedBacktest.Core.Tests/
├── OrderManagement/
│   ├── OrderGroupTests.cs               # NEW: Unit tests for OrderGroup
│   ├── ExtendedTradeSignalTests.cs      # NEW: Signal validation tests
│   └── OrderGroupLimitsTests.cs         # NEW: Limits validation tests

StockSharp.AdvancedBacktest.Infrastructure.Tests/
├── OrderManagement/
│   ├── OrderGroupManagerTests.cs        # NEW: Manager integration tests
│   └── OrderGroupJsonPersistenceTests.cs # NEW: Persistence tests
```

**Structure Decision**: Follows existing Core/Infrastructure separation pattern. New `OrderManagement/` directory in Infrastructure mirrors Core structure for order group implementation.

## Complexity Tracking

> No Constitution Check violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | - | - |

## Post-Design Constitution Re-Check

*Verified after Phase 1 design completion.*

| Principle | Status | Design Validation |
|-----------|--------|-------------------|
| I. Separation of Concerns | ✅ PASS | Core: Models (OrderGroup, GroupedOrder, enums), Abstractions (IOrderGroupManager, IOrderGroupPersistence). Infrastructure: OrderGroupManager implementation, JSON persistence |
| II. Test-First Development | ✅ REQUIRED | Test files specified in project structure; implementation blocked until tests written |
| III. Financial Precision | ✅ PASS | All price/volume fields use `decimal` in data model; custom JSON context uses DecimalConverter |
| IV. Composition Over Inheritance | ✅ PASS | OrderGroup composes GroupedOrder list; no inheritance hierarchies introduced |
| V. Explicit Visibility | ✅ REQUIRED | All contracts specify explicit public/internal modifiers |
| VI. System.Text.Json Standard | ✅ PASS | OrderGroupJsonContext uses source generation; DecimalConverter pattern from existing codebase |
| VII. End-to-End Testability | ✅ PASS | IStrategyOrderOperations abstraction enables mocking; NullOrderGroupPersistence for backtest isolation |

**Post-Design Gate Result**: PASS - Design complies with all constitution principles.

## Generated Artifacts

| Artifact | Path | Description |
|----------|------|-------------|
| research.md | `specs/003-order-group-management/research.md` | Technical decisions and rationale |
| data-model.md | `specs/003-order-group-management/data-model.md` | Entity definitions, state machines, relationships |
| IOrderGroupManager.cs | `specs/003-order-group-management/contracts/IOrderGroupManager.cs` | Main abstraction interface |
| IOrderGroupPersistence.cs | `specs/003-order-group-management/contracts/IOrderGroupPersistence.cs` | Persistence abstraction |
| quickstart.md | `specs/003-order-group-management/quickstart.md` | Usage examples and patterns |

## Next Steps

Run `/speckit.tasks` to generate the implementation task list from this plan.

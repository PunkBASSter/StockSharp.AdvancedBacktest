# Implementation Plan: Isolated Order Groups with Split Position Management

**Branch**: `004-isolated-order-groups` | **Date**: 2025-12-15 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/004-isolated-order-groups/spec.md`

## Summary

Implement isolated order groups to enable multiple concurrent positions with independent SL/TP management. Key capabilities:
- Multiple concurrent order groups (default limit: 5)
- Split exit orders with multiple protective pairs per entry (e.g., 50% at TP1, 50% at TP2)
- Auxiliary timeframe (5-min) for more granular protection level checking during backtests
- Unified debug mode abstraction supporting both AI and human debug simultaneously
- Reusable, strategy-agnostic position management APIs

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: StockSharp (Algo, BusinessEntities, Messages), Microsoft.Extensions.Options
**Storage**: N/A (in-memory state management)
**Testing**: xUnit v3 with Microsoft.NET.Test.Sdk
**Target Platform**: Windows (StockSharp primary support)
**Project Type**: Multi-assembly solution (Core + Infrastructure)
**Performance Goals**: Handle up to 5 concurrent order groups with negligible overhead; auxiliary TF adds 12x more candle processing
**Constraints**: All financial calculations must use decimal; auxiliary TF must be completely invisible in outputs
**Scale/Scope**: Per-strategy state management; typical backtest runs 1000s of candles

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Separation of Concerns | PASS | Core entities (OrderRegistry, OrderRequest, ProtectivePair, EntryOrderGroup) in Core assembly; DebugModeProvider in Infrastructure |
| II. Test-First Development | PENDING | Tests to be written before implementation per TDD |
| III. Financial Precision | PASS | All prices/volumes use decimal type |
| IV. Composition Over Inheritance | PASS | Order groups composed of protective pairs; no inheritance hierarchies |
| V. Explicit Visibility | PENDING | Will enforce during implementation |
| VI. System.Text.Json Standard | PASS | No JSON serialization needed for core entities |
| VII. End-to-End Testability | PASS | All components testable with mock data; no external dependencies |

**Dependency Rule**:
- OrderRegistry, OrderRequest, ProtectivePair, EntryOrderGroup, OrderPositionManager → Core assembly
- DebugModeProvider, TimestampRemapper, IDebugModeOutput → Infrastructure assembly
- Infrastructure depends on Core; Core MUST NOT depend on Infrastructure ✅

## Project Structure

### Documentation (this feature)

```text
specs/004-isolated-order-groups/
├── plan.md              # This file
├── research.md          # Phase 0 output - design decisions
├── data-model.md        # Phase 1 output - entity definitions
├── quickstart.md        # Phase 1 output - usage examples
├── contracts/           # Phase 1 output - API contracts
│   ├── order-management-api.md
│   └── debug-mode-api.md
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
StockSharp.AdvancedBacktest.Core/
├── OrderManagement/
│   ├── OrderRequest.cs           # Entry order + protective pairs input
│   ├── ProtectivePair.cs         # SL/TP/volume specification
│   ├── EntryOrderGroup.cs        # Order group with state machine
│   ├── OrderGroupState.cs        # State enum
│   ├── ProtectivePairOrders.cs   # Actual SL/TP orders
│   ├── OrderRegistry.cs          # Central registry for order groups
│   └── OrderPositionManager.cs   # Orchestration (existing, to be refactored)

StockSharp.AdvancedBacktest.Infrastructure/
├── DebugMode/
│   ├── DebugModeProvider.cs      # Unified debug mode abstraction
│   ├── IDebugModeOutput.cs       # Output interface
│   └── TimestampRemapper.cs      # Auxiliary TF timestamp remapping

StockSharp.AdvancedBacktest.Core.Tests/
├── OrderManagement/
│   ├── OrderRegistryTests.cs
│   ├── OrderRequestTests.cs
│   ├── EntryOrderGroupTests.cs
│   └── OrderPositionManagerTests.cs (existing, to be extended)

StockSharp.AdvancedBacktest.Infrastructure.Tests/
├── DebugMode/
│   ├── DebugModeProviderTests.cs
│   └── TimestampRemapperTests.cs
```

**Structure Decision**: Follows existing Core/Infrastructure decomposition. Order management entities go in Core (business logic). Debug mode infrastructure goes in Infrastructure (operational concerns).

## Complexity Tracking

No constitution violations requiring justification.

## Design Artifacts

### Phase 0: Research

See [research.md](./research.md) for:
- Order group state machine design (4-state model)
- Multiple protective pairs per entry (volume validation)
- Auxiliary timeframe subscription pattern (invisible 5-min subscription)
- Timestamp remapping for auxiliary TF events
- Unified debug mode abstraction
- Concurrent order group limit (configurable, default 5)
- Pessimistic SL/TP trigger order

### Phase 1: Design & Contracts

See [data-model.md](./data-model.md) for entity definitions:
- OrderRequest (record)
- ProtectivePair (record)
- EntryOrderGroup (class with state machine)
- ProtectivePairOrders (class)
- OrderRegistry (class)
- OrderGroupState (enum)
- TimestampRemapper (static class)
- DebugModeProvider (class)
- IDebugModeOutput (interface)

See [contracts/order-management-api.md](./contracts/order-management-api.md) for:
- OrderRegistry API (RegisterGroup, GetActiveGroups, GetGroupById, FindMatchingGroup, Reset)
- EntryOrderGroup API (TransitionTo, GetProtectivePairs, RemovePair)
- OrderPositionManager API (HandleOrderRequest, CheckProtectionLevels, OnOwnTradeReceived, CloseAllPositions, Reset)

See [contracts/debug-mode-api.md](./contracts/debug-mode-api.md) for:
- DebugModeProvider API (Initialize, CaptureEvent, CaptureCandle, CaptureIndicator, CaptureTrade, Cleanup)
- TimestampRemapper API (RemapToMainTimeframe)
- IDebugModeOutput interface

See [quickstart.md](./quickstart.md) for usage examples:
- Creating OrderRequest with multiple protective pairs
- Using OrderPositionManager
- Enabling concurrent positions
- Checking for duplicate signals (entry price + SL + TP matching)
- Handling entry cancellation

## Recent Clarifications (2025-12-15)

Key design decisions from latest clarification session:

1. **Duplicate Signal Detection**: Match by entry price + SL + TP (all three values must match within price step tolerances). Changed `FindSimilarGroup(price, tolerance)` to `FindMatchingGroup(entryPrice, slPrice, tpPrice, tolerance)`.

2. **API Reusability**: All position management components (OrderRegistry, OrderPositionManager, OrderRequest, ProtectivePair) MUST be designed as reusable, strategy-agnostic APIs. Added FR-016 and SC-010.

3. **Deletion-Based Tracking**: Registry only contains active orders - no `IsClosed` flags. When a protective pair is closed, it's removed from the dictionary via `RemovePair(pairId)`.

4. **Auxiliary TF Visibility**: Completely invisible in all outputs. Events triggered by auxiliary TF are attributed to parent main TF candle for display using `TimestampRemapper.RemapToMainTimeframe()`.

## Next Steps

Run `/speckit.tasks` to generate implementation task list.

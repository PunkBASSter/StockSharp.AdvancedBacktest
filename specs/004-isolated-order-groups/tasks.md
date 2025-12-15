# Tasks: Isolated Order Groups with Split Position Management

**Input**: Design documents from `/specs/004-isolated-order-groups/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: TDD approach per constitution - tests written first for all user stories

**Organization**: Tasks grouped by user story to enable independent implementation and testing

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure (consolidated design):
- **Core entities**: `StockSharp.AdvancedBacktest.Core/OrderManagement/`
  - `OrderRegistry.cs` - Contains OrderGroupState enum, EntryOrderGroup record, OrderRegistry class
  - `OrderRequest.cs` - Contains ProtectivePair record, OrderRequest record
  - `OrderPositionManager.cs` - Main orchestration class
  - `IStrategyOrderOperations.cs` - Minimal interface (PlaceOrder, CancelOrder)
- **Infrastructure**: `StockSharp.AdvancedBacktest.Infrastructure/DebugMode/`
- **Core tests**: `StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/`
- **Infrastructure tests**: `StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/`
- **Integration tests**: `StockSharp.AdvancedBacktest.Tests/OrderManagement/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project structure and foundational types

- [x] T001 Create OrderGroupState enum in OrderRegistry.cs
- [x] T002 Create ProtectivePair record in OrderRequest.cs
- [x] T003 Create EntryOrderGroup record with Matches() in OrderRegistry.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Create OrderRequest record in OrderRequest.cs
- [x] T005 Create IStrategyOrderOperations interface (PlaceOrder, CancelOrder only) in IStrategyOrderOperations.cs
- [x] T006 Create OrderRegistry class with RegisterGroup, GetActiveGroups, FindMatchingGroup, FindGroupByOrder, Reset in OrderRegistry.cs

**Checkpoint**: ✅ Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Auxiliary Timeframe for Order Maintenance (Priority: P1) 🎯 MVP

**Goal**: Enable order/position maintenance at configurable intervals (default: 5 minutes) independent of main strategy timeframe

**Independent Test**: Run backtest with hourly candles and verify order maintenance executes at 5-minute intervals

> **⚠️ CRITICAL DESIGN REQUIREMENT**: Auxiliary timeframe MUST be configurable via `CustomStrategyBase` property (not hardcoded). The launcher (`Program.cs`) MUST explicitly set the auxiliary TF value (e.g., 5 minutes) when configuring the strategy. This ensures strategy reusability across different timeframe combinations.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T007 [P] [US1] Unit test for TimestampRemapper.RemapToMainTimeframe() in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/TimestampRemapperTests.cs
- [x] T008 [P] [US1] Unit test for DebugModeProvider auxiliary TF filtering in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/DebugModeProviderTests.cs
- [x] T009 [P] [US1] Unit test for DebugModeProvider simultaneous AI+human debug in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/DebugModeProviderTests.cs
- [x] T009a [P] [US1] Unit test for CustomStrategyBase.AuxiliaryTimeframe property configuration in StockSharp.AdvancedBacktest.Core.Tests/Strategies/CustomStrategyBaseTests.cs

### Implementation for User Story 1

- [x] T010 [P] [US1] Implement TimestampRemapper static class in StockSharp.AdvancedBacktest.Infrastructure/DebugMode/TimestampRemapper.cs
- [x] T011 [P] [US1] Create IDebugModeOutput interface in StockSharp.AdvancedBacktest.Infrastructure/DebugMode/IDebugModeOutput.cs
- [x] T012 [US1] Implement DebugModeProvider class with auxiliary TF filtering in StockSharp.AdvancedBacktest.Infrastructure/DebugMode/DebugModeProvider.cs
- [x] T013 [US1] Add CaptureCandle, CaptureIndicator, CaptureTrade methods to DebugModeProvider
- [x] T014 [US1] Add configurable AuxiliaryTimeframe property to CustomStrategyBase (TimeSpan?, default: null = disabled) in StockSharp.AdvancedBacktest.Core/Strategies/CustomStrategyBase.cs
- [x] T014a [US1] Implement auxiliary TF candle subscription creation in CustomStrategyBase.OnStarted2() when AuxiliaryTimeframe is set
- [x] T014b [US1] Integrate DebugModeProvider with CustomStrategyBase for auxiliary TF event filtering
- [x] T014c [US1] Update Program.cs to explicitly set strategy.AuxiliaryTimeframe = TimeSpan.FromMinutes(5) in StockSharp.AdvancedBacktest.LauncherTemplate/Program.cs

**Checkpoint**: Auxiliary TF infrastructure complete - configurable, invisible in all outputs, timestamps remapped correctly

---

## Phase 4: User Story 2 - Order Group Registry and State Tracking (Priority: P1)

**Goal**: Centralized registry tracking all order groups with state machine and query capabilities

**Independent Test**: Register multiple order groups and query/filter by state, price levels, volume

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T015 [P] [US2] Unit test for OrderRequest validation (volume sum, price direction) in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRequestTests.cs
- [x] T016 [P] [US2] Unit test for EntryOrderGroup.Matches() in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs
- [x] T018 [P] [US2] Unit test for OrderRegistry.RegisterGroup() in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs
- [x] T019 [P] [US2] Unit test for OrderRegistry.GetActiveGroups() in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs
- [x] T020 [P] [US2] Unit test for OrderRegistry.FindMatchingGroup() with OrderRequest matching in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs
- [x] T021 [P] [US2] Unit test for OrderRegistry concurrent group limit in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs

### Implementation for User Story 2

- [x] T025 [US2] Implement OrderRegistry.RegisterGroup() with concurrent limit check in OrderRegistry.cs
- [x] T026 [US2] Implement OrderRegistry.GetActiveGroups() and FindGroupByOrder() in OrderRegistry.cs
- [x] T027 [US2] Implement OrderRegistry.FindMatchingGroup() accepting OrderRequest in OrderRegistry.cs
- [x] T028 [US2] Implement OrderRegistry.Reset() in OrderRegistry.cs

**Checkpoint**: OrderRegistry fully functional - can register, query, and match order groups

---

## Phase 5: User Story 3 - Multiple Concurrent Positions (Priority: P2)

**Goal**: Support multiple simultaneous positions with independent SL/TP management

**Independent Test**: Generate multiple sequential signals, verify each creates independent order group

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T029 [P] [US3] Unit test for OrderPositionManager.HandleOrderRequest() creates new group in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T030 [P] [US3] Unit test for OrderPositionManager handles multiple concurrent groups in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T031 [P] [US3] Unit test for OrderPositionManager.CheckProtectionLevels() closes only triggered group in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T032 [P] [US3] Unit test for duplicate signal detection skips existing match in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T032a [P] [US3] Unit test for entry order expiration cancels all associated protective orders in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs

### Implementation for User Story 3

- [x] T033 [US3] Refactor OrderPositionManager to use OrderRegistry (constructor takes strategy, security, strategyName) in OrderPositionManager.cs
- [x] T034 [US3] Implement HandleOrderRequest() returning Order? for caller to register, with duplicate detection using FindMatchingGroup() in OrderPositionManager.cs
- [x] T034a [US3] Handle entry order expiration - cancel all associated protective orders in group and transition to Closed in OrderPositionManager.cs
- [x] T035 [US3] Update CheckProtectionLevels() to iterate over all active groups in OrderPositionManager.cs
- [x] T036 [US3] Implement OnOwnTradeReceived() to update correct group state in OrderPositionManager.cs
- [x] T037 [US3] Add pessimistic SL/TP trigger order (SL first when both hit) in OrderPositionManager.cs
- [x] T038 [US3] Update Reset() to clear OrderRegistry in OrderPositionManager.cs

**Checkpoint**: Multiple concurrent positions work - each group independently managed

---

## Phase 6: User Story 4 - Split Exit Orders with Multiple SL/TP Pairs (Priority: P2)

**Goal**: Support multiple protective pairs per entry for partial exit strategies

**Independent Test**: Enter position with volume 1.0, define two pairs (0.5 each), verify partial exits work correctly

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T039 [P] [US4] Unit test for multiple protective pairs creation from OrderRequest in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs
- [x] T040 [P] [US4] Unit test for partial exit cancels only corresponding SL in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T041 [P] [US4] Unit test for pair removal when one pair closes in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T042 [P] [US4] Unit test for group transitions to Closed when all pairs removed in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T042a [P] [US4] Unit test for protective order type configuration (limit vs market) in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRequestTests.cs

### Implementation for User Story 4

- [x] T043 [US4] EntryOrderGroup stores multiple ProtectivePairs as Dictionary in OrderRegistry.cs
- [x] T044 [US4] Implement protective order placement for multiple pairs in OrderPositionManager.cs
- [x] T044a [US4] Add protective order type configuration (limit vs market) to ProtectivePair - supports FR-009 in OrderRequest.cs
- [x] T045 [US4] Implement cancel-opposing-order logic when one pair fills in OrderPositionManager.cs
- [x] T046 [US4] Update CheckProtectionLevels() to check all pairs in each group in OrderPositionManager.cs
- [x] T047 [US4] Auto-transition group to Closed when all pairs removed in OrderPositionManager.cs

**Checkpoint**: Split exits work - partial positions close correctly with proper cancellation

---

## Phase 7: User Story 5 - Partial Fill Handling with Market Close Retry (Priority: P3)

**Goal**: Handle partial fills gracefully with market order retries

**Independent Test**: Simulate partial fill, verify retry mechanism attempts market close up to 5 times

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T048 [P] [US5] Unit test for partial fill triggers market order for remaining volume in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T049 [P] [US5] Unit test for market order retry up to 5 times in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T050 [P] [US5] Unit test for 5th retry failure logs error in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs
- [x] T051 [P] [US5] Unit test for successful retry properly closes group in StockSharp.AdvancedBacktest.Tests/OrderManagement/OrderPositionManagerTests.cs

### Implementation for User Story 5

- [x] T052 [US5] Add partial fill detection in OnOwnTradeReceived() in OrderPositionManager.cs
- [x] T053 [US5] Implement market order placement for remaining volume in OrderPositionManager.cs
- [x] T054 [US5] Implement retry counter and logic (max 5 attempts) in OrderPositionManager.cs
- [x] T055 [US5] Add error logging and manual intervention flag after 5 failures in OrderPositionManager.cs
- [x] T056 [US5] Ensure proper group closure after successful retry in OrderPositionManager.cs

**Checkpoint**: ✅ Partial fills handled gracefully - no orphaned positions

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Integration, cleanup, and validation across all stories

- [x] T057 Update ZigZagBreakoutStrategy to use new OrderPositionManager API (constructor, HandleOrderRequest returns Order?) in ZigZagBreakoutStrategy.cs
- [x] T058 Update CustomStrategyBase to implement IStrategyOrderOperations with PlaceOrder method in CustomStrategyBase.cs
- [x] T059 [P] Add state transition logging in EntryOrderGroup for debugging (FR-012)
- [x] T060 [P] Add order event logging in OrderPositionManager for debugging (FR-012)
- [x] T061 Run quickstart.md validation - verify all usage examples work
- [x] T062 Verify auxiliary TF is invisible in all outputs (SC-005)
- [x] T063 Verify position management APIs are strategy-agnostic (SC-010)

**Checkpoint**: ✅ All polish tasks complete - feature ready for integration

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately ✅ COMPLETE
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories ✅ COMPLETE
- **User Story 1 (Phase 3)**: Depends on Foundational - Infrastructure for auxiliary TF ✅ COMPLETE
- **User Story 2 (Phase 4)**: Depends on Foundational - Core registry and state tracking ✅ IMPLEMENTATION COMPLETE (tests pending)
- **User Story 3 (Phase 5)**: Depends on US1 + US2 - Multiple concurrent positions ✅ IMPLEMENTATION COMPLETE (some tests pending)
- **User Story 4 (Phase 6)**: Depends on US2 + US3 - Split exits build on concurrent positions (partial)
- **User Story 5 (Phase 7)**: Depends on US3 + US4 - Partial fills need position management in place
- **Polish (Phase 8)**: Depends on all user stories being complete (partial)

### Current Status Summary

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1 (Setup) | ✅ Complete | Consolidated into OrderRegistry.cs, OrderRequest.cs |
| Phase 2 (Foundation) | ✅ Complete | IStrategyOrderOperations simplified to PlaceOrder/CancelOrder |
| Phase 3 (US1) | ✅ Complete | Auxiliary TF subscription, OnAuxiliaryCandle(), DebugModeProvider filtering |
| Phase 4 (US2) | ✅ Complete | Implementation + unit tests done |
| Phase 5 (US3) | ✅ Complete | Implementation + tests done |
| Phase 6 (US4) | ✅ Complete | Implementation + tests done |
| Phase 7 (US5) | ✅ Complete | Partial fill handling with market close retry |
| Phase 8 (Polish) | ✅ Complete | All polish and validation tasks done |

---

## Summary

| Metric | Count |
|--------|-------|
| **Total Tasks** | ~55 (reduced from 71 due to consolidation) |
| **Completed** | 55 |
| **In Progress** | 0 |
| **Pending** | 0 |

**Feature Status**: ✅ COMPLETE - All phases implemented and tested

---

## Notes

- Design consolidated: OrderGroupState, EntryOrderGroup moved into OrderRegistry.cs
- IStrategyOrderOperations simplified to minimal interface (PlaceOrder, CancelOrder only)
- HandleOrderRequest returns Order? - caller (strategy) is responsible for RegisterOrder
- Constructor pattern: OrderPositionManager(IStrategyOrderOperations strategy, Security security, string strategyName)
- Tolerance parameter made optional with sensible default (0.00000001m)

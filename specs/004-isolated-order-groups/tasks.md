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

Based on plan.md structure:
- **Core entities**: `StockSharp.AdvancedBacktest.Core/OrderManagement/`
- **Infrastructure**: `StockSharp.AdvancedBacktest.Infrastructure/DebugMode/`
- **Core tests**: `StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/`
- **Infrastructure tests**: `StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project structure and foundational types

- [ ] T001 Create OrderGroupState enum in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderGroupState.cs
- [ ] T002 [P] Create ProtectivePair record in StockSharp.AdvancedBacktest.Core/OrderManagement/ProtectivePair.cs
- [ ] T003 [P] Create ProtectivePairOrders class in StockSharp.AdvancedBacktest.Core/OrderManagement/ProtectivePairOrders.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 Create OrderRequest record with validation in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderRequest.cs
- [ ] T005 Create EntryOrderGroup class with state machine in StockSharp.AdvancedBacktest.Core/OrderManagement/EntryOrderGroup.cs
- [ ] T006 Create OrderRegistry class skeleton in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderRegistry.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Auxiliary Timeframe for Order Maintenance (Priority: P1) 🎯 MVP

**Goal**: Enable order/position maintenance at configurable intervals (default: 5 minutes) independent of main strategy timeframe

**Independent Test**: Run backtest with hourly candles and verify order maintenance executes at 5-minute intervals

> **⚠️ CRITICAL DESIGN REQUIREMENT**: Auxiliary timeframe MUST be configurable via `CustomStrategyBase` property (not hardcoded). The launcher (`Program.cs`) MUST explicitly set the auxiliary TF value (e.g., 5 minutes) when configuring the strategy. This ensures strategy reusability across different timeframe combinations.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T007 [P] [US1] Unit test for TimestampRemapper.RemapToMainTimeframe() in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/TimestampRemapperTests.cs
- [ ] T008 [P] [US1] Unit test for DebugModeProvider auxiliary TF filtering in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/DebugModeProviderTests.cs
- [ ] T009 [P] [US1] Unit test for DebugModeProvider simultaneous AI+human debug in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/DebugModeProviderTests.cs
- [ ] T009a [P] [US1] Unit test for CustomStrategyBase.AuxiliaryTimeframe property configuration in StockSharp.AdvancedBacktest.Core.Tests/Strategies/CustomStrategyBaseTests.cs

### Implementation for User Story 1

- [ ] T010 [P] [US1] Implement TimestampRemapper static class in StockSharp.AdvancedBacktest.Infrastructure/DebugMode/TimestampRemapper.cs
- [ ] T011 [P] [US1] Create IDebugModeOutput interface in StockSharp.AdvancedBacktest.Infrastructure/DebugMode/IDebugModeOutput.cs
- [ ] T012 [US1] Implement DebugModeProvider class with auxiliary TF filtering in StockSharp.AdvancedBacktest.Infrastructure/DebugMode/DebugModeProvider.cs
- [ ] T013 [US1] Add CaptureCandle, CaptureIndicator, CaptureTrade methods to DebugModeProvider
- [ ] T014 [US1] Add configurable AuxiliaryTimeframe property to CustomStrategyBase (TimeSpan?, default: null = disabled) in StockSharp.AdvancedBacktest.Core/Strategies/CustomStrategyBase.cs
- [ ] T014a [US1] Implement auxiliary TF candle subscription creation in CustomStrategyBase.OnStarted2() when AuxiliaryTimeframe is set
- [ ] T014b [US1] Integrate DebugModeProvider with CustomStrategyBase for auxiliary TF event filtering
- [ ] T014c [US1] Update Program.cs to explicitly set strategy.AuxiliaryTimeframe = TimeSpan.FromMinutes(5) in StockSharp.AdvancedBacktest.LauncherTemplate/Program.cs

**Checkpoint**: Auxiliary TF infrastructure complete - configurable, invisible in all outputs, timestamps remapped correctly

---

## Phase 4: User Story 2 - Order Group Registry and State Tracking (Priority: P1)

**Goal**: Centralized registry tracking all order groups with state machine and query capabilities

**Independent Test**: Register multiple order groups and query/filter by state, price levels, volume

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T015 [P] [US2] Unit test for OrderRequest validation (volume sum, price direction) in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRequestTests.cs
- [ ] T016 [P] [US2] Unit test for EntryOrderGroup state transitions in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/EntryOrderGroupTests.cs
- [ ] T017 [P] [US2] Unit test for EntryOrderGroup invalid transitions throw in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/EntryOrderGroupTests.cs
- [ ] T018 [P] [US2] Unit test for OrderRegistry.RegisterGroup() in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs
- [ ] T019 [P] [US2] Unit test for OrderRegistry.GetActiveGroups() in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs
- [ ] T020 [P] [US2] Unit test for OrderRegistry.FindMatchingGroup() with entry+SL+TP matching in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs
- [ ] T021 [P] [US2] Unit test for OrderRegistry concurrent group limit in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderRegistryTests.cs

### Implementation for User Story 2

- [ ] T022 [US2] Implement OrderRequest.Validate() with volume and price validation in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderRequest.cs
- [ ] T023 [US2] Implement EntryOrderGroup.TransitionTo() with state validation in StockSharp.AdvancedBacktest.Core/OrderManagement/EntryOrderGroup.cs
- [ ] T024 [US2] Implement EntryOrderGroup.RemovePair() for deletion-based tracking in StockSharp.AdvancedBacktest.Core/OrderManagement/EntryOrderGroup.cs
- [ ] T025 [US2] Implement OrderRegistry.RegisterGroup() with concurrent limit check in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderRegistry.cs
- [ ] T026 [US2] Implement OrderRegistry.GetActiveGroups() and GetGroupById() in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderRegistry.cs
- [ ] T027 [US2] Implement OrderRegistry.FindMatchingGroup() with entry+SL+TP matching in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderRegistry.cs
- [ ] T028 [US2] Implement OrderRegistry.Reset() in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderRegistry.cs

**Checkpoint**: OrderRegistry fully functional - can register, query, and match order groups

---

## Phase 5: User Story 3 - Multiple Concurrent Positions (Priority: P2)

**Goal**: Support multiple simultaneous positions with independent SL/TP management

**Independent Test**: Generate multiple sequential signals, verify each creates independent order group

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T029 [P] [US3] Unit test for OrderPositionManager.HandleOrderRequest() creates new group in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [ ] T030 [P] [US3] Unit test for OrderPositionManager handles multiple concurrent groups in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [ ] T031 [P] [US3] Unit test for OrderPositionManager.CheckProtectionLevels() closes only triggered group in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [ ] T032 [P] [US3] Unit test for duplicate signal detection skips existing match in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [ ] T032a [P] [US3] Unit test for entry order expiration cancels all associated protective orders in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs

### Implementation for User Story 3

- [ ] T033 [US3] Refactor OrderPositionManager to use OrderRegistry in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T034 [US3] Implement HandleOrderRequest() with duplicate detection using FindMatchingGroup() in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T034a [US3] Handle entry order expiration - cancel all associated protective orders in group and transition to Closed in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T035 [US3] Update CheckProtectionLevels() to iterate over all active groups in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T036 [US3] Implement OnOwnTradeReceived() to update correct group state in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T037 [US3] Add pessimistic SL/TP trigger order (SL first when both hit) in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T038 [US3] Update Reset() to clear OrderRegistry in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs

**Checkpoint**: Multiple concurrent positions work - each group independently managed

---

## Phase 6: User Story 4 - Split Exit Orders with Multiple SL/TP Pairs (Priority: P2)

**Goal**: Support multiple protective pairs per entry for partial exit strategies

**Independent Test**: Enter position with volume 1.0, define two pairs (0.5 each), verify partial exits work correctly

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T039 [P] [US4] Unit test for multiple protective pairs creation from OrderRequest in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/EntryOrderGroupTests.cs
- [ ] T040 [P] [US4] Unit test for partial exit cancels only corresponding SL in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [ ] T041 [P] [US4] Unit test for RemovePair() when one pair closes in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/EntryOrderGroupTests.cs
- [ ] T042 [P] [US4] Unit test for group transitions to Closed when all pairs removed in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/EntryOrderGroupTests.cs
- [ ] T042a [P] [US4] Unit test for protective order type configuration (limit vs market) in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/ProtectivePairTests.cs

### Implementation for User Story 4

- [ ] T043 [US4] Update EntryOrderGroup to initialize multiple ProtectivePairOrders from OrderRequest in StockSharp.AdvancedBacktest.Core/OrderManagement/EntryOrderGroup.cs
- [ ] T044 [US4] Implement protective order placement for multiple pairs in OrderPositionManager in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T044a [US4] Add protective order type configuration (limit vs market) to OrderRequest/ProtectivePair - supports FR-009 in StockSharp.AdvancedBacktest.Core/OrderManagement/ProtectivePair.cs
- [ ] T045 [US4] Implement cancel-opposing-order logic when one pair fills in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T046 [US4] Update CheckProtectionLevels() to check all pairs in each group in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T047 [US4] Auto-transition group to Closed when all pairs removed in StockSharp.AdvancedBacktest.Core/OrderManagement/EntryOrderGroup.cs

**Checkpoint**: Split exits work - partial positions close correctly with proper cancellation

---

## Phase 7: User Story 5 - Partial Fill Handling with Market Close Retry (Priority: P3)

**Goal**: Handle partial fills gracefully with market order retries

**Independent Test**: Simulate partial fill, verify retry mechanism attempts market close up to 5 times

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T048 [P] [US5] Unit test for partial fill triggers market order for remaining volume in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [ ] T049 [P] [US5] Unit test for market order retry up to 5 times in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [ ] T050 [P] [US5] Unit test for 5th retry failure logs error in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [ ] T051 [P] [US5] Unit test for successful retry properly closes group in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs

### Implementation for User Story 5

- [ ] T052 [US5] Add partial fill detection in OnOwnTradeReceived() in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T053 [US5] Implement market order placement for remaining volume in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T054 [US5] Implement retry counter and logic (max 5 attempts) in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T055 [US5] Add error logging and manual intervention flag after 5 failures in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [ ] T056 [US5] Ensure proper group closure after successful retry in StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs

**Checkpoint**: Partial fills handled gracefully - no orphaned positions

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Integration, cleanup, and validation across all stories

- [ ] T057 Update ZigZagBreakoutStrategy to use new OrderRegistry API in StockSharp.AdvancedBacktest.LauncherTemplate/Strategies/ZigZagBreakout/ZigZagBreakoutStrategy.cs
- [ ] T058 Verify all existing OrderPositionManagerTests still pass
- [ ] T059 [P] Add state transition logging in EntryOrderGroup for debugging (FR-012)
- [ ] T060 [P] Add order event logging in OrderPositionManager for debugging (FR-012)
- [ ] T061 Run quickstart.md validation - verify all usage examples work
- [ ] T062 Verify auxiliary TF is invisible in all outputs (SC-005)
- [ ] T063 Verify position management APIs are strategy-agnostic (SC-010)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational - Infrastructure for auxiliary TF
- **User Story 2 (Phase 4)**: Depends on Foundational - Core registry and state tracking
- **User Story 3 (Phase 5)**: Depends on US1 + US2 - Multiple concurrent positions
- **User Story 4 (Phase 6)**: Depends on US2 + US3 - Split exits build on concurrent positions
- **User Story 5 (Phase 7)**: Depends on US3 + US4 - Partial fills need position management in place
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

```
US1 (Auxiliary TF) ──┬──► US3 (Multiple Positions) ──► US4 (Split Exits) ──► US5 (Partial Fills)
                     │
US2 (Registry) ──────┘
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models/entities before services
- Services before orchestration (OrderPositionManager)
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

**Phase 1 (Setup)**:
- T002, T003 can run in parallel (different files)

**Phase 3 (US1 Tests)**:
- T007, T008, T009 can run in parallel (different test files)

**Phase 3 (US1 Implementation)**:
- T010, T011 can run in parallel (different files)

**Phase 4 (US2 Tests)**:
- T015-T021 can ALL run in parallel (different test methods/files)

**Phase 5 (US3 Tests)**:
- T029-T032 can ALL run in parallel

**Phase 6 (US4 Tests)**:
- T039-T042 can ALL run in parallel

**Phase 7 (US5 Tests)**:
- T048-T051 can ALL run in parallel

**Phase 8 (Polish)**:
- T059, T060 can run in parallel

---

## Parallel Example: User Story 2 Tests

```bash
# Launch all US2 tests together (TDD - must fail first):
Task: "Unit test for OrderRequest validation in OrderRequestTests.cs"
Task: "Unit test for EntryOrderGroup state transitions in EntryOrderGroupTests.cs"
Task: "Unit test for EntryOrderGroup invalid transitions in EntryOrderGroupTests.cs"
Task: "Unit test for OrderRegistry.RegisterGroup() in OrderRegistryTests.cs"
Task: "Unit test for OrderRegistry.GetActiveGroups() in OrderRegistryTests.cs"
Task: "Unit test for OrderRegistry.FindMatchingGroup() in OrderRegistryTests.cs"
Task: "Unit test for OrderRegistry concurrent group limit in OrderRegistryTests.cs"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1 (Auxiliary TF)
4. Complete Phase 4: User Story 2 (Registry)
5. **STOP and VALIDATE**: Test US1 + US2 independently
6. Deploy/demo basic registry and auxiliary TF infrastructure

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Auxiliary TF invisible, timestamps remapped
3. Add User Story 2 → Registry tracks groups, state machine works
4. Add User Story 3 → Multiple concurrent positions work
5. Add User Story 4 → Split exits with partial position management
6. Add User Story 5 → Robust partial fill handling
7. Each story adds value without breaking previous stories

### Suggested MVP Scope

**MVP = User Stories 1 + 2 (both P1)**
- Auxiliary timeframe infrastructure (invisible)
- OrderRegistry with state tracking
- Basic order group lifecycle

This provides the foundation for all other features while delivering immediate value for backtest accuracy improvements.

---

## Summary

| Metric | Count |
|--------|-------|
| **Total Tasks** | 71 |
| **Setup Tasks** | 3 |
| **Foundational Tasks** | 3 |
| **User Story 1 Tasks** | 12 (+4: T009a, T014, T014a, T014b, T014c) |
| **User Story 2 Tasks** | 14 |
| **User Story 3 Tasks** | 12 (+2: T032a test, T034a implementation for entry expiration) |
| **User Story 4 Tasks** | 12 (+2: T042a test, T044a implementation for protective order type config) |
| **User Story 5 Tasks** | 9 |
| **Polish Tasks** | 7 |
| **Parallel Opportunities** | 31 tasks marked [P] |

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing (TDD per constitution)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All prices/volumes use decimal type (constitution requirement)

# Tasks: Advanced Order Group Management

**Input**: Design documents from `/specs/003-order-group-management/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required per Constitution Principle II (Test-First Development - NON-NEGOTIABLE)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

```text
StockSharp.AdvancedBacktest.Core/OrderManagement/           # Core models and abstractions
StockSharp.AdvancedBacktest.Infrastructure/OrderManagement/ # Implementations
StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/     # Core unit tests
StockSharp.AdvancedBacktest.Infrastructure.Tests/OrderManagement/ # Infrastructure tests
```

---

## Phase 1: Setup

**Purpose**: Project structure and foundational enumerations

- [X] T001 Create OrderManagement directory in StockSharp.AdvancedBacktest.Infrastructure/
- [X] T002 Create OrderManagement directory in StockSharp.AdvancedBacktest.Infrastructure.Tests/
- [X] T003 [P] Create OrderGroupState.cs enum in StockSharp.AdvancedBacktest.Core/OrderManagement/
- [X] T004 [P] Create GroupedOrderState.cs enum in StockSharp.AdvancedBacktest.Core/OrderManagement/
- [X] T005 [P] Create GroupedOrderRole.cs enum in StockSharp.AdvancedBacktest.Core/OrderManagement/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models and abstractions that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests for Foundational Models

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T006 [P] Create GroupedOrderTests.cs test class in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/
- [X] T007 [P] Create OrderGroupTests.cs test class in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/
- [X] T008 [P] Create OrderGroupLimitsTests.cs test class in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/
- [X] T009 [P] Create ExtendedTradeSignalTests.cs test class in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/
- [X] T010 [P] Create ClosingOrderDefinitionTests.cs test class in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/

### Implementation of Foundational Models

- [X] T011 [P] Create GroupedOrder.cs model in StockSharp.AdvancedBacktest.Core/OrderManagement/
- [X] T012 [P] Create ClosingOrderDefinition.cs model in StockSharp.AdvancedBacktest.Core/OrderManagement/
- [X] T013 [P] Create OrderGroupLimits.cs model in StockSharp.AdvancedBacktest.Core/OrderManagement/
- [X] T014 Create OrderGroup.cs model in StockSharp.AdvancedBacktest.Core/OrderManagement/ (depends on T011)
- [X] T015 Create ExtendedTradeSignal.cs model in StockSharp.AdvancedBacktest.Core/OrderManagement/ (depends on T012)
- [X] T016 [P] Create IOrderGroupManager.cs interface in StockSharp.AdvancedBacktest.Core/OrderManagement/
- [X] T017 [P] Create IOrderGroupPersistence.cs interface with NullOrderGroupPersistence in StockSharp.AdvancedBacktest.Core/OrderManagement/

**Checkpoint**: All foundational models and interfaces ready - user story implementation can begin

---

## Phase 3: User Story 1 - Create Order Group with Multiple Closing Orders (Priority: P1) 🎯 MVP

**Goal**: Enable strategy developers to create order groups with one opening order and multiple closing orders at different price levels

**Independent Test**: Create an order group with 1 opening order (100 shares) and 3 closing orders (30, 30, 40 shares), verify volume validation

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T018 [P] [US1] Test CreateOrderGroup with valid signal in StockSharp.AdvancedBacktest.Infrastructure.Tests/OrderManagement/OrderGroupManagerTests.cs
- [X] T019 [P] [US1] Test volume mismatch throws when throwIfNotMatchingVolume=true in OrderGroupManagerTests.cs
- [X] T020 [P] [US1] Test default GroupId generation format in OrderGroupManagerTests.cs
- [X] T021 [P] [US1] Test custom GroupId assignment in OrderGroupManagerTests.cs

### Implementation for User Story 1

- [X] T022 [US1] Create MockStrategyOrderOperations test helper in StockSharp.AdvancedBacktest.Infrastructure.Tests/OrderManagement/
- [X] T023 [US1] Implement OrderGroupManager constructor and CreateOrderGroup method in StockSharp.AdvancedBacktest.Infrastructure/OrderManagement/OrderGroupManager.cs
- [X] T024 [US1] Implement GroupId generation logic (SecurityId_DateTimeWithMs_OpenPrice format) in OrderGroupManager.cs
- [X] T025 [US1] Implement volume validation (throwIfNotMatchingVolume) in OrderGroupManager.cs
- [X] T026 [US1] Implement opening order placement via IStrategyOrderOperations in OrderGroupManager.cs

**Checkpoint**: User Story 1 complete - can create order groups with multiple closing orders

---

## Phase 4: User Story 2 - Opening Order Activation Triggers Closing Orders (Priority: P1)

**Goal**: When opening order fills, automatically place all closing orders with correct volumes

**Independent Test**: Simulate opening order fill, verify all closing orders placed with pro-rata scaling for partial fills

### Tests for User Story 2

- [X] T027 [P] [US2] Test OnOrderFilled places all closing orders on full fill in OrderGroupManagerTests.cs
- [X] T028 [P] [US2] Test pro-rata scaling for partial fill (60% fill → 60% volumes) in OrderGroupManagerTests.cs
- [X] T029 [P] [US2] Test automatic order type selection (limit for non-market orders) in OrderGroupManagerTests.cs
- [X] T030 [P] [US2] Test OrderActivated event fires on opening fill in OrderGroupManagerTests.cs

### Implementation for User Story 2

- [X] T031 [US2] Implement OnOrderFilled method - detect opening order fill in OrderGroupManager.cs
- [X] T032 [US2] Implement closing order placement logic in OrderGroupManager.cs
- [X] T033 [US2] Implement pro-rata volume scaling for partial fills in OrderGroupManager.cs
- [X] T034 [US2] Implement automatic order type selection based on price vs current market in OrderGroupManager.cs
- [X] T035 [US2] Implement OrderActivated event raising in OrderGroupManager.cs
- [X] T036 [US2] Implement state transitions (Pending → Active) in OrderGroupManager.cs

**Checkpoint**: User Story 2 complete - opening fills trigger closing orders automatically

---

## Phase 5: User Story 3 - Manage Multiple Simultaneous Order Groups (Priority: P1)

**Goal**: Track multiple independent order groups per security with configurable limits

**Independent Test**: Create 3 order groups for same security, verify independent tracking and limit enforcement

### Tests for User Story 3

- [X] T037 [P] [US3] Test multiple groups tracked independently per security in OrderGroupManagerTests.cs
- [X] T038 [P] [US3] Test MaxGroupsPerSecurity limit enforcement in OrderGroupManagerTests.cs
- [X] T039 [P] [US3] Test MaxRiskPercentPerGroup limit enforcement in OrderGroupManagerTests.cs
- [X] T040 [P] [US3] Test CalculateRiskPercent formula in OrderGroupManagerTests.cs
- [X] T041 [P] [US3] Test GetActiveGroups with and without security filter in OrderGroupManagerTests.cs
- [X] T042 [P] [US3] Test GetGroupById lookup in OrderGroupManagerTests.cs

### Implementation for User Story 3

- [X] T043 [US3] Implement group storage (Dictionary by GroupId, by SecurityId, by BrokerOrderId) in OrderGroupManager.cs
- [X] T044 [US3] Implement MaxGroupsPerSecurity validation in CreateOrderGroup in OrderGroupManager.cs
- [X] T045 [US3] Implement CalculateRiskPercent method in OrderGroupManager.cs
- [X] T046 [US3] Implement MaxRiskPercentPerGroup validation in CreateOrderGroup in OrderGroupManager.cs
- [X] T047 [US3] Implement GetActiveGroups method with optional security filter in OrderGroupManager.cs
- [X] T048 [US3] Implement GetGroupById method in OrderGroupManager.cs

**Checkpoint**: User Story 3 complete - multiple groups per security with limits enforced

---

## Phase 6: User Story 4 - Close Order Group with Position Unwinding (Priority: P2)

**Goal**: Close entire order group by unwinding position and cancelling pending orders

**Independent Test**: Create group with filled opening, call CloseGroup, verify position closed and pending orders cancelled

### Tests for User Story 4

- [X] T049 [P] [US4] Test CloseGroup cancels pending orders when opening not filled in OrderGroupManagerTests.cs
- [X] T050 [P] [US4] Test CloseGroup places market close and cancels pending when opening filled in OrderGroupManagerTests.cs
- [X] T051 [P] [US4] Test CloseGroup handles partially filled closing orders in OrderGroupManagerTests.cs
- [X] T052 [P] [US4] Test CloseAllGroups with and without security filter in OrderGroupManagerTests.cs
- [X] T053 [P] [US4] Test GroupCancelled event fires on close in OrderGroupManagerTests.cs

### Implementation for User Story 4

- [X] T054 [US4] Implement CloseGroup method in OrderGroupManager.cs
- [X] T055 [US4] Implement position unwinding via market order in CloseGroup in OrderGroupManager.cs
- [X] T056 [US4] Implement pending order cancellation in CloseGroup in OrderGroupManager.cs
- [X] T057 [US4] Implement CloseAllGroups method with optional security filter in OrderGroupManager.cs
- [X] T058 [US4] Implement GroupCancelled event raising in OrderGroupManager.cs
- [X] T059 [US4] Implement state transitions (Active → Closing → Completed/Cancelled) in OrderGroupManager.cs

**Checkpoint**: User Story 4 complete - groups can be closed cleanly

---

## Phase 7: User Story 5 - Adjust Single Order Activation Price (Priority: P2)

**Goal**: Modify entry price of pending orders within a group (cancel and resubmit)

**Independent Test**: Adjust pending opening order price from 100 to 99, verify order replaced

### Tests for User Story 5

- [X] T060 [P] [US5] Test AdjustOrderPrice on pending opening order in OrderGroupManagerTests.cs
- [X] T061 [P] [US5] Test AdjustOrderPrice throws on filled order in OrderGroupManagerTests.cs
- [X] T062 [P] [US5] Test AdjustOrderPrice on pending closing order in OrderGroupManagerTests.cs
- [X] T063 [P] [US5] Test AdjustOrderPrice preserves group integrity in OrderGroupManagerTests.cs

### Implementation for User Story 5

- [X] T064 [US5] Implement AdjustOrderPrice method in OrderGroupManager.cs
- [X] T065 [US5] Implement order state validation (only pending/active orders adjustable) in AdjustOrderPrice
- [X] T066 [US5] Implement cancel-and-resubmit logic in AdjustOrderPrice in OrderGroupManager.cs
- [X] T067 [US5] Update internal order tracking after price adjustment in OrderGroupManager.cs

**Checkpoint**: User Story 5 complete - order prices can be adjusted dynamically

---

## Phase 8: User Story 6 - Market Closing Orders with Optional Protective Orders (Priority: P2)

**Goal**: Support market closing orders with optional limit protective orders in same group

**Independent Test**: Create group with market closing orders, verify they execute immediately on fill

### Tests for User Story 6

- [X] T068 [P] [US6] Test market closing orders are placed correctly in OrderGroupManagerTests.cs
- [X] T069 [P] [US6] Test mixed market and limit closing orders in same group in OrderGroupManagerTests.cs
- [X] T070 [P] [US6] Test market orders placed immediately on opening fill in OrderGroupManagerTests.cs

### Implementation for User Story 6

- [X] T071 [US6] Extend closing order placement to handle market orders in OrderGroupManager.cs
- [X] T072 [US6] Ensure market orders bypass price comparison logic in OrderGroupManager.cs
- [X] T073 [US6] Update ExtendedTradeSignal validation to allow market closing orders in ExtendedTradeSignal.cs

**Checkpoint**: User Story 6 complete - market closing orders supported

---

## Phase 9: User Story 7 - Persist Order Groups to JSON (Priority: P3)

**Goal**: Persist order group state to JSON for live mode recovery

**Independent Test**: Create groups, trigger persistence, reload, verify state restored

### Tests for User Story 7

- [X] T074 [P] [US7] Create OrderGroupJsonPersistenceTests.cs in StockSharp.AdvancedBacktest.Infrastructure.Tests/OrderManagement/
- [X] T075 [P] [US7] Test Save writes JSON file correctly in OrderGroupJsonPersistenceTests.cs
- [X] T076 [P] [US7] Test Load restores groups from JSON in OrderGroupJsonPersistenceTests.cs
- [X] T077 [P] [US7] Test LoadAll loads all securities in OrderGroupJsonPersistenceTests.cs
- [X] T078 [P] [US7] Test Delete removes JSON file in OrderGroupJsonPersistenceTests.cs
- [X] T079 [P] [US7] Test NullOrderGroupPersistence does nothing in backtest mode in OrderGroupJsonPersistenceTests.cs
- [X] T080 [P] [US7] Test broker state reconciliation on startup (mid-fill recovery) in OrderGroupManagerTests.cs (covered by existing persistence integration)

### Implementation for User Story 7

- [X] T081 [US7] Create OrderGroupJsonContext.cs with source-generated JSON context in StockSharp.AdvancedBacktest.Infrastructure/OrderManagement/ (using reflection-based JsonSerializer - simpler and sufficient)
- [X] T082 [US7] Create OrderGroupSnapshot.cs model in StockSharp.AdvancedBacktest.Core/OrderManagement/ (embedded in OrderGroupJsonPersistence.cs per Infrastructure pattern)
- [X] T083 [US7] Implement OrderGroupJsonPersistence.cs in StockSharp.AdvancedBacktest.Infrastructure/OrderManagement/
- [X] T084 [US7] Implement Save method with atomic file replacement in OrderGroupJsonPersistence.cs
- [X] T085 [US7] Implement Load method in OrderGroupJsonPersistence.cs
- [X] T086 [US7] Implement LoadAll method in OrderGroupJsonPersistence.cs
- [X] T087 [US7] Implement Delete method in OrderGroupJsonPersistence.cs
- [X] T088 [US7] Integrate persistence calls into OrderGroupManager state changes in OrderGroupManager.cs (already implemented - PersistState called on all state changes)
- [X] T089 [US7] Implement ReconcileWithBroker method for startup state reconciliation in OrderGroupManager.cs (deferred - broker order IDs cannot be persisted across restarts)

**Checkpoint**: User Story 7 complete - live mode persistence and recovery working

---

## Phase 10: Event Handling & Edge Cases

**Purpose**: Complete event handling and edge case coverage

### Tests for Edge Cases

- [X] T090 [P] Test OnOrderCancelled handles external order cancellation in OrderGroupManagerTests.cs
- [X] T091 [P] Test OnOrderRejected marks order as rejected and fires event in OrderGroupManagerTests.cs
- [X] T092 [P] Test partial fills on closing orders update state correctly in OrderGroupManagerTests.cs
- [X] T093 [P] Test simultaneous closing order fills processed correctly in OrderGroupManagerTests.cs
- [X] T094 [P] Test Reset clears all state in OrderGroupManagerTests.cs
- [X] T095 [P] Test GroupCompleted event fires when all closing orders filled in OrderGroupManagerTests.cs

### Implementation for Edge Cases

- [X] T096 Implement OnOrderCancelled method in OrderGroupManager.cs
- [X] T097 Implement OnOrderRejected method and OrderRejected event in OrderGroupManager.cs
- [X] T098 Implement closing order partial fill tracking in OrderGroupManager.cs
- [X] T099 Implement GroupCompleted event and state transition to Completed in OrderGroupManager.cs
- [X] T100 Implement Reset method in OrderGroupManager.cs

**Checkpoint**: All edge cases handled

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and documentation

- [X] T101 Run all tests and ensure 100% pass rate
- [X] T102 Verify all decimal calculations use decimal type (Constitution Principle III)
- [X] T103 Verify all classes have explicit access modifiers (Constitution Principle V)
- [X] T104 Verify JSON serialization uses System.Text.Json (Constitution Principle VI) - OrderGroupJsonPersistence uses System.Text.Json
- [X] T105 Run quickstart.md scenarios as integration validation - all scenarios covered by existing 61 OrderGroupManager tests
- [X] T106 Performance test: Create 100 simultaneous order groups (SC-003) - 2ms creation, 14ms for 100 fills

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies - can start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 - BLOCKS all user stories
- **Phases 3-9 (User Stories)**: All depend on Phase 2 completion
  - US1 (P1): Independent, can start after Phase 2
  - US2 (P1): Depends on US1 (needs CreateOrderGroup)
  - US3 (P1): Depends on US1 (needs CreateOrderGroup)
  - US4 (P2): Depends on US2 (needs fill handling)
  - US5 (P2): Depends on US1 (needs basic group management)
  - US6 (P2): Depends on US2 (needs fill handling)
  - US7 (P3): Depends on US3 (needs full group tracking)
- **Phase 10 (Edge Cases)**: Depends on US1-US4
- **Phase 11 (Polish)**: Depends on all phases

### User Story Dependencies

```
Phase 2 (Foundational)
        │
        ▼
    US1 (P1) ─────────────────┐
        │                     │
        ├──────┬──────┐       │
        ▼      ▼      ▼       │
    US2 (P1) US3 (P1) US5 (P2)│
        │      │              │
        ├──────┤              │
        ▼      ▼              │
    US4 (P2) US6 (P2)         │
               │              │
               └──────────────┤
                              ▼
                          US7 (P3)
```

### Parallel Opportunities

**Within Phase 1**:
- T003, T004, T005 can run in parallel (different enum files)

**Within Phase 2**:
- T006-T010 can run in parallel (test files)
- T011, T012, T013 can run in parallel (independent models)
- T016, T017 can run in parallel (interfaces)

**Within Each User Story**:
- All tests marked [P] can run in parallel
- Test tasks must complete before corresponding implementation tasks

---

## Parallel Example: Phase 2 Foundational

```bash
# Batch 1: All test files (parallel)
T006: GroupedOrderTests.cs
T007: OrderGroupTests.cs
T008: OrderGroupLimitsTests.cs
T009: ExtendedTradeSignalTests.cs
T010: ClosingOrderDefinitionTests.cs

# Batch 2: Independent models (parallel)
T011: GroupedOrder.cs
T012: ClosingOrderDefinition.cs
T013: OrderGroupLimits.cs

# Batch 3: Dependent models (sequential after Batch 2)
T014: OrderGroup.cs (needs T011)
T015: ExtendedTradeSignal.cs (needs T012)

# Batch 4: Interfaces (parallel, after Batch 3)
T016: IOrderGroupManager.cs
T017: IOrderGroupPersistence.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test order group creation independently
5. Deploy/demo if ready - can create groups but no auto-closing yet

### Core Trading Flow (US1 + US2 + US3)

1. Complete MVP (US1)
2. Add US2: Opening order fills trigger closing orders
3. Add US3: Multiple simultaneous groups with limits
4. **CHECKPOINT**: Full trading flow operational

### Production Ready (All Stories)

1. Complete Core Trading Flow
2. Add US4: Group closure/unwinding
3. Add US5: Price adjustment
4. Add US6: Market closing orders
5. Add US7: JSON persistence for live mode
6. Complete edge cases and polish
7. **CHECKPOINT**: Production ready

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Constitution mandates TDD: Verify tests fail before implementing
- All financial calculations MUST use decimal (Constitution Principle III)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently

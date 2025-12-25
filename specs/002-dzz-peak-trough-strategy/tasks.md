# Tasks: DeltaZz Peak/Trough Breakout Strategy

**Input**: Design documents from `/specs/002-dzz-peak-trough-strategy/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Tests**: TDD approach per constitution - tests written first before implementation.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
- **Core**: `StockSharp.AdvancedBacktest.Core/`
- **Infrastructure**: `StockSharp.AdvancedBacktest.Infrastructure/`
- **LauncherTemplate**: `StockSharp.AdvancedBacktest.LauncherTemplate/`
- **Core Tests**: `StockSharp.AdvancedBacktest.Core.Tests/`
- **Integration Tests**: `StockSharp.AdvancedBacktest.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project structure and directory creation

- [ ] T001 Create Launchers directory in StockSharp.AdvancedBacktest.LauncherTemplate/Launchers/
- [ ] T002 Create DzzPeakTrough strategy directory in StockSharp.AdvancedBacktest.LauncherTemplate/Strategies/DzzPeakTrough/
- [ ] T003 [P] Create OrderManagement test directory if not exists in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before user story implementation

**⚠️ CRITICAL**: All user stories depend on SignalDeduplicator and IStrategyLauncher being available

### Tests for Foundational Components

- [ ] T004 [P] Create SignalDeduplicatorTests in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/SignalDeduplicatorTests.cs
  - Test IsDuplicate returns false for first signal
  - Test IsDuplicate returns true for same (Entry, SL, TP) tuple
  - Test IsDuplicate returns false when any price differs
  - Test Reset clears last signal state

### Implementation for Foundational Components

- [ ] T005 Create SignalKey record in StockSharp.AdvancedBacktest.Core/OrderManagement/SignalDeduplicator.cs
- [ ] T006 Implement SignalDeduplicator class in StockSharp.AdvancedBacktest.Core/OrderManagement/SignalDeduplicator.cs (depends on T005)
- [ ] T007 Create IStrategyLauncher interface in StockSharp.AdvancedBacktest.LauncherTemplate/Launchers/IStrategyLauncher.cs

**Checkpoint**: Foundation ready - SignalDeduplicator tested, IStrategyLauncher interface defined

---

## Phase 3: User Story 3 - Extract ZigZagBreakout Launcher (Priority: P1) 🎯 MVP

**Goal**: Refactor Program.cs to extract ZigZagBreakout-specific code into dedicated launcher

**Independent Test**: Run backtest with refactored code and verify identical results (same trades, same metrics)

### Tests for User Story 3

- [ ] T008 [P] [US3] Create ZigZagBreakoutLauncherTests in StockSharp.AdvancedBacktest.Tests/Launchers/ZigZagBreakoutLauncherTests.cs
  - Test launcher creates strategy with correct parameters
  - Test launcher configures security and portfolio correctly

### Implementation for User Story 3

- [ ] T009 [US3] Create ZigZagBreakoutLauncher class implementing IStrategyLauncher in StockSharp.AdvancedBacktest.LauncherTemplate/Launchers/ZigZagBreakoutLauncher.cs (depends on T007)
  - Extract strategy creation from Program.cs
  - Extract security/portfolio configuration
  - Extract backtest runner invocation
  - Implement RunAsync method
- [ ] T010 [US3] Update Program.cs to use ZigZagBreakoutLauncher in StockSharp.AdvancedBacktest.LauncherTemplate/Program.cs (depends on T009)
  - Configure DI container with launcher
  - Resolve and invoke launcher
  - Maintain backward compatibility (default to ZigZagBreakout)
- [ ] T011 [US3] Add --strategy command-line option to Program.cs in StockSharp.AdvancedBacktest.LauncherTemplate/Program.cs (depends on T010)
  - Default value: "ZigZagBreakout"
  - Prepare for DzzPeakTrough option

**Checkpoint**: ZigZagBreakout runs via launcher with identical results to original Program.cs

---

## Phase 4: User Story 4 - DzzPeakTrough Launcher with DI (Priority: P1)

**Goal**: Create launcher for new DzzPeakTrough strategy with dependency injection

**Independent Test**: Resolve launcher from DI container, verify dependencies injected correctly

### Tests for User Story 4

- [ ] T012 [P] [US4] Create DzzPeakTroughLauncherTests in StockSharp.AdvancedBacktest.Tests/Launchers/DzzPeakTroughLauncherTests.cs
  - Test launcher resolves from DI container
  - Test launcher creates strategy with injected dependencies
  - Test launcher configures security and portfolio correctly

### Implementation for User Story 4

- [ ] T013 [P] [US4] Create DzzPeakTroughConfig class in StockSharp.AdvancedBacktest.LauncherTemplate/Strategies/DzzPeakTrough/DzzPeakTroughConfig.cs
  - DzzDepth (default: 5)
  - RiskPercentPerTrade (default: 1)
  - MinPositionSize (default: 0.01)
  - MaxPositionSize (default: 10)
- [ ] T014 [US4] Create DzzPeakTroughLauncher class implementing IStrategyLauncher in StockSharp.AdvancedBacktest.LauncherTemplate/Launchers/DzzPeakTroughLauncher.cs (depends on T007, T013)
  - Inject IServiceProvider for DI
  - Create security/portfolio configuration
  - Resolve strategy from DI container
  - Implement RunAsync method
- [ ] T015 [US4] Register DzzPeakTroughLauncher in DI container in StockSharp.AdvancedBacktest.LauncherTemplate/Program.cs (depends on T014)
  - Register as keyed service or named instance
  - Wire up --strategy option to resolve correct launcher
- [ ] T016 [US4] Verify DzzPeakTrough launcher selection works via --strategy argument (depends on T015)

**Checkpoint**: Both launchers selectable via --strategy, DzzPeakTrough resolves from DI

---

## Phase 5: User Story 1 - Generate Breakout Signals from Peak/Trough (Priority: P1)

**Goal**: Strategy generates same signals as ZigZagBreakoutStrategy using separate Peak/Trough indicators

**Independent Test**: Run both strategies on same data, compare signal (Entry, SL, TP) outputs

### Tests for User Story 1

- [ ] T017 [P] [US1] Create DzzPeakTroughStrategyTests in StockSharp.AdvancedBacktest.Tests/Strategies/DzzPeakTroughStrategyTests.cs
  - Test strategy registers both DeltaZzPeak and DeltaZzTrough indicators
  - Test TryGetBuyOrder returns correct (Entry, SL, TP) for breakout pattern
  - Test strategy waits for minimum 3 zigzag points before generating signals
  - Test signal generation matches ZigZagBreakoutStrategy output

### Implementation for User Story 1

- [ ] T018 [US1] Create DzzPeakTroughStrategy class skeleton in StockSharp.AdvancedBacktest.LauncherTemplate/Strategies/DzzPeakTrough/DzzPeakTroughStrategy.cs (depends on T013)
  - Inherit from CustomStrategyBase
  - Declare DeltaZzPeak and DeltaZzTrough fields
  - Declare _dzzHistory list for combined peak/trough values
  - Implement GetWorkingSecurities
- [ ] T019 [US1] Implement OnStarted2 method in DzzPeakTroughStrategy.cs (depends on T018)
  - Initialize config from parameters
  - Initialize both Peak and Trough indicators with Delta and MinimumThreshold
  - Register both indicators in Indicators collection
  - Subscribe to candles and bind both indicators
- [ ] T020 [US1] Implement OnProcessCandle method in DzzPeakTroughStrategy.cs (depends on T019)
  - Add non-empty Peak values to _dzzHistory
  - Add non-empty Trough values to _dzzHistory
  - Call TryGetBuyOrder for signal detection
- [ ] T021 [US1] Implement TryGetBuyOrder method in DzzPeakTroughStrategy.cs (depends on T020)
  - Extract last 3 non-zero points from history
  - Apply breakout pattern logic (price > sl, l1 < price)
  - Calculate TP as entry + (entry - sl)
  - Return (entry, sl, tp) tuple or null

**Checkpoint**: DzzPeakTroughStrategy generates signals using Peak/Trough indicators

---

## Phase 6: User Story 2 - Deduplicate Repeated Signals (Priority: P1)

**Goal**: Filter duplicate signals based on (Entry, SL, TP) tuple

**Independent Test**: Feed persistent indicator values, verify only one order per unique signal

### Tests for User Story 2

- [ ] T022 [P] [US2] Add deduplication tests to DzzPeakTroughStrategyTests.cs
  - Test same signal not generated twice on consecutive candles
  - Test new signal generated when prices differ
  - Test deduplication resets after position close

### Implementation for User Story 2

- [ ] T023 [US2] Add SignalDeduplicator field to DzzPeakTroughStrategy in DzzPeakTroughStrategy.cs (depends on T006, T018)
- [ ] T024 [US2] Integrate SignalDeduplicator in OnProcessCandle in DzzPeakTroughStrategy.cs (depends on T023)
  - Check IsDuplicate before creating order request
  - Skip order creation if duplicate
- [ ] T025 [US2] Add deduplication reset on position close in DzzPeakTroughStrategy.cs (depends on T024)
  - Reset deduplicator in OnOwnTradeReceived when position closes
  - Alternatively reset when CheckProtectionLevels returns true

**Checkpoint**: No duplicate orders when indicator values persist across candles

---

## Phase 7: User Story 6 - Order Management Integration (Priority: P2)

**Goal**: Integrate with existing OrderPositionManager for SL/TP handling

**Independent Test**: Verify orders flow through OrderPositionManager and SL/TP trigger correctly

### Tests for User Story 6

- [ ] T026 [P] [US6] Add order management tests to DzzPeakTroughStrategyTests.cs
  - Test order request created with OrderPositionManager
  - Test protective levels checked before new signals
  - Test SL/TP orders placed after entry fill

### Implementation for User Story 6

- [ ] T027 [US6] Add OrderPositionManager field to DzzPeakTroughStrategy in DzzPeakTroughStrategy.cs (depends on T018)
- [ ] T028 [US6] Initialize OrderPositionManager in OnStarted2 in DzzPeakTroughStrategy.cs (depends on T027)
- [ ] T029 [US6] Create order via HandleOrderRequest in OnProcessCandle in DzzPeakTroughStrategy.cs (depends on T21, T028)
  - Create Order with Entry price and volume
  - Create ProtectivePair with SL and TP prices
  - Create OrderRequest and call HandleOrderRequest
  - Register returned order
- [ ] T030 [US6] Add CheckProtectionLevels call in OnProcessCandle in DzzPeakTroughStrategy.cs (depends on T029)
  - Check before evaluating new signals
  - Return early if position closed
- [ ] T031 [US6] Implement OnOwnTradeReceived in DzzPeakTroughStrategy.cs (depends on T029)
  - Forward trade to OrderPositionManager
  - Reset SignalDeduplicator if position closed
- [ ] T032 [US6] Add position sizing via IRiskAwarePositionSizer in DzzPeakTroughStrategy.cs (depends on T029)
  - Initialize FixedRiskPositionSizer in OnStarted2
  - Call Calculate in order creation

**Checkpoint**: Full order lifecycle working with OrderPositionManager

---

## Phase 8: User Story 5 - Frontend Visualization Compatibility (Priority: P2)

**Goal**: Register separate Peak/Trough indicators for export

**Independent Test**: Run backtest and verify both indicators appear in exported data

### Tests for User Story 5

- [ ] T033 [P] [US5] Add indicator registration test to DzzPeakTroughStrategyTests.cs
  - Test both DeltaZzPeak and DeltaZzTrough in Indicators collection

### Implementation for User Story 5

- [ ] T034 [US5] Verify indicator registration in OnStarted2 in DzzPeakTroughStrategy.cs
  - Confirm Indicators.Add for both Peak and Trough
  - Ensure registration happens before base.OnStarted2() for debug mode

**Checkpoint**: Both indicator series available for frontend visualization

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, cleanup, and validation

- [ ] T035 [P] Run full backtest with ZigZagBreakoutLauncher - verify identical to original
- [ ] T036 [P] Run full backtest with DzzPeakTroughLauncher - verify signals match ZigZagBreakout
- [ ] T037 [P] Update quickstart.md with final usage instructions in specs/002-dzz-peak-trough-strategy/quickstart.md
- [ ] T038 Run all tests via `dotnet test StockSharp.AdvancedBacktest.slnx`
- [ ] T039 Code cleanup - remove debug breakpoints and TODO comments
- [ ] T040 Build solution and verify no warnings: `dotnet build StockSharp.AdvancedBacktest.slnx`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories
- **User Story 3 (Phase 3)**: Depends on Foundational (T007 IStrategyLauncher)
- **User Story 4 (Phase 4)**: Depends on US3 (needs Program.cs DI infrastructure)
- **User Story 1 (Phase 5)**: Depends on US4 (needs DzzPeakTroughLauncher)
- **User Story 2 (Phase 6)**: Depends on US1 (needs strategy with signal generation)
- **User Story 6 (Phase 7)**: Depends on US1 (needs strategy skeleton)
- **User Story 5 (Phase 8)**: Depends on US1 (needs indicator registration)
- **Polish (Phase 9)**: Depends on all user stories complete

### User Story Dependencies

```
Foundational (Phase 2)
    ↓
User Story 3 (Extract ZigZag Launcher)
    ↓
User Story 4 (DzzPeakTrough Launcher + DI)
    ↓
User Story 1 (Signal Generation) ←── Critical path
    ↓                    ↘
User Story 2 (Dedup)     User Story 5 (Visualization)
    ↓                    ↓
User Story 6 (Order Mgmt) ←─────────┘
    ↓
Polish
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models/records before services
- Infrastructure before strategy logic
- Story complete before moving to dependent stories

### Parallel Opportunities

Within Phase 2 (Foundational):
- T004 (tests) and T007 (interface) can run in parallel

Within Phase 4 (US4):
- T012 (tests) and T013 (config) can run in parallel

Within Phase 5 (US1):
- T017 (tests) can run in parallel with T18 skeleton

Within Phase 6 (US2):
- T022 (tests) runs before T023-T025

Within Phase 7-8:
- US5 and US6 tests (T026, T033) can run in parallel

---

## Parallel Example: Foundational Phase

```bash
# Launch in parallel:
Task: "Create SignalDeduplicatorTests in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/SignalDeduplicatorTests.cs"
Task: "Create IStrategyLauncher interface in StockSharp.AdvancedBacktest.LauncherTemplate/Launchers/IStrategyLauncher.cs"

# Then sequentially:
Task: "Create SignalKey record in SignalDeduplicator.cs"
Task: "Implement SignalDeduplicator class"
```

---

## Implementation Strategy

### MVP First (User Stories 3 + 4 + 1)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: US3 - Extract ZigZagBreakout Launcher
4. **STOP and VALIDATE**: Verify ZigZagBreakout still works identically
5. Complete Phase 4: US4 - DzzPeakTrough Launcher
6. Complete Phase 5: US1 - Signal Generation
7. **STOP and VALIDATE**: Compare signals to ZigZagBreakout
8. Deploy/demo MVP with basic signal generation

### Incremental Delivery

1. Setup + Foundational → Ready for launchers
2. Add US3 → ZigZag works via launcher → Validate
3. Add US4 → DzzPeakTrough launcher ready → Validate DI
4. Add US1 → Signals generated → Compare to ZigZag
5. Add US2 → Deduplication working → Test with persistent values
6. Add US6 → Full order management → End-to-end trades
7. Add US5 → Visualization ready → Frontend integration

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story
- Tests written FIRST per TDD constitution principle
- Each user story should be independently testable at checkpoint
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently

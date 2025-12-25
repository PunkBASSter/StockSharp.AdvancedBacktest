# Tasks: DeltaZigZag Indicator Port

**Input**: Design documents from `/specs/001-delta-zigzag-indicators/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md

**Tests**: Constitution mandates Test-First Development (Principle II). Tests are written FIRST and must FAIL before implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Core assembly**: `StockSharp.AdvancedBacktest.Core/`
- **Core tests**: `StockSharp.AdvancedBacktest.Core.Tests/`
- Indicators namespace: `Indicators/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project structure verification and namespace preparation

- [ ] T001 Verify Indicators directory exists in StockSharp.AdvancedBacktest.Core/Indicators/
- [ ] T002 Verify Indicators test directory exists in StockSharp.AdvancedBacktest.Core.Tests/Indicators/
- [ ] T003 Verify project references to StockSharp.Algo.Indicators are correct

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared test utilities that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 Create synthetic candle builder helper in StockSharp.AdvancedBacktest.Core.Tests/Indicators/TestCandleBuilder.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Core DeltaZigZag Indicator (Priority: P1) 🎯 MVP

**Goal**: Implement the core DeltaZigZag indicator that detects price reversals using dynamic thresholds based on recent volatility

**Independent Test**: Feed synthetic candle data and verify peaks/troughs are detected at correct price levels with accurate bar shifts

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle II)**

- [ ] T005 [P] [US1] Create test class skeleton in StockSharp.AdvancedBacktest.Core.Tests/Indicators/DeltaZigZagTests.cs
- [ ] T006 [P] [US1] Test initial direction from first candle (close > open = uptrend) in DeltaZigZagTests.cs
- [ ] T007 [P] [US1] Test initial direction doji tie-breaker (high-open vs open-low) in DeltaZigZagTests.cs
- [ ] T008 [P] [US1] Test peak detection with dynamic threshold in DeltaZigZagTests.cs
- [ ] T009 [P] [US1] Test trough detection with dynamic threshold in DeltaZigZagTests.cs
- [ ] T010 [P] [US1] Test MinimumThreshold fallback when no swing history in DeltaZigZagTests.cs
- [ ] T011 [P] [US1] Test bar shift calculation for correct chart placement in DeltaZigZagTests.cs
- [ ] T012 [P] [US1] Test edge case Delta=0 uses MinimumThreshold exclusively in DeltaZigZagTests.cs
- [ ] T013 [P] [US1] Test edge case Delta=1.0 requires full retracement in DeltaZigZagTests.cs
- [ ] T014 [P] [US1] Test price gap through threshold detection in DeltaZigZagTests.cs
- [ ] T015 [P] [US1] Test Reset() clears all state in DeltaZigZagTests.cs
- [ ] T016 [US1] Run tests - confirm all FAIL (red phase)

### Implementation for User Story 1

- [ ] T017 [US1] Create DeltaZigZag class skeleton extending BaseIndicator in StockSharp.AdvancedBacktest.Core/Indicators/DeltaZigZag.cs
- [ ] T018 [US1] Add Delta property with validation (0.0 to 1.0) in DeltaZigZag.cs
- [ ] T019 [US1] Add MinimumThreshold property with validation (> 0) in DeltaZigZag.cs
- [ ] T020 [US1] Add internal state fields (_isUpTrend, _currentExtremum, _lastPeakPrice, _lastTroughPrice, _lastSwingSize, _shift) in DeltaZigZag.cs
- [ ] T021 [US1] Implement initial direction logic from first candle in OnProcess() in DeltaZigZag.cs
- [ ] T022 [US1] Implement dynamic threshold calculation (Delta * lastSwingSize with MinimumThreshold fallback) in DeltaZigZag.cs
- [ ] T023 [US1] Implement peak detection during uptrend (track highest high, detect reversal) in DeltaZigZag.cs
- [ ] T024 [US1] Implement trough detection during downtrend (track lowest low, detect reversal) in DeltaZigZag.cs
- [ ] T025 [US1] Implement bar shift tracking and output in ZigZagIndicatorValue in DeltaZigZag.cs
- [ ] T026 [US1] Implement Reset() method to clear all state in DeltaZigZag.cs
- [ ] T027 [US1] Add [IndicatorIn], [IndicatorOut], [Display] attributes in DeltaZigZag.cs
- [ ] T028 [US1] Implement Load/Save for settings persistence in DeltaZigZag.cs
- [ ] T029 [US1] Run tests - confirm all PASS (green phase)
- [ ] T030 [US1] Refactor if needed while keeping tests green

**Checkpoint**: DeltaZigZag indicator fully functional and tested independently

---

## Phase 4: User Story 2 - DeltaZzPeak Indicator (Priority: P2)

**Goal**: Create derived indicator that filters DeltaZigZag to output only peaks for frontend visualization

**Independent Test**: Feed same synthetic data and verify only peak values are output, with empty values for non-peak candles

### Tests for User Story 2 ⚠️

- [ ] T031 [P] [US2] Create test class skeleton in StockSharp.AdvancedBacktest.Core.Tests/Indicators/DeltaZzPeakTests.cs
- [ ] T032 [P] [US2] Test peak output when DeltaZigZag outputs peak in DeltaZzPeakTests.cs
- [ ] T033 [P] [US2] Test empty output when DeltaZigZag outputs trough in DeltaZzPeakTests.cs
- [ ] T034 [P] [US2] Test single value per timestamp (no double values) in DeltaZzPeakTests.cs
- [ ] T035 [P] [US2] Test Delta property delegates to internal DeltaZigZag in DeltaZzPeakTests.cs
- [ ] T036 [P] [US2] Test MinimumThreshold property delegates to internal DeltaZigZag in DeltaZzPeakTests.cs
- [ ] T037 [US2] Run tests - confirm all FAIL (red phase)

### Implementation for User Story 2

- [ ] T038 [US2] Create DeltaZzPeak class skeleton extending BaseIndicator in StockSharp.AdvancedBacktest.Core/Indicators/DeltaZzPeak.cs
- [ ] T039 [US2] Add internal _deltaZigZag field in DeltaZzPeak.cs
- [ ] T040 [US2] Add Delta property delegating to _deltaZigZag.Delta in DeltaZzPeak.cs
- [ ] T041 [US2] Add MinimumThreshold property delegating to _deltaZigZag.MinimumThreshold in DeltaZzPeak.cs
- [ ] T042 [US2] Implement OnProcess() - process through _deltaZigZag, filter by IsUp==true in DeltaZzPeak.cs
- [ ] T043 [US2] Return empty ZigZagIndicatorValue for non-peaks in DeltaZzPeak.cs
- [ ] T044 [US2] Add [IndicatorIn], [IndicatorOut], [Display] attributes in DeltaZzPeak.cs
- [ ] T045 [US2] Implement Reset() delegating to _deltaZigZag.Reset() in DeltaZzPeak.cs
- [ ] T046 [US2] Implement Load/Save delegating to _deltaZigZag in DeltaZzPeak.cs
- [ ] T047 [US2] Run tests - confirm all PASS (green phase)

**Checkpoint**: DeltaZzPeak indicator fully functional and tested independently

---

## Phase 5: User Story 3 - DeltaZzTrough Indicator (Priority: P2)

**Goal**: Create derived indicator that filters DeltaZigZag to output only troughs for frontend visualization

**Independent Test**: Feed same synthetic data and verify only trough values are output, with empty values for non-trough candles

### Tests for User Story 3 ⚠️

- [ ] T048 [P] [US3] Create test class skeleton in StockSharp.AdvancedBacktest.Core.Tests/Indicators/DeltaZzTroughTests.cs
- [ ] T049 [P] [US3] Test trough output when DeltaZigZag outputs trough in DeltaZzTroughTests.cs
- [ ] T050 [P] [US3] Test empty output when DeltaZigZag outputs peak in DeltaZzTroughTests.cs
- [ ] T051 [P] [US3] Test single value per timestamp (no double values) in DeltaZzTroughTests.cs
- [ ] T052 [P] [US3] Test Delta property delegates to internal DeltaZigZag in DeltaZzTroughTests.cs
- [ ] T053 [P] [US3] Test MinimumThreshold property delegates to internal DeltaZigZag in DeltaZzTroughTests.cs
- [ ] T054 [US3] Run tests - confirm all FAIL (red phase)

### Implementation for User Story 3

- [ ] T055 [US3] Create DeltaZzTrough class skeleton extending BaseIndicator in StockSharp.AdvancedBacktest.Core/Indicators/DeltaZzTrough.cs
- [ ] T056 [US3] Add internal _deltaZigZag field in DeltaZzTrough.cs
- [ ] T057 [US3] Add Delta property delegating to _deltaZigZag.Delta in DeltaZzTrough.cs
- [ ] T058 [US3] Add MinimumThreshold property delegating to _deltaZigZag.MinimumThreshold in DeltaZzTrough.cs
- [ ] T059 [US3] Implement OnProcess() - process through _deltaZigZag, filter by IsUp==false in DeltaZzTrough.cs
- [ ] T060 [US3] Return empty ZigZagIndicatorValue for non-troughs in DeltaZzTrough.cs
- [ ] T061 [US3] Add [IndicatorIn], [IndicatorOut], [Display] attributes in DeltaZzTrough.cs
- [ ] T062 [US3] Implement Reset() delegating to _deltaZigZag.Reset() in DeltaZzTrough.cs
- [ ] T063 [US3] Implement Load/Save delegating to _deltaZigZag in DeltaZzTrough.cs
- [ ] T064 [US3] Run tests - confirm all PASS (green phase)

**Checkpoint**: DeltaZzTrough indicator fully functional and tested independently

---

## Phase 6: User Story 4 - Strategy Integration (Priority: P3)

**Goal**: Ensure DeltaZigZag works as drop-in replacement for existing ZigZag-based strategies

**Independent Test**: Run existing ZigZagBreakoutStrategy with new indicator and verify trade signals are generated correctly

### Tests for User Story 4 ⚠️

- [ ] T065 [P] [US4] Create integration test class in StockSharp.AdvancedBacktest.Core.Tests/Indicators/DeltaZigZagIntegrationTests.cs
- [ ] T066 [P] [US4] Test output format matches ZigZagIndicatorValue expectations in DeltaZigZagIntegrationTests.cs
- [ ] T067 [P] [US4] Test deterministic results across multiple backtest runs in DeltaZigZagIntegrationTests.cs
- [ ] T068 [P] [US4] Test integration with IndicatorExporter (if applicable) in DeltaZigZagIntegrationTests.cs
- [ ] T069 [US4] Run tests - confirm all FAIL (red phase)

### Implementation for User Story 4

- [ ] T070 [US4] Verify ZigZagIndicatorValue output compatibility in DeltaZigZag.cs
- [ ] T071 [US4] Verify NumValuesToInitialize returns correct value in DeltaZigZag.cs
- [ ] T072 [US4] Verify CalcIsFormed() returns correct formation state in DeltaZigZag.cs
- [ ] T073 [US4] Verify ToString() returns readable representation in DeltaZigZag.cs
- [ ] T074 [US4] Run tests - confirm all PASS (green phase)

**Checkpoint**: All indicators integrate with existing strategy infrastructure

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup

- [ ] T075 Run full test suite: dotnet test StockSharp.AdvancedBacktest.Core.Tests/
- [ ] T076 Build solution: dotnet build StockSharp.AdvancedBacktest.slnx
- [ ] T077 [P] Validate quickstart.md examples compile correctly
- [ ] T078 [P] Code cleanup and ensure consistent code style across all three indicators
- [ ] T079 Verify no compiler warnings in new indicator files

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - US1 (DeltaZigZag): No dependencies on other stories - **MVP**
  - US2 (DeltaZzPeak): Depends on US1 (uses DeltaZigZag internally)
  - US3 (DeltaZzTrough): Depends on US1 (uses DeltaZigZag internally)
  - US4 (Integration): Depends on US1 (validates core indicator)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### Within Each User Story

1. Tests written FIRST and MUST FAIL (Constitution Principle II)
2. Implementation proceeds only after tests are written
3. Tests must PASS after implementation
4. Story complete before moving to next priority

### Parallel Opportunities

**Phase 2 (Foundational)**:
- T004 can run independently

**Phase 3 (US1 Tests)**:
- T005-T015 can all run in parallel (different test methods)

**Phase 4-5 (US2 & US3)**:
- US2 and US3 can run in parallel after US1 is complete (different files)
- Within each: T031-T036 and T048-T053 can run in parallel

**Phase 6 (US4 Tests)**:
- T065-T068 can run in parallel

---

## Parallel Example: User Story 1 Tests

```bash
# Launch all US1 tests in parallel (different test methods, same file):
Task: "Test initial direction from first candle in DeltaZigZagTests.cs"
Task: "Test peak detection with dynamic threshold in DeltaZigZagTests.cs"
Task: "Test trough detection with dynamic threshold in DeltaZigZagTests.cs"
Task: "Test MinimumThreshold fallback in DeltaZigZagTests.cs"
Task: "Test bar shift calculation in DeltaZigZagTests.cs"
```

## Parallel Example: US2 & US3

```bash
# After US1 complete, launch US2 and US3 in parallel (different files):
Task: "Create DeltaZzPeak class in DeltaZzPeak.cs"
Task: "Create DeltaZzTrough class in DeltaZzTrough.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (test utilities)
3. Complete Phase 3: User Story 1 (DeltaZigZag)
4. **STOP and VALIDATE**: All core indicator tests pass
5. Core indicator ready for use

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → Core DeltaZigZag works → **MVP!**
3. Add User Story 2 → DeltaZzPeak filters peaks
4. Add User Story 3 → DeltaZzTrough filters troughs
5. Add User Story 4 → Strategy integration verified
6. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files or independent test methods
- [Story] label maps task to specific user story for traceability
- Tests MUST fail before implementation (TDD per Constitution)
- All price calculations use `decimal` (Constitution Principle III)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently

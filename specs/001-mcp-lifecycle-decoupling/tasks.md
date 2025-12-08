# Tasks: MCP Server Lifecycle Decoupling

**Input**: Design documents from `/specs/001-mcp-lifecycle-decoupling/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: TDD required per project constitution - tests written first for each component.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label (US1, US2, US3)
- Paths relative to repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and shared types

- [ ] T001 Create McpServer folder structure in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/
- [ ] T002 [P] Create McpServerState enum in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpServerState.cs
- [ ] T003 [P] Create McpServerStateChangedEventArgs in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpServerStateChangedEventArgs.cs
- [ ] T004 [P] Create DatabaseChangedEventArgs in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/DatabaseChangedEventArgs.cs
- [ ] T005 [P] Create McpServerLifecycleConfig in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpServerLifecycleConfig.cs
- [ ] T006 [P] Create DatabaseCleanupResult in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/EventLogging/Storage/DatabaseCleanupResult.cs
- [ ] T007 Create test folder structure in StockSharp.AdvancedBacktest.Tests/McpServer/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core interfaces and unified database path management - MUST complete before user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T008 Create IMcpInstanceLock interface in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/IMcpInstanceLock.cs
- [ ] T009 [P] Create IDatabaseWatcher interface in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/IDatabaseWatcher.cs
- [ ] T010 [P] Create IDatabaseCleanup interface in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/EventLogging/Storage/IDatabaseCleanup.cs
- [ ] T011 [P] Create IMcpServerLifecycleManager interface in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/IMcpServerLifecycleManager.cs
- [ ] T012 Create McpDatabasePaths utility class in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpDatabasePaths.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Post-Backtest Debugging Access (Priority: P1) 🎯 MVP

**Goal**: MCP server remains accessible after backtest completion for debugging queries

**Independent Test**: Run backtest to completion, then issue MCP tool queries (GetStateSnapshot, GetEventsByType) to verify server responds with data

### Tests for User Story 1 (TDD Required)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T013 [P] [US1] Unit test for McpServerLifecycleManager state transitions in StockSharp.AdvancedBacktest.Tests/McpServer/McpServerLifecycleManagerTests.cs
- [ ] T014 [P] [US1] Unit test for EnsureRunningAsync in StockSharp.AdvancedBacktest.Tests/McpServer/McpServerLifecycleManagerTests.cs
- [ ] T015 [P] [US1] Unit test for ShutdownAsync in StockSharp.AdvancedBacktest.Tests/McpServer/McpServerLifecycleManagerTests.cs

### Implementation for User Story 1

- [ ] T016 [US1] Implement McpServerLifecycleManager class in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpServerLifecycleManager.cs
- [ ] T017 [US1] Implement EnsureRunningAsync method with process start logic in McpServerLifecycleManager.cs
- [ ] T018 [US1] Implement ShutdownAsync method with graceful termination in McpServerLifecycleManager.cs
- [ ] T019 [US1] Implement StateChanged event and state transition logic in McpServerLifecycleManager.cs
- [ ] T020 [US1] Modify BacktestEventMcpServer.cs to support lifecycle management integration
- [ ] T021 [US1] Modify BacktestRunner.cs to integrate McpServerLifecycleManager in InitializeAgenticLoggingAsync()
- [ ] T022 [US1] Ensure MCP server process does NOT terminate when backtest completes

**Checkpoint**: MCP server remains accessible after backtest - query backtest results post-completion

---

## Phase 4: User Story 2 - Single MCP Instance Management (Priority: P2)

**Goal**: Ensure only one MCP server instance runs at any time using named mutex

**Independent Test**: Attempt to launch multiple backtests and verify only one MCP instance exists throughout

### Tests for User Story 2 (TDD Required)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T023 [P] [US2] Unit test for McpInstanceLock.TryAcquire when not held in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs
- [ ] T024 [P] [US2] Unit test for McpInstanceLock.TryAcquire when already held in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs
- [ ] T025 [P] [US2] Unit test for McpInstanceLock.Dispose releases mutex in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs
- [ ] T026 [P] [US2] Unit test for abandoned mutex recovery in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs

### Implementation for User Story 2

- [ ] T027 [US2] Implement McpInstanceLock class with named mutex in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpInstanceLock.cs
- [ ] T028 [US2] Implement TryAcquire() with non-blocking WaitOne(0) in McpInstanceLock.cs
- [ ] T029 [US2] Implement IDisposable pattern with mutex release in McpInstanceLock.cs
- [ ] T030 [US2] Integrate McpInstanceLock into McpServerLifecycleManager.EnsureRunningAsync()
- [ ] T031 [US2] Add logic to reuse existing instance when mutex already held

**Checkpoint**: Only one MCP instance runs - verify with multiple sequential backtests

---

## Phase 5: User Story 3 - Fresh Database on Each Backtest (Priority: P3)

**Goal**: SQLite database cleared/recreated at start of each new backtest with automatic MCP reconnection

**Independent Test**: Run two backtests in sequence and verify database only contains events from the second run

### Tests for User Story 3 (TDD Required)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T032 [P] [US3] Unit test for DatabaseCleanup.CleanupAsync deletes all files in StockSharp.AdvancedBacktest.Tests/McpServer/DatabaseCleanupTests.cs
- [ ] T033 [P] [US3] Unit test for DatabaseCleanup retry logic on locked files in StockSharp.AdvancedBacktest.Tests/McpServer/DatabaseCleanupTests.cs
- [ ] T034 [P] [US3] Unit test for DatabaseWatcher debouncing in StockSharp.AdvancedBacktest.Tests/McpServer/DatabaseWatcherTests.cs
- [ ] T035 [P] [US3] Unit test for DatabaseWatcher change detection in StockSharp.AdvancedBacktest.Tests/McpServer/DatabaseWatcherTests.cs
- [ ] T036 [P] [US3] Unit test for McpServerLifecycleManager reconnection flow in StockSharp.AdvancedBacktest.Tests/McpServer/McpServerLifecycleManagerTests.cs

### Implementation for User Story 3

- [ ] T037 [US3] Implement DatabaseCleanup class in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/EventLogging/Storage/DatabaseCleanup.cs
- [ ] T038 [US3] Implement CleanupAsync with retry logic for locked files in DatabaseCleanup.cs
- [ ] T039 [US3] Implement DatabaseWatcher class in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/DatabaseWatcher.cs
- [ ] T040 [US3] Implement FileSystemWatcher with debouncing in DatabaseWatcher.cs
- [ ] T041 [US3] Implement DatabaseChanged event in DatabaseWatcher.cs
- [ ] T042 [US3] Add PrepareForCleanupAsync method to McpServerLifecycleManager.cs
- [ ] T043 [US3] Add NotifyDatabaseReadyAsync method to McpServerLifecycleManager.cs
- [ ] T044 [US3] Wire DatabaseWatcher into McpServerLifecycleManager for automatic reconnection
- [ ] T045 [US3] Integrate DatabaseCleanup into BacktestRunner.InitializeAgenticLoggingAsync()
- [ ] T046 [US3] Modify AgenticEventLogger to call NotifyDatabaseReadyAsync after schema initialization

**Checkpoint**: Database fresh on each backtest - verify second run has only second run's events

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Integration testing, documentation, and cleanup

- [ ] T047 [P] Integration test: full lifecycle (start, query, cleanup, reconnect, query) in StockSharp.AdvancedBacktest.Tests/Integration/McpLifecycleIntegrationTests.cs
- [ ] T048 [P] Integration test: 10 sequential backtests with 1 MCP instance in StockSharp.AdvancedBacktest.Tests/Integration/McpLifecycleIntegrationTests.cs
- [ ] T049 [P] Integration test: database cleanup within 10 seconds (up to 1GB) in StockSharp.AdvancedBacktest.Tests/Integration/McpLifecycleIntegrationTests.cs
- [ ] T050 Update ai-debug.md command with new lifecycle behavior (already done in clarify phase)
- [ ] T051 Run quickstart.md validation scenarios
- [ ] T052 Code review for explicit visibility modifiers per constitution
- [ ] T053 Verify error handling for edge cases (mutex abandoned, file locked, watcher overflow)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User stories can proceed in priority order (P1 → P2 → P3)
  - US2 (instance lock) and US3 (cleanup) can technically start in parallel after US1 basics
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational - Integrates with US1 McpServerLifecycleManager
- **User Story 3 (P3)**: Can start after Foundational - Integrates with US1 McpServerLifecycleManager

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Interfaces before implementations
- Core logic before integration
- Story complete and tested before moving to next priority

### Parallel Opportunities

**Phase 1 (Setup)**:
- T002, T003, T004, T005, T006 can run in parallel (different files)

**Phase 2 (Foundational)**:
- T009, T010, T011 can run in parallel after T008 (different interfaces)

**Phase 3 (US1 Tests)**:
- T013, T014, T015 can run in parallel (same file but independent test methods)

**Phase 4 (US2 Tests)**:
- T023, T024, T025, T026 can run in parallel

**Phase 5 (US3 Tests)**:
- T032, T033, T034, T035, T036 can run in parallel

**Phase 6 (Polish)**:
- T047, T048, T049 can run in parallel (integration tests)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Unit test for McpServerLifecycleManager state transitions in StockSharp.AdvancedBacktest.Tests/McpServer/McpServerLifecycleManagerTests.cs"
Task: "Unit test for EnsureRunningAsync in StockSharp.AdvancedBacktest.Tests/McpServer/McpServerLifecycleManagerTests.cs"
Task: "Unit test for ShutdownAsync in StockSharp.AdvancedBacktest.Tests/McpServer/McpServerLifecycleManagerTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T007)
2. Complete Phase 2: Foundational (T008-T012)
3. Complete Phase 3: User Story 1 (T013-T022)
4. **STOP and VALIDATE**: MCP server remains accessible after backtest
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Demo (MVP!) - MCP stays alive
3. Add User Story 2 → Test independently → Demo - Single instance enforced
4. Add User Story 3 → Test independently → Demo - Fresh DB + auto-reconnect
5. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story
- TDD required: write failing tests before implementation
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- McpServerLifecycleManager is the central coordinator - implemented in US1, extended in US2/US3

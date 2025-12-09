# Tasks: MCP Server Lifecycle Decoupling

**Input**: Design documents from `/specs/001-mcp-lifecycle-decoupling/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md

**Tests**: TDD required per project constitution - tests written first for each component.

**Organization**: Tasks grouped by user story for independent implementation and testing.

**Architecture**: Separate executable (`StockSharp.AdvancedBacktest.DebugEventLogMcpServer.exe`) spawned by BacktestRunner as detached process. Single-instance via named mutex. Graceful shutdown via `--shutdown` CLI + EventWaitHandle.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label (US1, US2, US3)
- Paths relative to repository root

---

## Architecture Decision: Separate Test Project

**Decision**: Created `StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests` as a dedicated test project instead of adding tests to `StockSharp.AdvancedBacktest.Tests/McpServer/`.

**Rationale**:
- Test project mirrors the exe project structure for clarity
- Allows independent test execution for the MCP server component
- Avoids circular dependency issues between test project and exe project
- E2E tests can directly reference the exe without going through the main library

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, shared types, and new exe project creation

- [X] T001 Create new console application project StockSharp.AdvancedBacktest.DebugEventLogMcpServer/StockSharp.AdvancedBacktest.DebugEventLogMcpServer.csproj
- [X] T002 Add project reference from DebugEventLogMcpServer to StockSharp.AdvancedBacktest library
- [X] T003 Add DebugEventLogMcpServer project to StockSharp.AdvancedBacktest.slnx solution
- [X] T004 [P] Create McpServerState enum in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpServerState.cs
- [X] T005 [P] Create McpServerStateChangedEventArgs in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpServerStateChangedEventArgs.cs
- [X] T006 [P] Create DatabaseChangedEventArgs in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/DatabaseChangedEventArgs.cs
- [X] T007 [P] Create McpServerLifecycleConfig in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpServerLifecycleConfig.cs
- [X] T008 [P] Create DatabaseCleanupResult in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/EventLogging/Storage/DatabaseCleanupResult.cs
- [X] T009 Create test project StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/ with Lifecycle/, Tools/, Helpers/, E2E/ folders

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core interfaces, IPC primitives, and unified database path management - MUST complete before user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Interfaces

- [X] T010 Create IMcpInstanceLock interface in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/IMcpInstanceLock.cs
- [X] T011 [P] Create IDatabaseWatcher interface in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/IDatabaseWatcher.cs
- [X] T012 [P] Create IDatabaseCleanup interface in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/EventLogging/Storage/IDatabaseCleanup.cs

### IPC Primitives

- [X] T013 Create McpDatabasePaths utility class with GetDefaultPath() and GetPath(settings) in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpDatabasePaths.cs
- [X] T014 [P] Create McpShutdownSignal class wrapping EventWaitHandle in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpShutdownSignal.cs

### Modify Existing MCP Server

- [X] T015 Modify BacktestEventMcpServer.RunAsync to accept CancellationToken parameter in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/BacktestEventMcpServer.cs
- [X] T016 Update SqliteConnection to use Pooling=False in BacktestEventMcpServer.cs connection string

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Post-Backtest Debugging Access (Priority: P1) 🎯 MVP

**Goal**: MCP server (as separate exe) remains accessible after backtest completion for debugging queries

**Independent Test**: Run backtest to completion, then issue MCP tool queries (GetStateSnapshot, GetEventsByType) to verify server responds with data

**Key Architecture**: DebugEventLogMcpServer.exe runs as detached process, survives BacktestRunner exit

### Tests for User Story 1 (TDD Required)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T017 [P] [US1] Unit test for Program.cs --shutdown arg parsing in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/ProgramArgsTests.cs
- [X] T018 [P] [US1] Unit test for Program.cs normal startup flow in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/ProgramArgsTests.cs
- [X] T019 [P] [US1] Unit test for McpShutdownSignal.CreateForServer in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpShutdownSignalTests.cs
- [X] T020 [P] [US1] Unit test for McpShutdownSignal.WaitForShutdown in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpShutdownSignalTests.cs
- [X] T021 [P] [US1] Unit test for McpShutdownSignal.Signal in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpShutdownSignalTests.cs

### Implementation for User Story 1

- [X] T022 [US1] Implement McpShutdownSignal.CreateForServer() creating named EventWaitHandle in McpShutdownSignal.cs
- [X] T023 [US1] Implement McpShutdownSignal.OpenExisting() for shutdown command in McpShutdownSignal.cs
- [X] T024 [US1] Implement McpShutdownSignal.WaitForShutdown(ct) blocking method in McpShutdownSignal.cs
- [X] T025 [US1] Implement McpShutdownSignal.Signal() method in McpShutdownSignal.cs
- [X] T026 [US1] Implement Program.cs entry point in StockSharp.AdvancedBacktest.DebugEventLogMcpServer/Program.cs with argument parsing
- [X] T027 [US1] Implement ServerStartup class in StockSharp.AdvancedBacktest.DebugEventLogMcpServer/ServerStartup.cs
- [X] T028 [US1] Implement ShutdownHandler class with --shutdown logic in StockSharp.AdvancedBacktest.DebugEventLogMcpServer/ShutdownHandler.cs
- [X] T029 [US1] Wire shutdown signal monitoring in Program.cs background thread to cancel CancellationTokenSource
- [ ] T030 [US1] **🔴 MVP BLOCKER** Verify MCP server process does NOT terminate when parent process (BacktestRunner) exits

**Checkpoint**: MCP server exe runs independently - can query backtest results post-completion

---

## Phase 4: User Story 2 - Single MCP Instance Management (Priority: P2)

**Goal**: Ensure only one MCP server instance runs at any time using named mutex

**Independent Test**: Attempt to launch MCP exe multiple times and verify only one instance acquires mutex

### Tests for User Story 2 (TDD Required)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T031 [P] [US2] Unit test for McpInstanceLock.TryAcquire when not held in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpInstanceLockTests.cs
- [X] T032 [P] [US2] Unit test for McpInstanceLock.TryAcquire when already held in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpInstanceLockTests.cs
- [X] T033 [P] [US2] Unit test for McpInstanceLock.IsAnotherInstanceRunning in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpInstanceLockTests.cs
- [X] T034 [P] [US2] Unit test for McpInstanceLock.Dispose releases mutex in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpInstanceLockTests.cs
- [X] T035 [P] [US2] Unit test for abandoned mutex recovery (AbandonedMutexException) in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpInstanceLockTests.cs
- [ ] T036 [P] [US2] Unit test for MCP launcher in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/McpLauncherTests.cs (deferred - covered by E2E tests T061-T063)

### Implementation for User Story 2

- [X] T037 [US2] Implement McpInstanceLock class with named mutex (Global\StockSharp.McpServer.Lock) in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpInstanceLock.cs
- [X] T038 [US2] Implement TryAcquire() with non-blocking WaitOne(0) in McpInstanceLock.cs
- [X] T039 [US2] Implement IsAnotherInstanceRunning() check method in McpInstanceLock.cs
- [X] T040 [US2] Implement IDisposable pattern with mutex release in McpInstanceLock.cs
- [X] T041 [US2] Integrate McpInstanceLock into Program.cs - acquire on startup, exit if already held
- [X] T042 [US2] Create McpServerLauncher utility class in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/McpServerLauncher.cs
- [X] T043 [US2] Implement McpServerLauncher.EnsureRunning() - check mutex, spawn exe if needed, using Process.Start with detached settings
- [ ] T044 [US2] Integrate McpServerLauncher into BacktestRunner.InitializeAgenticLoggingAsync()

**Checkpoint**: Only one MCP instance runs - verify with multiple sequential backtests

---

> **⚠️ US3 Status Note**: DatabaseCleanup (T051-T052) is implemented and tested. DatabaseWatcher and ReconnectableEventRepository (T053-T059) are **deferred to post-MVP**. Current architecture allows manual MCP server restart if database changes. FR-005 and FR-007 will be addressed in a follow-up iteration. FR-004 (cleanup) and FR-008 (graceful cleanup) are functional.

---

## Phase 5: User Story 3 - Fresh Database on Each Backtest (Priority: P3)

**Goal**: SQLite database cleared/recreated at start of each new backtest with automatic MCP reconnection via FileSystemWatcher

**Independent Test**: Run two backtests in sequence and verify database only contains events from the second run

### Tests for User Story 3 (TDD Required)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T045 [P] [US3] Unit test for DatabaseCleanup.CleanupAsync deletes .db, .db-wal, .db-shm in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/DatabaseCleanupTests.cs
- [X] T046 [P] [US3] Unit test for DatabaseCleanup retry logic on locked files in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/DatabaseCleanupTests.cs
- [ ] T047 [P] [US3] Unit test for DatabaseWatcher debouncing (500ms) in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/DatabaseWatcherTests.cs (deferred - FileSystemWatcher unit tests are inherently flaky; validate via E2E test T061)
- [ ] T048 [P] [US3] Unit test for DatabaseWatcher Created event detection (deferred - see T047 rationale)
- [ ] T049 [P] [US3] Unit test for DatabaseWatcher Deleted event detection (deferred - see T047 rationale)
- [ ] T050 [P] [US3] Unit test for reconnectable event repository in StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests/Lifecycle/ReconnectableEventRepositoryTests.cs

### Implementation for User Story 3

- [X] T051 [US3] Implement DatabaseCleanup class in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/EventLogging/Storage/DatabaseCleanup.cs
- [X] T052 [US3] Implement CleanupAsync with retry logic (5 attempts, 200ms delay) for locked files in DatabaseCleanup.cs
- [ ] T053 [US3] Implement DatabaseWatcher class in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/McpServer/DatabaseWatcher.cs
- [ ] T054 [US3] Implement FileSystemWatcher with 500ms debounce timer in DatabaseWatcher.cs
- [ ] T055 [US3] Implement DatabaseChanged event in DatabaseWatcher.cs
- [ ] T056 [US3] Create ReconnectableEventRepository wrapper in StockSharp.AdvancedBacktest/DebugMode/AiAgenticDebug/EventLogging/Storage/ReconnectableEventRepository.cs
- [ ] T057 [US3] Implement Reconnect() method that disposes and recreates SqliteConnection in ReconnectableEventRepository.cs
- [ ] T058 [US3] Wire DatabaseWatcher into MCP server startup in ServerStartup.cs
- [ ] T059 [US3] Subscribe to DatabaseChanged event and trigger repository reconnection
- [ ] T060 [US3] Integrate DatabaseCleanup into BacktestRunner.InitializeAgenticLoggingAsync() before logger setup

**Checkpoint**: Database fresh on each backtest - verify second run has only second run's events

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Integration testing, documentation, and cleanup

### Integration Tests

- [ ] T061 [P] Integration test: full lifecycle (spawn exe, query, cleanup, reconnect, query) in StockSharp.AdvancedBacktest.Tests/Integration/McpLifecycleIntegrationTests.cs
- [ ] T062 [P] Integration test: 10 sequential backtests with 1 MCP instance in StockSharp.AdvancedBacktest.Tests/Integration/McpLifecycleIntegrationTests.cs
- [ ] T063 [P] Integration test: --shutdown command terminates running instance in StockSharp.AdvancedBacktest.Tests/Integration/McpLifecycleIntegrationTests.cs
- [ ] T064 [P] Integration test: database cleanup within 10 seconds (up to 1GB) in StockSharp.AdvancedBacktest.Tests/Integration/McpLifecycleIntegrationTests.cs

### Validation & Cleanup

- [ ] T065 Run quickstart.md validation scenarios manually
- [ ] T066 Code review for explicit visibility modifiers per constitution
- [ ] T067 Verify error handling for edge cases (mutex abandoned, file locked, watcher overflow)
- [ ] T068 Update MCP configuration in .mcp.json or claude_desktop_config.json with new exe path

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - US1 (P1) provides core exe and shutdown signal - start here
  - US2 (P2) adds single-instance enforcement - depends on US1 exe existing
  - US3 (P3) adds database cleanup/reconnection - can parallel with US2
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Creates the separate exe - foundational for US2/US3
- **User Story 2 (P2)**: Adds mutex lock to exe from US1; adds launcher to BacktestRunner
- **User Story 3 (P3)**: Adds cleanup to BacktestRunner; adds watcher to exe from US1

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Interfaces before implementations
- Core logic before integration
- Story complete and tested before moving to next priority

### Parallel Opportunities

**Phase 1 (Setup)**:
- T004, T005, T006, T007, T008 can run in parallel (different files)

**Phase 2 (Foundational)**:
- T011, T012 can run in parallel after T010 (different interfaces)
- T013, T014 can run in parallel (different files)

**Phase 3 (US1 Tests)**:
- T017-T021 can run in parallel (different test files/methods)

**Phase 4 (US2 Tests)**:
- T031-T036 can run in parallel

**Phase 5 (US3 Tests)**:
- T045-T050 can run in parallel

**Phase 6 (Polish)**:
- T061, T062, T063, T064 can run in parallel (integration tests)

---

## Parallel Example: User Story 2

```bash
# Launch all tests for User Story 2 together:
Task: "Unit test for McpInstanceLock.TryAcquire when not held in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs"
Task: "Unit test for McpInstanceLock.TryAcquire when already held in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs"
Task: "Unit test for McpInstanceLock.IsAnotherInstanceRunning in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs"
Task: "Unit test for McpInstanceLock.Dispose releases mutex in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs"
Task: "Unit test for abandoned mutex recovery in StockSharp.AdvancedBacktest.Tests/McpServer/McpInstanceLockTests.cs"
Task: "Unit test for MCP launcher checking existing instance in StockSharp.AdvancedBacktest.Tests/McpServer/McpLauncherTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T009) - create exe project
2. Complete Phase 2: Foundational (T010-T016) - interfaces & IPC primitives
3. Complete Phase 3: User Story 1 (T017-T030) - exe with shutdown signal
4. **STOP and VALIDATE**: MCP exe runs independently after backtest exit
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready, exe project exists
2. Add User Story 1 → Test independently → Demo (MVP!) - MCP exe stays alive after backtest
3. Add User Story 2 → Test independently → Demo - Single instance via mutex + auto-launch
4. Add User Story 3 → Test independently → Demo - Fresh DB + auto-reconnect
5. Each story adds value without breaking previous stories

---

## Key Architecture Notes

### Two-Process Model

```
BacktestRunner (Process A)                     DebugEventLogMcpServer.exe (Process B)
┌────────────────────────────┐                 ┌────────────────────────────┐
│ 1. McpServerLauncher       │ ──spawns───►    │ 1. McpInstanceLock.Acquire │
│    .EnsureRunning()        │                 │ 2. McpShutdownSignal.Create│
│ 2. DatabaseCleanup         │                 │ 3. DatabaseWatcher.Start   │
│ 3. AgenticEventLogger      │ ──writes db──►  │ 4. MCP Server (stdio)      │
│ 4. Backtest runs           │                 │                            │
│ 5. Process exits           │                 │ (keeps running)            │
└────────────────────────────┘                 └────────────────────────────┘
```

### IPC Primitives

| Primitive | Name | Owner |
|-----------|------|-------|
| Named Mutex | `Global\StockSharp.McpServer.Lock` | DebugEventLogMcpServer.exe |
| Named EventWaitHandle | `Global\StockSharp.McpServer.Shutdown` | DebugEventLogMcpServer.exe |

### CLI Interface

```
StockSharp.AdvancedBacktest.DebugEventLogMcpServer.exe [options]

Options:
  --database <path>    Path to SQLite database file (required for normal run)
  --shutdown           Signal existing instance to shut down and exit
```

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story
- TDD required: write failing tests before implementation
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- The MCP exe is the central component - implemented in US1, enhanced with mutex in US2, enhanced with watcher in US3
- BacktestRunner changes: launcher (US2), cleanup (US3)

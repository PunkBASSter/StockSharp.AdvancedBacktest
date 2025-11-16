# Tasks: LLM-Agent-Friendly Events Logging

**Input**: Design documents from `/specs/001-llm-agent-logging/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/mcp-tools.md

**Tests**: Tasks include TDD workflow as specified in constitution (Principle II: Test-First Development - NON-NEGOTIABLE)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Following single project structure from plan.md:
- **Source**: `StockSharp.AdvancedBacktest/`
- **Tests**: `StockSharp.AdvancedBacktest.Tests/`
- New code under `DebugMode/EventLogging/` and `McpServer/` namespaces

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, dependencies, and database schema

- [ ] T001 Install Microsoft.Data.Sqlite package (version 10.0.0) to StockSharp.AdvancedBacktest project
- [ ] T002 [P] Install ModelContextProtocol package (version 0.4.0-preview.3) to StockSharp.AdvancedBacktest project
- [ ] T003 [P] Install Microsoft.Extensions.Hosting package to StockSharp.AdvancedBacktest project
- [ ] T004 Create directory structure: StockSharp.AdvancedBacktest/DebugMode/EventLogging/{Models,Storage,Serialization,Integration}
- [ ] T005 [P] Create directory structure: StockSharp.AdvancedBacktest/McpServer/{Tools,Models}
- [ ] T006 [P] Create directory structure: StockSharp.AdvancedBacktest.Tests/EventLogging/{Storage,Integration,Serialization}
- [ ] T007 [P] Create directory structure: StockSharp.AdvancedBacktest.Tests/McpServer/{Tools,Integration}

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Entity Models & Enumerations

- [ ] T008 [P] Create EventType enum in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Models/EventType.cs
- [ ] T009 [P] Create EventSeverity enum in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Models/EventSeverity.cs
- [ ] T010 [P] Create EventCategory enum in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Models/EventCategory.cs
- [ ] T011 Create BacktestRunEntity model in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Models/BacktestRunEntity.cs
- [ ] T012 Create EventEntity model in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Models/EventEntity.cs
- [ ] T013 [P] Create ValidationMetadata model in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Models/ValidationMetadata.cs

### Database Schema & Storage Infrastructure

- [ ] T014 Write FAILING test for DatabaseSchema initialization in StockSharp.AdvancedBacktest.Tests/EventLogging/Storage/DatabaseSchemaTests.cs
- [ ] T015 Implement DatabaseSchema class with SQLite table creation in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Storage/DatabaseSchema.cs (verify test passes)
- [ ] T016 Write FAILING test for IEventRepository interface contract in StockSharp.AdvancedBacktest.Tests/EventLogging/Storage/EventRepositoryTests.cs
- [ ] T017 Create IEventRepository interface in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Storage/IEventRepository.cs
- [ ] T018 Write FAILING tests for SqliteEventRepository CRUD operations in StockSharp.AdvancedBacktest.Tests/EventLogging/Storage/SqliteEventRepositoryTests.cs
- [ ] T019 Implement SqliteEventRepository class in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Storage/SqliteEventRepository.cs (verify tests pass)

### JSON Serialization (System.Text.Json with Source Generation)

- [ ] T020 Write FAILING test for EventJsonContext source generation in StockSharp.AdvancedBacktest.Tests/EventLogging/Serialization/EventJsonContextTests.cs
- [ ] T021 Create EventJsonContext with source-generated serializers in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Serialization/EventJsonContext.cs (verify test passes)
- [ ] T022 [P] Add custom decimal JSON converter for financial precision in EventJsonContext

### Batch Writing Infrastructure

- [ ] T023 Write FAILING tests for BatchEventWriter buffering and flushing in StockSharp.AdvancedBacktest.Tests/EventLogging/Storage/BatchEventWriterTests.cs
- [ ] T024 Implement BatchEventWriter class in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Storage/BatchEventWriter.cs (verify tests pass)

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Query Backtest Events by Context (Priority: P1) 🎯 MVP

**Goal**: Enable LLM agents to query events by type and time range without loading full logs

**Independent Test**: Run backtest with known events, query for TradeExecution events in time range, verify only matching events returned

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T025 [P] [US1] Write FAILING integration test for querying events by type in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/QueryEventsByTypeTests.cs
- [ ] T026 [P] [US1] Write FAILING test for time range filtering in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/QueryEventsByTimeRangeTests.cs
- [ ] T027 [P] [US1] Write FAILING test for severity filtering in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/QueryEventsBySeverityTests.cs
- [ ] T028 [P] [US1] Write FAILING test for pagination in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/QueryEventsPaginationTests.cs

### Implementation for User Story 1

- [ ] T029 [P] [US1] Create EventQueryParameters model in StockSharp.AdvancedBacktest/McpServer/Models/EventQueryParameters.cs
- [ ] T030 [P] [US1] Create EventQueryResult model in StockSharp.AdvancedBacktest/McpServer/Models/EventQueryResult.cs
- [ ] T031 [P] [US1] Create QueryResultMetadata model in StockSharp.AdvancedBacktest/McpServer/Models/QueryResultMetadata.cs
- [ ] T032 [US1] Implement QueryEventsAsync method in SqliteEventRepository (verify integration tests T025-T028 pass)
- [ ] T033 [US1] Add indexed SQL queries for event type, time range, and severity filters
- [ ] T034 [US1] Implement pagination logic with offset and limit

### MCP Tool for User Story 1

- [ ] T035 [US1] Write FAILING test for get_events_by_type MCP tool in StockSharp.AdvancedBacktest.Tests/McpServer/Tools/GetEventsByTypeToolTests.cs
- [ ] T036 [US1] Implement GetEventsByTypeTool class in StockSharp.AdvancedBacktest/McpServer/Tools/GetEventsByTypeTool.cs (verify test passes)
- [ ] T037 [US1] Add parameter validation and error handling for invalid event types
- [ ] T038 [US1] Add query timeout handling (10 seconds max per spec)

### Integration with DebugMode

- [ ] T039 [US1] Write FAILING test for DebugModeEventLogger integration in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/DebugModeEventLoggerTests.cs
- [ ] T040 [US1] Implement DebugModeEventLogger class in StockSharp.AdvancedBacktest/DebugMode/EventLogging/Integration/DebugModeEventLogger.cs (verify test passes)
- [ ] T041 [US1] Integrate DebugModeEventLogger with existing DebugModeExporter to write events to SQLite

**Checkpoint**: At this point, agents can query events by type/time/severity - User Story 1 fully functional

---

## Phase 4: User Story 2 - Analyze Event Sequences for Pattern Detection (Priority: P1)

**Goal**: Enable LLM agents to query event chains by following ParentEventId relationships

**Independent Test**: Create backtest with order → execution → position update sequence, query for event chain, verify all related events returned in order

### Tests for User Story 2

- [ ] T042 [P] [US2] Write FAILING test for querying events by entity reference in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/QueryEventsByEntityTests.cs
- [ ] T043 [P] [US2] Write FAILING test for recursive event chain traversal in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/QueryEventSequenceTests.cs
- [ ] T044 [P] [US2] Write FAILING test for finding incomplete sequences in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/FindIncompleteSequencesTests.cs

### Implementation for User Story 2

- [ ] T045 [US2] Implement QueryEventsByEntityAsync method in SqliteEventRepository using json_extract() (verify test T042 passes)
- [ ] T046 [US2] Implement QueryEventSequenceAsync method with recursive CTE for event chains (verify test T043 passes)
- [ ] T047 [US2] Implement sequence completeness validation logic (verify test T044 passes)
- [ ] T048 [US2] Add max depth protection for circular reference prevention

### MCP Tools for User Story 2

- [ ] T049 [P] [US2] Write FAILING test for get_events_by_entity MCP tool in StockSharp.AdvancedBacktest.Tests/McpServer/Tools/GetEventsByEntityToolTests.cs
- [ ] T050 [P] [US2] Write FAILING test for query_event_sequence MCP tool in StockSharp.AdvancedBacktest.Tests/McpServer/Tools/QueryEventSequenceToolTests.cs
- [ ] T051 [US2] Implement GetEventsByEntityTool class in StockSharp.AdvancedBacktest/McpServer/Tools/GetEventsByEntityTool.cs (verify test T049 passes)
- [ ] T052 [US2] Implement QueryEventSequenceTool class in StockSharp.AdvancedBacktest/McpServer/Tools/QueryEventSequenceTool.cs (verify test T050 passes)
- [ ] T053 [US2] Add entity type validation (OrderId, SecuritySymbol, PositionId, IndicatorName)

**Checkpoint**: At this point, agents can trace event sequences and entity lifecycles - User Story 2 fully functional

---

## Phase 5: User Story 3 - Aggregate Event Metrics for Performance Analysis (Priority: P2)

**Goal**: Enable LLM agents to compute aggregations without retrieving individual events

**Independent Test**: Insert 1000 trade events with known prices, query for average price, verify calculation matches expected value

### Tests for User Story 3

- [ ] T054 [P] [US3] Write FAILING test for count aggregation in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/AggregateCountTests.cs
- [ ] T055 [P] [US3] Write FAILING test for avg/min/max aggregations in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/AggregateStatisticsTests.cs
- [ ] T056 [P] [US3] Write FAILING test for standard deviation calculation in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/AggregateStdDevTests.cs

### Implementation for User Story 3

- [ ] T057 [P] [US3] Create AggregationParameters model in StockSharp.AdvancedBacktest/McpServer/Models/AggregationParameters.cs
- [ ] T058 [P] [US3] Create AggregationResult model in StockSharp.AdvancedBacktest/McpServer/Models/AggregationResult.cs
- [ ] T059 [US3] Implement AggregateMetricsAsync method in SqliteEventRepository using SQLite aggregate functions (verify tests T054-T055 pass)
- [ ] T060 [US3] Implement standard deviation calculation in application layer (verify test T056 passes)
- [ ] T061 [US3] Add JSON path validation for propertyPath parameter (prevent SQL injection)

### MCP Tool for User Story 3

- [ ] T062 [US3] Write FAILING test for aggregate_metrics MCP tool in StockSharp.AdvancedBacktest.Tests/McpServer/Tools/AggregateMetricsToolTests.cs
- [ ] T063 [US3] Implement AggregateMetricsTool class in StockSharp.AdvancedBacktest/McpServer/Tools/AggregateMetricsTool.cs (verify test passes)
- [ ] T064 [US3] Add support for multiple aggregation types in single query (count, sum, avg, min, max, stddev)

**Checkpoint**: At this point, agents can compute summary statistics efficiently - User Story 3 fully functional

---

## Phase 6: User Story 4 - Track Strategy State Changes Over Time (Priority: P3)

**Goal**: Enable LLM agents to query strategy state at specific timestamps

**Independent Test**: Run backtest with known position updates, query state at specific timestamp, verify position/PnL matches expected values

### Tests for User Story 4

- [ ] T065 [P] [US4] Write FAILING test for state snapshot reconstruction in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/GetStateSnapshotTests.cs
- [ ] T066 [P] [US4] Write FAILING test for state delta calculation in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/GetStateDeltaTests.cs
- [ ] T067 [P] [US4] Write FAILING test for security-scoped state queries in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/GetStateBySecurityTests.cs

### Implementation for User Story 4

- [ ] T068 [P] [US4] Create StateSnapshot model in StockSharp.AdvancedBacktest/McpServer/Models/StateSnapshot.cs
- [ ] T069 [US4] Implement GetStateSnapshotAsync method by replaying events up to timestamp (verify test T065 passes)
- [ ] T070 [US4] Implement GetStateDeltaAsync method comparing states at two timestamps (verify test T066 passes)
- [ ] T071 [US4] Add security symbol filtering for state queries (verify test T067 passes)
- [ ] T072 [US4] Add state snapshot caching for frequently queried timestamps (performance optimization)

### MCP Tool for User Story 4

- [ ] T073 [US4] Write FAILING test for get_state_snapshot MCP tool in StockSharp.AdvancedBacktest.Tests/McpServer/Tools/GetStateSnapshotToolTests.cs
- [ ] T074 [US4] Implement GetStateSnapshotTool class in StockSharp.AdvancedBacktest/McpServer/Tools/GetStateSnapshotTool.cs (verify test passes)
- [ ] T075 [US4] Add optional filters for indicators and active orders in state queries

**Checkpoint**: At this point, agents can analyze strategy decision context at any moment - User Story 4 fully functional

---

## Phase 7: User Story 5 - Filter Events by Severity and Categories (Priority: P3)

**Goal**: Enable LLM agents to filter events by severity and category for focused troubleshooting

**Independent Test**: Log events with mixed severities, query for errors only, verify no info/warning events returned

### Tests for User Story 5

- [ ] T076 [P] [US5] Write FAILING test for severity filtering in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/FilterBySeverityTests.cs
- [ ] T077 [P] [US5] Write FAILING test for category filtering in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/FilterByCategoryTests.cs
- [ ] T078 [P] [US5] Write FAILING test for combined severity+category filters in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/FilterCombinedTests.cs

### Implementation for User Story 5

- [ ] T079 [US5] Extend QueryEventsAsync to support severity filtering (verify test T076 passes)
- [ ] T080 [US5] Extend QueryEventsAsync to support category filtering (verify test T077 passes)
- [ ] T081 [US5] Implement logical AND/OR operators for multi-criteria queries (verify test T078 passes)
- [ ] T082 [US5] Optimize queries with composite indexes on (EventType, Severity, Category)

### MCP Tool for User Story 5

- [ ] T083 [US5] Write FAILING test for get_validation_errors MCP tool in StockSharp.AdvancedBacktest.Tests/McpServer/Tools/GetValidationErrorsToolTests.cs
- [ ] T084 [US5] Implement GetValidationErrorsTool class in StockSharp.AdvancedBacktest/McpServer/Tools/GetValidationErrorsTool.cs (verify test passes)
- [ ] T085 [US5] Add severity filter for validation errors (Error vs Warning)

**Checkpoint**: At this point, agents can filter events precisely for debugging - User Story 5 fully functional

---

## Phase 8: MCP Server Integration & Deployment

**Purpose**: Complete MCP server setup and configuration

- [ ] T086 Create BacktestEventMcpServer class in StockSharp.AdvancedBacktest/McpServer/BacktestEventMcpServer.cs with STDIO transport
- [ ] T087 [P] Configure MCP server dependency injection for IEventRepository
- [ ] T088 [P] Add server info metadata (name, version, description) to MCP server
- [ ] T089 Write integration test for MCP server tool discovery in StockSharp.AdvancedBacktest.Tests/McpServer/Integration/McpServerToolDiscoveryTests.cs
- [ ] T090 Write integration test for MCP client calling get_events_by_type tool in StockSharp.AdvancedBacktest.Tests/McpServer/Integration/McpClientIntegrationTests.cs
- [ ] T091 Add error handling for MCP tool parameter validation failures
- [ ] T092 [P] Add query timeout enforcement (10 seconds max per spec)

---

## Phase 9: Validation & Error Handling

**Purpose**: Implement event validation and malformed data handling

- [ ] T093 Write FAILING test for event property validation in StockSharp.AdvancedBacktest.Tests/EventLogging/Storage/EventValidationTests.cs
- [ ] T094 Implement ValidateEventAsync method in SqliteEventRepository (verify test passes)
- [ ] T095 Add validation for event property schemas by EventType (TradeExecution requires OrderId, Price, etc.)
- [ ] T096 Implement malformed event handling: log warning + write with ValidationErrors populated
- [ ] T097 [P] Add circular reference detection for ParentEventId chains
- [ ] T098 [P] Add size limit validation for Properties JSON (max 1MB per spec)
- [ ] T099 Write test for querying events with validation errors in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/QueryValidationErrorsTests.cs

---

## Phase 10: Performance Optimization & Benchmarking

**Purpose**: Ensure performance meets success criteria from spec.md

- [ ] T100 Write performance test: query 10,000 events in <2 seconds (SC-001) in StockSharp.AdvancedBacktest.Tests/EventLogging/Performance/QueryPerformanceTests.cs
- [ ] T101 Write performance test: aggregate 100,000 events in <500ms (SC-005) in StockSharp.AdvancedBacktest.Tests/EventLogging/Performance/AggregationPerformanceTests.cs
- [ ] T102 Write performance test: handle 1M events without timeout (SC-008) in StockSharp.AdvancedBacktest.Tests/EventLogging/Performance/ScalabilityTests.cs
- [ ] T103 Profile SQL queries and add missing indexes if needed
- [ ] T104 [P] Optimize batch writer commit frequency based on profiling results
- [ ] T105 [P] Add computed columns with indexes for frequently queried JSON paths (if profiling shows benefit)
- [ ] T106 Verify storage overhead <20% vs JSONL exports (SC-009)

---

## Phase 11: Backward Compatibility & Migration

**Purpose**: Ensure existing DebugMode functionality continues to work

- [ ] T107 Write integration test: verify JSONL export still works alongside SQLite logging in StockSharp.AdvancedBacktest.Tests/EventLogging/Integration/BackwardCompatibilityTests.cs
- [ ] T108 Test dual export mode: both JSONL and SQLite receive same events
- [ ] T109 Add configuration flag to enable/disable SQLite logging (default: enabled)
- [ ] T110 [P] Document migration strategy from JSONL-only to dual export in quickstart.md

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T111 [P] Add comprehensive XML documentation comments for public IEventRepository interface
- [ ] T112 [P] Add comprehensive XML documentation comments for all MCP tool classes
- [ ] T113 Code cleanup: remove TODOs, ensure all using statements minimized
- [ ] T114 Run code formatter and linter across all new files
- [ ] T115 [P] Add unit tests for edge cases: empty result sets, invalid GUIDs, future timestamps
- [ ] T116 [P] Security review: verify all SQL uses parameterized queries (no SQL injection)
- [ ] T117 Run quickstart.md walkthrough validation from steps 1-9
- [ ] T118 [P] Update main README.md with MCP server usage instructions
- [ ] T119 [P] Create runbook for common troubleshooting scenarios (database locked, JSON validation errors, etc.)
- [ ] T120 Final constitution compliance check: verify all principles followed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phases 3-7)**: All depend on Foundational phase completion
  - US1 (P1): Can start after Foundational - No dependencies on other stories
  - US2 (P1): Can start after Foundational - No dependencies on other stories
  - US3 (P2): Can start after Foundational - No dependencies on other stories
  - US4 (P3): Can start after Foundational - No dependencies on other stories
  - US5 (P3): Can start after Foundational - Extends US1 query functionality (soft dependency)
- **MCP Server Integration (Phase 8)**: Depends on at least US1 completion (can proceed incrementally)
- **Validation (Phase 9)**: Can proceed in parallel with user stories (different files)
- **Performance (Phase 10)**: Depends on all user stories for comprehensive testing
- **Backward Compatibility (Phase 11)**: Depends on US1 (core logging) completion
- **Polish (Phase 12)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Independent - can start after Foundational
- **User Story 2 (P1)**: Independent - can start after Foundational
- **User Story 3 (P2)**: Independent - can start after Foundational
- **User Story 4 (P3)**: Independent - can start after Foundational
- **User Story 5 (P3)**: Soft dependency on US1 (extends query functionality) but independently testable

### Within Each User Story

1. Tests MUST be written FIRST and FAIL before implementation (TDD per constitution)
2. Models before services
3. Services before MCP tools
4. Core implementation before integration
5. Story complete before moving to next priority

### Parallel Opportunities

- **Setup (Phase 1)**: Tasks T002-T007 can run in parallel
- **Foundational (Phase 2)**:
  - Enums T008-T010 can run in parallel
  - Models T011-T013 can run in parallel
  - JSON serialization T020-T022 can run in parallel with database schema work
- **User Stories**: Once Foundational completes, all 5 user stories can start in parallel (if team capacity allows)
- **Within US1**: Tests T025-T028 and models T029-T031 can run in parallel
- **Within US2**: Tests T042-T044 and tools T049-T050 can run in parallel
- **Within US3**: Tests T054-T056 and models T057-T058 can run in parallel
- **Within US4**: Tests T065-T067 can run in parallel
- **Within US5**: Tests T076-T078 can run in parallel
- **Polish (Phase 12)**: Tasks T111-T119 can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together (write FIRST, ensure they FAIL):
Task T025: "Write FAILING integration test for querying events by type"
Task T026: "Write FAILING test for time range filtering"
Task T027: "Write FAILING test for severity filtering"
Task T028: "Write FAILING test for pagination"

# Launch all models for User Story 1 together:
Task T029: "Create EventQueryParameters model"
Task T030: "Create EventQueryResult model"
Task T031: "Create QueryResultMetadata model"

# Then implement repository methods (sequential, depends on tests)
Task T032: "Implement QueryEventsAsync" (verify all T025-T028 tests pass)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T007)
2. Complete Phase 2: Foundational (T008-T024) - CRITICAL
3. Complete Phase 3: User Story 1 (T025-T041)
4. **STOP and VALIDATE**: Run backtest, query events via MCP tool, verify results
5. Deploy/demo if ready

**MVP Delivers**: Agents can query backtest events by type, time range, and severity - core value proposition proven

### Incremental Delivery

1. **Foundation** (Phases 1-2) → Database + batch writing ready
2. **+ US1** (Phase 3) → Test independently → **Deploy/Demo (MVP!)**
3. **+ US2** (Phase 4) → Test independently → Deploy/Demo (event sequences)
4. **+ US3** (Phase 5) → Test independently → Deploy/Demo (aggregations)
5. **+ US4** (Phase 6) → Test independently → Deploy/Demo (state tracking)
6. **+ US5** (Phase 7) → Test independently → Deploy/Demo (filtering)
7. **+ MCP Server** (Phase 8) → Full MCP integration
8. **+ Validation** (Phase 9) → Robust error handling
9. **+ Performance** (Phase 10) → Meets all success criteria
10. Each increment adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers after Foundational phase completes:

- **Developer A**: User Story 1 (T025-T041) - Core query functionality
- **Developer B**: User Story 2 (T042-T053) - Event sequences
- **Developer C**: User Story 3 (T054-T064) - Aggregations
- **Developer D**: Validation & Error Handling (Phase 9, T093-T099) - Independent files
- **All together**: Performance testing (Phase 10) and Polish (Phase 12)

Stories complete and integrate independently, then merge for final MCP server integration.

---

## Task Summary

**Total Tasks**: 120

**Tasks by Phase**:
- Phase 1 (Setup): 7 tasks
- Phase 2 (Foundational): 17 tasks (BLOCKS all user stories)
- Phase 3 (US1 - P1): 17 tasks
- Phase 4 (US2 - P1): 12 tasks
- Phase 5 (US3 - P2): 11 tasks
- Phase 6 (US4 - P3): 11 tasks
- Phase 7 (US5 - P3): 10 tasks
- Phase 8 (MCP Server): 7 tasks
- Phase 9 (Validation): 7 tasks
- Phase 10 (Performance): 7 tasks
- Phase 11 (Backward Compat): 4 tasks
- Phase 12 (Polish): 10 tasks

**Parallel Opportunities**: 45 tasks marked [P] can run in parallel within their phase

**Independent Test Criteria**:
- US1: Query known events by type/time/severity, verify correct results returned
- US2: Query event chain by entity/parent, verify complete sequence returned
- US3: Aggregate 1000 events, verify calculations match expected values
- US4: Query state at timestamp, verify position/PnL matches expected state
- US5: Filter events by severity/category, verify no unwanted events returned

**Suggested MVP Scope**: Phases 1-3 (Setup + Foundational + US1) = 41 tasks
- Delivers core value: agents can query events efficiently
- Proves architecture and performance viability
- Foundation for incremental additions

---

## Notes

- [P] tasks = different files, no dependencies within phase
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- **TDD Required**: Write tests FIRST, verify they FAIL, then implement (per constitution Principle II)
- Verify tests pass after each implementation task
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Constitution compliance verified in Phase 12 (T120)
- Performance targets from spec.md validated in Phase 10

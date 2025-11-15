# Feature Specification: LLM-Agent-Friendly Events Logging

**Feature Branch**: `001-llm-agent-logging`
**Created**: 2025-11-13
**Status**: Draft
**Input**: User description: "In my current project I want to implement the following: ## 4. LLM-agent-friendly events logging of backtest execution: - Enhance logging in backtest engine to produce structured, LLM-agent-friendly logs. Requires full PRD and TRD. The idea is to enable LLM agents to analyze backtest runs and identify issues or optimization opportunities. Potential implementation could involve a DB engine with MCP as an agent-friendly interface (to save tokens, by fetching only relevant events). In the existing @StockSharp.AdvancedBacktest\DebugMode\ (made in accordance to @docs\5_TRD_DebugMode.md ) we already export events from backend, but I'd like to have them logged in an agent-friendly format"

## Overview

This feature enhances the existing backtest event logging system to produce structured, queryable logs optimized for analysis by LLM agents. The goal is to enable AI agents to identify performance issues, trading logic bugs, and optimization opportunities by querying specific event sequences without loading entire execution logs. This logging system could be used by E2E tests of indicators and strategies for asserting expected behavior.

## Clarifications

### Session 2025-11-15

- Q: What storage implementation will be used for the event database? → A: SQLite embedded database
- Q: How will the MCP server expose SQLite queries to agents? → A: MCP tools with structured query builders
- Q: What database schema approach will be used for storing event properties? → A: Hybrid normalized schema with JSON properties
- Q: When are events written to the SQLite database during backtest execution? → A: Batch commits during execution, queryable after completion
- Q: How should the system handle malformed or incomplete event information during writing? → A: Log warning and write event with validation metadata

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Query Backtest Events by Context (Priority: P1)

An LLM agent needs to analyze why a backtest run produced unexpected results (e.g., lower Sharpe ratio than expected). The agent queries for specific event types (trade executions, order rejections, indicator calculations) within a time window to identify patterns or anomalies.

**Why this priority**: This is the core use case - enabling agents to retrieve only relevant events without parsing massive log files. Without this, token costs for analysis become prohibitive.

**Independent Test**: Can be fully tested by running a backtest with known events, querying for specific event types via interface, and verifying the returned events match expectations. Delivers immediate value by allowing targeted event retrieval.

**Acceptance Scenarios**:

1. **Given** a completed backtest run, **When** agent queries for all trade execution events between timestamps T1 and T2, **Then** system returns only trade execution events within that time range in structured format
2. **Given** a backtest run with order rejections, **When** agent queries for rejection events with reason filters, **Then** system returns rejection events grouped by rejection reason
3. **Given** multiple backtest runs, **When** agent queries events by run identifier, **Then** system returns events only from the specified run

---

### User Story 2 - Analyze Event Sequences for Pattern Detection (Priority: P1)

An LLM agent investigates a trading strategy that enters positions but fails to exit properly. The agent queries for sequences of related events (order placement → execution → position update) to identify where the exit logic fails.

**Why this priority**: Pattern detection across event sequences is essential for debugging complex trading logic. This enables root cause analysis without manual log inspection.

**Independent Test**: Can be tested by creating a backtest with a known bug (e.g., missing exit condition), querying for position entry events without matching exit events, and verifying the agent can identify the pattern. Delivers value by automating bug detection.

**Acceptance Scenarios**:

1. **Given** a backtest with unclosed positions, **When** agent queries for entry events without matching exit events, **Then** system returns events showing position entries lacking corresponding exits
2. **Given** a backtest run, **When** agent queries for events by entity reference (e.g., specific order ID), **Then** system returns all events related to that entity in chronological order
3. **Given** event sequences, **When** agent requests causal relationships, **Then** system identifies and returns parent-child event relationships (e.g., order → execution → position update)

---

### User Story 3 - Aggregate Event Metrics for Performance Analysis (Priority: P2)

An LLM agent evaluates backtest performance by aggregating event metrics (trade win rate, average slippage, indicator calculation frequency) without retrieving individual events.

**Why this priority**: Aggregations enable high-level performance analysis with minimal token usage. While less critical than raw event retrieval, it significantly improves analysis efficiency.

**Independent Test**: Can be tested by running a backtest with known trade outcomes, querying for aggregated metrics (e.g., win rate, average profit), and verifying calculations match expected values. Delivers value by providing summary statistics for quick assessment.

**Acceptance Scenarios**:

1. **Given** a completed backtest, **When** agent requests trade win rate for a time period, **Then** system returns percentage of profitable trades without retrieving individual trade events
2. **Given** multiple backtests, **When** agent queries for comparative metrics, **Then** system returns aggregated metrics for each run enabling performance comparison
3. **Given** backtest events, **When** agent requests statistical summaries, **Then** system provides count, min, max, average, and standard deviation for numeric event properties

---

### User Story 4 - Track Strategy State Changes Over Time (Priority: P3)

An LLM agent analyzes how strategy state (positions, PnL, indicators) evolves during a backtest to understand decision-making context at specific moments.

**Why this priority**: State tracking is crucial for understanding "why did the strategy do X at time Y?" questions. Essential for debugging decision logic.

**Independent Test**: Can be tested by running a backtest with known state transitions, querying for state at specific timestamps, and verifying returned state matches expected values. Delivers value by providing decision context.

**Acceptance Scenarios**:

1. **Given** a backtest run, **When** agent queries for strategy state at timestamp T, **Then** system returns position, PnL, indicator values, and active orders at that moment
2. **Given** a backtest with multiple securities, **When** agent queries state for a specific security, **Then** system returns state scoped to that security only
3. **Given** state change events, **When** agent requests state delta between timestamps, **Then** system returns what changed between T1 and T2

---

### User Story 5 - Filter Events by Severity and Categories (Priority: P3)

An LLM agent focuses analysis on critical issues by filtering events by severity level (error, warning, info) or category (execution, risk, data).

**Why this priority**: Filtering reduces noise when investigating specific issue types. Less critical than core query functionality but improves usability.

**Independent Test**: Can be tested by logging events with different severity levels, querying for errors only, and verifying no info/warning events are returned. Delivers value by enabling focused troubleshooting.

**Acceptance Scenarios**:

1. **Given** backtest events with mixed severity levels, **When** agent queries for errors only, **Then** system returns events marked as errors without lower-severity events
2. **Given** events categorized by system (execution, market data, indicators), **When** agent filters by category, **Then** system returns only events in that category
3. **Given** nested event categories, **When** agent queries parent category, **Then** system returns all events in that category and subcategories

---

### Edge Cases

- What happens when an agent queries for events during a time range with no events? System should return empty result set with metadata indicating no matches.
- How does the system handle queries spanning multiple backtest runs? System should require explicit run identifier or return error indicating ambiguous query.
- What happens when event data contains malformed or incomplete information? System logs warning, writes event to database with validation metadata (error flags, list of missing/invalid fields), and continues backtest execution. Agents can query for events with validation issues to identify data quality problems.
- How are large result sets handled to prevent token overflow? System should support pagination and result size limits with clear indicators when results are truncated.
- What happens when an agent queries for future timestamps? System should return error indicating invalid time range.
- How does the system handle concurrent queries from multiple agents? System should maintain query isolation and consistent read semantics.
- What happens when querying for events before logging system was implemented? System should clearly indicate time ranges with missing or unavailable data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST log backtest events in a structured, machine-readable format that preserves semantic relationships between events
- **FR-002**: System MUST assign unique identifiers to each backtest run and each event within a run
- **FR-003**: System MUST capture event timestamps with millisecond precision to enable temporal ordering and querying
- **FR-004**: System MUST log event type, severity level, and category for every event
- **FR-005**: System MUST support querying events by time range, event type, severity, category, and backtest run identifier
- **FR-006**: System MUST support filtering events by entity references (order IDs, security symbols, position IDs)
- **FR-007**: System MUST provide event metadata including context (strategy state, market conditions) at the time of the event
- **FR-008**: System MUST support aggregation queries (count, sum, average) on numeric event properties without retrieving individual events
- **FR-009**: System MUST support pagination for large result sets with configurable page size
- **FR-010**: System MUST preserve causal relationships between events (e.g., order → execution → position update)
- **FR-011**: System MUST log events with structured properties (key-value pairs) rather than unstructured text logs, using hybrid schema with indexed core fields (timestamp, type, severity, run_id) and JSON column for event-specific properties
- **FR-012**: System MUST support querying for events by multiple criteria combined with logical operators (AND, OR)
- **FR-013**: System MUST provide query response metadata including result count, query execution time, and whether results are truncated
- **FR-014**: System MUST maintain backward compatibility with existing debug mode event export functionality
- **FR-015**: System MUST allow querying for state snapshots at specific timestamps
- **FR-016**: System MUST support querying for state changes (deltas) between two timestamps
- **FR-017**: System MUST provide clear error messages when queries fail validation or exceed resource limits
- **FR-018**: System MUST log event relationships explicitly (parent event ID, related entity IDs) to enable graph-based analysis
- **FR-019**: System MUST expose query functionality through MCP tools with structured parameters (e.g., get_events_by_type, aggregate_metrics) rather than raw SQL execution
- **FR-020**: Each MCP tool MUST provide clear parameter schemas defining required and optional query parameters with type validation
- **FR-021**: System MUST commit events to database in batches during backtest execution to balance memory usage and write performance
- **FR-022**: Event database MUST be queryable only after backtest run completes to ensure data consistency and avoid complex concurrent access patterns
- **FR-023**: System MUST validate event data during write operations and preserve malformed events with validation metadata (error flags, missing fields list) rather than rejecting them
- **FR-024**: System MUST log warnings for validation failures while allowing backtest execution to continue, ensuring data loss does not occur due to event quality issues

### Key Entities

- **Backtest Run**: Represents a single execution of a backtest with unique identifier, start/end timestamps, strategy configuration hash, and associated events
- **Event**: A logged occurrence during backtest execution with unique ID, timestamp, type, severity, category, properties (key-value pairs stored in JSON column), and relationships to other events and entities. Core fields (ID, timestamp, type, severity, category, run_id) are indexed for fast filtering; event-specific data stored in flexible JSON properties column
- **Event Type**: Classification of events (e.g., TradeExecution, OrderRejection, IndicatorCalculation, PositionUpdate, StateChange)
- **Event Severity**: Importance level (Error, Warning, Info, Debug) indicating criticality
- **Event Category**: Functional grouping (Execution, MarketData, Indicators, Risk, Performance) for filtering
- **Entity Reference**: Link to trading entities (Order ID, Security Symbol, Position ID, Indicator Name) enabling entity-based queries
- **State Snapshot**: Captured strategy state at a specific timestamp including positions, PnL, indicator values, active orders
- **Event Relationship**: Explicit link between related events (parent-child, causal, sequential) enabling pattern detection

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: LLM agents can retrieve specific event types from a backtest run containing 10,000+ events in under 2 seconds
- **SC-002**: Query results consume 50% fewer tokens compared to parsing equivalent unstructured log files
- **SC-003**: Agents can identify root causes of strategy issues (e.g., missing exit logic) through event sequence queries without human intervention in 80% of common debugging scenarios
- **SC-004**: System supports at least 100 concurrent agent queries without performance degradation
- **SC-005**: Aggregation queries return results in under 500ms for datasets with 100,000+ events
- **SC-006**: Query interface reduces agent token usage by 70% compared to loading and parsing full log files
- **SC-007**: Agents successfully detect known performance issues (e.g., excessive slippage, order rejections) through automated queries in 95% of test cases
- **SC-008**: System handles backtest runs with 1 million+ events without query timeout or memory overflow
- **SC-009**: Event log storage overhead remains under 20% compared to existing debug mode export file sizes
- **SC-010**: Query response metadata enables agents to determine if additional queries are needed without retrieving all data

## Assumptions

- The existing DebugMode event export infrastructure will be extended rather than replaced
- LLM agents will interact with the query interface programmatically through MCP tools with structured parameters (not via natural language queries requiring parsing or raw SQL execution)
- MCP tool definitions will serve as the contract for agent-system interaction, providing discoverability and type safety
- Event storage will persist beyond backtest execution for post-run analysis using SQLite database files (one file per backtest run or consolidated storage, to be determined during planning)
- Events are written in batches during backtest execution (not buffered entirely in memory) and database becomes available for querying only after backtest completion
- Batch size and commit frequency will be tuned during implementation to balance memory usage, write performance, and database lock contention
- Query interface will be designed for programmatic access, not human-readable formats
- Token efficiency is measured against loading equivalent data from JSONL files currently produced by DebugMode
- Backward compatibility means existing JSONL exports continue to work alongside new query interface
- Event volume estimates (10,000-1,000,000 events per run) are based on typical backtest durations and event frequency
- Query performance targets assume reasonable hardware (SSD storage, sufficient RAM for indexing)
- SQLite provides sufficient query performance and concurrency for local backtesting workloads without requiring separate database server infrastructure
- Hybrid schema (indexed core fields + JSON properties) provides optimal balance between query performance on common filters (time, type, severity) and flexibility for diverse event-specific properties without schema changes

## Dependencies

- Existing DebugMode event export infrastructure (DebugModeExporter, DebugEventBuffer, FileBasedWriter)
- Backtest execution framework (CustomStrategyBase, Strategy lifecycle hooks)
- Event data models (CandleDataPoint, IndicatorDataPoint, TradeDataPoint, StateDataPoint)
- SQLite embedded database (file-based storage with SQL query capabilities, no separate server infrastructure required)

## Out of Scope

- Natural language query parsing (agents use structured query API)
- Real-time event streaming or querying during backtest execution (database becomes queryable only after backtest completes, focus is on post-run analysis)
- Event visualization UI for humans (this is agent-focused infrastructure)
- Historical event migration from previous backtests run before this feature existed
- Event schema evolution and version management (assumes schema stability)
- Distributed query execution across multiple machines
- Event retention policies and automated cleanup
- Integration with external monitoring or observability platforms

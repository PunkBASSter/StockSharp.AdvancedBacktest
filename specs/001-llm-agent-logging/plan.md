# Implementation Plan: LLM-Agent-Friendly Events Logging

**Branch**: `001-llm-agent-logging` | **Date**: 2025-11-15 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-llm-agent-logging/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Enhance the existing backtest event logging system to produce structured, queryable logs optimized for LLM agent analysis. Implementation uses SQLite embedded database with hybrid schema (indexed core fields + JSON properties) and exposes queries through MCP tools with structured query builders. Events are written in batches during backtest execution and become queryable after completion, enabling agents to retrieve specific event sequences without loading entire logs.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**:
- SQLite (Microsoft.Data.Sqlite or System.Data.SQLite) - NEEDS CLARIFICATION on specific package choice
- System.Text.Json with source generation for event serialization
- MCP server SDK (NEEDS CLARIFICATION on .NET MCP server library)
- Existing StockSharp.AdvancedBacktest.DebugMode infrastructure

**Storage**: SQLite embedded database (file-based, hybrid schema with indexed core fields + JSON properties column)
**Testing**: xUnit v3 with Microsoft.NET.Test.Sdk targeting .NET 10
**Target Platform**: Windows/Linux (cross-platform .NET 10)
**Project Type**: Single library project extending existing StockSharp.AdvancedBacktest
**Performance Goals**:
- Query events from 10,000+ event runs in <2 seconds
- Aggregation queries on 100,000+ events in <500ms
- Support 100 concurrent agent queries without degradation
- Handle 1 million+ events per run without timeout

**Constraints**:
- Maintain backward compatibility with existing DebugMode JSONL exports
- Storage overhead <20% compared to current JSONL export sizes
- Post-run analysis only (no real-time querying during backtest execution)
- Batch commit strategy to balance memory usage and write performance

**Scale/Scope**:
- Event volume: 10,000-1,000,000 events per backtest run
- Multiple concurrent agent queries (100+)
- Token reduction: 70% fewer tokens vs loading full JSONL files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. Separation of Concerns
✅ **PASS** - Event logging system is pure infrastructure (not trading logic). SQLite storage, MCP query interface, and event writing are separate from strategy execution. Extends existing DebugMode infrastructure pattern.

### II. Test-First Development (NON-NEGOTIABLE)
⚠️ **PENDING** - Tests will be written first during implementation phase following TDD:
1. Write failing tests for event storage/retrieval
2. Implement SQLite event repository
3. Write failing tests for MCP query tools
4. Implement MCP server with query builders

### III. Financial Precision
✅ **PASS** - Event properties using System.Text.Json with custom decimal converters (already required by constitution). SQLite REAL type stores decimals without precision loss when properly configured. All numeric event properties (prices, PnL, metrics) will use decimal type.

### IV. Composition Over Inheritance
✅ **PASS** - Design favors composition:
- Event repository interface for storage abstraction
- Pluggable query builders as MCP tools
- Batch writer strategy pattern
- No deep inheritance hierarchies planned

### V. Explicit Visibility
✅ **PASS** - All classes will have explicit access modifiers. Self-documenting names for event types, query parameters, and storage components. Comments only for batch commit strategy and SQLite optimization rationale.

### VI. System.Text.Json Standard
✅ **PASS** - System.Text.Json with source generation for event serialization. JSON column in SQLite stores event properties. No Newtonsoft.Json needed (no StockSharp serialization dependency for this feature).

### VII. End-to-End Testability
✅ **PASS** - Fully testable in isolation:
- In-memory SQLite database for tests
- Mock backtest runs with synthetic events
- Validation of query results without real backtests
- MCP tool contract testing without live MCP server

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
StockSharp.AdvancedBacktest/
├── DebugMode/
│   ├── EventLogging/                    # NEW: Event logging infrastructure
│   │   ├── Models/                      # Event entity models
│   │   │   ├── BacktestRunEntity.cs
│   │   │   ├── EventEntity.cs
│   │   │   └── ValidationMetadata.cs
│   │   ├── Storage/                     # SQLite storage layer
│   │   │   ├── IEventRepository.cs
│   │   │   ├── SqliteEventRepository.cs
│   │   │   ├── DatabaseSchema.cs
│   │   │   └── BatchEventWriter.cs
│   │   ├── Serialization/               # JSON converters
│   │   │   └── EventJsonContext.cs      # Source-generated JSON context
│   │   └── Integration/                 # Integration with existing DebugMode
│   │       └── DebugModeEventLogger.cs
│   ├── DebugModeExporter.cs             # EXISTING - will integrate with EventLogger
│   ├── DebugEventBuffer.cs              # EXISTING
│   └── FileBasedWriter.cs               # EXISTING
│
├── McpServer/                            # NEW: MCP server implementation
│   ├── Tools/                            # MCP tool implementations
│   │   ├── GetEventsByTypeToolBuilder.cs
│   │   ├── AggregateMetricsToolBuilder.cs
│   │   ├── GetEventsByEntityToolBuilder.cs
│   │   ├── GetStateSnapshotToolBuilder.cs
│   │   └── QueryEventSequenceToolBuilder.cs
│   ├── Models/                           # Tool parameter models
│   │   ├── EventQueryParameters.cs
│   │   ├── AggregationParameters.cs
│   │   └── QueryResultMetadata.cs
│   └── BacktestEventMcpServer.cs         # Main MCP server

StockSharp.AdvancedBacktest.Tests/
├── EventLogging/                         # NEW: Event logging tests
│   ├── Storage/
│   │   ├── SqliteEventRepositoryTests.cs
│   │   └── BatchEventWriterTests.cs
│   ├── Integration/
│   │   └── DebugModeEventLoggerTests.cs
│   └── Serialization/
│       └── EventJsonContextTests.cs
│
└── McpServer/                            # NEW: MCP server tests
    ├── Tools/
    │   ├── GetEventsByTypeToolTests.cs
    │   ├── AggregateMetricsToolTests.cs
    │   └── QueryEventSequenceToolTests.cs
    └── Integration/
        └── BacktestEventMcpServerTests.cs
```

**Structure Decision**: Single project extension following existing StockSharp.AdvancedBacktest structure. Event logging components added under `DebugMode/EventLogging/` to maintain separation from existing file-based export. MCP server is new top-level namespace `McpServer/` as it's an independent infrastructure component. Tests mirror source structure in StockSharp.AdvancedBacktest.Tests.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitution violations. All gates pass or are pending implementation phase validation.

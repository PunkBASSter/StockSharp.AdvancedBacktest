# Implementation Plan: MCP Server Lifecycle Decoupling

**Branch**: `001-mcp-lifecycle-decoupling` | **Date**: 2025-12-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-mcp-lifecycle-decoupling/spec.md`

## Summary

Decouple MCP server lifecycle from backtest execution to enable post-backtest debugging. The MCP server will auto-start on the first `--ai-debug` backtest, remain alive after backtest completion, use a named mutex to ensure single-instance operation, and employ a FileSystemWatcher to detect database changes for automatic reconnection. Database will be recreated fresh on each backtest start.

## Technical Context

**Language/Version**: C# / .NET 8.0
**Primary Dependencies**: Microsoft.Data.Sqlite (8.0.11), ModelContextProtocol (0.4.0-preview.3), Microsoft.Extensions.Hosting (8.0.1)
**Storage**: SQLite (events.db) with WAL mode for concurrent read/write
**Testing**: xUnit v3 with Microsoft.NET.Test.Sdk (TDD required per constitution)
**Target Platform**: Windows (primary), cross-platform compatible via .NET
**Project Type**: Single solution with multiple projects (library + launcher)
**Performance Goals**: Query results within 5 seconds of backtest completion (SC-001), database cleanup within 10 seconds (SC-005)
**Constraints**: Single-machine environment, stdio transport for MCP, single MCP instance at a time
**Scale/Scope**: Single developer debugging scenario, databases up to 1GB

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Separation of Concerns | ✅ PASS | MCP server (infrastructure) separated from backtest execution (orchestration) |
| II. Test-First Development | ✅ REQUIRED | Tests must be written first for: mutex acquisition, FileSystemWatcher, database cleanup, lifecycle coordination |
| III. Financial Precision | ✅ N/A | Feature does not involve financial calculations |
| IV. Composition Over Inheritance | ✅ PASS | Using DI for IEventRepository, composable services |
| V. Explicit Visibility | ✅ REQUIRED | All new classes must have explicit access modifiers |
| VI. System.Text.Json Standard | ✅ N/A | No new JSON serialization required |
| VII. End-to-End Testability | ✅ REQUIRED | Must support mock database paths, isolated testing |

**Gate Status**: ✅ PASS - No violations requiring justification

## Project Structure

### Documentation (this feature)

```text
specs/001-mcp-lifecycle-decoupling/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── mcp-lifecycle.md # Lifecycle state machine contract
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
StockSharp.AdvancedBacktest/
├── DebugMode/
│   └── AiAgenticDebug/
│       ├── McpServer/
│       │   ├── BacktestEventMcpServer.cs      [MODIFY - add lifecycle management]
│       │   ├── McpServerLifecycleManager.cs   [NEW - singleton manager]
│       │   ├── McpInstanceLock.cs             [NEW - named mutex wrapper]
│       │   └── DatabaseWatcher.cs             [NEW - FileSystemWatcher wrapper]
│       └── EventLogging/
│           ├── Integration/
│           │   └── AgenticEventLogger.cs      [MODIFY - add MCP auto-start]
│           └── Storage/
│               └── DatabaseCleanup.cs         [NEW - cleanup logic]
└── Backtest/
    └── BacktestRunner.cs                      [MODIFY - integrate lifecycle manager]

StockSharp.AdvancedBacktest.Tests/
├── McpServer/
│   ├── McpServerLifecycleManagerTests.cs      [NEW]
│   ├── McpInstanceLockTests.cs                [NEW]
│   ├── DatabaseWatcherTests.cs                [NEW]
│   └── DatabaseCleanupTests.cs                [NEW]
└── Integration/
    └── McpLifecycleIntegrationTests.cs        [NEW]
```

**Structure Decision**: Extending existing `DebugMode/AiAgenticDebug` namespace with new lifecycle management classes. Tests follow existing pattern in `StockSharp.AdvancedBacktest.Tests`.

## Complexity Tracking

> No violations requiring justification - design follows existing patterns.

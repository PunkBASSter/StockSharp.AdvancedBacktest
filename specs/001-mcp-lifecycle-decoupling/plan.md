# Implementation Plan: MCP Server Lifecycle Decoupling

**Branch**: `001-mcp-lifecycle-decoupling` | **Date**: 2025-12-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-mcp-lifecycle-decoupling/spec.md`

## Summary

Decouple MCP server lifecycle from backtest execution by creating a separate executable project (`StockSharp.AdvancedBacktest.DebugEventLogMcpServer`) that runs as a detached process. The backtest application will lazily spawn this server on first `--ai-debug` run, and the server will persist after backtest completion for post-mortem debugging queries. Single-instance enforcement via named mutex, with graceful shutdown via `--shutdown` CLI flag using EventWaitHandle signaling.

## Technical Context

**Language/Version**: C# / .NET 8.0 (matching existing StockSharp.AdvancedBacktest)
**Primary Dependencies**:
- ModelContextProtocol (0.4.0-preview.3) - MCP server implementation
- Microsoft.Data.Sqlite (8.0.11) - Event database
- Microsoft.Extensions.Hosting (8.0.1) - Generic host for server lifecycle
**Storage**: SQLite file at `%LocalAppData%/StockSharp/AdvancedBacktest/event_logs.db`
**Testing**: xUnit v3 with Microsoft.NET.Test.Sdk
**Target Platform**: Windows (due to named mutex/EventWaitHandle IPC)
**Project Type**: Console application (new project) + library modifications
**Performance Goals**: MCP queries respond within 5 seconds of backtest completion
**Constraints**: Single-machine only, no distributed concerns
**Scale/Scope**: Single MCP instance, database up to 1GB

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Separation of Concerns | PASS | MCP server (infrastructure) cleanly separated from trading logic; new exe isolates process lifecycle |
| II. Test-First Development | REQUIRES ATTENTION | Tests must be written FIRST for: instance lock, shutdown signaling, database watcher, launcher |
| III. Financial Precision | N/A | No financial calculations in MCP server; event data is read-only |
| IV. Composition Over Inheritance | PASS | Components composed via DI: IEventRepository, IDatabaseWatcher, IMcpInstanceLock |
| V. Explicit Visibility | REQUIRES ATTENTION | All new classes/members must have explicit access modifiers |
| VI. System.Text.Json Standard | PASS | No new JSON serialization; MCP library handles protocol |
| VII. End-to-End Testability | PASS | Can test with synthetic SQLite DB; no external dependencies |

**Gate Result**: PASS (with TDD compliance required during implementation)

## Project Structure

### Documentation (this feature)

```text
specs/001-mcp-lifecycle-decoupling/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
# New console application project
StockSharp.AdvancedBacktest.DebugEventLogMcpServer/
├── StockSharp.AdvancedBacktest.DebugEventLogMcpServer.csproj
├── Program.cs                    # Entry point: parse args, handle --shutdown, run server
├── ServerStartup.cs              # Normal server startup logic
└── ShutdownHandler.cs            # --shutdown flag handling logic

# Existing library - modifications
StockSharp.AdvancedBacktest/
├── DebugMode/
│   └── AiAgenticDebug/
│       ├── McpServer/
│       │   ├── BacktestEventMcpServer.cs      # MODIFIED: Add CancellationToken support
│       │   ├── IMcpInstanceLock.cs            # NEW: Interface for mutex abstraction
│       │   ├── McpInstanceLock.cs             # NEW: Named mutex implementation
│       │   ├── IDatabaseWatcher.cs            # NEW: Interface for file watcher
│       │   ├── DatabaseWatcher.cs             # NEW: FileSystemWatcher implementation
│       │   ├── McpDatabasePaths.cs            # NEW: Centralized path management
│       │   ├── McpServerState.cs              # NEW: State enum
│       │   └── McpShutdownSignal.cs           # NEW: EventWaitHandle wrapper
│       └── EventLogging/
│           └── Storage/
│               ├── IDatabaseCleanup.cs        # NEW: Interface for cleanup
│               └── DatabaseCleanup.cs         # NEW: Pre-backtest DB cleanup
└── Backtest/
    └── BacktestRunner.cs                      # MODIFIED: Launch MCP exe, cleanup DB

# Tests
StockSharp.AdvancedBacktest.Tests/
└── McpServer/
    ├── McpInstanceLockTests.cs                # NEW
    ├── DatabaseWatcherTests.cs                # NEW
    ├── McpShutdownSignalTests.cs              # NEW
    ├── DatabaseCleanupTests.cs                # NEW
    └── McpLauncherIntegrationTests.cs         # NEW
```

**Structure Decision**: New console application project for MCP server executable, with shared interfaces/implementations remaining in the main library. Tests co-located with existing test project.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| 4th project (new exe) | MCP server must survive parent process exit | In-process server dies with backtest app; cannot achieve FR-001/FR-003 without separate process |

## Phase 0: Research Summary

### R1: Windows IPC for Single-Instance + Shutdown Signaling

**Decision**: Named Mutex + Named EventWaitHandle combination
**Rationale**:
- Named Mutex: Standard Windows pattern for single-instance apps; survives process crashes; auto-releases on process exit
- EventWaitHandle: Lightweight signaling without complex serialization; simpler than named pipes for "just shutdown" use case
**Alternatives Considered**:
- Named Pipes: More complex, overkill for simple shutdown signal
- Memory-mapped files: Overkill, requires manual synchronization
- Process enumeration: Unreliable, race conditions

### R2: Detached Process Spawning

**Decision**: `ProcessStartInfo` with `UseShellExecute = false`, no output redirection, let process inherit console
**Rationale**: Process needs to detach from parent without blocking; stdio used by MCP client, not parent
**Key Code Pattern**:
```csharp
var psi = new ProcessStartInfo
{
    FileName = mcpServerExePath,
    Arguments = $"--database \"{databasePath}\"",
    UseShellExecute = false,
    CreateNoWindow = true
};
Process.Start(psi);
// Do NOT wait - let it run independently
```

### R3: FileSystemWatcher for Database Changes

**Decision**: Watch parent directory for database file creation/deletion; debounce rapid events
**Rationale**: FR-007 requires MCP server to detect database recreation without restart
**Considerations**:
- Watch directory, not file (file gets deleted/recreated)
- Use 500ms debounce to coalesce rapid file system events
- Reopen SqliteConnection when file changes detected

### R4: Graceful SQLite Connection Handling

**Decision**: Dispose and reopen connection on database file change; use connection pooling OFF
**Rationale**: SQLite default pooling holds file locks; need clean release for cleanup
**Connection String**: `Data Source={path};Pooling=False`

## Phase 1: Design Artifacts

### Data Model

See [data-model.md](./data-model.md) for entity definitions.

Key entities:
- **McpServerState**: Enum (Stopped, Starting, Running, Reconnecting, Stopping, Error)
- **McpInstanceLock**: Wraps named mutex, tracks ownership
- **McpShutdownSignal**: Wraps EventWaitHandle for signaling

### Component Interactions

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         BacktestRunner                                   │
│                                                                         │
│  1. Check if MCP exe running (try acquire mutex)                        │
│  2. If not running: spawn detached MCP exe process                      │
│  3. Cleanup old database file (signal MCP to release connection first)  │
│  4. Run backtest (events written to new DB)                             │
│  5. Backtest completes → process exits                                  │
│  6. MCP exe continues running                                           │
└─────────────────────────────────────────────────────────────────────────┘
          │
          │ Spawns (if needed)
          ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                  DebugEventLogMcpServer.exe                             │
│                                                                         │
│  ┌──────────────┐  ┌──────────────────┐  ┌────────────────────────┐    │
│  │ McpInstance  │  │ DatabaseWatcher  │  │ McpShutdownSignal      │    │
│  │ Lock (Mutex) │  │ (FileSystem)     │  │ (EventWaitHandle)      │    │
│  └──────────────┘  └──────────────────┘  └────────────────────────┘    │
│         │                   │                      │                    │
│         │                   ▼                      ▼                    │
│         │          On file change:          On signal:                  │
│         │          - Close DB connection    - Graceful shutdown         │
│         │          - Reopen connection      - Close connections         │
│         │                                   - Release mutex             │
│         │                                   - Exit process              │
│         ▼                                                               │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    MCP Server (stdio)                            │   │
│  │  - GetEventsByType tool                                          │   │
│  │  - GetStateSnapshot tool                                         │   │
│  │  - (other query tools)                                           │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

### IPC Primitives

| Primitive | Name | Purpose |
|-----------|------|---------|
| Named Mutex | `Global\StockSharp.McpServer.Lock` | Single-instance enforcement |
| Named EventWaitHandle | `Global\StockSharp.McpServer.Shutdown` | Shutdown signaling |

### CLI Interface

```
StockSharp.AdvancedBacktest.DebugEventLogMcpServer.exe [options]

Options:
  --database <path>    Path to SQLite database file (required for normal run)
  --shutdown           Signal existing instance to shut down and exit
  --help               Display help
```

## Post-Design Constitution Re-Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Separation of Concerns | PASS | Clear separation: exe (process lifecycle), library (MCP protocol, data access) |
| II. Test-First Development | READY | Test interfaces defined; can write tests against IMcpInstanceLock, IDatabaseWatcher |
| III. Financial Precision | N/A | - |
| IV. Composition Over Inheritance | PASS | All components injectable via interfaces |
| V. Explicit Visibility | READY | Interface contracts define public surface |
| VI. System.Text.Json Standard | PASS | - |
| VII. End-to-End Testability | PASS | Can test process spawning, mutex behavior, file watcher in isolation |

**Final Gate Result**: PASS - Ready for task generation via `/speckit.tasks`

# Data Model: MCP Server Lifecycle Decoupling

**Feature**: 001-mcp-lifecycle-decoupling
**Date**: 2025-12-08

## Entities

### 1. McpServerState (Enum)

Represents the current state of the MCP server lifecycle.

```
┌─────────────┐
│   Stopped   │ ◄── Initial state
└─────┬───────┘
      │ Start()
      ▼
┌─────────────┐     Reconnect()     ┌─────────────────┐
│   Running   │ ◄─────────────────► │   Reconnecting  │
└─────┬───────┘                     └─────────────────┘
      │ Stop()
      ▼
┌─────────────┐
│   Stopped   │
└─────────────┘
```

**States**:
- `Stopped`: MCP server process not running
- `Starting`: MCP server process launching (transient)
- `Running`: MCP server accepting queries
- `Reconnecting`: MCP server closing old connection, opening new database
- `Stopping`: MCP server shutting down (transient)
- `Error`: MCP server encountered unrecoverable error

**Transitions**:
| From | Event | To |
|------|-------|-----|
| Stopped | Start() | Starting |
| Starting | ProcessStarted | Running |
| Starting | ProcessFailed | Error |
| Running | DatabaseChanged | Reconnecting |
| Reconnecting | ConnectionReady | Running |
| Reconnecting | ConnectionFailed | Error |
| Running | Stop() | Stopping |
| Stopping | ProcessExited | Stopped |
| Error | Restart() | Starting |

### 2. McpInstanceLock

Wraps OS-level named mutex for single-instance enforcement.

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| MutexName | string (const) | `"Global\StockSharp.McpServer.Lock"` |
| _mutex | Mutex | OS mutex handle |
| _ownsLock | bool | Whether this instance acquired the lock |
| _disposed | bool | Disposal state |

**Operations**:
- `TryAcquire()`: Attempts non-blocking acquisition, returns bool
- `IsAnotherInstanceRunning()`: Checks if mutex held by another process
- `Dispose()`: Releases mutex if owned

**Validation Rules**:
- Mutex name must be unique system-wide
- Must be disposed before process exit to release lock

### 3. McpShutdownSignal

Wraps EventWaitHandle for cross-process shutdown signaling.

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| EventName | string (const) | `"Global\StockSharp.McpServer.Shutdown"` |
| _handle | EventWaitHandle | OS event handle |
| _isOwner | bool | Whether this instance created the handle |

**Operations**:
- `CreateForServer()`: Creates new handle for server to listen on
- `OpenExisting()`: Opens existing handle for shutdown command
- `WaitForShutdown(ct)`: Blocks until signaled or cancelled
- `Signal()`: Signals shutdown to running server
- `Dispose()`: Releases handle

### 4. DatabaseWatcherConfig

Configuration for the FileSystemWatcher.

**Fields**:
| Field | Type | Default | Description |
|-------|------|---------|-------------|
| DatabasePath | string | required | Full path to SQLite database file |
| DebounceMs | int | 500 | Milliseconds to wait before triggering reconnect |
| WatchWalFiles | bool | true | Also watch -wal and -shm files |

### 5. DatabaseCleanupResult

Result of database cleanup operation.

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| Success | bool | Whether cleanup completed successfully |
| FilesDeleted | string[] | List of deleted file paths |
| ElapsedMs | long | Time taken for cleanup |
| Error | string? | Error message if failed |

### 6. McpServerLifecycleConfig

Configuration for the lifecycle manager.

**Fields**:
| Field | Type | Default | Description |
|-------|------|---------|-------------|
| DatabasePath | string | required | Path to SQLite database |
| AutoStart | bool | true | Auto-start on first backtest |
| StartupTimeoutMs | int | 10000 | Max wait for MCP server to start |
| ShutdownTimeoutMs | int | 5000 | Max wait for graceful shutdown |
| ReconnectDelayMs | int | 1000 | Delay after database change before reconnect |

## Relationships

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       Backtest Process                                   │
│  ┌──────────────────────┐                                               │
│  │  BacktestRunner      │                                               │
│  │  (orchestrator)      │                                               │
│  └──────────┬───────────┘                                               │
│             │ 1. Check if MCP running (mutex)                           │
│             │ 2. Spawn exe if not running                               │
│             │ 3. Cleanup old database                                   │
│             │ 4. Run backtest                                           │
│             │ 5. Exit (MCP keeps running)                               │
└─────────────┼───────────────────────────────────────────────────────────┘
              │
              │ spawns (detached)
              ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                  DebugEventLogMcpServer.exe (separate process)          │
│                                                                         │
│  ┌──────────────────┐  ┌──────────────────┐  ┌────────────────────┐    │
│  │ McpInstanceLock  │  │ McpShutdownSignal│  │ DatabaseWatcher    │    │
│  │ (named mutex)    │  │ (EventWaitHandle)│  │ (FileSystemWatcher)│    │
│  └────────┬─────────┘  └────────┬─────────┘  └────────┬───────────┘    │
│           │                     │                     │                 │
│           │ acquired            │ listens             │ monitors        │
│           ▼                     ▼                     ▼                 │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    MCP Server (stdio transport)                  │   │
│  │  - GetEventsByType                                               │   │
│  │  - GetStateSnapshot                                              │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
              │
              │ reads/writes
              ▼
    ┌─────────────────────────────────┐
    │  SQLite Database (events.db)    │
    │  + WAL files                    │
    └─────────────────────────────────┘
```

## State Transitions Detail

### Startup Flow (BacktestRunner spawns MCP exe)

```
1. BacktestRunner.InitializeAgenticLoggingAsync()
   └─► Check if MCP exe running (IsAnotherInstanceRunning via mutex check)
       ├─► [yes] → MCP already running, skip spawn
       └─► [no]  → Spawn DebugEventLogMcpServer.exe as detached process
                   └─► Process.Start() with UseShellExecute=false, CreateNoWindow=true

2. DebugEventLogMcpServer.exe startup (separate process)
   └─► Program.Main()
       ├─► McpInstanceLock.TryAcquire()
       │   └─► [fail] → Exit (another instance running)
       ├─► McpShutdownSignal.CreateForServer()
       ├─► DatabaseWatcher.Start()
       └─► BacktestEventMcpServer.RunAsync() (blocks on stdio)
```

### Database Cleanup Flow (BacktestRunner cleans DB, MCP reconnects)

```
1. BacktestRunner.RunAsync() [new backtest]
   └─► DatabaseCleanup.CleanupDatabaseAsync()
       └─► Delete events.db, events.db-wal, events.db-shm

2. MCP Server (running in separate process)
   └─► DatabaseWatcher detects file deletion/creation
       └─► OnDatabaseChanged event (after 500ms debounce)
           ├─► Close existing SqliteConnection
           └─► Open new SqliteConnection to fresh database
```

### Shutdown Flow (via --shutdown CLI)

```
1. User runs: DebugEventLogMcpServer.exe --shutdown

2. Shutdown instance (new process, exits quickly)
   ├─► Check mutex → not acquired = another instance running
   ├─► Open existing McpShutdownSignal
   ├─► Signal() → sets EventWaitHandle
   └─► Wait for mutex release (confirms shutdown)

3. Running MCP server instance
   └─► Background thread detects shutdown signal
       └─► cts.Cancel()
           ├─► Host stops gracefully
           ├─► Close SqliteConnection
           ├─► Dispose McpShutdownSignal
           └─► Dispose McpInstanceLock (releases mutex)
```

## Invariants

1. **Single Instance**: At most one MCP server process with acquired mutex at any time
2. **State Consistency**: State machine transitions are atomic and logged
3. **Database Access**: MCP server never holds exclusive lock on database (WAL mode)
4. **Cleanup Ordering**: Database files deleted only after connections closed
5. **Watcher Lifecycle**: FileSystemWatcher disposed before parent manager

## Error Handling

| Error Condition | Recovery Strategy |
|-----------------|-------------------|
| Mutex abandoned (process crash) | Mutex auto-released by OS, next TryAcquire succeeds |
| Database locked during cleanup | Retry with exponential backoff (max 5 attempts) |
| FileSystemWatcher buffer overflow | Log warning, trigger manual reconnect check |
| MCP process exit unexpectedly | Set state to Error, allow restart on next backtest |
| Database path invalid | Throw ArgumentException during config validation |

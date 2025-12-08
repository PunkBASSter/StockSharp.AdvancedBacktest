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
| MutexName | string (const) | `"Global\\StockSharp.AdvancedBacktest.McpServer"` |
| _mutex | Mutex | OS mutex handle |
| _ownsLock | bool | Whether this instance acquired the lock |
| _disposed | bool | Disposal state |

**Operations**:
- `TryAcquire()`: Attempts non-blocking acquisition, returns bool
- `IsHeld`: Property indicating if lock is currently held by any process
- `Dispose()`: Releases mutex if owned

**Validation Rules**:
- Mutex name must be unique system-wide
- Must be disposed before process exit to release lock

### 3. DatabaseWatcherConfig

Configuration for the FileSystemWatcher.

**Fields**:
| Field | Type | Default | Description |
|-------|------|---------|-------------|
| DatabasePath | string | required | Full path to SQLite database file |
| DebounceMs | int | 500 | Milliseconds to wait before triggering reconnect |
| WatchWalFiles | bool | true | Also watch -wal and -shm files |

### 4. DatabaseCleanupResult

Result of database cleanup operation.

**Fields**:
| Field | Type | Description |
|-------|------|-------------|
| Success | bool | Whether cleanup completed successfully |
| FilesDeleted | string[] | List of deleted file paths |
| ElapsedMs | long | Time taken for cleanup |
| Error | string? | Error message if failed |

### 5. McpServerLifecycleConfig

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
┌──────────────────────┐
│  BacktestRunner      │
│  (orchestrator)      │
└──────────┬───────────┘
           │ uses
           ▼
┌──────────────────────────────┐
│  McpServerLifecycleManager   │──────► McpServerState
│  (singleton)                 │
└──────────┬───────────────────┘
           │ owns
           ├──────────────────────┐
           ▼                      ▼
┌──────────────────┐    ┌─────────────────┐
│  McpInstanceLock │    │  DatabaseWatcher │
│  (named mutex)   │    │  (file events)   │
└──────────────────┘    └─────────────────┘
           │                      │
           │                      │ monitors
           ▼                      ▼
    ┌─────────────────────────────────┐
    │  SQLite Database (events.db)    │
    │  + WAL files                    │
    └─────────────────────────────────┘
```

## State Transitions Detail

### Startup Flow

```
1. BacktestRunner.InitializeAgenticLoggingAsync()
   └─► McpServerLifecycleManager.EnsureRunningAsync()
       ├─► McpInstanceLock.TryAcquire()
       │   ├─► [success] → Continue to start process
       │   └─► [fail] → Return true (existing instance running)
       └─► Start MCP server process
           └─► DatabaseWatcher.Start()
```

### Database Cleanup Flow

```
1. BacktestRunner.RunAsync() [new backtest]
   └─► DatabaseCleanup.CleanupDatabaseAsync()
       ├─► McpServerLifecycleManager.PrepareForCleanup()
       │   └─► State = Reconnecting
       │       └─► Close SqliteConnection
       ├─► Delete events.db, events.db-wal, events.db-shm
       └─► DatabaseWatcher triggers Created event
           └─► McpServerLifecycleManager.Reconnect()
               └─► State = Running
```

### Shutdown Flow

```
1. User signals shutdown (or process exit)
   └─► McpServerLifecycleManager.ShutdownAsync()
       ├─► State = Stopping
       ├─► DatabaseWatcher.Stop()
       ├─► _mcpProcess.Kill()
       ├─► McpInstanceLock.Dispose()
       └─► State = Stopped
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

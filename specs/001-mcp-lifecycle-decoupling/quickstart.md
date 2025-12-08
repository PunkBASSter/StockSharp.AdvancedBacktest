# Quickstart: MCP Server Lifecycle Decoupling

**Feature**: 001-mcp-lifecycle-decoupling
**Date**: 2025-12-08

## Overview

This feature decouples the MCP server lifecycle from backtest execution, enabling:
- Post-backtest debugging queries
- Single MCP instance management
- Fresh database on each backtest
- Automatic reconnection on database changes

## Getting Started

### 1. Run a Debug Backtest

```powershell
$env:StockSharp__HistoryPath="C:\path\to\Hydra\Storage"
$env:StockSharp__StorageFormat="Binary"
dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate -- --ai-debug
```

The MCP server will auto-start on the first `--ai-debug` backtest.

### 2. Query After Backtest Completes

The MCP server remains accessible after the backtest finishes. Use MCP tools to query:

```
# Via Claude Code or similar MCP client
get_events_by_type(type="TradeExecution", limit=10)
get_state_snapshot(timestamp="2020-07-03T14:00:00")
```

### 3. Run Multiple Backtests

Each new backtest:
1. Cleans up the previous database
2. Creates a fresh database
3. MCP server automatically reconnects

```powershell
# Run first backtest
dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate -- --ai-debug

# Query results...

# Run second backtest (database auto-cleaned)
dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate -- --ai-debug

# Query fresh results...
```

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                     Backtest Process                              │
│  1. Check if MCP exe running (mutex)                             │
│  2. Spawn DebugEventLogMcpServer.exe if not running              │
│  3. Cleanup old database                                          │
│  4. Run backtest (writes events)                                  │
│  5. Exit → MCP server keeps running                              │
└──────────────────────────────────────────────────────────────────┘
           │
           │ spawns (detached)           writes events
           ▼                                   │
┌──────────────────────────────────────────────│────────────────────┐
│  DebugEventLogMcpServer.exe (separate process)                    │
│  ┌─────────────────┐  ┌─────────────────┐    │                   │
│  │ McpInstanceLock │  │ DatabaseWatcher │    │                   │
│  │ (named mutex)   │  │ (file events)   │    │                   │
│  └─────────────────┘  └─────────────────┘    │                   │
│                              │               │                    │
│                              │ monitors      │                    │
│                              ▼               ▼                    │
│                       ┌─────────────────────────┐                 │
│                       │ SQLite Database         │                 │
│                       │ (events.db)             │                 │
│                       └─────────────────────────┘                 │
│                                                                   │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │                MCP Server (stdio transport)                │   │
│  │  AI agent (Claude Code) connects here for queries         │   │
│  └───────────────────────────────────────────────────────────┘   │
└───────────────────────────────────────────────────────────────────┘
```

## Key Components

### McpServerLifecycleManager

Singleton manager that controls MCP server lifecycle:

```csharp
// Ensure MCP server is running (auto-starts if needed)
await lifecycleManager.EnsureRunningAsync(databasePath);

// Prepare for database cleanup
await lifecycleManager.PrepareForCleanupAsync();

// Notify database is ready after cleanup
await lifecycleManager.NotifyDatabaseReadyAsync();

// Graceful shutdown
await lifecycleManager.ShutdownAsync();
```

### McpInstanceLock

Ensures single MCP instance via named mutex:

```csharp
using var instanceLock = new McpInstanceLock();
if (instanceLock.TryAcquire())
{
    // We have the lock, start MCP server
}
else
{
    // Another instance is running, reuse it
}
```

### DatabaseWatcher

Monitors database file changes:

```csharp
var watcher = new DatabaseWatcher(databasePath, debounceMs: 500);
watcher.DatabaseChanged += (s, e) =>
{
    // Reconnect to new database
};
watcher.Start();
```

### DatabaseCleanup

Safely deletes database files:

```csharp
var result = await cleanup.CleanupAsync(databasePath);
// result.Success, result.FilesDeleted, result.ElapsedMs
```

## Configuration

### Via BacktestConfig

```json
{
  "AgenticLogging": {
    "Enabled": true,
    "DatabasePath": "debug/events.db",
    "BatchSize": 1000,
    "FlushInterval": "00:00:30"
  }
}
```

### Via Environment Variable

```powershell
$env:STOCKSHARP_MCP_DATABASE="C:\custom\path\events.db"
```

## Testing

### Unit Tests (TDD)

Tests must be written first:

```csharp
public class McpInstanceLockTests
{
    [Fact]
    public void TryAcquire_WhenNotHeld_ReturnsTrue()
    {
        using var lock1 = new McpInstanceLock();
        Assert.True(lock1.TryAcquire());
    }

    [Fact]
    public void TryAcquire_WhenAlreadyHeld_ReturnsFalse()
    {
        using var lock1 = new McpInstanceLock();
        using var lock2 = new McpInstanceLock();

        Assert.True(lock1.TryAcquire());
        Assert.False(lock2.TryAcquire());
    }
}
```

### Integration Tests

```csharp
public class McpLifecycleIntegrationTests
{
    [Fact]
    public async Task MultipleBacktests_SingleMcpInstance()
    {
        // Run 10 backtests
        // Assert only 1 MCP instance throughout
    }

    [Fact]
    public async Task DatabaseCleanup_McpReconnects()
    {
        // Start MCP, query data
        // Cleanup database
        // Verify MCP reconnects to new DB
    }
}
```

## Troubleshooting

### MCP Server Not Starting

1. Check if another instance is running:
   ```powershell
   Get-Process | Where-Object { $_.ProcessName -like "*DebugEventLogMcpServer*" }
   ```

2. Manually stop existing instance:
   ```powershell
   StockSharp.AdvancedBacktest.DebugEventLogMcpServer.exe --shutdown
   ```

3. Check for orphaned mutex (process crash releases it automatically)

### Database Locked During Cleanup

1. MCP server connection should auto-close
2. If persists, check for external tools (DB Browser for SQLite)
3. Cleanup retries 5 times with 200ms backoff

### FileSystemWatcher Missing Events

1. Debounce may coalesce rapid changes
2. Manual reconnect available via `NotifyDatabaseReadyAsync()`
3. Check watcher error event log

## Related Documentation

- [spec.md](./spec.md) - Full specification
- [data-model.md](./data-model.md) - Entity definitions
- [contracts/mcp-lifecycle.md](./contracts/mcp-lifecycle.md) - Interface contracts
- [ai-debug.md](../../.claude/commands/ai-debug.md) - AI agent usage guide

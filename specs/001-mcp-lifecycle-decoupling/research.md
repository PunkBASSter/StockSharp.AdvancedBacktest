# Research: MCP Server Lifecycle Decoupling

**Feature**: 001-mcp-lifecycle-decoupling
**Date**: 2025-12-08

## Research Tasks

### 1. Named Mutex Implementation in .NET

**Context**: FR-002 requires single MCP instance enforcement via named mutex/semaphore.

**Decision**: Use `System.Threading.Mutex` with a named identifier.

**Rationale**:
- `Mutex` is the standard .NET mechanism for cross-process synchronization
- Named mutexes work across process boundaries (required since MCP server is separate process)
- Well-documented, battle-tested API
- Supports `WaitOne(0)` for non-blocking acquisition check

**Implementation Pattern**:
```csharp
public sealed class McpInstanceLock : IDisposable
{
    private const string MutexName = "Global\\StockSharp.AdvancedBacktest.McpServer";
    private readonly Mutex _mutex;
    private bool _ownsLock;

    public bool TryAcquire()
    {
        _mutex = new Mutex(false, MutexName, out bool createdNew);
        _ownsLock = _mutex.WaitOne(0); // Non-blocking
        return _ownsLock;
    }

    public void Dispose()
    {
        if (_ownsLock) _mutex.ReleaseMutex();
        _mutex?.Dispose();
    }
}
```

**Alternatives Considered**:
- **Semaphore**: Overkill for single-instance (count=1 scenario)
- **Lock file**: Less reliable (orphaned files on crash), requires cleanup logic
- **TCP port binding**: Adds network dependency, firewall concerns

---

### 2. FileSystemWatcher for Database Change Detection

**Context**: FR-007 requires MCP server to detect database file changes and reconnect.

**Decision**: Use `System.IO.FileSystemWatcher` with debouncing.

**Rationale**:
- Native .NET class, no external dependencies
- Supports filtering by file name pattern
- Events: Created, Changed, Deleted, Renamed
- Works with SQLite's WAL mode (watches main .db file)

**Implementation Pattern**:
```csharp
public sealed class DatabaseWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Action<string> _onDatabaseChanged;
    private readonly Timer _debounceTimer;

    public DatabaseWatcher(string databasePath, Action<string> onDatabaseChanged)
    {
        var directory = Path.GetDirectoryName(databasePath);
        var fileName = Path.GetFileName(databasePath);

        _watcher = new FileSystemWatcher(directory!, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.EnableRaisingEvents = true;
    }
}
```

**Debouncing Strategy**:
- SQLite writes trigger multiple events (WAL mode: main file + WAL + SHM)
- Use 500ms debounce timer to coalesce rapid events
- Only trigger reconnect once per logical database change

**Alternatives Considered**:
- **Polling**: Wastes CPU, slower detection
- **Named pipe from backtest**: More complex, requires IPC setup
- **Shared memory**: Overkill for file change notification

---

### 3. SQLite Connection Management During Database Recreation

**Context**: FR-008/FR-009 require graceful connection handling during database cleanup.

**Decision**: Implement connection pooling with explicit close-before-delete pattern.

**Rationale**:
- SQLite WAL mode allows concurrent readers
- Must close all connections before file deletion
- Connection pool pattern enables graceful reconnection

**Implementation Pattern**:
```csharp
public sealed class DatabaseCleanup
{
    public async Task CleanupDatabaseAsync(string databasePath, CancellationToken ct)
    {
        // 1. Signal MCP server to close connections
        // 2. Wait for confirmation (via filesystem event or callback)
        // 3. Delete database files: .db, .db-wal, .db-shm
        // 4. Signal MCP server to reconnect

        var files = new[]
        {
            databasePath,
            databasePath + "-wal",
            databasePath + "-shm"
        };

        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                await RetryDeleteAsync(file, ct);
            }
        }
    }

    private async Task RetryDeleteAsync(string path, CancellationToken ct)
    {
        const int maxRetries = 5;
        const int delayMs = 200;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs, ct);
            }
        }
        throw new IOException($"Failed to delete {path} after {maxRetries} attempts");
    }
}
```

**Race Condition Prevention**:
- MCP server tracks "reconnecting" state during cleanup
- Queries during reconnect return "database unavailable" error
- FileSystemWatcher triggers reconnect after new database created

**Alternatives Considered**:
- **Connection close callback**: More coupled, requires MCP server awareness of cleanup
- **SQLite `PRAGMA wal_checkpoint(TRUNCATE)`**: Flushes WAL but doesn't solve deletion

---

### 4. Separate Executable Project Architecture

**Context**: FR-001 requires MCP server to survive parent process exit.

**Decision**: Create separate console application `StockSharp.AdvancedBacktest.DebugEventLogMcpServer` that runs as a detached process.

**Rationale**:
- In-process server dies when backtest app exits - blocks post-mortem debugging
- Separate exe can be spawned by backtest, then continues independently
- MCP uses stdio transport - AI agent (Claude Code) connects directly to the exe
- BacktestRunner spawns the exe if not already running (lazy start)

**Project Structure**:
```
StockSharp.AdvancedBacktest.DebugEventLogMcpServer/
├── Program.cs                    # Entry point with --shutdown handling
├── StockSharp.AdvancedBacktest.DebugEventLogMcpServer.csproj
└── (references main library for shared types)
```

**Spawning Pattern (from BacktestRunner)**:
```csharp
var psi = new ProcessStartInfo
{
    FileName = mcpServerExePath,
    Arguments = $"--database \"{databasePath}\"",
    UseShellExecute = false,
    CreateNoWindow = true,
    // Do NOT redirect stdio - MCP client needs them
    RedirectStandardInput = false,
    RedirectStandardOutput = false,
    RedirectStandardError = false
};
Process.Start(psi);
// Do NOT wait - let it run independently
```

**Alternatives Considered**:
- **In-process hosting**: Dies with parent process - defeats FR-001
- **Windows Service**: Overkill, requires admin install, not dev-friendly
- **Background worker thread**: Still in-process, still dies with parent

---

### 5. Shutdown Signaling via EventWaitHandle

**Context**: FR-011/FR-012 require explicit shutdown mechanism for the detached MCP server.

**Decision**: Use `--shutdown` CLI flag with Named EventWaitHandle signaling.

**Rationale**:
- MCP server has no way to receive commands (stdio used by MCP protocol)
- Running another instance with `--shutdown` can signal existing instance
- EventWaitHandle is lightweight, no serialization overhead
- Named mutex already exists for instance detection - reuse for shutdown confirmation

**Shutdown Protocol**:
```
User runs: DebugEventLogMcpServer.exe --shutdown
    │
    ├─► 1. Try acquire mutex (WaitOne(0))
    │       └─► Acquired? No instance running → exit
    │
    ├─► 2. Open existing EventWaitHandle
    │       "Global\StockSharp.McpServer.Shutdown"
    │
    ├─► 3. Signal the handle: Set()
    │
    └─► 4. Wait for mutex release (confirms shutdown)
            └─► Timeout 10s? Warn user
```

**Server-Side Handling**:
```csharp
// In Program.cs
using var shutdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset,
    @"Global\StockSharp.McpServer.Shutdown");

using var cts = new CancellationTokenSource();

// Background thread monitors shutdown signal
_ = Task.Run(() =>
{
    shutdownEvent.WaitOne();
    cts.Cancel();
});

// Run server until cancellation
await BacktestEventMcpServer.RunAsync(args, databasePath, cts.Token);
```

**Shutdown Command Implementation**:
```csharp
private static async Task ShutdownExistingInstance()
{
    using var mutex = new Mutex(false, @"Global\StockSharp.McpServer.Lock", out _);

    if (mutex.WaitOne(0))
    {
        mutex.ReleaseMutex();
        Console.WriteLine("No MCP server running.");
        return;
    }

    using var signal = EventWaitHandle.OpenExisting(@"Global\StockSharp.McpServer.Shutdown");
    signal.Set();

    Console.WriteLine("Shutdown signal sent. Waiting...");

    if (mutex.WaitOne(TimeSpan.FromSeconds(10)))
    {
        mutex.ReleaseMutex();
        Console.WriteLine("MCP server stopped.");
    }
    else
    {
        Console.Error.WriteLine("Timeout - may need manual kill.");
    }
}
```

**Alternatives Considered**:
- **Named Pipes**: More complex, requires protocol design
- **HTTP endpoint**: Adds network dependency, firewall issues
- **Process.Kill**: Not graceful, may corrupt SQLite

---

### 6. Database Path Unification

**Context**: Current implementation has inconsistent database paths between backtest and MCP server.

**Decision**: Standardize on single configurable path with sensible default.

**Rationale**:
- Current defaults differ: `debug/events.db` vs `%LOCALAPPDATA%/StockSharp/...`
- Single source of truth prevents configuration drift
- Environment variable override for CI/testing scenarios

**Unified Path Strategy**:
```csharp
public static class McpDatabasePaths
{
    public static string GetDefaultPath()
    {
        // Default: relative to launcher project
        return Path.Combine(
            AppContext.BaseDirectory,
            "debug",
            "events.db"
        );
    }

    public static string GetPath(AgenticLoggingSettings? settings)
    {
        if (!string.IsNullOrEmpty(settings?.DatabasePath))
            return Path.GetFullPath(settings.DatabasePath);

        var envPath = Environment.GetEnvironmentVariable("STOCKSHARP_MCP_DATABASE");
        if (!string.IsNullOrEmpty(envPath))
            return Path.GetFullPath(envPath);

        return GetDefaultPath();
    }
}
```

**Alternatives Considered**:
- **Keep both paths**: Confusing, error-prone
- **Always use AppData**: Harder to locate for debugging
- **Config file only**: Less flexible for CI scenarios

---

## Summary of Technical Decisions

| Area | Decision | Key Benefit |
|------|----------|-------------|
| Instance Lock | Named Mutex (`Global\StockSharp.McpServer.Lock`) | Cross-process, auto-release on crash |
| Change Detection | FileSystemWatcher + 500ms debounce | Native, event-driven |
| Connection Management | `Pooling=False`, explicit close-before-delete | Prevents file locks |
| Process Architecture | Separate exe (`DebugEventLogMcpServer`) | Survives parent exit |
| Shutdown Signaling | `--shutdown` + EventWaitHandle | Graceful, no network deps |
| Database Path | Unified with env override | Single source of truth |

## Open Questions Resolved

All NEEDS CLARIFICATION items from Technical Context have been resolved through this research phase.

## References

- [System.Threading.Mutex](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex)
- [System.IO.FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)
- [SQLite WAL Mode](https://www.sqlite.org/wal.html)
- [MCP Protocol Specification](https://modelcontextprotocol.io/)

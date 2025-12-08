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

### 4. MCP Server Process Management

**Context**: FR-006 requires auto-start on first --ai-debug backtest.

**Decision**: Launch MCP server as child process via `Process.Start()`.

**Rationale**:
- MCP protocol uses stdio transport (requires separate process)
- Child process lifecycle can be managed by parent
- Existing pattern in codebase (DebugWebAppLauncher)

**Implementation Pattern**:
```csharp
public sealed class McpServerLifecycleManager
{
    private Process? _mcpProcess;
    private readonly McpInstanceLock _lock;

    public async Task<bool> EnsureRunningAsync(string databasePath)
    {
        if (!_lock.TryAcquire())
        {
            // Another instance already running - reuse it
            return true;
        }

        _mcpProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project McpServerProject -- --database \"{databasePath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        _mcpProcess.Start();
        // Don't wait - MCP server runs independently
        return true;
    }

    public void Shutdown()
    {
        _mcpProcess?.Kill(entireProcessTree: true);
        _lock.Dispose();
    }
}
```

**Alternatives Considered**:
- **In-process hosting**: Not possible with stdio transport
- **Background service**: More complex, requires host lifecycle management
- **Windows service**: Platform-specific, overkill for dev tooling

---

### 5. Database Path Unification

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
| Instance Lock | Named Mutex (`Global\\...`) | Cross-process, reliable |
| Change Detection | FileSystemWatcher + debounce | Native, event-driven |
| Connection Management | Explicit close-before-delete | Prevents file locks |
| Process Management | Child process via Process.Start | Independent lifecycle |
| Database Path | Unified with env override | Single source of truth |

## Open Questions Resolved

All NEEDS CLARIFICATION items from Technical Context have been resolved through this research phase.

## References

- [System.Threading.Mutex](https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex)
- [System.IO.FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)
- [SQLite WAL Mode](https://www.sqlite.org/wal.html)
- [MCP Protocol Specification](https://modelcontextprotocol.io/)

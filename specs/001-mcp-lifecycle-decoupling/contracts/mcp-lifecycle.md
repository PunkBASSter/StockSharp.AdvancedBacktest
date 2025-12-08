# MCP Lifecycle Contract

**Feature**: 001-mcp-lifecycle-decoupling
**Date**: 2025-12-08

## Interface Contracts

### IMcpServerLifecycleManager

Main entry point for MCP server lifecycle management.

```csharp
public interface IMcpServerLifecycleManager : IAsyncDisposable
{
    /// <summary>
    /// Current state of the MCP server.
    /// </summary>
    McpServerState State { get; }

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    event EventHandler<McpServerStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Ensures MCP server is running. Auto-starts if not running.
    /// Returns true if server is running (either started or already existed).
    /// </summary>
    Task<bool> EnsureRunningAsync(string databasePath, CancellationToken ct = default);

    /// <summary>
    /// Prepares MCP server for database cleanup (closes connections).
    /// </summary>
    Task PrepareForCleanupAsync(CancellationToken ct = default);

    /// <summary>
    /// Notifies MCP server that new database is ready.
    /// </summary>
    Task NotifyDatabaseReadyAsync(CancellationToken ct = default);

    /// <summary>
    /// Gracefully shuts down MCP server.
    /// </summary>
    Task ShutdownAsync(CancellationToken ct = default);
}
```

### IMcpInstanceLock

Single-instance enforcement.

```csharp
public interface IMcpInstanceLock : IDisposable
{
    /// <summary>
    /// Attempts to acquire the instance lock. Non-blocking.
    /// </summary>
    /// <returns>true if lock acquired, false if another instance holds it</returns>
    bool TryAcquire();

    /// <summary>
    /// Whether this instance currently holds the lock.
    /// </summary>
    bool IsAcquired { get; }
}
```

### IDatabaseWatcher

File system change detection.

```csharp
public interface IDatabaseWatcher : IDisposable
{
    /// <summary>
    /// Event raised when database file changes (debounced).
    /// </summary>
    event EventHandler<DatabaseChangedEventArgs>? DatabaseChanged;

    /// <summary>
    /// Starts watching for file changes.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops watching for file changes.
    /// </summary>
    void Stop();
}
```

### IDatabaseCleanup

Database cleanup operations.

```csharp
public interface IDatabaseCleanup
{
    /// <summary>
    /// Deletes the database and associated files (WAL, SHM).
    /// </summary>
    Task<DatabaseCleanupResult> CleanupAsync(string databasePath, CancellationToken ct = default);
}
```

## Event Contracts

### McpServerStateChangedEventArgs

```csharp
public sealed class McpServerStateChangedEventArgs : EventArgs
{
    public McpServerState PreviousState { get; init; }
    public McpServerState NewState { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
```

### DatabaseChangedEventArgs

```csharp
public sealed class DatabaseChangedEventArgs : EventArgs
{
    public string DatabasePath { get; init; } = string.Empty;
    public WatcherChangeTypes ChangeType { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
```

## Configuration Contracts

### McpServerLifecycleConfig

```csharp
public sealed class McpServerLifecycleConfig
{
    /// <summary>
    /// Path to SQLite database file.
    /// </summary>
    public required string DatabasePath { get; init; }

    /// <summary>
    /// Auto-start MCP server on first EnsureRunningAsync call. Default: true.
    /// </summary>
    public bool AutoStart { get; init; } = true;

    /// <summary>
    /// Maximum time to wait for MCP server to start. Default: 10 seconds.
    /// </summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum time to wait for graceful shutdown. Default: 5 seconds.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Delay after database change before triggering reconnect. Default: 1 second.
    /// </summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Debounce interval for FileSystemWatcher events. Default: 500ms.
    /// </summary>
    public TimeSpan WatcherDebounce { get; init; } = TimeSpan.FromMilliseconds(500);
}
```

## Behavioral Contracts

### Pre-conditions

| Method | Pre-condition |
|--------|---------------|
| `EnsureRunningAsync` | `databasePath` must be non-empty valid path |
| `PrepareForCleanupAsync` | State must be `Running` |
| `NotifyDatabaseReadyAsync` | State must be `Reconnecting` |
| `ShutdownAsync` | State must not be `Stopped` |
| `TryAcquire` | Lock not already acquired by this instance |

### Post-conditions

| Method | Post-condition |
|--------|----------------|
| `EnsureRunningAsync` (success) | State is `Running`, returns `true` |
| `EnsureRunningAsync` (already running) | State unchanged, returns `true` |
| `PrepareForCleanupAsync` | State is `Reconnecting` |
| `NotifyDatabaseReadyAsync` | State is `Running` |
| `ShutdownAsync` | State is `Stopped` |
| `TryAcquire` (success) | `IsAcquired` is `true` |
| `Dispose` | Mutex released if acquired |

### Invariants

1. State transitions follow defined state machine (see data-model.md)
2. At most one process holds the instance lock
3. DatabaseWatcher events are debounced (no rapid-fire callbacks)
4. Cleanup retries with backoff (max 5 attempts, 200ms base delay)

## Integration Points

### BacktestRunner Integration

```csharp
// In BacktestRunner.InitializeAgenticLoggingAsync()
private async Task InitializeAgenticLoggingAsync()
{
    if (_config.AgenticLogging?.Enabled != true) return;

    var databasePath = McpDatabasePaths.GetPath(_config.AgenticLogging);

    // Clean up previous database
    await _databaseCleanup.CleanupAsync(databasePath);

    // Ensure MCP server is running
    await _lifecycleManager.EnsureRunningAsync(databasePath);

    // Initialize logger (writes to database)
    _agenticLogger = new AgenticEventLogger(databasePath, ...);
}
```

### AgenticEventLogger Integration

```csharp
// AgenticEventLogger signals database ready after schema initialization
public async Task StartRunAsync(...)
{
    await _eventSink.InitializeAsync();
    await DatabaseSchema.InitializeAsync(_connection);

    // Notify MCP server database is ready
    await _lifecycleManager.NotifyDatabaseReadyAsync();
}
```

## Error Codes

| Code | Description |
|------|-------------|
| `MCP_ALREADY_RUNNING` | Another MCP instance holds the mutex |
| `MCP_START_TIMEOUT` | MCP server failed to start within timeout |
| `MCP_SHUTDOWN_TIMEOUT` | MCP server failed to stop within timeout |
| `DB_LOCKED` | Database file locked by another process |
| `DB_CLEANUP_FAILED` | Failed to delete database files after retries |
| `WATCHER_FAILED` | FileSystemWatcher encountered error |

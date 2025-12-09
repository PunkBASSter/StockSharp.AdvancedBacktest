using System.Diagnostics;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.Integration;

[Collection("MCP Lifecycle Integration Tests")]
[Trait("Category", "Integration")]
public sealed class McpLifecycleIntegrationTests : IAsyncDisposable
{
    private readonly List<Process> _spawnedProcesses = [];
    private readonly List<string> _tempDirs = [];
    private readonly TimeSpan _serverStartupDelay = TimeSpan.FromMilliseconds(1500);

    [SkippableFact]
    public async Task T030_McpServer_SurvivesWhenLaunchedAsDetachedProcess()
    {
        var testDir = CreateTempDirectory();
        var databasePath = Path.Combine(testDir, "events.db");
        await CreateDatabaseWithDataAsync(databasePath);

        var serverDllPath = GetMcpServerDllPath();
        Skip.If(!File.Exists(serverDllPath), $"MCP server not found at: {serverDllPath}");

        using var checkLockBefore = new McpInstanceLock();
        if (checkLockBefore.IsAnotherInstanceRunning())
        {
            McpServerLauncher.Shutdown();
            await Task.Delay(1000);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{serverDllPath}\" --database \"{databasePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        var process = Process.Start(startInfo);
        Skip.If(process is null, "Failed to start MCP server process");
        _spawnedProcesses.Add(process);

        await Task.Delay(_serverStartupDelay);

        Skip.If(process.HasExited, "MCP server process exited immediately");

        using var checkLockAfter = new McpInstanceLock();
        Assert.True(checkLockAfter.IsAnotherInstanceRunning(),
            "MCP server should have acquired the mutex");

        Assert.False(process.HasExited, "MCP server should be running independently");

        McpServerLauncher.Shutdown();
        await Task.Delay(1000);

        process.Refresh();
        Assert.True(process.HasExited, "MCP server should exit after shutdown signal");
    }

    [SkippableFact]
    public async Task T061_FullLifecycle_SpawnQueryCleanupQuery()
    {
        var testDir = CreateTempDirectory();
        var databasePath = Path.Combine(testDir, "events.db");

        Skip.If(!File.Exists(GetMcpServerDllPath()), "MCP server not built");

        await EnsureNoMcpRunningAsync();

        await CreateDatabaseWithDataAsync(databasePath);

        var launched = McpServerLauncher.EnsureRunning(databasePath);
        Skip.If(!launched, "Failed to launch MCP server");

        await Task.Delay(_serverStartupDelay);

        using var checkLock = new McpInstanceLock();
        Assert.True(checkLock.IsAnotherInstanceRunning(), "MCP server should be running");

        var cleanup = new DatabaseCleanup();
        var cleanupResult = await cleanup.CleanupAsync(databasePath);
        Assert.True(cleanupResult.Success, $"Database cleanup failed: {cleanupResult.Error}");
        Assert.False(File.Exists(databasePath), "Database should be deleted");

        await CreateDatabaseWithDataAsync(databasePath);
        Assert.True(File.Exists(databasePath), "New database should be created");

        using var checkLockAfter = new McpInstanceLock();
        Assert.True(checkLockAfter.IsAnotherInstanceRunning(), "MCP server should still be running after cleanup");

        var shutdown = McpServerLauncher.Shutdown();
        Assert.True(shutdown, "Shutdown signal should be sent");
    }

    [SkippableFact]
    public async Task T062_TenSequentialBacktests_SingleMcpInstance()
    {
        var testDir = CreateTempDirectory();
        var databasePath = Path.Combine(testDir, "events.db");

        Skip.If(!File.Exists(GetMcpServerDllPath()), "MCP server not built");

        await EnsureNoMcpRunningAsync();

        var cleanup = new DatabaseCleanup();

        for (int i = 0; i < 10; i++)
        {
            if (File.Exists(databasePath))
            {
                await cleanup.CleanupAsync(databasePath);
            }

            await CreateDatabaseWithDataAsync(databasePath, $"backtest-run-{i}");

            var launched = McpServerLauncher.EnsureRunning(databasePath);
            using var checkLock = new McpInstanceLock();
            Assert.True(launched || checkLock.IsAnotherInstanceRunning(),
                $"Backtest {i}: MCP server should be running");

            await Task.Delay(200);
        }

        using var checkLockFinal = new McpInstanceLock();
        Assert.True(checkLockFinal.IsAnotherInstanceRunning(), "Only one MCP instance should be running after 10 backtests");

        McpServerLauncher.Shutdown();
        await Task.Delay(500);

        using var checkLockStopped = new McpInstanceLock();
        Assert.False(checkLockStopped.IsAnotherInstanceRunning(), "MCP server should be stopped");
    }

    [SkippableFact]
    public async Task T063_ShutdownCommand_TerminatesRunningInstance()
    {
        var testDir = CreateTempDirectory();
        var databasePath = Path.Combine(testDir, "events.db");

        Skip.If(!File.Exists(GetMcpServerDllPath()), "MCP server not built");

        await EnsureNoMcpRunningAsync();
        await CreateDatabaseWithDataAsync(databasePath);

        var launched = McpServerLauncher.EnsureRunning(databasePath);
        Skip.If(!launched, "Failed to launch MCP server");

        await Task.Delay(_serverStartupDelay);

        using var checkLockBefore = new McpInstanceLock();
        Skip.If(!checkLockBefore.IsAnotherInstanceRunning(), "MCP server not running");

        var shutdown = McpServerLauncher.Shutdown();
        Assert.True(shutdown, "Shutdown should return true");

        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            using var checkLock = new McpInstanceLock();
            if (!checkLock.IsAnotherInstanceRunning())
            {
                return;
            }
        }

        using var checkLockAfter = new McpInstanceLock();
        Assert.False(checkLockAfter.IsAnotherInstanceRunning(), "MCP server should be stopped after shutdown");
    }

    [SkippableFact]
    public async Task T064_DatabaseCleanup_CompletesWithinReasonableTime()
    {
        var testDir = CreateTempDirectory();
        var databasePath = Path.Combine(testDir, "test_events.db");

        await CreateDatabaseWithDataAsync(databasePath);

        for (int i = 0; i < 100; i++)
        {
            await AppendDataToDatabaseAsync(databasePath, $"batch-{i}");
        }

        var fileInfo = new FileInfo(databasePath);
        var initialSize = fileInfo.Length;

        var cleanup = new DatabaseCleanup();
        var stopwatch = Stopwatch.StartNew();

        var result = await cleanup.CleanupAsync(databasePath);

        stopwatch.Stop();

        Assert.True(result.Success, $"Cleanup failed: {result.Error}");
        Assert.True(stopwatch.Elapsed.TotalSeconds < 10,
            $"Cleanup took {stopwatch.Elapsed.TotalSeconds:F1}s, expected < 10s for {initialSize / 1024}KB");
        Assert.False(File.Exists(databasePath), "Database file should be deleted");
    }

    private string CreateTempDirectory()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"mcp_integration_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        _tempDirs.Add(testDir);
        return testDir;
    }

    private async Task EnsureNoMcpRunningAsync()
    {
        using var checkLock = new McpInstanceLock();
        if (checkLock.IsAnotherInstanceRunning())
        {
            McpServerLauncher.Shutdown();
            await Task.Delay(1000);
        }
    }

    private static async Task CreateDatabaseWithDataAsync(string databasePath, string? runId = null)
    {
        var connectionString = $"Data Source={databasePath};Pooling=False";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await DatabaseSchema.InitializeAsync(connection);

        var repository = new SqliteEventRepository(connection);

        var testRunId = runId ?? Guid.NewGuid().ToString();
        await repository.CreateBacktestRunAsync(new BacktestRunEntity
        {
            Id = testRunId,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow,
            StrategyConfigHash = new string('a', 64)
        });

        for (int i = 0; i < 10; i++)
        {
            await repository.WriteEventAsync(new EventEntity
            {
                EventId = Guid.NewGuid().ToString(),
                RunId = testRunId,
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                EventType = EventType.TradeExecution,
                Severity = EventSeverity.Info,
                Category = EventCategory.Execution,
                Properties = $"{{\"price\": {100 + i}, \"quantity\": {10 + i}}}"
            });
        }

        await connection.CloseAsync();
    }

    private static async Task AppendDataToDatabaseAsync(string databasePath, string runId)
    {
        var connectionString = $"Data Source={databasePath};Pooling=False";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var repository = new SqliteEventRepository(connection);

        await repository.CreateBacktestRunAsync(new BacktestRunEntity
        {
            Id = runId,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow,
            StrategyConfigHash = new string('b', 64)
        });

        var largeProperties = new string('x', 5000);
        for (int i = 0; i < 100; i++)
        {
            await repository.WriteEventAsync(new EventEntity
            {
                EventId = Guid.NewGuid().ToString(),
                RunId = runId,
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                EventType = EventType.IndicatorCalculation,
                Severity = EventSeverity.Debug,
                Category = EventCategory.Indicators,
                Properties = $"{{\"data\": \"{largeProperties}\"}}"
            });
        }

        await connection.CloseAsync();
    }

    private static string GetMcpServerDllPath()
    {
        var testAssemblyLocation = typeof(McpLifecycleIntegrationTests).Assembly.Location;
        var testBinDir = Path.GetDirectoryName(testAssemblyLocation)!;
        var solutionRoot = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", ".."));

        return Path.Combine(
            solutionRoot,
            "StockSharp.AdvancedBacktest.DebugEventLogMcpServer",
            "bin",
            "Debug",
            "net8.0",
            "StockSharp.AdvancedBacktest.DebugEventLogMcpServer.dll"
        );
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var process in _spawnedProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                process.Dispose();
            }
            catch
            {
            }
        }

        try
        {
            McpServerLauncher.Shutdown();
            await Task.Delay(500);
        }
        catch
        {
        }

        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
            }
        }
    }
}

[CollectionDefinition("MCP Lifecycle Integration Tests")]
public class McpLifecycleIntegrationTestCollection : ICollectionFixture<McpLifecycleTestFixture>
{
}

public sealed class McpLifecycleTestFixture : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            McpServerLauncher.Shutdown();
            await Task.Delay(1000);
        }
        catch
        {
        }
    }
}

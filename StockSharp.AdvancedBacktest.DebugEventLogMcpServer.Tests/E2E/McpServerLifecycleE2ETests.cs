using StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Helpers;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.E2E;

[Collection("MCP E2E Tests")]
[Trait("Category", "E2E")]
public sealed class McpServerLifecycleE2ETests : IAsyncDisposable
{
    private TestDatabaseManager? _dbManager;
    private McpTestProcessLauncher? _launcher;

    private async Task StartServerAsync(string databasePath)
    {
        _launcher = new McpTestProcessLauncher();
        try
        {
            await _launcher.StartAsync(databasePath);
        }
        catch (McpServerAlreadyRunningException ex)
        {
            Skip.If(true, ex.Message);
        }
    }

    [SkippableFact]
    public async Task Server_StartupAndGracefulShutdown()
    {
        _dbManager = await TestDatabaseManager.CreateAsync();
        await _dbManager.CreateBacktestRunAsync();

        await StartServerAsync(_dbManager.DatabasePath);

        Assert.False(_launcher!.HasExited, "Server should be running after startup");

        // Initialize and verify server responds
        await using var client = new McpStdioClient(_launcher.StandardInput, _launcher.StandardOutput);
        var result = await client.InitializeAsync();
        Assert.True(result.TryGetProperty("protocolVersion", out _) ||
                    result.TryGetProperty("serverInfo", out _),
                    "Server should respond to initialize");

        // Graceful shutdown
        var stoppedGracefully = await _launcher.StopAsync(TimeSpan.FromSeconds(10));
        Assert.True(stoppedGracefully, "Server should stop gracefully");
        Assert.True(_launcher.HasExited, "Server should have exited");
        Assert.Equal(0, _launcher.ExitCode);
    }

    [SkippableFact]
    public async Task Server_InitializesWithDatabase()
    {
        _dbManager = await TestDatabaseManager.CreateAsync();
        var runId = await _dbManager.CreateBacktestRunAsync();
        await _dbManager.PopulateWithMockDataAsync(runId, MockDataProfile.Minimal);

        await StartServerAsync(_dbManager.DatabasePath);

        await using var client = new McpStdioClient(_launcher!.StandardInput, _launcher.StandardOutput);
        await client.InitializeAsync();

        // Verify the server can query the database
        var result = await client.CallToolAsync("GetEventsByTypeAsync", new
        {
            runId = runId,
            eventType = "TradeExecution",
            pageSize = 10,
            pageIndex = 0
        });

        Assert.True(result.TryGetProperty("content", out _), "Should return tool result");
    }

    [SkippableFact]
    public async Task Server_HandlesInvalidToolGracefully()
    {
        _dbManager = await TestDatabaseManager.CreateAsync();
        await _dbManager.CreateBacktestRunAsync();

        await StartServerAsync(_dbManager.DatabasePath);

        await using var client = new McpStdioClient(_launcher!.StandardInput, _launcher.StandardOutput);
        await client.InitializeAsync();

        // Call with invalid run ID - should return error in content, not crash
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await client.CallToolAsync("GetEventsByTypeAsync", new
            {
                runId = "non-existent-run-id",
                eventType = "InvalidEventType",
                pageSize = 10,
                pageIndex = 0
            });
        });

        // Server should still be running
        Assert.False(_launcher.HasExited, "Server should not crash on invalid input");
    }

    public async ValueTask DisposeAsync()
    {
        if (_launcher is not null)
        {
            if (!_launcher.HasExited)
            {
                await _launcher.StopAsync(TimeSpan.FromSeconds(5));
            }
            await _launcher.DisposeAsync();
        }

        if (_dbManager is not null)
            await _dbManager.DisposeAsync();
    }
}

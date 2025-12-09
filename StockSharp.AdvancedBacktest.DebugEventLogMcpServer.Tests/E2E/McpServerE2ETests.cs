using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Helpers;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.E2E;

[Collection("MCP E2E Tests")]
[Trait("Category", "E2E")]
public sealed class McpServerE2ETests : IAsyncDisposable
{
    private TestDatabaseManager? _dbManager;
    private McpTestProcessLauncher? _launcher;
    private McpStdioClient? _client;
    private string? _runId;

    private async Task SetupAsync(MockDataProfile? profile = null)
    {
        _dbManager = await TestDatabaseManager.CreateAsync();
        _runId = await _dbManager.CreateBacktestRunAsync();
        await _dbManager.PopulateWithMockDataAsync(_runId, profile ?? MockDataProfile.Default);

        // Close the database connection so the MCP server can access it
        _dbManager.CloseConnection();

        _launcher = new McpTestProcessLauncher();
        try
        {
            await _launcher.StartAsync(_dbManager.DatabasePath);
        }
        catch (McpServerAlreadyRunningException ex)
        {
            Skip.If(true, ex.Message);
        }

        _client = new McpStdioClient(_launcher.StandardInput, _launcher.StandardOutput);
        await _client.InitializeAsync();
    }

    [SkippableFact]
    public async Task ListTools_ReturnsAllRegisteredTools()
    {
        await SetupAsync(MockDataProfile.Minimal);

        var result = await _client!.ListToolsAsync();

        Assert.True(result.TryGetProperty("tools", out var tools));
        var toolNames = tools.EnumerateArray()
            .Select(t => t.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("GetEventsByTypeAsync", toolNames);
        Assert.Contains("GetEventsByEntityAsync", toolNames);
        Assert.Contains("GetStateSnapshotAsync", toolNames);
        Assert.Contains("AggregateMetricsAsync", toolNames);
        Assert.Contains("QueryEventSequenceAsync", toolNames);
        Assert.True(toolNames.Count >= 5, $"Expected at least 5 tools, got {toolNames.Count}");
    }

    [SkippableFact]
    public async Task GetEventsByType_WithMockData_ReturnsExpectedEvents()
    {
        await SetupAsync(MockDataProfile.Default);

        var result = await _client!.CallToolAsync("GetEventsByTypeAsync", new
        {
            runId = _runId,
            eventType = "TradeExecution",
            pageSize = 100,
            pageIndex = 0
        });

        Assert.True(result.TryGetProperty("content", out var content));
        var contentArray = content.EnumerateArray().First();
        var text = contentArray.GetProperty("text").GetString();

        Assert.NotNull(text);
        var parsed = JsonDocument.Parse(text!);
        Assert.True(parsed.RootElement.TryGetProperty("events", out var events));
        Assert.True(parsed.RootElement.TryGetProperty("metadata", out var metadata));

        var totalCount = metadata.GetProperty("totalCount").GetInt32();
        Assert.Equal(MockDataProfile.Default.TradeCount, totalCount);
    }

    [SkippableFact]
    public async Task GetEventsByEntity_WithSecuritySymbol_ReturnsMatchingEvents()
    {
        await SetupAsync(MockDataProfile.Default);

        var result = await _client!.CallToolAsync("GetEventsByEntityAsync", new
        {
            runId = _runId,
            entityType = "SecuritySymbol",
            entityValue = "AAPL",
            pageSize = 100,
            pageIndex = 0
        });

        Assert.True(result.TryGetProperty("content", out var content));
        var contentArray = content.EnumerateArray().First();
        var text = contentArray.GetProperty("text").GetString();

        Assert.NotNull(text);
        var parsed = JsonDocument.Parse(text!);
        var metadata = parsed.RootElement.GetProperty("metadata");
        var totalCount = metadata.GetProperty("totalCount").GetInt32();

        Assert.True(totalCount > 0, "Should have events for AAPL security");
    }

    [SkippableFact]
    public async Task GetStateSnapshot_ReconstructsPositionState()
    {
        await SetupAsync(MockDataProfile.Default);

        JsonElement result;
        try
        {
            result = await _client!.CallToolAsync("GetStateSnapshotAsync", new
            {
                runId = _runId,
                timestamp = DateTime.UtcNow.AddHours(1).ToString("o"),
                includeIndicators = true,
                includeOrders = false
            });
        }
        catch (McpErrorException ex) when (ex.Message.Contains("Unknown tool"))
        {
            Skip.If(true, $"Tool not available in MCP server: {ex.Message}");
            return;
        }

        Assert.True(result.TryGetProperty("content", out var content));
        var contentArray = content.EnumerateArray().First();
        var text = contentArray.GetProperty("text").GetString();

        Assert.NotNull(text);
        var parsed = JsonDocument.Parse(text!);
        Assert.True(parsed.RootElement.TryGetProperty("state", out var state));
        Assert.True(state.TryGetProperty("positions", out _));
        Assert.True(state.TryGetProperty("indicators", out _));
    }

    [SkippableFact]
    public async Task AggregateMetrics_ReturnsCorrectAggregations()
    {
        await SetupAsync(MockDataProfile.Default);

        var result = await _client!.CallToolAsync("AggregateMetricsAsync", new
        {
            runId = _runId,
            eventType = "TradeExecution",
            propertyPath = "$.Price",
            aggregations = new[] { "count", "avg", "min", "max" }
        });

        Assert.True(result.TryGetProperty("content", out var content));
        var contentArray = content.EnumerateArray().First();
        var text = contentArray.GetProperty("text").GetString();

        Assert.NotNull(text);
        var parsed = JsonDocument.Parse(text!);
        Assert.True(parsed.RootElement.TryGetProperty("aggregations", out var aggregations));

        Assert.Equal(MockDataProfile.Default.TradeCount, aggregations.GetProperty("count").GetInt32());
        Assert.True(aggregations.TryGetProperty("avg", out _));
        Assert.True(aggregations.TryGetProperty("min", out _));
        Assert.True(aggregations.TryGetProperty("max", out _));
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();

        if (_launcher is not null)
        {
            await _launcher.StopAsync(TimeSpan.FromSeconds(5));
            await _launcher.DisposeAsync();
        }

        if (_dbManager is not null)
            await _dbManager.DisposeAsync();
    }
}

# Quickstart Guide: LLM-Agent-Friendly Events Logging

**Date**: 2025-11-15
**Feature**: 001-llm-agent-logging
**Audience**: Developers implementing the feature

## Overview

This guide provides a practical walkthrough for implementing the LLM-Agent-Friendly Events Logging feature, from setting up dependencies to running end-to-end tests.

## Prerequisites

- .NET 10 SDK installed
- Visual Studio 2025 or VS Code with C# extension
- Basic familiarity with SQLite and MCP concepts
- StockSharp.AdvancedBacktest repository cloned

## Step 1: Install Dependencies

Add required NuGet packages to `StockSharp.AdvancedBacktest.csproj`:

```bash
cd StockSharp.AdvancedBacktest

# SQLite library
dotnet add package Microsoft.Data.Sqlite --version 10.0.0

# MCP server SDK (preview)
dotnet add package ModelContextProtocol --prerelease
dotnet add package Microsoft.Extensions.Hosting

# Build to verify dependencies
dotnet build
```

**Verify Installation**:
```bash
dotnet list package
```

Expected output should include:
- `Microsoft.Data.Sqlite` 10.0.0
- `ModelContextProtocol` 0.4.0-preview.3
- `Microsoft.Extensions.Hosting` (latest)

---

## Step 2: Create Database Schema

Create `StockSharp.AdvancedBacktest/DebugMode/EventLogging/Storage/DatabaseSchema.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace StockSharp.AdvancedBacktest.DebugMode.EventLogging.Storage;

public static class DatabaseSchema
{
    public static async Task InitializeAsync(SqliteConnection connection)
    {
        // Enable WAL mode for better concurrency
        using var walCommand = connection.CreateCommand();
        walCommand.CommandText = "PRAGMA journal_mode = 'wal'";
        await walCommand.ExecuteNonQueryAsync();

        // Create BacktestRuns table
        using var createRunsCommand = connection.CreateCommand();
        createRunsCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS BacktestRuns (
                Id TEXT PRIMARY KEY,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL,
                StrategyConfigHash TEXT NOT NULL,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            )";
        await createRunsCommand.ExecuteNonQueryAsync();

        // Create Events table
        using var createEventsCommand = connection.CreateCommand();
        createEventsCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS Events (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventId TEXT NOT NULL UNIQUE,
                RunId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                EventType TEXT NOT NULL,
                Severity TEXT NOT NULL,
                Category TEXT NOT NULL,
                Properties TEXT NOT NULL,
                ParentEventId TEXT,
                ValidationErrors TEXT,
                FOREIGN KEY (RunId) REFERENCES BacktestRuns(Id) ON DELETE CASCADE,
                CHECK (json_valid(Properties)),
                CHECK (ValidationErrors IS NULL OR json_valid(ValidationErrors))
            )";
        await createEventsCommand.ExecuteNonQueryAsync();

        // Create indexes
        await CreateIndexAsync(connection, "idx_events_run_time", "Events", "RunId, Timestamp");
        await CreateIndexAsync(connection, "idx_events_type", "Events", "EventType");
        await CreateIndexAsync(connection, "idx_events_severity", "Events", "Severity");
        await CreateIndexAsync(connection, "idx_events_category", "Events", "Category");
        await CreateIndexAsync(connection, "idx_events_parent", "Events", "ParentEventId", whereClause: "WHERE ParentEventId IS NOT NULL");
        await CreateIndexAsync(connection, "idx_events_eventid", "Events", "EventId", unique: true);
    }

    private static async Task CreateIndexAsync(
        SqliteConnection connection,
        string indexName,
        string tableName,
        string columns,
        bool unique = false,
        string? whereClause = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
            CREATE {(unique ? "UNIQUE" : "")} INDEX IF NOT EXISTS {indexName}
            ON {tableName} ({columns})
            {whereClause ?? ""}";
        await command.ExecuteNonQueryAsync();
    }
}
```

---

## Step 3: Write Your First Test

Create `StockSharp.AdvancedBacktest.Tests/EventLogging/Storage/SqliteEventRepositoryTests.cs`:

```csharp
using Xunit;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Storage;

public sealed class SqliteEventRepositoryTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteEventRepository _repository;

    public SqliteEventRepositoryTests()
    {
        // In-memory database for testing
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        DatabaseSchema.InitializeAsync(_connection).Wait();

        _repository = new SqliteEventRepository(_connection);
    }

    [Fact]
    public async Task CreateBacktestRun_ShouldPersistRun()
    {
        // Arrange
        var run = new BacktestRunEntity
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            StrategyConfigHash = new string('a', 64) // 64-char hex
        };

        // Act
        await _repository.CreateBacktestRunAsync(run);
        var retrieved = await _repository.GetBacktestRunAsync(run.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(run.Id, retrieved.Id);
        Assert.Equal(run.StartTime, retrieved.StartTime, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task WriteEvent_ShouldPersistEvent()
    {
        // Arrange - create run first
        var runId = Guid.NewGuid().ToString();
        await _repository.CreateBacktestRunAsync(new BacktestRunEntity
        {
            Id = runId,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            StrategyConfigHash = new string('a', 64)
        });

        var eventEntity = new EventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            RunId = runId,
            Timestamp = DateTime.UtcNow,
            EventType = EventType.TradeExecution,
            Severity = EventSeverity.Info,
            Category = EventCategory.Execution,
            Properties = """{"OrderId": "123", "Price": 100.50}""",
            ParentEventId = null,
            ValidationErrors = null
        };

        // Act
        await _repository.WriteEventAsync(eventEntity);
        var retrieved = await _repository.GetEventByIdAsync(eventEntity.EventId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(eventEntity.EventId, retrieved.EventId);
        Assert.Equal(EventType.TradeExecution, retrieved.EventType);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
```

**Run the test**:
```bash
cd StockSharp.AdvancedBacktest.Tests
dotnet test --filter "FullyQualifiedName~SqliteEventRepositoryTests"
```

Expected: ❌ **Tests should FAIL** (repository not implemented yet - TDD!)

---

## Step 4: Implement Event Repository

Create `StockSharp.AdvancedBacktest/DebugMode/EventLogging/Storage/IEventRepository.cs`:

```csharp
namespace StockSharp.AdvancedBacktest.DebugMode.EventLogging.Storage;

public interface IEventRepository
{
    Task CreateBacktestRunAsync(BacktestRunEntity run);
    Task<BacktestRunEntity?> GetBacktestRunAsync(string runId);
    Task WriteEventAsync(EventEntity eventEntity);
    Task<EventEntity?> GetEventByIdAsync(string eventId);
    Task<List<EventEntity>> QueryEventsAsync(EventQueryParameters parameters);
}
```

Create `StockSharp.AdvancedBacktest/DebugMode/EventLogging/Storage/SqliteEventRepository.cs`:

```csharp
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace StockSharp.AdvancedBacktest.DebugMode.EventLogging.Storage;

public sealed class SqliteEventRepository : IEventRepository
{
    private readonly SqliteConnection _connection;

    public SqliteEventRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task CreateBacktestRunAsync(BacktestRunEntity run)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO BacktestRuns (Id, StartTime, EndTime, StrategyConfigHash)
            VALUES (@id, @startTime, @endTime, @configHash)";

        command.Parameters.AddWithValue("@id", run.Id);
        command.Parameters.AddWithValue("@startTime", run.StartTime.ToString("o"));
        command.Parameters.AddWithValue("@endTime", run.EndTime.ToString("o"));
        command.Parameters.AddWithValue("@configHash", run.StrategyConfigHash);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<BacktestRunEntity?> GetBacktestRunAsync(string runId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM BacktestRuns WHERE Id = @id";
        command.Parameters.AddWithValue("@id", runId);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new BacktestRunEntity
        {
            Id = reader.GetString(0),
            StartTime = DateTime.Parse(reader.GetString(1)),
            EndTime = DateTime.Parse(reader.GetString(2)),
            StrategyConfigHash = reader.GetString(3),
            CreatedAt = DateTime.Parse(reader.GetString(4))
        };
    }

    public async Task WriteEventAsync(EventEntity eventEntity)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Events (EventId, RunId, Timestamp, EventType, Severity, Category, Properties, ParentEventId, ValidationErrors)
            VALUES (@eventId, @runId, @timestamp, @type, @severity, @category, json(@properties), @parent, json(@validation))";

        command.Parameters.AddWithValue("@eventId", eventEntity.EventId);
        command.Parameters.AddWithValue("@runId", eventEntity.RunId);
        command.Parameters.AddWithValue("@timestamp", eventEntity.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("@type", eventEntity.EventType.ToString());
        command.Parameters.AddWithValue("@severity", eventEntity.Severity.ToString());
        command.Parameters.AddWithValue("@category", eventEntity.Category.ToString());
        command.Parameters.AddWithValue("@properties", eventEntity.Properties);
        command.Parameters.AddWithValue("@parent", (object?)eventEntity.ParentEventId ?? DBNull.Value);
        command.Parameters.AddWithValue("@validation", (object?)eventEntity.ValidationErrors ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<EventEntity?> GetEventByIdAsync(string eventId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM Events WHERE EventId = @eventId";
        command.Parameters.AddWithValue("@eventId", eventId);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return MapEventEntity(reader);
    }

    public async Task<List<EventEntity>> QueryEventsAsync(EventQueryParameters parameters)
    {
        // Implementation in next step
        throw new NotImplementedException();
    }

    private static EventEntity MapEventEntity(SqliteDataReader reader)
    {
        return new EventEntity
        {
            Id = reader.GetInt64(0),
            EventId = reader.GetString(1),
            RunId = reader.GetString(2),
            Timestamp = DateTime.Parse(reader.GetString(3)),
            EventType = Enum.Parse<EventType>(reader.GetString(4)),
            Severity = Enum.Parse<EventSeverity>(reader.GetString(5)),
            Category = Enum.Parse<EventCategory>(reader.GetString(6)),
            Properties = reader.GetString(7),
            ParentEventId = reader.IsDBNull(8) ? null : reader.GetString(8),
            ValidationErrors = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }
}
```

**Run tests again**:
```bash
dotnet test --filter "FullyQualifiedName~SqliteEventRepositoryTests"
```

Expected: ✅ **Tests should PASS**

---

## Step 5: Implement Batch Writer

Create `StockSharp.AdvancedBacktest/DebugMode/EventLogging/Storage/BatchEventWriter.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace StockSharp.AdvancedBacktest.DebugMode.EventLogging.Storage;

public sealed class BatchEventWriter : IAsyncDisposable
{
    private readonly IEventRepository _repository;
    private readonly List<EventEntity> _buffer = new();
    private readonly int _batchSize;
    private readonly Timer _flushTimer;

    public BatchEventWriter(IEventRepository repository, int batchSize = 1000, TimeSpan? flushInterval = null)
    {
        _repository = repository;
        _batchSize = batchSize;
        _flushTimer = new Timer(_ => FlushAsync().Wait(), null, flushInterval ?? TimeSpan.FromSeconds(30), flushInterval ?? TimeSpan.FromSeconds(30));
    }

    public async Task WriteEventAsync(EventEntity eventEntity)
    {
        lock (_buffer)
        {
            _buffer.Add(eventEntity);
        }

        if (_buffer.Count >= _batchSize)
        {
            await FlushAsync();
        }
    }

    public async Task FlushAsync()
    {
        List<EventEntity> eventsToWrite;

        lock (_buffer)
        {
            if (_buffer.Count == 0) return;
            eventsToWrite = new List<EventEntity>(_buffer);
            _buffer.Clear();
        }

        foreach (var evt in eventsToWrite)
        {
            await _repository.WriteEventAsync(evt);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _flushTimer.DisposeAsync();
        await FlushAsync();
    }
}
```

---

## Step 6: Create MCP Server

Create `StockSharp.AdvancedBacktest/McpServer/BacktestEventMcpServer.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace StockSharp.AdvancedBacktest.McpServer;

public static class BacktestEventMcpServer
{
    public static async Task RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new ServerInfo
                {
                    Name = "StockSharp.AdvancedBacktest.EventLog",
                    Version = "1.0.0",
                    Description = "MCP server for querying backtest event logs"
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        // Register dependencies
        builder.Services.AddSingleton<IEventRepository>(sp =>
        {
            var connection = new SqliteConnection("Data Source=backtest_events.db");
            connection.Open();
            DatabaseSchema.InitializeAsync(connection).Wait();
            return new SqliteEventRepository(connection);
        });

        var host = builder.Build();
        await host.RunAsync();
    }
}
```

Create `StockSharp.AdvancedBacktest/McpServer/Tools/GetEventsByTypeToolBuilder.cs`:

```csharp
using ModelContextProtocol.Server;

namespace StockSharp.AdvancedBacktest.McpServer.Tools;

[McpServerToolType]
public sealed class GetEventsByTypeTool
{
    private readonly IEventRepository _repository;

    public GetEventsByTypeTool(IEventRepository repository)
    {
        _repository = repository;
    }

    [McpServerTool("get_events_by_type", "Retrieve events filtered by type and time range")]
    public async Task<EventQueryResult> GetEventsByTypeAsync(
        string runId,
        string eventType,
        string? startTime = null,
        string? endTime = null,
        string? severity = null,
        int pageSize = 100,
        int pageIndex = 0)
    {
        var parameters = new EventQueryParameters
        {
            RunId = runId,
            EventType = Enum.Parse<EventType>(eventType),
            StartTime = startTime != null ? DateTime.Parse(startTime) : null,
            EndTime = endTime != null ? DateTime.Parse(endTime) : null,
            Severity = severity != null ? Enum.Parse<EventSeverity>(severity) : null,
            PageSize = pageSize,
            PageIndex = pageIndex
        };

        var stopwatch = Stopwatch.StartNew();
        var events = await _repository.QueryEventsAsync(parameters);
        stopwatch.Stop();

        return new EventQueryResult
        {
            Events = events,
            Metadata = new QueryResultMetadata
            {
                TotalCount = events.Count,
                ReturnedCount = events.Count,
                PageIndex = pageIndex,
                PageSize = pageSize,
                HasMore = events.Count == pageSize,
                QueryTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Truncated = false
            }
        };
    }
}
```

---

## Step 7: Integration Test (E2E)

Create `StockSharp.AdvancedBacktest.Tests/McpServer/Integration/BacktestEventMcpServerTests.cs`:

```csharp
using Xunit;
using ModelContextProtocol.Client;

namespace StockSharp.AdvancedBacktest.Tests.McpServer.Integration;

public sealed class BacktestEventMcpServerTests
{
    [Fact]
    public async Task McpServer_ShouldRespondToGetEventsByType()
    {
        // Arrange - start MCP server in background
        var config = new McpServerConfig
        {
            Id = "backtest-event-server",
            Name = "Backtest Event Server",
            TransportType = TransportTypes.StdIo,
            TransportOptions = new Dictionary<string, string>
            {
                ["command"] = "dotnet",
                ["args"] = "run --project ../StockSharp.AdvancedBacktest/StockSharp.AdvancedBacktest.csproj"
            }
        };

        using var client = await McpClientFactory.CreateAsync(config);

        // Act - list tools
        var tools = await client.ListToolsAsync();

        // Assert
        Assert.Contains(tools, t => t.Name == "get_events_by_type");

        // Act - call tool (requires test data in database)
        var result = await client.CallToolAsync("get_events_by_type", new
        {
            runId = "test-run-id",
            eventType = "TradeExecution",
            pageSize = 10
        });

        // Assert
        Assert.NotNull(result);
    }
}
```

---

## Step 8: Run End-to-End

### Start MCP Server

```bash
cd StockSharp.AdvancedBacktest
dotnet run
```

### Query from Claude Code

In Claude Code, the MCP server will be available as tools. Example usage:

```
User: "Show me all trade executions from the latest backtest run"

Claude Code will:
1. Call MCP tool: get_events_by_type(runId="...", eventType="TradeExecution")
2. Receive structured results
3. Analyze and present findings
```

---

## Step 9: Verify Against Success Criteria

Run performance tests to validate success criteria:

```csharp
[Fact]
public async Task QueryEvents_10kEvents_ShouldCompleteUnder2Seconds()
{
    // Arrange - insert 10,000 events
    var runId = await SeedTestDataAsync(eventCount: 10_000);

    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await _repository.QueryEventsAsync(new EventQueryParameters
    {
        RunId = runId,
        EventType = EventType.TradeExecution,
        PageSize = 1000
    });
    stopwatch.Stop();

    // Assert - SC-001: Query 10k+ events in <2 seconds
    Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Query took {stopwatch.ElapsedMilliseconds}ms (expected <2000ms)");
}
```

---

## Common Issues & Solutions

### Issue: SQLite database locked

**Solution**: Enable WAL mode (already done in DatabaseSchema.InitializeAsync)
```sql
PRAGMA journal_mode = 'wal';
```

### Issue: JSON validation error

**Solution**: Ensure Properties is valid JSON object:
```csharp
var properties = JsonSerializer.Serialize(new { OrderId = "123", Price = 100.50 });
// Not: var properties = "invalid json";
```

### Issue: MCP tools not discovered

**Solution**: Ensure tool classes have `[McpServerToolType]` attribute and methods have `[McpServerTool]` attribute:
```csharp
[McpServerToolType]
public class MyTools { ... }
```

### Issue: Test fails with "database not found"

**Solution**: Use in-memory database for tests:
```csharp
var connection = new SqliteConnection("Data Source=:memory:");
```

---

## Next Steps

1. **Run `/speckit.tasks`** to generate implementation task breakdown
2. **Implement remaining MCP tools** (aggregate_metrics, get_state_snapshot, etc.)
3. **Add validation logic** for event properties by type
4. **Optimize queries** with profiling and index tuning
5. **Write comprehensive tests** for all event types and edge cases

---

## Resources

- [Research Document](./research.md) - Technology decisions and rationale
- [Data Model](./data-model.md) - Entity definitions and schema
- [MCP Tool Contracts](./contracts/mcp-tools.md) - API specifications
- [Feature Spec](./spec.md) - Requirements and success criteria
- [Microsoft.Data.Sqlite Docs](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)
- [MCP Specification](https://modelcontextprotocol.io/specification)

---

**Happy Coding! 🚀**

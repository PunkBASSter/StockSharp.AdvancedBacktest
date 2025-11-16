# Research: LLM-Agent-Friendly Events Logging

**Date**: 2025-11-15
**Feature**: 001-llm-agent-logging
**Status**: Complete

## Overview

This document captures research findings for technical decisions required by the LLM-Agent-Friendly Events Logging feature. All NEEDS CLARIFICATION items from the Technical Context have been resolved.

## 1. SQLite Library for .NET 10

### Decision

**Use Microsoft.Data.Sqlite version 10.0.0**

### Rationale

1. **Official .NET 10 Support**: Version 10.0.0 specifically targets .NET Standard 2.0, ensuring full .NET 10 compatibility
2. **Cross-Platform**: Fully supports Windows, Linux, and macOS - essential for cross-platform deployment
3. **Modern API Design**: Built by Microsoft's Entity Framework team with idiomatic C# patterns and async/await support
4. **Active Maintenance**: Actively maintained by Microsoft as part of the .NET ecosystem with regular updates
5. **JSON Column Support**: Includes SQLitePCLRaw.bundle_e_sqlite3 which bundles SQLite 3.38.0+, providing built-in JSON1 extension support for querying JSON columns
6. **Performance**: 2x faster for read operations compared to System.Data.SQLite, which aligns with our query-heavy workload (agents retrieving events)
7. **Alignment with Constitution**: Modern .NET patterns, explicit async support, cross-platform compatibility

### Alternatives Considered

**System.Data.SQLite**
- Rejected because:
  - Not truly cross-platform (Windows-only mixed-mode assembly requiring platform-specific native libraries)
  - Designed for .NET Framework, not optimized for modern .NET
  - Heavier dependency footprint
  - Less aligned with modern .NET patterns
  - While 3x faster for bulk inserts, our workload is read-heavy (queries) not write-heavy

**When to use System.Data.SQLite**: Only if Windows-only deployment is acceptable and maximum bulk insert performance is critical

### Implementation Notes

**Package Installation**:
```bash
dotnet add StockSharp.AdvancedBacktest package Microsoft.Data.Sqlite --version 10.0.0
```

**Key Configuration**:
- Enable WAL mode (`PRAGMA journal_mode = 'wal'`) for better concurrency
- Store JSON as TEXT columns and use `json()` function for validation
- Use `json_extract()` for querying JSON properties within SQLite queries

**Important Limitations**:
- SQLite does not support true async I/O - all `*Async()` methods execute synchronously with overhead
- Use async methods for API consistency, but don't expect performance benefits
- SQLite supports only 4 primitive types: INTEGER, REAL, TEXT, BLOB

**JSON Support**:
- JSON1 extension is included by default in SQLite 3.38.0+ (bundled)
- Store event properties as TEXT with JSON validation
- Use `json_extract()` in WHERE clauses for indexed queries on JSON fields
- Deserialize full JSON strings using System.Text.Json (per constitution)

---

## 2. MCP Server SDK for .NET

### Decision

**Use ModelContextProtocol version 0.4.0-preview.3 (official C# SDK)**

### Rationale

1. **Official SDK**: Developed by Anthropic in collaboration with Microsoft, ensuring alignment with MCP specification
2. **.NET 10 Compatibility**: Targets .NET 8.0+ and .NET Standard 2.0+, fully compatible with .NET 10
3. **Latest Protocol Support**: Version 0.4.0-preview.3 implements MCP specification 2025-06-18 with:
   - Enhanced authentication protocol
   - Elicitation support (interactive user prompts)
   - Structured tool output
   - Resource links in tool responses
4. **Multiple Transports**: Built-in support for STDIO (console), SSE (Server-Sent Events), and HTTP via ModelContextProtocol.AspNetCore
5. **Modern .NET Patterns**: Leverages dependency injection, hosted services, async/await
6. **Active Development**: Regularly updated to align with evolving MCP specification
7. **Tool Discovery**: Attribute-based tool registration with automatic parameter schema generation

### Alternatives Considered

**Mcp.Net (Community Implementation)**
- Repository: https://github.com/SamFold/Mcp.Net
- Version: 0.9.0
- Rejected because:
  - Community-maintained vs. official SDK
  - Less frequent updates and smaller ecosystem
  - Official SDK provides all needed functionality
- When to use: If you need features not yet in official SDK or prefer community governance

**Custom Implementation**
- Rejected because:
  - Significant development effort required for protocol compliance
  - MCP protocol is complex and evolving (auth, transport, tool schemas)
  - Official SDK handles all requirements
  - Ongoing maintenance burden for spec changes
  - Would violate constitution principle of reusing existing solutions

### Implementation Notes

**Package Installation**:
```bash
dotnet add StockSharp.AdvancedBacktest package ModelContextProtocol --prerelease
dotnet add StockSharp.AdvancedBacktest package Microsoft.Extensions.Hosting
```

**Recommended Architecture**:
1. Create MCP server as part of StockSharp.AdvancedBacktest project (McpServer namespace)
2. Use STDIO transport for local CLI usage and agent access
3. Define tools using `[McpServerTool]` attributes on repository methods
4. Leverage dependency injection to pass IEventRepository to tool classes

**Tool Categories for Event Logging**:
- `get_events_by_type`: Query events filtered by event type and time range
- `get_events_by_entity`: Query events related to specific order/security/position
- `aggregate_metrics`: Calculate aggregations without retrieving individual events
- `get_state_snapshot`: Retrieve strategy state at specific timestamp
- `query_event_sequence`: Find related event sequences (e.g., order → execution → position)

**Transport Strategy**:
- **Development/Production**: STDIO for local agent access (Claude Code, LLM agents)
- **Future Extension**: HTTP via ModelContextProtocol.AspNetCore if web access needed
- **Testing**: In-process client for unit/integration tests

**Preview Considerations**:
- Package is preview (0.4.0-preview.3) - expect potential breaking changes
- Monitor releases for updates to MCP specification
- Current version (2025-06-18 spec) is stable enough for development
- Lock version in .csproj to prevent unexpected updates

---

## 3. Database Schema Implementation

### Decision

**Hybrid normalized schema with JSON properties** (already decided in spec clarifications, confirmed feasible)

### Implementation with Microsoft.Data.Sqlite

**Core Tables**:

```sql
CREATE TABLE BacktestRuns (
    Id TEXT PRIMARY KEY,                -- GUID
    StartTime TEXT NOT NULL,            -- ISO 8601
    EndTime TEXT NOT NULL,
    StrategyConfigHash TEXT NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE Events (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId TEXT NOT NULL UNIQUE,       -- GUID for external references
    RunId TEXT NOT NULL,                -- FK to BacktestRuns.Id
    Timestamp TEXT NOT NULL,            -- ISO 8601 with milliseconds
    EventType TEXT NOT NULL,
    Severity TEXT NOT NULL,             -- Error, Warning, Info, Debug
    Category TEXT NOT NULL,             -- Execution, MarketData, Indicators, Risk, Performance
    Properties TEXT NOT NULL,           -- JSON column
    ParentEventId TEXT,                 -- For causal relationships
    ValidationErrors TEXT,              -- JSON array of validation issues, NULL if valid
    FOREIGN KEY (RunId) REFERENCES BacktestRuns(Id) ON DELETE CASCADE
);

CREATE INDEX idx_events_run_time ON Events(RunId, Timestamp);
CREATE INDEX idx_events_type ON Events(EventType);
CREATE INDEX idx_events_severity ON Events(Severity);
CREATE INDEX idx_events_category ON Events(Category);
CREATE INDEX idx_events_parent ON Events(ParentEventId);
```

**JSON Column Queries**:

```sql
-- Query events by entity reference stored in JSON properties
SELECT * FROM Events
WHERE RunId = @runId
  AND json_extract(Properties, '$.OrderId') = @orderId
ORDER BY Timestamp;

-- Aggregate metrics without retrieving individual events
SELECT
    EventType,
    COUNT(*) as Count,
    AVG(CAST(json_extract(Properties, '$.Profit') AS REAL)) as AvgProfit
FROM Events
WHERE RunId = @runId
  AND EventType = 'TradeExecution'
GROUP BY EventType;

-- Find events with validation errors
SELECT * FROM Events
WHERE ValidationErrors IS NOT NULL
  AND RunId = @runId;
```

**Rationale for Hybrid Approach**:
- Core fields (Id, Timestamp, Type, Severity, Category, RunId) are indexed for fast filtering
- Event-specific properties (OrderId, SecuritySymbol, Price, Quantity, etc.) stored in flexible JSON column
- Enables querying common patterns efficiently while supporting diverse event types without schema changes
- JSON columns searchable via `json_extract()` in WHERE clauses (SQLite supports this in indexed expressions if needed)

---

## 4. Batch Writing Strategy

### Decision

**Use batch commits with periodic flush (1000 events or 30 seconds, whichever comes first)**

### Rationale

1. **Memory Efficiency**: Prevents unbounded memory growth during long backtests (1M+ events)
2. **Write Performance**: Batching reduces SQLite lock contention and transaction overhead
3. **Crash Recovery**: Periodic commits ensure events are persisted even if backtest fails mid-execution
4. **Tunable**: Batch size and interval can be configured based on profiling during implementation

### Implementation Pattern

```csharp
public class BatchEventWriter : IDisposable
{
    private readonly List<EventEntity> _buffer = new();
    private readonly SqliteConnection _connection;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly Timer _flushTimer;

    public async Task WriteEventAsync(EventEntity eventEntity)
    {
        _buffer.Add(eventEntity);

        if (_buffer.Count >= _batchSize)
        {
            await FlushAsync();
        }
    }

    private async Task FlushAsync()
    {
        if (_buffer.Count == 0) return;

        using var transaction = _connection.BeginTransaction();

        // Bulk insert using prepared statement
        var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Events (EventId, RunId, Timestamp, EventType, Severity, Category, Properties, ParentEventId, ValidationErrors)
            VALUES (@eventId, @runId, @timestamp, @type, @severity, @category, @properties, @parent, @validation)";

        foreach (var evt in _buffer)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@eventId", evt.EventId);
            // ... add other parameters
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        _buffer.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync(); // Ensure final batch is written
        _flushTimer?.Dispose();
    }
}
```

**Configuration Values** (to be tuned during implementation):
- Batch size: 1000 events (estimated 100KB-1MB per batch depending on event complexity)
- Flush interval: 30 seconds
- WAL mode enabled for better write concurrency

---

## 5. MCP Tool Design Patterns

### Structured Query Builders

**Pattern**: Each MCP tool corresponds to a specific query pattern with strongly-typed parameters

**Example Tool Definitions**:

```csharp
[McpServerTool("get_events_by_type", "Retrieve events filtered by type and time range")]
public async Task<EventQueryResult> GetEventsByType(
    string runId,
    string eventType,
    string? startTime = null,
    string? endTime = null,
    int pageSize = 100,
    int pageIndex = 0)
{
    // Build SQL query with parameters
    // Return structured result with metadata
}

[McpServerTool("aggregate_metrics", "Calculate aggregations on event properties")]
public async Task<AggregationResult> AggregateMetrics(
    string runId,
    string eventType,
    string propertyPath,          // JSON path like "$.Profit"
    string aggregation,           // "count", "sum", "avg", "min", "max", "stddev"
    string? startTime = null,
    string? endTime = null)
{
    // Use SQLite aggregation functions + json_extract
}
```

**Prevents SQL Injection**:
- All user input is parameterized
- Event types validated against enum
- Time strings validated as ISO 8601
- JSON paths validated against safe patterns
- No raw SQL from agent queries

**Type Safety**:
- MCP SDK generates JSON schemas from method signatures
- Agents see required/optional parameters with types
- Runtime validation before query execution
- Clear error messages for invalid parameters

---

## Summary

All NEEDS CLARIFICATION items resolved:

| Decision Point | Resolution |
|---------------|-----------|
| SQLite package | Microsoft.Data.Sqlite 10.0.0 |
| MCP server SDK | ModelContextProtocol 0.4.0-preview.3 |
| Batch size | 1000 events (tunable) |
| Flush interval | 30 seconds (tunable) |
| Transport | STDIO for local agent access |
| Tool pattern | Attribute-based with structured parameters |

**Next Steps**: Proceed to Phase 1 (data model and contracts generation)

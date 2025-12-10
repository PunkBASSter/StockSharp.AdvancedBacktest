# AI Debug Mode for Backtest Analysis

Analyze backtest results using --ai-debug mode which captures events to a SQLite database for investigation.

$ARGUMENTS

## Prerequisites

Before using MCP tools, ensure the MCP server is configured in `.mcp.json` (project root). This allows Claude Code to access the debug event log tools directly.

## Quick Start for AI Agents

**To debug a strategy issue:**

1. Run the backtest with `--ai-debug` flag to populate the database
2. Use MCP tools to query events (preferred) OR direct SQL queries
3. The database contains only events from the most recent run (fresh on each backtest)

## Architecture

```
┌─────────────────────┐    writes to    ┌──────────────────────┐
│  Backtest Runner    │ ──────────────> │  SQLite Database     │
│  (--ai-debug flag)  │                 │  (events.db)         │
└─────────────────────┘                 └──────────────────────┘
                                                  │
                                                  │ queries via MCP
                                                  ▼
┌─────────────────────┐   MCP protocol  ┌──────────────────────┐
│  MCP Server         │ <─────────────> │  AI Agent            │
│  (stdio transport)  │                 │  (Claude Code)       │
└─────────────────────┘                 └──────────────────────┘
```

**Key behaviors:**
- MCP server is started by Claude Code when you use the MCP tools
- Database is **recreated fresh** on each new backtest (no stale data)
- MCP tools provide structured access to events without writing SQL

## Database Location

The SQLite database is created at:
```
StockSharp.AdvancedBacktest.LauncherTemplate/debug/events.db
```

## Step 1: Run a Debug Backtest

```powershell
$env:StockSharp__HistoryPath="C:\Users\Andrew\Documents\StockSharp\Hydra\Storage"
$env:StockSharp__StorageFormat="Binary"
dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate -- --ai-debug
```

This:
- Clears any existing database (fresh start)
- Populates the events database during backtest
- Outputs backtest statistics to console

## Step 2: Query the Results

### Option A: MCP Tools (Recommended)

When MCP is configured, the following tools are available. **Start by calling `ListBacktestRunsAsync` to get the runId** required by all other tools.

#### `ListBacktestRunsAsync`
List all available backtest runs. Use this first to get the runId needed for other tools.

**Parameters:** None

**Returns:**
- `runs`: Array of backtest runs with Id, StartTime, EndTime, StrategyConfigHash, CreatedAt
- `totalCount`: Number of available runs

**Example usage:**
```
List all backtest runs to find the runId for querying
```

#### `GetEventsByTypeAsync`
Retrieve backtest events filtered by event type and optional time range.

**Parameters:**
- `runId` (required): Unique identifier of the backtest run (GUID format)
- `eventType` (required): TradeExecution, OrderRejection, IndicatorCalculation, PositionUpdate, StateChange, MarketDataEvent, or RiskEvent
- `startTime` (optional): Start of time range in ISO 8601 format
- `endTime` (optional): End of time range in ISO 8601 format
- `severity` (optional): Error, Warning, Info, or Debug
- `pageSize` (default: 100, max: 1000): Number of events per page
- `pageIndex` (default: 0): Zero-based page index

**Example usage:**
```
Get all trade executions from the backtest run
```

#### `GetEventsByEntityAsync`
Retrieve events filtered by entity reference (OrderId, SecuritySymbol, PositionId, or IndicatorName).

**Parameters:**
- `runId` (required): Unique identifier of the backtest run
- `entityType` (required): OrderId, SecuritySymbol, PositionId, or IndicatorName
- `entityValue` (required): Value of the entity to search for
- `eventTypeFilter` (optional): Comma-separated list of event types (e.g., 'TradeExecution,OrderRejection')
- `pageSize` (default: 100, max: 1000): Number of events per page
- `pageIndex` (default: 0): Zero-based page index

**Example usage:**
```
Get all events for security BTCUSDT
```

#### `AggregateMetricsAsync`
Calculate aggregations on event properties without retrieving individual events.

**Parameters:**
- `runId` (required): Unique identifier of the backtest run
- `eventType` (required): Type of events to aggregate
- `propertyPath` (required): JSON path to the property (e.g., '$.Price', '$.Quantity')
- `aggregations` (required): Array of functions: count, sum, avg, min, max, stddev
- `startTime` (optional): Start of time range (ISO 8601)
- `endTime` (optional): End of time range (ISO 8601)

**Example usage:**
```
Calculate average trade price and total volume
```

#### `GetStateSnapshotAsync`
Retrieve strategy state (positions, PnL, indicators, active orders) at a specific timestamp.

**Parameters:**
- `runId` (required): Unique identifier of the backtest run
- `timestamp` (required): Timestamp to query state for (ISO 8601 format)
- `securitySymbol` (optional): Filter state to specific security
- `includeIndicators` (default: true): Include indicator values in state
- `includeActiveOrders` (default: true): Include active orders in state

**Example usage:**
```
Get strategy state at 2020-07-03T14:00:00Z
```

#### `QueryEventSequenceAsync`
Query event sequences by traversing parent-child relationships.

**Parameters:**
- `runId` (required): Unique identifier of the backtest run
- `rootEventId` (optional): Root event ID to start chain traversal from
- `sequencePattern` (optional): Comma-separated list of expected event types (e.g., 'TradeExecution,PositionUpdate')
- `findIncomplete` (default: false): Include incomplete sequences
- `maxDepth` (default: 10, max: 100): Maximum depth of chain traversal
- `pageSize` (default: 50, max: 100): Number of sequences per page
- `pageIndex` (default: 0): Zero-based page index

**Example usage:**
```
Find all trade execution chains that didn't result in position updates
```

### Option B: Direct SQL Queries

For quick analysis or when MCP is not available, query the SQLite database directly using a tool like `sqlite3`:

**Get the run ID first:**
```sql
SELECT Id, StartTime, EndTime, StrategyConfigHash
FROM BacktestRuns
ORDER BY CreatedAt DESC
LIMIT 1;
```

**Get trade executions around a specific time:**
```sql
SELECT Timestamp, EventType, Properties
FROM Events
WHERE EventType = 'TradeExecution'
AND Timestamp >= '2020-07-01T00:00:00'
AND Timestamp <= '2020-07-10T23:59:59'
ORDER BY Timestamp;
```

**Find the last trade:**
```sql
SELECT Timestamp, Properties
FROM Events
WHERE EventType = 'TradeExecution'
ORDER BY Timestamp DESC
LIMIT 1;
```

**Get indicator values at a timestamp:**
```sql
SELECT Timestamp, json_extract(Properties, '$.IndicatorName') as Indicator,
       json_extract(Properties, '$.Value') as Value
FROM Events
WHERE EventType = 'IndicatorCalculation'
AND Timestamp <= '2020-07-03T14:00:00'
ORDER BY Timestamp DESC
LIMIT 20;
```

**Event statistics:**
```sql
SELECT EventType, COUNT(*) as Count,
       MIN(Timestamp) as FirstEvent,
       MAX(Timestamp) as LastEvent
FROM Events
GROUP BY EventType
ORDER BY Count DESC;
```

## Database Schema

### Tables
- **BacktestRuns**: Metadata for each backtest run (Id, StartTime, EndTime, StrategyConfigHash, CreatedAt)
- **Events**: All logged events with properties stored as JSON

### Event Types
- `TradeExecution` - Order fills with price, volume, direction, PnL
- `IndicatorCalculation` - Indicator values at each timestamp
- `PositionUpdate` - Position changes with realized/unrealized PnL
- `StateChange` - Strategy state transitions
- `OrderRejection` - Rejected orders with reasons
- `MarketDataEvent` - Market data updates (if LogMarketData=true)
- `RiskEvent` - Risk-related events

### Events Table Columns
- `Id` - Auto-increment primary key
- `EventId` - Unique GUID for the event
- `RunId` - Foreign key to BacktestRuns
- `Timestamp` - ISO 8601 timestamp with millisecond precision
- `EventType` - Enum string (TradeExecution, IndicatorCalculation, etc.)
- `Severity` - Error, Warning, Info, Debug
- `Category` - Execution, MarketData, Indicators, Risk, Performance
- `Properties` - JSON object with event-specific data
- `ParentEventId` - For event chains (order -> execution -> position)
- `ValidationErrors` - JSON array if event had validation issues

### Event Properties (JSON)
Each event has a `Properties` column with event-specific data:
- TradeExecution: `{OrderId, Price, Quantity, Direction, RealizedPnL, SecuritySymbol}`
- IndicatorCalculation: `{IndicatorName, SecuritySymbol, Value, Parameters}`
- PositionUpdate: `{SecuritySymbol, Quantity, AveragePrice, UnrealizedPnL, RealizedPnL}`
- StateChange: `{ProcessState, Position, PnL, UnrealizedPnL}`

## Debugging Workflow for AI Agents

Given a problem description (e.g., "strategy stopped trading after July 3rd"), follow this workflow:

### 1. Understand the Problem Scope
- Parse the user's issue description from $ARGUMENTS
- Identify: timeframe, symptom type (missing trades, wrong signals, unexpected exits, etc.)

### 2. Run Debug Backtest (if database doesn't exist or is stale)
```powershell
$env:StockSharp__HistoryPath="C:\Users\Andrew\Documents\StockSharp\Hydra\Storage"
$env:StockSharp__StorageFormat="Binary"
dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate -- --ai-debug
```

### 3. Get the Run ID
Use `ListBacktestRunsAsync` to get the list of available runs and their IDs.
```
Call ListBacktestRunsAsync to see available runs - use the most recent run's Id
```

### 4. Get Overview Statistics
Use `GetEventsByTypeAsync` or SQL to get event counts by type.

### 5. Investigate Based on Problem Type

**For "stopped trading" issues:**
- Use `GetEventsByTypeAsync` with eventType="TradeExecution" to find the last trades
- Check for OrderRejection events after the last trade
- Use `GetStateSnapshotAsync` at the time trading stopped to see position/indicator state

**For "wrong entry/exit" issues:**
- Use `GetEventsByEntityAsync` with the specific OrderId
- Use `GetStateSnapshotAsync` at the trade timestamp to see indicator values
- Use `QueryEventSequenceAsync` to trace the full event chain

**For "position not closing" issues:**
- Use `QueryEventSequenceAsync` with sequencePattern="TradeExecution,PositionUpdate" and findIncomplete=true
- Use `GetEventsByEntityAsync` with entityType="SecuritySymbol" to see all events for that security

**For "unexpected PnL" issues:**
- Use `AggregateMetricsAsync` on TradeExecution events with propertyPath="$.RealizedPnL"
- Use `GetEventsByTypeAsync` with eventType="PositionUpdate" to track PnL changes over time

### 6. Reconstruct State at Critical Moments
Use `GetStateSnapshotAsync` to understand what the strategy "saw" at decision points:
- Position sizes and average prices
- Indicator values at that timestamp
- Active orders pending execution
- Realized and unrealized PnL

### 7. Check for Validation Errors
Query events with ValidationErrors not null to find data quality issues.

## Common Investigation Patterns

### Find Last Trade
```
Use GetEventsByTypeAsync with eventType="TradeExecution", pageSize=5, then sort by timestamp descending
```

### Analyze Position at Timestamp
```
Use GetStateSnapshotAsync with the specific timestamp
```

### Find Incomplete Event Chains
```
Use QueryEventSequenceAsync with findIncomplete=true to find trades without position updates
```

### Calculate Trading Statistics
```
Use AggregateMetricsAsync with aggregations=["count", "avg", "sum", "min", "max"]
```

## MCP Configuration

The MCP server is configured in `.mcp.json` (project root):

```json
{
  "mcpServers": {
    "backtest-debug": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "StockSharp.AdvancedBacktest.DebugEventLogMcpServer",
        "--",
        "--database",
        "StockSharp.AdvancedBacktest.LauncherTemplate/debug/events.db"
      ],
      "cwd": "${workspaceFolder}"
    }
  }
}
```

The MCP server uses stdio transport and is started automatically by Claude Code when you invoke any of the MCP tools.

## Related Files

- `.mcp.json` - MCP server configuration for Claude Code (project root)
- `specs/001-llm-agent-logging/` - Full specification and MCP tool contracts
- `specs/001-llm-agent-logging/contracts/mcp-tools.md` - MCP tool API specifications
- `StockSharp.AdvancedBacktest.Infrastructure/DebugMode/AiAgenticDebug/EventLogging/Storage/SqliteEventRepository.cs` - Database query implementation
- `StockSharp.AdvancedBacktest.Infrastructure/DebugMode/AiAgenticDebug/McpServer/BacktestEventMcpServer.cs` - MCP server entry point
- `StockSharp.AdvancedBacktest.Infrastructure/DebugMode/AiAgenticDebug/McpServer/Tools/` - MCP tool implementations

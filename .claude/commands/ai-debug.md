# AI Debug Mode for Backtest Analysis

Analyze backtest results using --ai-debug mode which captures events to a SQLite database for investigation.

$ARGUMENTS

## Quick Start for AI Agents

**To debug a strategy issue:**

1. Run the backtest with `--ai-debug` flag (this auto-starts the MCP server)
2. After backtest completes, the MCP server remains accessible for queries
3. Use MCP tools OR direct SQL queries to investigate the issue
4. The database contains only events from the most recent run (fresh on each backtest)

## Architecture

```
┌─────────────────────┐    writes to    ┌──────────────────────┐
│  Backtest Runner    │ ──────────────> │  SQLite Database     │
│  (--ai-debug flag)  │                 │  (events.db)         │
└─────────────────────┘                 └──────────────────────┘
         │                                        │
         │ auto-starts (first run)                │ queries
         ▼                                        ▼
┌─────────────────────┐   MCP protocol  ┌──────────────────────┐
│  MCP Server         │ <─────────────> │  AI Agent            │
│  (stays alive)      │    (stdio)      │  (Claude Code)       │
└─────────────────────┘                 └──────────────────────┘
```

**Key behaviors:**
- MCP server **auto-starts** on first `--ai-debug` backtest run
- MCP server **stays alive** after backtest completes (can query results anytime)
- Database is **recreated fresh** on each new backtest (no stale data)
- Only **one MCP server instance** runs at a time (enforced via named mutex)
- MCP server **auto-detects** database changes via file system watcher

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
- Auto-starts MCP server (if not already running)
- MCP server remains accessible after backtest completes

## Step 2: Query the Results

### Option A: MCP Tools (Recommended for AI Agents)

When the MCP server is running, use these tools:

- `get_events_by_type` - Query events by type and time range
- `get_events_by_entity` - Query events by entity (OrderId, SecuritySymbol, etc.)
- `aggregate_metrics` - Calculate aggregations (count, avg, min, max, stddev)
- `get_state_snapshot` - Reconstruct strategy state at a timestamp
- `query_event_sequence` - Find event chains with parent-child relationships
- `get_validation_errors` - Find events with validation issues

### Option B: Direct SQL Queries

For quick analysis without MCP, query the SQLite database directly:

**List all backtest runs:**
```sql
SELECT Id, StartTime, EndTime, StrategyConfigHash
FROM BacktestRuns
ORDER BY CreatedAt DESC;
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

### 2. Run Debug Backtest (if not already done)
```powershell
dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate -- --ai-debug
```

### 3. Get Overview Statistics
```sql
SELECT EventType, COUNT(*) as Count,
       MIN(Timestamp) as FirstEvent,
       MAX(Timestamp) as LastEvent
FROM Events
GROUP BY EventType
ORDER BY Count DESC;
```

### 4. Investigate Based on Problem Type

**For "stopped trading" issues:**
```sql
-- Find the last trade and what happened after
SELECT * FROM Events
WHERE EventType = 'TradeExecution'
ORDER BY Timestamp DESC LIMIT 5;

-- Check for order rejections after that time
SELECT Timestamp, Properties FROM Events
WHERE EventType = 'OrderRejection'
AND Timestamp > '[last_trade_time]'
ORDER BY Timestamp;
```

**For "wrong entry/exit" issues:**
```sql
-- Get indicator values around the trade
SELECT Timestamp,
       json_extract(Properties, '$.IndicatorName') as Indicator,
       json_extract(Properties, '$.Value') as Value
FROM Events
WHERE EventType = 'IndicatorCalculation'
AND Timestamp BETWEEN '[trade_time - 1 hour]' AND '[trade_time + 1 hour]'
ORDER BY Timestamp;
```

**For "position not closing" issues:**
```sql
-- Find entries without matching exits
SELECT e1.Timestamp as EntryTime,
       json_extract(e1.Properties, '$.Direction') as Direction,
       json_extract(e1.Properties, '$.Price') as Price
FROM Events e1
WHERE e1.EventType = 'TradeExecution'
AND json_extract(e1.Properties, '$.Direction') = 'Buy'
AND NOT EXISTS (
    SELECT 1 FROM Events e2
    WHERE e2.EventType = 'TradeExecution'
    AND json_extract(e2.Properties, '$.Direction') = 'Sell'
    AND e2.Timestamp > e1.Timestamp
)
ORDER BY e1.Timestamp DESC;
```

**For "unexpected PnL" issues:**
```sql
-- Track position updates with PnL changes
SELECT Timestamp,
       json_extract(Properties, '$.Quantity') as Position,
       json_extract(Properties, '$.RealizedPnL') as RealizedPnL,
       json_extract(Properties, '$.UnrealizedPnL') as UnrealizedPnL
FROM Events
WHERE EventType = 'PositionUpdate'
ORDER BY Timestamp;
```

### 5. Reconstruct State at Critical Moments
Use `get_state_snapshot` MCP tool or query events before a specific timestamp to understand what the strategy "saw" at decision points.

### 6. Check for Validation Errors
```sql
SELECT Timestamp, EventType, ValidationErrors
FROM Events
WHERE ValidationErrors IS NOT NULL
ORDER BY Timestamp;
```

## Common Investigation Patterns

### Find Last Trade
```sql
SELECT * FROM Events
WHERE EventType = 'TradeExecution'
ORDER BY Timestamp DESC LIMIT 1;
```

### Analyze Position at Timestamp
```sql
SELECT * FROM Events
WHERE EventType IN ('PositionUpdate', 'TradeExecution')
AND Timestamp <= '2020-07-03T14:00:00'
ORDER BY Timestamp DESC LIMIT 50;
```

### Event Statistics
```sql
SELECT EventType, COUNT(*) as Count,
       MIN(Timestamp) as FirstEvent,
       MAX(Timestamp) as LastEvent
FROM Events
GROUP BY EventType
ORDER BY Count DESC;
```

## MCP Server Management

The MCP server lifecycle is independent from backtest execution:

- **Auto-starts**: On first `--ai-debug` backtest run
- **Stays alive**: After backtest completes, remains accessible for queries
- **Single instance**: Only one MCP server runs (enforced via named mutex)
- **Auto-reconnects**: Detects database changes via file system watcher

To manually stop the MCP server (if needed), terminate the process or close Claude Code session.

## Related Files

- `specs/001-llm-agent-logging/` - Full specification and MCP tool contracts
- `specs/001-llm-agent-logging/contracts/mcp-tools.md` - MCP tool API specifications
- `specs/001-mcp-lifecycle-decoupling/` - MCP lifecycle decoupling specification
- `DebugMode/AiAgenticDebug/EventLogging/Storage/SqliteEventRepository.cs` - Database query implementation
- `DebugMode/AiAgenticDebug/McpServer/BacktestEventMcpServer.cs` - MCP server entry point

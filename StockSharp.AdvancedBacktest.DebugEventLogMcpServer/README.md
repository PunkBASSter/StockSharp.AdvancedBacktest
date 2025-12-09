# StockSharp.AdvancedBacktest.DebugEventLogMcpServer

MCP (Model Context Protocol) server for querying backtest debug events. Enables AI agents like Claude Code to analyze trading strategy backtests through structured queries.

## Features

- **Post-backtest debugging**: Server remains running after backtest completes for analysis
- **Single instance management**: Named mutex ensures only one server runs at a time
- **Automatic database cleanup**: Previous database cleared on each new backtest
- **Graceful shutdown**: `--shutdown` command for clean termination

## Installation

The MCP server is built as part of the main solution:

```powershell
dotnet build StockSharp.AdvancedBacktest.slnx
```

Output: `StockSharp.AdvancedBacktest.DebugEventLogMcpServer/bin/Debug/net8.0/StockSharp.AdvancedBacktest.DebugEventLogMcpServer.dll`

## Usage

### Automatic (Recommended)

The MCP server starts automatically when running a backtest with `--ai-debug`:

```powershell
$env:StockSharp__HistoryPath="C:\path\to\Hydra\Storage"
$env:StockSharp__StorageFormat="Binary"
dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate -- --ai-debug
```

### Manual Startup

```powershell
dotnet StockSharp.AdvancedBacktest.DebugEventLogMcpServer.dll --database "path/to/events.db"
```

### Shutdown

```powershell
dotnet StockSharp.AdvancedBacktest.DebugEventLogMcpServer.dll --shutdown
```

## Claude Code Integration

Add to `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "stocksharp-debug": {
      "command": "dotnet",
      "args": [
        "C:\\path\\to\\StockSharp.AdvancedBacktest.DebugEventLogMcpServer.dll",
        "--database",
        "C:\\path\\to\\debug\\events.db"
      ]
    }
  }
}
```

## MCP Tools

### get_events_by_type

Query events filtered by type, severity, and time range.

```
get_events_by_type(
  run_id: "abc123",
  event_type: "TradeExecution",
  severity: "Info",
  start_time: "2020-01-01T00:00:00Z",
  end_time: "2020-12-31T23:59:59Z",
  page_index: 0,
  page_size: 100
)
```

**Event Types**: `TradeExecution`, `OrderPlacement`, `PositionChange`, `IndicatorCalculation`, `SignalGeneration`, `RiskCheck`, `MarketData`, `StrategyState`

**Severities**: `Debug`, `Info`, `Warning`, `Error`, `Critical`

### get_events_by_entity

Query events for a specific entity (order, position, indicator).

```
get_events_by_entity(
  run_id: "abc123",
  entity_type: "Order",
  entity_id: "order-123",
  page_index: 0,
  page_size: 50
)
```

### get_state_snapshot

Get strategy state at a specific point in time.

```
get_state_snapshot(
  run_id: "abc123",
  timestamp: "2020-07-15T14:30:00Z",
  include_indicators: true,
  include_orders: true,
  security_filter: "BTCUSDT"
)
```

### aggregate_metrics

Calculate aggregated metrics for events.

```
aggregate_metrics(
  run_id: "abc123",
  event_type: "TradeExecution",
  metric_property: "pnl",
  aggregation: "sum",
  start_time: "2020-01-01T00:00:00Z",
  end_time: "2020-12-31T23:59:59Z"
)
```

**Aggregations**: `count`, `sum`, `avg`, `min`, `max`, `stddev`

### query_event_sequence

Trace causal chains of events.

```
query_event_sequence(
  run_id: "abc123",
  root_event_type: "SignalGeneration",
  max_depth: 5,
  start_time: "2020-07-15T00:00:00Z"
)
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  BacktestRunner Process                                      │
│  ┌─────────────────┐  ┌─────────────────┐                   │
│  │ DatabaseCleanup │  │ McpServerLauncher│                   │
│  │ (clears old db) │  │ (spawns exe)    │                   │
│  └─────────────────┘  └─────────────────┘                   │
│           │                    │                             │
│           ▼                    ▼ spawns (detached)          │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ AgenticEventLogger                                       ││
│  │ - Writes events to SQLite                               ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                              │
                              │ writes
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  DebugEventLogMcpServer.exe (separate process)              │
│  ┌─────────────────┐  ┌─────────────────┐                   │
│  │ McpInstanceLock │  │ McpShutdownSignal│                   │
│  │ (named mutex)   │  │ (EventWaitHandle)│                   │
│  └─────────────────┘  └─────────────────┘                   │
│                              │                               │
│  ┌───────────────────────────▼──────────────────────────┐   │
│  │ MCP Server (stdio transport)                          │   │
│  │ - GetEventsByType, GetEventsByEntity                  │   │
│  │ - GetStateSnapshot, AggregateMetrics                  │   │
│  │ - QueryEventSequence                                  │   │
│  └───────────────────────────────────────────────────────┘   │
│                              │                               │
│                              ▼ reads                         │
│  ┌───────────────────────────────────────────────────────┐   │
│  │ SQLite Database (events.db)                           │   │
│  └───────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## IPC Primitives

| Primitive | Name | Purpose |
|-----------|------|---------|
| Named Mutex | `Global\StockSharp.McpServer.Lock` | Single instance enforcement |
| Named Event | `Global\StockSharp.McpServer.Shutdown` | Graceful shutdown signal |

## Database Schema

**backtest_runs**
- `id` (TEXT PRIMARY KEY)
- `start_time`, `end_time` (TEXT)
- `strategy_config_hash` (TEXT)

**events**
- `event_id` (TEXT PRIMARY KEY)
- `run_id` (TEXT FOREIGN KEY)
- `timestamp` (TEXT)
- `event_type`, `severity`, `category` (TEXT)
- `properties` (TEXT - JSON)

## Troubleshooting

### Server Not Starting

1. Check if another instance is running:
   ```powershell
   # Try shutdown command
   dotnet StockSharp.AdvancedBacktest.DebugEventLogMcpServer.dll --shutdown
   ```

2. Verify database path exists and is writable

### Database Locked

- MCP server uses `Pooling=False` to avoid connection issues
- Cleanup retries 5 times with 200ms backoff

### No Events in Database

- Verify `AgenticLogging.Enabled = true` in backtest config
- Check `LogIndicators`, `LogTrades`, `LogMarketData` flags

## Development

### Running Tests

```powershell
dotnet test StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests
```

### Test Categories

- **Lifecycle**: Mutex, shutdown signal, instance management
- **Tools**: MCP tool implementations
- **E2E**: Full server lifecycle tests

## License

Part of StockSharp.AdvancedBacktest. See main repository for license information.

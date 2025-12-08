# Data Model: LLM-Agent-Friendly Events Logging

**Date**: 2025-11-15
**Feature**: 001-llm-agent-logging

## Overview

This document defines the data model for the event logging system, including entity relationships, validation rules, and state transitions.

## Entity Relationship Diagram

```text
┌─────────────────┐
│ BacktestRun     │
│─────────────────│
│ Id (PK)         │◄─────┐
│ StartTime       │      │
│ EndTime         │      │
│ ConfigHash      │      │1
│ CreatedAt       │      │
└─────────────────┘      │
                         │
                         │*
                    ┌────┴────────────┐
                    │ Event           │
                    │─────────────────│
                    │ Id (PK)         │
                    │ EventId         │◄──┐
                    │ RunId (FK)      │   │
                    │ Timestamp       │   │
                    │ EventType       │   │*
                    │ Severity        │   │
                    │ Category        │   │(self-ref)
                    │ Properties      │   │
                    │ ParentEventId ──┼───┘
                    │ ValidationErrors│
                    └─────────────────┘
```

## Core Entities

### BacktestRun

Represents a single execution of a backtest with metadata and lifecycle tracking.

**Properties**:

| Property | Type | Required | Description | Constraints |
|----------|------|----------|-------------|-------------|
| Id | string (GUID) | Yes | Unique identifier for the backtest run | Primary key, immutable |
| StartTime | DateTime | Yes | Backtest execution start timestamp | ISO 8601, must be <= EndTime |
| EndTime | DateTime | Yes | Backtest execution end timestamp | ISO 8601, must be >= StartTime |
| StrategyConfigHash | string | Yes | SHA-256 hash of strategy configuration | 64 hex characters, immutable |
| CreatedAt | DateTime | Yes | Record creation timestamp | ISO 8601, auto-generated |

**Validation Rules**:
- Id must be a valid GUID in lowercase hyphenated format
- StartTime and EndTime must be valid ISO 8601 timestamps with timezone
- EndTime must be after or equal to StartTime
- StrategyConfigHash must be lowercase hexadecimal, exactly 64 characters
- CreatedAt is auto-generated on insert, immutable

**Lifecycle**:
1. Created when backtest starts with StartTime and ConfigHash
2. Updated with EndTime when backtest completes
3. Never deleted (archival for historical analysis)

**Relationships**:
- Has many Events (1:N, cascade delete)

---

### Event

A logged occurrence during backtest execution with structured properties and relationships.

**Properties**:

| Property | Type | Required | Description | Constraints |
|----------|------|----------|-------------|-------------|
| Id | long | Yes | Auto-increment primary key | Surrogate key for internal use |
| EventId | string (GUID) | Yes | External unique identifier | Unique index, immutable |
| RunId | string (GUID) | Yes | Foreign key to BacktestRun | Must exist in BacktestRuns.Id |
| Timestamp | DateTime | Yes | Event occurrence time with millisecond precision | ISO 8601, must be within [Run.StartTime, Run.EndTime] |
| EventType | string | Yes | Classification of event | Must be valid EventType enum value |
| Severity | string | Yes | Importance level | Must be valid EventSeverity enum value |
| Category | string | Yes | Functional grouping | Must be valid EventCategory enum value |
| Properties | string (JSON) | Yes | Event-specific key-value data | Valid JSON object, max 1MB |
| ParentEventId | string (GUID) | No | Reference to parent event for causal chains | Must exist in Events.EventId if not null |
| ValidationErrors | string (JSON) | No | Validation issues detected during write | JSON array of error objects, null if valid |

**Validation Rules**:
- Id is auto-generated sequence, immutable
- EventId must be unique across all events
- RunId must reference existing BacktestRun
- Timestamp must fall within backtest run time range (StartTime <= Timestamp <= EndTime)
- EventType must be one of: TradeExecution, OrderRejection, IndicatorCalculation, PositionUpdate, StateChange, MarketDataEvent, RiskEvent
- Severity must be one of: Error, Warning, Info, Debug
- Category must be one of: Execution, MarketData, Indicators, Risk, Performance
- Properties must be valid JSON object (not array or primitive)
- Properties size must not exceed 1MB when serialized
- ParentEventId must reference an existing Event.EventId if provided
- ParentEventId must not create circular references (validated at application layer)
- ValidationErrors must be JSON array of {field: string, error: string} objects if present

**State Transitions**:
Events are immutable once written. No updates or deletes allowed except via cascade delete when parent BacktestRun is deleted.

**Relationships**:
- Belongs to BacktestRun (N:1, required)
- Has optional parent Event (self-referential N:1 for causal chains)
- Has many child Events (self-referential 1:N via ParentEventId)

**Properties JSON Schema**:

The Properties column stores event-specific data with flexible schema. Common property structures:

```json
// TradeExecution event
{
  "OrderId": "string (GUID)",
  "SecuritySymbol": "string",
  "Direction": "Buy | Sell",
  "Quantity": "decimal",
  "Price": "decimal",
  "Commission": "decimal",
  "Slippage": "decimal",
  "ExecutionTime": "ISO 8601"
}

// OrderRejection event
{
  "OrderId": "string (GUID)",
  "SecuritySymbol": "string",
  "RejectionReason": "string",
  "RequestedQuantity": "decimal",
  "RequestedPrice": "decimal"
}

// IndicatorCalculation event
{
  "IndicatorName": "string",
  "SecuritySymbol": "string",
  "Value": "decimal",
  "Parameters": "object"
}

// PositionUpdate event
{
  "SecuritySymbol": "string",
  "Quantity": "decimal",
  "AveragePrice": "decimal",
  "UnrealizedPnL": "decimal",
  "RealizedPnL": "decimal"
}

// StateChange event
{
  "StateType": "Position | PnL | Indicator | Order",
  "StateBefore": "object",
  "StateAfter": "object",
  "ChangeReason": "string"
}
```

**ValidationErrors JSON Schema**:

```json
[
  {
    "Field": "Properties.Price",
    "Error": "Missing required field",
    "Severity": "Warning"
  },
  {
    "Field": "Properties.OrderId",
    "Error": "Invalid GUID format",
    "Severity": "Error"
  }
]
```

---

## Enumerations

### EventType

Classifies events by their nature and origin.

| Value | Description | Typical Properties |
|-------|-------------|-------------------|
| TradeExecution | Completed trade (buy/sell) | OrderId, SecuritySymbol, Direction, Quantity, Price, Commission, Slippage |
| OrderRejection | Order rejected by system/broker | OrderId, SecuritySymbol, RejectionReason, RequestedQuantity, RequestedPrice |
| IndicatorCalculation | Indicator value computed | IndicatorName, SecuritySymbol, Value, Parameters |
| PositionUpdate | Position state changed | SecuritySymbol, Quantity, AveragePrice, UnrealizedPnL, RealizedPnL |
| StateChange | Strategy state transition | StateType, StateBefore, StateAfter, ChangeReason |
| MarketDataEvent | Market data received/processed | SecuritySymbol, DataType (Candle, Tick, Level2), Data |
| RiskEvent | Risk limit check or violation | RiskType, Threshold, CurrentValue, Action |

### EventSeverity

Indicates event importance level for filtering and alerting.

| Value | Description | Use Cases |
|-------|-------------|-----------|
| Error | Critical issue requiring attention | Order rejection, strategy exception, data corruption |
| Warning | Non-critical issue | Risk limit approached, delayed data, validation issue |
| Info | Normal operation event | Trade execution, position update, state change |
| Debug | Detailed diagnostic information | Indicator calculation, internal state transitions |

### EventCategory

Functional grouping for organizing event queries.

| Value | Description | Event Types |
|-------|-------------|-------------|
| Execution | Order and trade execution | TradeExecution, OrderRejection |
| MarketData | Market data processing | MarketDataEvent |
| Data | Raw data events (candles, ticks) | MarketDataEvent (candle data) |
| Indicators | Technical indicator calculations | IndicatorCalculation |
| Analysis | Indicator and signal analysis | IndicatorCalculation (analysis context) |
| Risk | Risk management events | RiskEvent |
| Performance | Strategy state and metrics | PositionUpdate, StateChange |
| Portfolio | Position and PnL tracking | PositionUpdate, StateChange (portfolio context) |

---

## Database Schema (SQLite)

### Table: BacktestRuns

```sql
CREATE TABLE BacktestRuns (
    Id TEXT PRIMARY KEY,                            -- GUID
    StartTime TEXT NOT NULL,                        -- ISO 8601
    EndTime TEXT NOT NULL,                          -- ISO 8601
    StrategyConfigHash TEXT NOT NULL,               -- SHA-256 hex
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

-- No indexes needed (small table, queries by PK only)
```

### Table: Events

```sql
CREATE TABLE Events (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId TEXT NOT NULL UNIQUE,                   -- GUID
    RunId TEXT NOT NULL,
    Timestamp TEXT NOT NULL,                        -- ISO 8601 with milliseconds
    EventType TEXT NOT NULL,
    Severity TEXT NOT NULL,
    Category TEXT NOT NULL,
    Properties TEXT NOT NULL,                       -- JSON object
    ParentEventId TEXT,                             -- FK to Events.EventId (self-ref)
    ValidationErrors TEXT,                          -- JSON array, NULL if valid
    FOREIGN KEY (RunId) REFERENCES BacktestRuns(Id) ON DELETE CASCADE,
    CHECK (json_valid(Properties)),                 -- Validate JSON syntax
    CHECK (ValidationErrors IS NULL OR json_valid(ValidationErrors))
);

-- Performance indexes
CREATE INDEX idx_events_run_time ON Events(RunId, Timestamp);
CREATE INDEX idx_events_type ON Events(EventType);
CREATE INDEX idx_events_severity ON Events(Severity);
CREATE INDEX idx_events_category ON Events(Category);
CREATE INDEX idx_events_parent ON Events(ParentEventId) WHERE ParentEventId IS NOT NULL;
CREATE UNIQUE INDEX idx_events_eventid ON Events(EventId);
```

**Index Strategy**:
- Composite index on (RunId, Timestamp) for primary query pattern: "get events for run X in time range Y"
- Individual indexes on EventType, Severity, Category for filtering
- Partial index on ParentEventId (only where NOT NULL) for event chain queries
- Unique index on EventId for external references

**Query Patterns**:

```sql
-- Get events by type and time range (uses idx_events_run_time + idx_events_type)
SELECT * FROM Events
WHERE RunId = @runId
  AND Timestamp BETWEEN @startTime AND @endTime
  AND EventType = @eventType
ORDER BY Timestamp;

-- Get events by entity reference in JSON (uses idx_events_run_time + JSON extraction)
SELECT * FROM Events
WHERE RunId = @runId
  AND json_extract(Properties, '$.OrderId') = @orderId
ORDER BY Timestamp;

-- Get event chains (uses idx_events_parent recursively)
WITH RECURSIVE EventChain AS (
    SELECT * FROM Events WHERE EventId = @rootEventId
    UNION ALL
    SELECT e.* FROM Events e
    INNER JOIN EventChain ec ON e.ParentEventId = ec.EventId
)
SELECT * FROM EventChain ORDER BY Timestamp;

-- Aggregate metrics (uses idx_events_run_time + idx_events_type)
SELECT
    COUNT(*) as TradeCount,
    AVG(CAST(json_extract(Properties, '$.Price') AS REAL)) as AvgPrice,
    SUM(CAST(json_extract(Properties, '$.Commission') AS REAL)) as TotalCommission
FROM Events
WHERE RunId = @runId
  AND EventType = 'TradeExecution';
```

---

## C# Entity Models

### BacktestRunEntity.cs

```csharp
namespace StockSharp.AdvancedBacktest.DebugMode.EventLogging.Models;

public sealed class BacktestRunEntity
{
    public required string Id { get; init; }                      // GUID
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required string StrategyConfigHash { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    // Navigation property (not mapped to DB column)
    public ICollection<EventEntity>? Events { get; init; }
}
```

### EventEntity.cs

```csharp
namespace StockSharp.AdvancedBacktest.DebugMode.EventLogging.Models;

public sealed class EventEntity
{
    public long Id { get; init; }                                 // Auto-increment
    public required string EventId { get; init; }                 // GUID
    public required string RunId { get; init; }                   // FK
    public required DateTime Timestamp { get; init; }
    public required EventType EventType { get; init; }
    public required EventSeverity Severity { get; init; }
    public required EventCategory Category { get; init; }
    public required string Properties { get; init; }              // JSON string
    public string? ParentEventId { get; init; }
    public string? ValidationErrors { get; init; }                // JSON string

    // Navigation properties (not mapped to DB columns)
    public BacktestRunEntity? Run { get; init; }
    public EventEntity? ParentEvent { get; init; }
    public ICollection<EventEntity>? ChildEvents { get; init; }
}
```

### ValidationMetadata.cs

```csharp
namespace StockSharp.AdvancedBacktest.DebugMode.EventLogging.Models;

public sealed record ValidationError(
    string Field,
    string Error,
    string Severity
);

public sealed class ValidationMetadata
{
    public required IReadOnlyList<ValidationError> Errors { get; init; }

    public bool HasErrors => Errors.Any(e => e.Severity == "Error");
    public bool HasWarnings => Errors.Any(e => e.Severity == "Warning");

    public string ToJson() => JsonSerializer.Serialize(Errors, EventJsonContext.Default.ValidationErrorList);

    public static ValidationMetadata? FromJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var errors = JsonSerializer.Deserialize(json, EventJsonContext.Default.ValidationErrorList);
        return errors != null ? new ValidationMetadata { Errors = errors } : null;
    }
}
```

---

## Validation Rules Summary

### BacktestRun Validation

1. **Id**: Valid lowercase GUID with hyphens
2. **StartTime/EndTime**: Valid ISO 8601, EndTime >= StartTime
3. **StrategyConfigHash**: Exactly 64 lowercase hex characters
4. **CreatedAt**: Auto-generated, immutable

### Event Validation

1. **EventId**: Valid lowercase GUID, unique across all events
2. **RunId**: Must reference existing BacktestRun.Id
3. **Timestamp**: Valid ISO 8601, must fall within [Run.StartTime, Run.EndTime]
4. **EventType**: Must be valid enum value
5. **Severity**: Must be valid enum value
6. **Category**: Must be valid enum value
7. **Properties**:
   - Must be valid JSON object (not array/primitive)
   - Must not exceed 1MB serialized size
   - Must contain expected fields for EventType (validated at application layer)
8. **ParentEventId**: Must reference existing Event.EventId if not null, no circular references
9. **ValidationErrors**: Must be JSON array of ValidationError objects if not null

### Validation Strategy

- **Database constraints**: JSON syntax, foreign keys, unique indexes
- **Application layer validation**:
  - Event property schemas by type
  - Circular reference detection in parent chains
  - Size limits (1MB Properties)
  - Business rules (timestamp within run, etc.)
- **Write behavior**: Log warnings for validation failures, preserve malformed events with ValidationErrors populated

---

## State Management

### Event Immutability

Events are **write-once, read-many**:
- Once inserted, events cannot be updated
- Events are only deleted via cascade when parent BacktestRun is deleted
- No soft deletes or status flags

### Backtest Run Lifecycle

1. **Created**: StartTime and ConfigHash set when backtest begins
2. **Running**: Events are written in batches as backtest executes
3. **Completed**: EndTime set when backtest finishes
4. **Queryable**: Database becomes available for agent queries after completion
5. **Archived**: Runs persisted indefinitely for historical analysis

### Batch Writing State

- Events buffered in memory during backtest execution
- Flushed to SQLite in batches (1000 events or 30 seconds)
- Final flush on backtest completion ensures all events persisted
- Database queryable only after backtest completes (no concurrent read/write)

---

## Performance Considerations

### Index Usage

- Composite index (RunId, Timestamp) covers 80%+ of queries (filtered by run and time)
- Separate indexes on EventType, Severity, Category for additional filtering
- Partial index on ParentEventId saves space (only 10-20% of events have parents)

### JSON Query Optimization

- Core fields indexed as columns for fast filtering
- JSON properties queried via `json_extract()` in WHERE clauses
- For frequently queried JSON paths, consider creating computed columns with indexes (future optimization)

### Storage Estimates

| Component | Size per Event | Notes |
|-----------|---------------|-------|
| Core fields | ~100 bytes | Id, RunId, Timestamp, Type, Severity, Category |
| Properties JSON | 200-500 bytes avg | Varies by EventType |
| Indexes | ~50 bytes | Overhead for index entries |
| **Total per event** | **350-650 bytes** | ~1.5GB for 3M events |

**Scaling**:
- 10,000 events: ~6MB database
- 100,000 events: ~60MB database
- 1,000,000 events: ~600MB database
- Meets <20% storage overhead goal vs JSONL exports

---

## Migration and Compatibility

### Backward Compatibility with DebugMode

- Existing JSONL export continues to work alongside SQLite logging
- Both systems can run simultaneously (JSONL for humans, SQLite for agents)
- No changes to existing event data models (CandleDataPoint, TradeDataPoint, etc.)
- DebugModeEventLogger transforms existing events into EventEntity format

### Future Schema Evolution

- JSON Properties column enables adding new event types without schema changes
- Enum additions (EventType, Severity, Category) handled via application validation
- Breaking changes require database migration scripts
- Version tracking via constitution amendment process

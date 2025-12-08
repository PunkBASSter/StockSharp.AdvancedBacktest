# MCP Tool Contracts

**Date**: 2025-11-15
**Feature**: 001-llm-agent-logging
**Protocol**: Model Context Protocol (MCP)

## Overview

This document defines the MCP tool contracts for the Backtest Event Logging system. Each tool provides structured query capabilities for LLM agents to analyze backtest events without loading entire datasets.

## Tool Naming Convention

- Tool names use snake_case following MCP conventions
- Parameter names use camelCase to match C# conventions
- All tools prefixed with domain context (e.g., `backtest_*`)

## Common Types

### EventQueryResult

```json
{
  "events": [
    {
      "eventId": "string (GUID)",
      "runId": "string (GUID)",
      "timestamp": "ISO 8601 string",
      "eventType": "TradeExecution | OrderRejection | IndicatorCalculation | PositionUpdate | StateChange | MarketDataEvent | RiskEvent",
      "severity": "Error | Warning | Info | Debug",
      "category": "Execution | MarketData | Indicators | Risk | Performance",
      "properties": "object (event-specific JSON)",
      "parentEventId": "string (GUID) | null",
      "validationErrors": "array | null"
    }
  ],
  "metadata": {
    "totalCount": "integer",
    "returnedCount": "integer",
    "pageIndex": "integer",
    "pageSize": "integer",
    "hasMore": "boolean",
    "queryTimeMs": "integer",
    "truncated": "boolean"
  }
}
```

### AggregationResult

```json
{
  "aggregations": {
    "count": "integer",
    "sum": "decimal | null",
    "avg": "decimal | null",
    "min": "decimal | null",
    "max": "decimal | null",
    "stddev": "decimal | null"
  },
  "metadata": {
    "totalEvents": "integer",
    "queryTimeMs": "integer",
    "eventType": "string",
    "propertyPath": "string"
  }
}
```

### StateSnapshot

```json
{
  "timestamp": "ISO 8601 string",
  "runId": "string (GUID)",
  "state": {
    "positions": [
      {
        "securitySymbol": "string",
        "quantity": "decimal",
        "averagePrice": "decimal",
        "unrealizedPnL": "decimal",
        "realizedPnL": "decimal"
      }
    ],
    "indicators": [
      {
        "name": "string",
        "securitySymbol": "string",
        "value": "decimal",
        "parameters": "object"
      }
    ],
    "activeOrders": [
      {
        "orderId": "string (GUID)",
        "securitySymbol": "string",
        "direction": "Buy | Sell",
        "quantity": "decimal",
        "price": "decimal"
      }
    ],
    "pnl": {
      "total": "decimal",
      "realized": "decimal",
      "unrealized": "decimal"
    }
  },
  "metadata": {
    "queryTimeMs": "integer",
    "reconstructed": "boolean"
  }
}
```

---

## Tool 1: get_events_by_type

Query events filtered by event type and optional time range.

### Description

Retrieve backtest events of a specific type (e.g., TradeExecution, OrderRejection) within an optional time window. Supports pagination for large result sets.

### Parameters

```json
{
  "runId": {
    "type": "string",
    "description": "Unique identifier of the backtest run",
    "required": true,
    "format": "GUID"
  },
  "eventType": {
    "type": "string",
    "description": "Type of events to retrieve",
    "required": true,
    "enum": ["TradeExecution", "OrderRejection", "IndicatorCalculation", "PositionUpdate", "StateChange", "MarketDataEvent", "RiskEvent"]
  },
  "startTime": {
    "type": "string",
    "description": "Start of time range (inclusive)",
    "required": false,
    "format": "ISO 8601"
  },
  "endTime": {
    "type": "string",
    "description": "End of time range (inclusive)",
    "required": false,
    "format": "ISO 8601"
  },
  "severity": {
    "type": "string",
    "description": "Filter by severity level",
    "required": false,
    "enum": ["Error", "Warning", "Info", "Debug"]
  },
  "pageSize": {
    "type": "integer",
    "description": "Number of events per page",
    "required": false,
    "default": 100,
    "minimum": 1,
    "maximum": 1000
  },
  "pageIndex": {
    "type": "integer",
    "description": "Zero-based page index",
    "required": false,
    "default": 0,
    "minimum": 0
  }
}
```

### Returns

`EventQueryResult` with events matching the criteria.

### Example Request

```json
{
  "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "eventType": "TradeExecution",
  "startTime": "2025-01-01T00:00:00Z",
  "endTime": "2025-12-31T23:59:59Z",
  "pageSize": 50,
  "pageIndex": 0
}
```

### Example Response

```json
{
  "events": [
    {
      "eventId": "11111111-2222-3333-4444-555555555555",
      "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "timestamp": "2025-06-15T14:30:45.123Z",
      "eventType": "TradeExecution",
      "severity": "Info",
      "category": "Execution",
      "properties": {
        "OrderId": "99999999-8888-7777-6666-555555555555",
        "SecuritySymbol": "AAPL",
        "Direction": "Buy",
        "Quantity": 100,
        "Price": 175.50,
        "Commission": 1.00,
        "Slippage": 0.05
      },
      "parentEventId": null,
      "validationErrors": null
    }
  ],
  "metadata": {
    "totalCount": 237,
    "returnedCount": 50,
    "pageIndex": 0,
    "pageSize": 50,
    "hasMore": true,
    "queryTimeMs": 45,
    "truncated": false
  }
}
```

---

## Tool 2: get_events_by_entity

Query events related to a specific trading entity (order, security, position).

### Description

Retrieve all events associated with a particular entity reference (e.g., all events for a specific order ID or security symbol). Useful for tracing entity lifecycle.

### Parameters

```json
{
  "runId": {
    "type": "string",
    "description": "Unique identifier of the backtest run",
    "required": true,
    "format": "GUID"
  },
  "entityType": {
    "type": "string",
    "description": "Type of entity to query",
    "required": true,
    "enum": ["OrderId", "SecuritySymbol", "PositionId", "IndicatorName"]
  },
  "entityValue": {
    "type": "string",
    "description": "Value of the entity identifier",
    "required": true
  },
  "eventTypes": {
    "type": "array",
    "description": "Filter by event types (empty = all types)",
    "required": false,
    "items": {
      "type": "string",
      "enum": ["TradeExecution", "OrderRejection", "IndicatorCalculation", "PositionUpdate", "StateChange", "MarketDataEvent", "RiskEvent"]
    }
  },
  "pageSize": {
    "type": "integer",
    "description": "Number of events per page",
    "required": false,
    "default": 100,
    "minimum": 1,
    "maximum": 1000
  },
  "pageIndex": {
    "type": "integer",
    "description": "Zero-based page index",
    "required": false,
    "default": 0,
    "minimum": 0
  }
}
```

### Returns

`EventQueryResult` with events related to the entity, ordered chronologically.

### Example Request

```json
{
  "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "entityType": "OrderId",
  "entityValue": "99999999-8888-7777-6666-555555555555",
  "eventTypes": ["TradeExecution", "OrderRejection"],
  "pageSize": 100,
  "pageIndex": 0
}
```

### SQL Implementation Note

```sql
SELECT * FROM Events
WHERE RunId = @runId
  AND json_extract(Properties, '$.' || @entityType) = @entityValue
  AND (@eventTypes IS NULL OR EventType IN (@eventTypes))
ORDER BY Timestamp
LIMIT @pageSize OFFSET (@pageIndex * @pageSize);
```

---

## Tool 3: aggregate_metrics

Calculate aggregations on event properties without retrieving individual events.

### Description

Compute statistical aggregations (count, sum, average, min, max, standard deviation) on numeric properties of events. Token-efficient for high-level analysis.

### Parameters

```json
{
  "runId": {
    "type": "string",
    "description": "Unique identifier of the backtest run",
    "required": true,
    "format": "GUID"
  },
  "eventType": {
    "type": "string",
    "description": "Type of events to aggregate",
    "required": true,
    "enum": ["TradeExecution", "OrderRejection", "IndicatorCalculation", "PositionUpdate", "StateChange", "MarketDataEvent", "RiskEvent"]
  },
  "propertyPath": {
    "type": "string",
    "description": "JSON path to the property to aggregate (e.g., '$.Price', '$.Quantity')",
    "required": true,
    "pattern": "^\\$\\.[a-zA-Z0-9_\\.]+$"
  },
  "aggregations": {
    "type": "array",
    "description": "Aggregation functions to compute",
    "required": false,
    "default": ["count", "avg"],
    "items": {
      "type": "string",
      "enum": ["count", "sum", "avg", "min", "max", "stddev"]
    }
  },
  "startTime": {
    "type": "string",
    "description": "Start of time range (inclusive)",
    "required": false,
    "format": "ISO 8601"
  },
  "endTime": {
    "type": "string",
    "description": "End of time range (inclusive)",
    "required": false,
    "format": "ISO 8601"
  }
}
```

### Returns

`AggregationResult` with requested aggregations.

### Example Request

```json
{
  "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "eventType": "TradeExecution",
  "propertyPath": "$.Price",
  "aggregations": ["count", "avg", "min", "max", "stddev"],
  "startTime": "2025-01-01T00:00:00Z",
  "endTime": "2025-12-31T23:59:59Z"
}
```

### Example Response

```json
{
  "aggregations": {
    "count": 237,
    "sum": null,
    "avg": 175.23,
    "min": 150.00,
    "max": 200.50,
    "stddev": 12.45
  },
  "metadata": {
    "totalEvents": 237,
    "queryTimeMs": 23,
    "eventType": "TradeExecution",
    "propertyPath": "$.Price"
  }
}
```

### SQL Implementation Note

```sql
SELECT
    COUNT(*) as count,
    AVG(CAST(json_extract(Properties, @propertyPath) AS REAL)) as avg,
    MIN(CAST(json_extract(Properties, @propertyPath) AS REAL)) as min,
    MAX(CAST(json_extract(Properties, @propertyPath) AS REAL)) as max,
    -- Note: SQLite doesn't have STDDEV, compute in application layer
FROM Events
WHERE RunId = @runId
  AND EventType = @eventType
  AND Timestamp BETWEEN @startTime AND @endTime;
```

---

## Tool 4: get_state_snapshot

Retrieve strategy state at a specific timestamp.

### Description

Reconstruct strategy state (positions, PnL, indicators, active orders) at a given moment by replaying events up to that timestamp.

### Parameters

```json
{
  "runId": {
    "type": "string",
    "description": "Unique identifier of the backtest run",
    "required": true,
    "format": "GUID"
  },
  "timestamp": {
    "type": "string",
    "description": "Timestamp to query state for",
    "required": true,
    "format": "ISO 8601"
  },
  "securitySymbol": {
    "type": "string",
    "description": "Filter state to specific security (empty = all securities)",
    "required": false
  },
  "includeIndicators": {
    "type": "boolean",
    "description": "Include indicator values in state",
    "required": false,
    "default": true
  },
  "includeActiveOrders": {
    "type": "boolean",
    "description": "Include active orders in state",
    "required": false,
    "default": true
  }
}
```

### Returns

`StateSnapshot` with reconstructed state at the specified timestamp.

### Example Request

```json
{
  "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": "2025-06-15T14:30:00Z",
  "securitySymbol": "AAPL",
  "includeIndicators": true,
  "includeActiveOrders": true
}
```

### Example Response

```json
{
  "timestamp": "2025-06-15T14:30:00Z",
  "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "state": {
    "positions": [
      {
        "securitySymbol": "AAPL",
        "quantity": 100,
        "averagePrice": 175.50,
        "unrealizedPnL": 250.00,
        "realizedPnL": 500.00
      }
    ],
    "indicators": [
      {
        "name": "SMA_10",
        "securitySymbol": "AAPL",
        "value": 176.20,
        "parameters": { "Period": 10 }
      },
      {
        "name": "SMA_20",
        "securitySymbol": "AAPL",
        "value": 174.80,
        "parameters": { "Period": 20 }
      }
    ],
    "activeOrders": [],
    "pnl": {
      "total": 750.00,
      "realized": 500.00,
      "unrealized": 250.00
    }
  },
  "metadata": {
    "queryTimeMs": 87,
    "reconstructed": true
  }
}
```

### Implementation Note

State reconstruction strategy:
1. Query PositionUpdate events up to timestamp
2. Query IndicatorCalculation events at or before timestamp
3. Query StateChange events for PnL
4. Reconstruct active orders from order events without execution/cancellation
5. Cache state snapshots for frequently queried timestamps (optimization)

---

## Tool 5: query_event_sequence

Find sequences of related events (e.g., order → execution → position update).

### Description

Query event chains by following ParentEventId relationships. Useful for debugging trading logic failures and identifying missing events.

### Parameters

```json
{
  "runId": {
    "type": "string",
    "description": "Unique identifier of the backtest run",
    "required": true,
    "format": "GUID"
  },
  "rootEventId": {
    "type": "string",
    "description": "Starting event ID for the sequence",
    "required": false,
    "format": "GUID"
  },
  "sequencePattern": {
    "type": "array",
    "description": "Expected event type sequence to match (e.g., ['OrderPlaced', 'TradeExecution', 'PositionUpdate'])",
    "required": false,
    "items": {
      "type": "string",
      "enum": ["TradeExecution", "OrderRejection", "IndicatorCalculation", "PositionUpdate", "StateChange", "MarketDataEvent", "RiskEvent"]
    }
  },
  "findIncomplete": {
    "type": "boolean",
    "description": "Find sequences missing expected events (e.g., order without execution)",
    "required": false,
    "default": false
  },
  "maxDepth": {
    "type": "integer",
    "description": "Maximum depth of event chain to traverse",
    "required": false,
    "default": 10,
    "minimum": 1,
    "maximum": 50
  },
  "pageSize": {
    "type": "integer",
    "description": "Number of sequences per page",
    "required": false,
    "default": 50,
    "minimum": 1,
    "maximum": 100
  },
  "pageIndex": {
    "type": "integer",
    "description": "Zero-based page index",
    "required": false,
    "default": 0,
    "minimum": 0
  }
}
```

### Returns

```json
{
  "sequences": [
    {
      "rootEventId": "string (GUID)",
      "events": ["array of EventEntity"],
      "complete": "boolean",
      "missingEventTypes": ["array of EventType | null"]
    }
  ],
  "metadata": {
    "totalSequences": "integer",
    "returnedCount": "integer",
    "pageIndex": "integer",
    "pageSize": "integer",
    "hasMore": "boolean",
    "queryTimeMs": "integer"
  }
}
```

### Example Request

```json
{
  "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "sequencePattern": ["TradeExecution", "PositionUpdate"],
  "findIncomplete": true,
  "maxDepth": 10,
  "pageSize": 50,
  "pageIndex": 0
}
```

### SQL Implementation Note

```sql
-- Recursive CTE for event chains
WITH RECURSIVE EventChain AS (
    SELECT * FROM Events WHERE EventId = @rootEventId
    UNION ALL
    SELECT e.* FROM Events e
    INNER JOIN EventChain ec ON e.ParentEventId = ec.EventId
    WHERE ec.depth < @maxDepth
)
SELECT * FROM EventChain ORDER BY Timestamp;
```

---

## Tool 6: get_validation_errors

Query events with validation errors to identify data quality issues.

### Description

Retrieve events that failed validation during write operations. Useful for debugging data pipeline issues and identifying malformed events.

### Parameters

```json
{
  "runId": {
    "type": "string",
    "description": "Unique identifier of the backtest run",
    "required": true,
    "format": "GUID"
  },
  "severityFilter": {
    "type": "string",
    "description": "Filter by error severity",
    "required": false,
    "enum": ["Error", "Warning"]
  },
  "pageSize": {
    "type": "integer",
    "description": "Number of events per page",
    "required": false,
    "default": 100,
    "minimum": 1,
    "maximum": 1000
  },
  "pageIndex": {
    "type": "integer",
    "description": "Zero-based page index",
    "required": false,
    "default": 0,
    "minimum": 0
  }
}
```

### Returns

`EventQueryResult` with events having ValidationErrors populated.

### Example Response

```json
{
  "events": [
    {
      "eventId": "11111111-2222-3333-4444-555555555555",
      "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "timestamp": "2025-06-15T14:30:45.123Z",
      "eventType": "TradeExecution",
      "severity": "Info",
      "category": "Execution",
      "properties": {
        "OrderId": "invalid-guid",
        "SecuritySymbol": "AAPL",
        "Quantity": 100
      },
      "parentEventId": null,
      "validationErrors": [
        {
          "Field": "Properties.OrderId",
          "Error": "Invalid GUID format",
          "Severity": "Error"
        },
        {
          "Field": "Properties.Price",
          "Error": "Missing required field",
          "Severity": "Warning"
        }
      ]
    }
  ],
  "metadata": {
    "totalCount": 15,
    "returnedCount": 15,
    "pageIndex": 0,
    "pageSize": 100,
    "hasMore": false,
    "queryTimeMs": 12,
    "truncated": false
  }
}
```

---

## Error Responses

All tools return errors in standard format:

```json
{
  "error": {
    "code": "string",
    "message": "string",
    "details": "object | null"
  }
}
```

### Error Codes

| Code | Description | HTTP Status |
|------|-------------|-------------|
| `INVALID_PARAMETER` | Parameter validation failed | 400 |
| `RUN_NOT_FOUND` | Backtest run ID not found | 404 |
| `EVENT_NOT_FOUND` | Event ID not found | 404 |
| `QUERY_TIMEOUT` | Query exceeded time limit | 408 |
| `RESULT_TOO_LARGE` | Result set exceeds size limit | 413 |
| `INVALID_TIME_RANGE` | StartTime > EndTime or future timestamp | 400 |
| `INVALID_JSON_PATH` | PropertyPath syntax invalid | 400 |
| `DATABASE_ERROR` | SQLite operation failed | 500 |

### Example Error Response

```json
{
  "error": {
    "code": "INVALID_TIME_RANGE",
    "message": "StartTime must be before or equal to EndTime",
    "details": {
      "startTime": "2025-12-31T23:59:59Z",
      "endTime": "2025-01-01T00:00:00Z"
    }
  }
}
```

---

## Performance Guarantees

All tools must meet these performance targets (from spec success criteria):

| Metric | Target | Measured By |
|--------|--------|-------------|
| Query time (10k events) | <2 seconds | metadata.queryTimeMs |
| Query time (100k events aggregate) | <500ms | metadata.queryTimeMs |
| Token reduction | 70% vs JSONL | Response size comparison |
| Concurrent queries | 100+ without degradation | Load testing |

---

## Security Considerations

### SQL Injection Prevention

- All parameters are passed via parameterized queries
- Event types validated against enum
- JSON paths validated against safe patterns (`^\$\.[a-zA-Z0-9_\.]+$`)
- No raw SQL execution from tool parameters

### Access Control

- RunId required for all queries (future: add authentication)
- No cross-run queries (prevents data leakage)
- Result size limits enforced (max 1000 events per page)
- Query timeout enforced (10 seconds max)

### Data Privacy

- No personally identifiable information in events (trading data only)
- Event properties may contain sensitive strategy parameters
- Future: add encryption-at-rest for SQLite database files

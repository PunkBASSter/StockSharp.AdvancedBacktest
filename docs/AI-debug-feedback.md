# AI Debug System: Usefulness Assessment

Feedback from Claude Code (AI agent) on the `/ai-debug` command and event logging database.

## What It Does Well

### 1. Structured Event Capture
- Captures trades, indicators, positions, state changes with timestamps
- Event chaining via `ParentEventId` enables causal tracing
- JSON `Properties` column allows flexible data storage

### 2. MCP Integration
- Tools like `GetEventsByTypeAsync`, `GetStateSnapshotAsync` provide structured access
- Pagination, filtering by time/entity/type
- Aggregation capabilities (avg, sum, count on properties)

### 3. Query Patterns
- `QueryEventSequenceAsync` for finding incomplete chains (trade -> no position update)
- State reconstruction at arbitrary timestamps

## Honest Limitations for AI Agents

### 1. Event Coverage Gaps

Currently logs:
- `TradeExecution`, `IndicatorCalculation`, `PositionUpdate`, `StateChange`

Missing (would be more useful):
- **Signal generation events** - why did the strategy decide to trade?
- **Condition evaluation traces** - which if-branch was taken?
- **Order submission vs fill** - only captures fills, not the intent

### 2. Indicator Values Are Opaque

```csharp
Value = value.GetValue<object>()  // Just a number
```

For complex indicators like DeltaZigZag, the log doesn't capture:
- `IsPending` vs confirmed status
- `ExtremumTime` for pending points
- The actual trend direction

### 3. MCP Latency

Each tool call = dotnet process startup + SQLite query. For interactive debugging, this is slow compared to reading source code directly.

### 4. Real-World Example

For the duplicate timestamp bug (Dec 2024), the debugging approach was:
- Read the `DeltaZigZag.cs` source directly
- Used `git diff` to see recent changes
- Reasoned about the logic

The event database was **not needed** because:
- The error message was specific: "time=1577908800, prev time=1577908800"
- The bug was in **code logic**, not in **runtime behavior**

## When AI Debug IS Useful

| Scenario | Usefulness |
|----------|------------|
| "Why did strategy stop trading on date X?" | High - can query last trades, state at that time |
| "Position PnL seems wrong" | Medium - can trace trade chain |
| "Indicator not triggering signals" | Low - need signal generation events |
| "Code bug in indicator logic" | Very Low - read source code instead |
| "Frontend chart assertion failed" | Very Low - it's a data format issue |

## Recommendations for Higher AI Utility

### 1. Log Decision Points
Add `SignalGeneration` event type with conditions evaluated:
```json
{
  "EventType": "SignalGeneration",
  "Properties": {
    "SignalType": "BuyEntry",
    "ConditionsEvaluated": [
      { "Name": "PriceAboveTrough", "Value": true },
      { "Name": "TrendConfirmed", "Value": true }
    ],
    "ResultingAction": "PlaceOrder"
  }
}
```

### 2. Richer Indicator Logging
Serialize full indicator value objects including all properties:
```json
{
  "IndicatorName": "DeltaZigZag",
  "Value": 15234.50,
  "IsPending": true,
  "ExtremumTime": "2020-07-03T14:00:00Z",
  "IsUp": true,
  "IsFormed": true
}
```

### 3. Pre-built Diagnostic Queries
Add MCP tools for common patterns:
- "Show me the 5 bars before/after this timestamp with all indicators"
- "What was the strategy state when this order was placed?"
- "Compare indicator values at entry vs exit"

### 4. Direct SQLite Access Option
Skip MCP overhead for simple queries - running `sqlite3` via Bash is faster for quick lookups.

## Bottom Line

The system is **well-architected** but currently most useful for **runtime behavior debugging** (why did the strategy do X at time Y), not for **code logic bugs**.

The biggest gap: it captures **what happened** but not **why the strategy decided** to do it.

---

*Assessment date: December 2024*
*Assessed by: Claude Code (Opus 4.5)*

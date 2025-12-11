# Data Model: Advanced Order Group Management

**Branch**: `003-order-group-management` | **Date**: 2025-12-11

## Entities

### OrderGroupState (Enumeration)

Represents the lifecycle state of an order group.

| Value | Description |
|-------|-------------|
| `Pending` | Opening order submitted but not yet filled |
| `Active` | Opening order filled, closing orders placed |
| `Closing` | Group closure initiated, unwinding position |
| `Completed` | All closing orders filled, position closed |
| `Cancelled` | Group cancelled before completion |

**State Transitions**:
```
Pending → Active (opening order filled)
Pending → Cancelled (opening order cancelled/rejected)
Active → Closing (CloseGroup called)
Active → Completed (all closing orders filled)
Active → Cancelled (external cancellation)
Closing → Completed (position closed)
Closing → Cancelled (closure failed)
```

### GroupedOrderState (Enumeration)

Represents the state of an individual order within a group.

| Value | Description |
|-------|-------------|
| `Pending` | Order created but not yet sent to exchange |
| `Active` | Order sent and acknowledged by exchange |
| `PartiallyFilled` | Order partially executed |
| `Filled` | Order fully executed |
| `Cancelled` | Order cancelled |
| `Rejected` | Order rejected by exchange |

### GroupedOrderRole (Enumeration)

Distinguishes opening from closing orders.

| Value | Description |
|-------|-------------|
| `Opening` | Entry order that establishes position |
| `Closing` | Exit order that reduces/closes position |

### GroupedOrder (Model)

Represents a single order within an order group.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `OrderId` | `string` | Yes | Unique identifier within group |
| `Role` | `GroupedOrderRole` | Yes | Opening or Closing |
| `Price` | `decimal` | Yes | Target execution price (0 for market orders) |
| `Volume` | `decimal` | Yes | Order quantity |
| `FilledVolume` | `decimal` | Yes | Executed quantity (default 0) |
| `OrderType` | `OrderTypes` | Yes | Limit, Market, etc. |
| `State` | `GroupedOrderState` | Yes | Current order state |
| `BrokerOrder` | `Order?` | No | Reference to StockSharp Order once placed |
| `CreatedAt` | `DateTimeOffset` | Yes | Order creation timestamp |
| `FilledAt` | `DateTimeOffset?` | No | Fill timestamp |

**Validation Rules**:
- `Price` must be > 0 for limit orders
- `Volume` must be > 0
- `FilledVolume` must be ≤ `Volume`

### OrderGroup (Model)

Represents a collection of related orders for a single trade concept.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `GroupId` | `string` | Yes | Unique identifier (default: `{SecurityId}_{DateTimeMs}_{Price}`) |
| `SecurityId` | `string` | Yes | Security identifier |
| `Direction` | `Sides` | Yes | Buy (long) or Sell (short) |
| `State` | `OrderGroupState` | Yes | Current group state |
| `OpeningOrder` | `GroupedOrder` | Yes | Single entry order |
| `ClosingOrders` | `List<GroupedOrder>` | Yes | One or more exit orders |
| `CreatedAt` | `DateTimeOffset` | Yes | Group creation timestamp |
| `ActivatedAt` | `DateTimeOffset?` | No | When opening order filled |
| `CompletedAt` | `DateTimeOffset?` | No | When group completed/cancelled |

**Validation Rules**:
- Exactly one `OpeningOrder`
- At least one `ClosingOrder`
- Sum of `ClosingOrders.Volume` must equal `OpeningOrder.Volume` (when validation enabled)
- All closing orders must have opposite direction to opening order

**Computed Properties**:
- `TotalClosingVolume`: Sum of all closing order volumes
- `FilledClosingVolume`: Sum of filled volumes on closing orders
- `RemainingVolume`: `OpeningOrder.FilledVolume - FilledClosingVolume`
- `IsVolumeMatched`: `TotalClosingVolume == OpeningOrder.Volume`

### OrderGroupLimits (Configuration)

Configuration for order group constraints.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `MaxGroupsPerSecurity` | `int` | No | 10 | Maximum simultaneous groups per security |
| `MaxRiskPercentPerGroup` | `decimal` | No | 2.0 | Maximum risk % of equity per group |
| `ThrowIfNotMatchingVolume` | `bool` | No | true | Validate volume matching on creation |

**Validation Rules**:
- `MaxGroupsPerSecurity` must be ≥ 1
- `MaxRiskPercentPerGroup` must be > 0 and ≤ 100

### ClosingOrderDefinition (Signal Component)

Defines a single closing order within an ExtendedTradeSignal.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Price` | `decimal` | Yes | Target price (0 for market) |
| `Volume` | `decimal` | Yes | Order quantity |
| `OrderType` | `OrderTypes` | No | Defaults to Limit (auto-selected if not specified) |

### ExtendedTradeSignal (Model)

Extended signal supporting multiple closing orders.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Direction` | `Sides` | Yes | Buy or Sell |
| `EntryPrice` | `decimal` | Yes | Opening order price |
| `EntryVolume` | `decimal` | Yes | Opening order volume |
| `EntryOrderType` | `OrderTypes` | No | Default: Limit |
| `ClosingOrders` | `List<ClosingOrderDefinition>` | Yes | One or more closing order definitions |
| `StopLossPrice` | `decimal?` | No | Stop-loss price for risk calculation |
| `GroupId` | `string?` | No | Custom group identifier |
| `ExpiryTime` | `DateTimeOffset?` | No | Order expiration time |

**Validation Rules**:
- `EntryPrice` must be > 0 for limit orders
- `EntryVolume` must be > 0
- At least one closing order required
- If `StopLossPrice` provided:
  - For Buy: `StopLossPrice < EntryPrice`
  - For Sell: `StopLossPrice > EntryPrice`

### OrderGroupSnapshot (Persistence)

JSON-serializable snapshot for persistence.

| Field | Type | Description |
|-------|------|-------------|
| `SecurityId` | `string` | Security identifier |
| `LastUpdated` | `DateTimeOffset` | Last modification timestamp |
| `Groups` | `List<OrderGroup>` | All order groups for this security |

## Relationships

```
ExtendedTradeSignal (input)
        │
        ▼ creates
    OrderGroup
        │
        ├── 1 OpeningOrder (GroupedOrder, Role=Opening)
        │
        └── N ClosingOrders (GroupedOrder, Role=Closing)

OrderGroupLimits (config) ──validates──▶ OrderGroup creation

OrderGroupSnapshot ◀──persists── OrderGroup[]
```

## State Machine Diagrams

### OrderGroup State Transitions

```
                    ┌─────────────┐
                    │   Pending   │
                    └──────┬──────┘
                           │
           ┌───────────────┼───────────────┐
           ▼               │               ▼
    ┌─────────────┐        │        ┌─────────────┐
    │  Cancelled  │◀───────┘        │   Active    │
    └─────────────┘                 └──────┬──────┘
           ▲                               │
           │               ┌───────────────┼───────────────┐
           │               ▼               │               ▼
           │        ┌─────────────┐        │        ┌─────────────┐
           └────────│   Closing   │        │        │  Completed  │
                    └──────┬──────┘        │        └─────────────┘
                           │               │               ▲
                           └───────────────┴───────────────┘
```

### GroupedOrder State Transitions

```
    ┌─────────────┐
    │   Pending   │
    └──────┬──────┘
           │
           ├───────────────┬───────────────┐
           ▼               ▼               ▼
    ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
    │   Active    │ │  Cancelled  │ │  Rejected   │
    └──────┬──────┘ └─────────────┘ └─────────────┘
           │               ▲               ▲
           ├───────────────┤               │
           ▼               │               │
    ┌─────────────┐        │               │
    │ PartialFill │────────┴───────────────┘
    └──────┬──────┘
           │
           ▼
    ┌─────────────┐
    │   Filled    │
    └─────────────┘
```

## Index Requirements

For efficient lookups in `IOrderGroupManager`:

1. **By GroupId**: O(1) lookup via `Dictionary<string, OrderGroup>`
2. **By SecurityId**: O(1) lookup via `Dictionary<string, List<OrderGroup>>`
3. **By BrokerOrderId**: O(1) reverse lookup via `Dictionary<long, (OrderGroup, GroupedOrder)>`

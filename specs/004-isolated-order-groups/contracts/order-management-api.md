# Internal API Contract: Order Management

**Feature**: 004-isolated-order-groups
**Assembly**: StockSharp.AdvancedBacktest.Core

## OrderRegistry API

### RegisterGroup

Registers a new order group from an OrderRequest.

```csharp
EntryOrderGroup RegisterGroup(OrderRequest request)
```

**Parameters**:
- `request`: OrderRequest containing entry order and protective pairs

**Returns**: Created `EntryOrderGroup` with unique GroupId

**Throws**:
- `ArgumentNullException`: If request is null
- `ArgumentException`: If validation fails (volumes don't match, invalid prices)
- `InvalidOperationException`: If MaxConcurrentGroups limit reached

**Example**:
```csharp
var entry = new Order { Side = Sides.Buy, Price = 100m, Volume = 1m, ... };
var request = new OrderRequest(entry, [
    new ProtectivePair(95m, 110m, 0.5m),
    new ProtectivePair(95m, 115m, 0.5m)
]);

var group = registry.RegisterGroup(request);
// group.GroupId = "abc-123-..."
// group.State = OrderGroupState.Pending
```

---

### GetActiveGroups

Returns all order groups that are not in Closed state.

```csharp
EntryOrderGroup[] GetActiveGroups()
```

**Returns**: Array of active order groups (may be empty)

---

### GetGroupById

Retrieves a specific order group by ID.

```csharp
EntryOrderGroup? GetGroupById(string groupId)
```

**Parameters**:
- `groupId`: The unique identifier of the group

**Returns**: The order group if found, null otherwise

---

### FindMatchingGroup

Finds an existing group matching entry price, SL, and TP within tolerance.

```csharp
EntryOrderGroup? FindMatchingGroup(
    decimal entryPrice,
    decimal slPrice,
    decimal tpPrice,
    decimal tolerance)
```

**Parameters**:
- `entryPrice`: Target entry price to match
- `slPrice`: Target stop-loss price to match
- `tpPrice`: Target take-profit price to match
- `tolerance`: Maximum price difference for all comparisons (typically one price step)

**Returns**: First matching active group where all three prices match within tolerance, or null if none found

**Use Case**: Duplicate signal detection before placing new orders. A signal is considered a duplicate only if entry price AND SL AND TP all match existing group levels.

---

### Reset

Clears all order groups from the registry.

```csharp
void Reset()
```

**Use Case**: Called when strategy resets or during cleanup

---

## EntryOrderGroup API

### TransitionTo

Transitions the order group to a new state.

```csharp
void TransitionTo(OrderGroupState newState)
```

**Parameters**:
- `newState`: Target state

**Throws**:
- `InvalidOperationException`: If transition is not valid from current state

**Valid Transitions**:
```
Pending → EntryFilled | Closed
EntryFilled → ProtectionActive | Closed
ProtectionActive → Closed
```

---

### GetProtectivePairs

Returns all protective pairs in the group (registry only contains active pairs).

```csharp
ProtectivePairOrders[] GetProtectivePairs()
```

**Returns**: Array of protective pairs currently in the group

---

### RemovePair

Removes a protective pair from the group when it's closed (one of SL/TP filled or cancelled).

```csharp
bool RemovePair(string pairId)
```

**Parameters**:
- `pairId`: ID of the protective pair to remove

**Returns**: True if pair was found and removed, false otherwise

**Notes**: When one order in a pair fills or is cancelled, the OrderPositionManager cancels the other order and then removes the pair from the group. The registry only contains active pairs - no closed/historical tracking.

---

## OrderPositionManager API

### HandleOrderRequest

Main entry point for processing new order signals.

```csharp
void HandleOrderRequest(OrderRequest? request)
```

**Parameters**:
- `request`: New order request, or null to cancel pending orders

**Behavior**:
- If null: Cancels all pending (unfilled) entry orders
- If existing similar group: May update or ignore based on price change
- If new signal: Registers and places entry order

---

### CheckProtectionLevels

Checks if any protective levels are hit by the candle.

```csharp
bool CheckProtectionLevels(ICandleMessage candle)
```

**Parameters**:
- `candle`: Current candle to check against

**Returns**: True if any position was closed, false otherwise

**Notes**:
- Called from both main TF and auxiliary TF handlers
- Uses pessimistic ordering (SL before TP) when both could trigger

---

### OnOwnTradeReceived

Processes trade fill notifications.

```csharp
void OnOwnTradeReceived(MyTrade trade)
```

**Parameters**:
- `trade`: Trade fill from StockSharp

**Behavior**:
- Identifies which order group the trade belongs to
- Updates state machine accordingly
- Places protective orders when entry fills
- Cancels opposing orders when protection fills

---

### CloseAllPositions

Emergency market close of all active positions.

```csharp
void CloseAllPositions()
```

**Behavior**:
- Cancels all pending and active orders
- Places market orders to close remaining positions
- Transitions all groups to Closed state

---

### Reset

Clears all state for strategy reset.

```csharp
void Reset()
```

**Behavior**:
- Clears OrderRegistry
- Resets internal caches
- Should be called from strategy's OnReseted()

# Internal API Contract: Order Management

**Feature**: 004-isolated-order-groups
**Assembly**: StockSharp.AdvancedBacktest.Core

## IStrategyOrderOperations Interface

Minimal interface for order execution, implemented by `CustomStrategyBase`.

```csharp
public interface IStrategyOrderOperations
{
    Order PlaceOrder(Order order);
    void CancelOrder(Order order);
}
```

---

## OrderRegistry API

### Constructor

```csharp
public OrderRegistry(string strategyId)
```

**Parameters**:
- `strategyId`: Unique identifier for the strategy (used as prefix for group keys)

---

### RegisterGroup

Registers a new order group with an entry order and protective pairs.

```csharp
EntryOrderGroup RegisterGroup(Order entryOrder, List<ProtectivePair> protectivePairs)
```

**Parameters**:
- `entryOrder`: The entry order (not yet placed)
- `protectivePairs`: List of protective pair specifications

**Returns**: Created `EntryOrderGroup` with unique GroupId

**Throws**:
- `ArgumentNullException`: If entryOrder is null
- `ArgumentException`: If validation fails (volumes don't match when multiple pairs)
- `InvalidOperationException`: If MaxConcurrentGroups limit reached

**Example**:
```csharp
var entry = new Order { Side = Sides.Buy, Price = 100m, Volume = 1m, ... };
var pairs = new List<ProtectivePair>
{
    new(95m, 110m, 0.5m),
    new(95m, 115m, 0.5m)
};

var group = registry.RegisterGroup(entry, pairs);
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

### FindMatchingGroup

Finds an existing group matching the OrderRequest within tolerance.

```csharp
EntryOrderGroup? FindMatchingGroup(OrderRequest request, decimal tolerance = 0.00000001m)
```

**Parameters**:
- `request`: OrderRequest containing entry order and protective pairs to match
- `tolerance`: Maximum price difference for comparisons (default: 0.00000001m)

**Returns**: First matching active group where entry price, side, volume, and all protective pairs match within tolerance, or null if none found

**Use Case**: Duplicate signal detection before placing new orders. Uses `EntryOrderGroup.Matches()` internally.

---

### FindGroupByOrder

Finds the order group containing a specific order (entry or protective).

```csharp
EntryOrderGroup? FindGroupByOrder(Order order)
```

**Parameters**:
- `order`: The order to search for

**Returns**: The order group containing the order, or null if not found

**Use Case**: Called from `OnOwnTradeReceived` to identify which group a trade fill belongs to.

---

### Reset

Clears all order groups from the registry.

```csharp
void Reset()
```

**Use Case**: Called when strategy resets or during cleanup

---

## EntryOrderGroup API

### Matches

Compares this group with an incoming OrderRequest to detect duplicates.

```csharp
bool Matches(OrderRequest request, decimal tolerance = 0.00000001m)
```

**Parameters**:
- `request`: OrderRequest to compare against
- `tolerance`: Maximum price difference for all comparisons (default: 0.00000001m)

**Returns**: True if entry price, side, volume, and all protective pairs match within tolerance

**Comparison Logic**:
1. Entry price matches within tolerance
2. Entry side matches exactly
3. Entry volume matches exactly
4. Protective pair count matches
5. All protective pairs match (sorted by SL, then TP):
   - StopLossPrice within tolerance
   - TakeProfitPrice within tolerance
   - Volume matches exactly

---

### State Property

Mutable state property for state machine transitions.

```csharp
public OrderGroupState State { get; set; }
```

**Valid Transitions**:
```
Pending → EntryFilled | Closed
EntryFilled → ProtectionActive | Closed
ProtectionActive → Closed
```

---

### ProtectivePairs Property

Dictionary of protective pairs with their orders.

```csharp
Dictionary<string, (Order? SlOrder, Order? TpOrder, ProtectivePair Spec)> ProtectivePairs
```

**Notes**:
- Key: Unique pair ID (GUID)
- SlOrder/TpOrder: Null until placed, populated after entry fill
- Pairs are removed when closed (deletion-based tracking)

---

## OrderPositionManager API

### Constructor

```csharp
public OrderPositionManager(IStrategyOrderOperations strategy, Security security, string strategyName)
```

**Parameters**:
- `strategy`: Strategy implementing IStrategyOrderOperations (typically CustomStrategyBase)
- `security`: The security being traded
- `strategyName`: Unique identifier for the strategy

---

### ActiveOrders

Returns all active order groups.

```csharp
EntryOrderGroup[] ActiveOrders()
```

**Returns**: Array of active order groups from the internal registry

---

### HandleOrderRequest

Main entry point for processing new order signals.

```csharp
Order? HandleOrderRequest(OrderRequest? orderRequest)
```

**Parameters**:
- `orderRequest`: New order request, or null to cancel pending orders

**Returns**: The entry order to register with StockSharp, or null if no action needed

**Behavior**:
- If null: Cancels all pending (unfilled) entry orders, returns null
- If matching group exists in Pending state: Returns null (duplicate signal)
- Otherwise: Registers group and returns entry order for caller to register

**Example**:
```csharp
var request = new OrderRequest(entryOrder, [protectivePair]);
var orderToRegister = _orderManager.HandleOrderRequest(request);
if (orderToRegister != null)
    RegisterOrder(orderToRegister);
```

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
- Uses pessimistic ordering (SL checked before TP) when both could trigger
- Places market order to close position when level hit
- Removes protective pair from group after closing

---

### OnOwnTradeReceived

Processes trade fill notifications.

```csharp
void OnOwnTradeReceived(MyTrade trade)
```

**Parameters**:
- `trade`: Trade fill from StockSharp

**Behavior**:
- Identifies which order group the trade belongs to via `FindGroupByOrder`
- For entry fills: Transitions to EntryFilled, checks immediate protection, places protective orders
- For protective fills: Cancels opposing order, removes pair from group
- Transitions to Closed when all pairs removed

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
- Resets internal candle cache
- Should be called from strategy's OnReseted()

# Order Position Manager

The dedicated component of CustomStrategyBase with orders and positions tracking functionality.
The main goal is to isolate position management logic from the trading strategy itself.
Responsilities:

- makes decisions if the strategy can accept new trading signals placing new orders
- receives a signal from the strategy (effectively, a set of order details;each can have a set of order models with price, volume, direction, sl/tp, expiry, etc.)
- when signals are accepted, it places orders according to the order models
- if some limits are reached (max open positions, max total risk, etc.), it rejects new signals
- tracks orders lifecycle (placed, partially filled, filled, cancelled, rejected)
- if orders set is null, it is considered as a request to delete all pending orders
- can receive a command to close all open positions
- supports different order execution models (market, limit, stop, etc.)
- along with main orders, it can place additional protective orders (stop-loss, take-profit if their levels are specified in signals)
- SL/TP orders are cancelled when an opposite protective order is filled
- has configurable positions limit (max open positions, max total risk)

So, it handles everything related to orders and positions, while the strategy focuses on trading logic. The data about orders and positions is available to the strategy via properties and events of OrderPositionManager.

FIRST IMPLEMENTATION MUST SUPPORT ONLY A SINGLE ORDER AND SINGLE POSITION AT A TIME (LONG OR SHORT) FOR SIMPLICITY. With positions limit = 1, max total risk = 3%. But with fully functional SL/TP orders handling.

Public API exposed to the strategy (approximate signatures):

```csharp
bool CanTrade(); // checks if new trading signals can be accepted
void HandleSignal(List<TradeSignal>? signal); // main method to receive trading signals from the strategy (null signal means "delete all pending orders")
void CloseAllPositions(); // - method to close all open positions
```

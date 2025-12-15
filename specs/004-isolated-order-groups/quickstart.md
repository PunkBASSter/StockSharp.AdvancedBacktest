# Quickstart: Isolated Order Groups with Split Position Management

**Feature**: 004-isolated-order-groups
**Date**: 2025-12-15

## Overview

This feature enables strategies to manage multiple concurrent positions with independent stop-loss and take-profit pairs. It also introduces an invisible auxiliary timeframe for more accurate protection level checking during backtesting.

## Basic Usage

### 1. Creating an Order Request with Multiple Protective Pairs

```csharp
// Create entry order
var entryOrder = new Order
{
    Side = Sides.Buy,
    Price = 100m,
    Volume = 1.0m,
    Security = Security,
    Portfolio = Portfolio,
    Type = OrderTypes.Limit
};

// Define split exit: 50% at TP1 (105), 50% at TP2 (110), same SL (95)
var protectivePairs = new List<ProtectivePair>
{
    new ProtectivePair(StopLossPrice: 95m, TakeProfitPrice: 105m, Volume: 0.5m),
    new ProtectivePair(StopLossPrice: 95m, TakeProfitPrice: 110m, Volume: 0.5m)
};

var orderRequest = new OrderRequest(entryOrder, protectivePairs);
```

### 2. Using OrderPositionManager

```csharp
public class MyStrategy : CustomStrategyBase
{
    private OrderPositionManager _orderManager;

    protected override void OnStarted2(DateTime time)
    {
        base.OnStarted2(time);

        // OrderPositionManager creates its own OrderRegistry internally
        _orderManager = new OrderPositionManager(this, Security, Name);

        // Subscribe to main timeframe candles
        SubscribeCandles(mainSubscription)
            .Bind(_indicator, OnProcessCandle)
            .Start();
    }

    private void OnProcessCandle(ICandleMessage candle, decimal? indicatorValue)
    {
        // Check protection levels first
        if (_orderManager.CheckProtectionLevels(candle))
            return; // Position was closed

        // Check for new signal
        var signal = TryGetSignal();
        if (signal.HasValue)
        {
            var (price, sl, tp) = signal.Value;
            var orderRequest = CreateOrderRequest(price, sl, tp);

            // HandleOrderRequest returns the Order to register, or null
            var orderToRegister = _orderManager.HandleOrderRequest(orderRequest);
            if (orderToRegister != null)
                RegisterOrder(orderToRegister);
        }
    }

    protected override void OnOwnTradeReceived(MyTrade trade)
    {
        base.OnOwnTradeReceived(trade);
        _orderManager.OnOwnTradeReceived(trade);
    }
}
```

### 3. Enabling Multiple Concurrent Positions

By default, up to 5 concurrent order groups are allowed. Each new signal creates an independent order group, even if existing positions are open:

```csharp
// First signal: creates group A
var order1 = _orderManager.HandleOrderRequest(orderRequest1);
if (order1 != null) RegisterOrder(order1);

// Second signal (while A is still active): creates group B
var order2 = _orderManager.HandleOrderRequest(orderRequest2);
if (order2 != null) RegisterOrder(order2);

// Group A's SL/TP and Group B's SL/TP are tracked independently
```

### 4. Order Group States

Each order group progresses through these states:

```
Pending → EntryFilled → ProtectionActive → Closed
```

Query active groups:
```csharp
var activeGroups = _orderManager.ActiveOrders();
foreach (var group in activeGroups)
{
    Console.WriteLine($"Group {group.GroupId}: State={group.State}, Entry={group.EntryOrder.Price}");
}
```

## Auxiliary Timeframe (Invisible)

The auxiliary timeframe is **completely internal** - you don't interact with it directly. It's set up automatically when using `OrderPositionManager` and improves backtest accuracy by checking protection levels more frequently.

**What you need to know**:
- SL/TP checks happen every 5 minutes (not just every hourly candle)
- All events triggered by auxiliary TF appear under the parent main TF candle in outputs
- No auxiliary TF data appears in debug mode, charts, or reports

## Unified Debug Mode

Both AI debug and human debug can run simultaneously:

```csharp
var debugProvider = new DebugModeProvider
{
    IsHumanDebugEnabled = true,
    IsAiDebugEnabled = true,
    MainTimeframe = TimeSpan.FromHours(1)
};

debugProvider.SetHumanOutput(humanOutput);
debugProvider.SetAiOutput(aiOutput);

// Events are automatically filtered and timestamped correctly
debugProvider.CaptureCandle(candle, securityId, isAuxiliaryTimeframe: false);
```

## Common Patterns

### Pattern: Split Exit with ZigZag and ATR Targets

```csharp
private OrderRequest CreateSplitExitRequest(decimal entryPrice, decimal zigzagTarget, decimal atrTarget)
{
    var sl = CalculateStopLoss(entryPrice);

    return new OrderRequest(
        new Order { Side = Sides.Buy, Price = entryPrice, Volume = 1.0m, ... },
        [
            new ProtectivePair(sl, zigzagTarget, 0.5m),  // 50% at ZZ target
            new ProtectivePair(sl, atrTarget, 0.5m)      // 50% at ATR target
        ]
    );
}
```

### Pattern: Checking for Duplicate Signals

Duplicate detection is built into `HandleOrderRequest()` - it uses `FindMatchingGroup()` internally:

```csharp
// If an existing pending group matches the new request (same entry, SL, TP, volume),
// HandleOrderRequest returns null (no new order needed)
var orderToRegister = _orderManager.HandleOrderRequest(orderRequest);
if (orderToRegister == null)
{
    // Either duplicate signal or null request - no action needed
    return;
}
RegisterOrder(orderToRegister);
```

For manual duplicate checking with OrderRegistry directly:

```csharp
var registry = new OrderRegistry("my-strategy");
var existingGroup = registry.FindMatchingGroup(orderRequest);

if (existingGroup != null && existingGroup.State == OrderGroupState.Pending)
{
    // Exact same signal already pending - skip
    return;
}
```

### Pattern: Handling Entry Cancellation

```csharp
// Cancel all pending entry orders (keep filled positions)
_orderManager.HandleOrderRequest(null);
```

## Testing

All components are testable in isolation:

```csharp
[Fact]
public void RegisterGroup_WithValidRequest_CreatesGroup()
{
    var registry = new OrderRegistry("test-strategy");
    var entry = new Order { Side = Sides.Buy, Price = 100m, Volume = 1m };
    var pairs = new List<ProtectivePair> { new(95m, 110m, null) };

    var group = registry.RegisterGroup(entry, pairs);

    Assert.NotNull(group);
    Assert.Equal(OrderGroupState.Pending, group.State);
}

[Fact]
public void RegisterGroup_ExceedsLimit_Throws()
{
    var registry = new OrderRegistry("test") { MaxConcurrentGroups = 2 };
    var entry1 = new Order { Side = Sides.Buy, Price = 100m, Volume = 1m };
    var entry2 = new Order { Side = Sides.Buy, Price = 101m, Volume = 1m };
    var entry3 = new Order { Side = Sides.Buy, Price = 102m, Volume = 1m };
    var pairs = new List<ProtectivePair> { new(95m, 110m, null) };

    registry.RegisterGroup(entry1, pairs);
    registry.RegisterGroup(entry2, pairs);

    Assert.Throws<InvalidOperationException>(() =>
        registry.RegisterGroup(entry3, pairs));
}
```

## Migration from Single Position

If upgrading from the previous single-position model:

1. Replace direct order placement with `OrderRequest` + `HandleOrderRequest()`
2. Replace `_order` field with `ActiveOrders()` queries
3. Keep `OnOwnTradeReceived()` delegation to `OrderPositionManager`
4. **Important**: `HandleOrderRequest()` now returns `Order?` - you must register it with `RegisterOrder()`
5. Existing tests should continue passing (backward compatible API)

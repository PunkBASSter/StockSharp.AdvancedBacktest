# Quickstart: Advanced Order Group Management

**Branch**: `003-order-group-management` | **Date**: 2025-12-11

## Overview

This feature enables trading strategies to manage multiple order groups per security, where each group contains one opening order and multiple closing orders at different price levels for smoother equity curves.

## Basic Usage

### 1. Configure Limits

```csharp
var limits = new OrderGroupLimits
{
    MaxGroupsPerSecurity = 5,
    MaxRiskPercentPerGroup = 2.0m,
    ThrowIfNotMatchingVolume = true
};
```

### 2. Create Order Group Manager

```csharp
// In strategy initialization
var persistence = NullOrderGroupPersistence.Instance; // Backtest mode
var manager = new OrderGroupManager(strategyOperations, limits, persistence);

// Subscribe to events
manager.OrderActivated += (group, order) =>
    LogInfo($"Entry filled for {group.GroupId}");
manager.GroupCompleted += group =>
    LogInfo($"Group {group.GroupId} completed");
```

### 3. Create Order Group with Multiple Take-Profits

```csharp
var signal = new ExtendedTradeSignal
{
    Direction = Sides.Buy,
    EntryPrice = 100.00m,
    EntryVolume = 100,
    StopLossPrice = 95.00m,  // For risk calculation
    ClosingOrders = new List<ClosingOrderDefinition>
    {
        new() { Price = 105.00m, Volume = 30 },  // TP1: 30%
        new() { Price = 110.00m, Volume = 30 },  // TP2: 30%
        new() { Price = 120.00m, Volume = 40 }   // TP3: 40%
    }
};

var group = manager.CreateOrderGroup(signal);
// Opening order placed immediately, closing orders placed on fill
```

### 4. Handle Order Events

```csharp
// In strategy's OnOwnTradeReceived
protected override void OnOwnTradeReceived(MyTrade trade)
{
    base.OnOwnTradeReceived(trade);
    _orderGroupManager.OnOrderFilled(trade.Order, trade);
}

// In strategy's order event handlers
protected override void OnOrderChanged(Order order)
{
    base.OnOrderChanged(order);

    if (order.State == OrderStates.Done && order.Balance == 0)
        return; // Already handled by OnOwnTradeReceived

    if (order.State == OrderStates.Failed)
        _orderGroupManager.OnOrderRejected(order);
    else if (order.State == OrderStates.Done && order.Balance > 0)
        _orderGroupManager.OnOrderCancelled(order);
}
```

### 5. Close Groups Manually

```csharp
// Close a specific group (unwind position + cancel pending orders)
manager.CloseGroup(groupId);

// Close all groups for a security
manager.CloseAllGroups("AAPL@NASDAQ");

// Close all groups
manager.CloseAllGroups();
```

### 6. Adjust Order Prices

```csharp
// Trail the entry price down
manager.AdjustOrderPrice(groupId, openingOrderId, newPrice: 99.00m);

// Move a take-profit level
manager.AdjustOrderPrice(groupId, closingOrderId, newPrice: 125.00m);
```

## Live Mode with Persistence

```csharp
// In live strategy initialization
var persistence = new OrderGroupJsonPersistence(
    storagePath: "./order_groups",
    strategyId: "MyStrategy"
);

var manager = new OrderGroupManager(strategyOperations, limits, persistence);

// On strategy start, restore state
var restoredGroups = persistence.LoadAll();
foreach (var (securityId, groups) in restoredGroups)
{
    // Reconcile with broker state...
}
```

## Risk Calculation

```csharp
// Check risk before creating group
decimal risk = manager.CalculateRiskPercent(
    entryPrice: 100.00m,
    volume: 100,
    stopLossPrice: 95.00m,
    currentEquity: 50000.00m
);

// Risk = (100 * 100 * 0.05) / 50000 = 1.0%
if (risk > limits.MaxRiskPercentPerGroup)
{
    LogWarning($"Risk {risk}% exceeds limit");
}
```

## Partial Fill Handling

When opening orders partially fill, closing orders are scaled proportionally:

```csharp
// Original: Entry 100 shares, Closing [30, 30, 40]
// 60% fill: Entry 60 shares filled
// Closing orders placed: [18, 18, 24] (pro-rata scaling)
```

## Common Patterns

### Scale-In Strategy

```csharp
// Create multiple groups at different entry levels
var entries = new[] { 100m, 98m, 96m };
foreach (var entry in entries)
{
    var signal = CreateSignalAtPrice(entry);
    try
    {
        manager.CreateOrderGroup(signal);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("limit"))
    {
        LogWarning("Group limit reached");
        break;
    }
}
```

### End-of-Day Closure

```csharp
// In strategy's time-based logic
if (IsEndOfTradingDay())
{
    manager.CloseAllGroups();
}
```

## Testing

```csharp
// Use mock IStrategyOrderOperations for unit tests
var mockOperations = new MockStrategyOrderOperations();
var manager = new OrderGroupManager(
    mockOperations,
    new OrderGroupLimits(),
    NullOrderGroupPersistence.Instance
);

// Create group and verify orders placed
var group = manager.CreateOrderGroup(testSignal);
Assert.Equal(OrderGroupState.Pending, group.State);
Assert.Single(mockOperations.PlacedOrders);

// Simulate fill
mockOperations.SimulateFill(group.OpeningOrder.BrokerOrder);
Assert.Equal(OrderGroupState.Active, group.State);
Assert.Equal(3, mockOperations.PlacedOrders.Count); // Entry + 3 exits
```

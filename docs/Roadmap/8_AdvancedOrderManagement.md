# Advanced Order Management

## Problem
Currently orders are managed by @StockSharp.AdvancedBacktest.Core\OrderManagement\OrderPositionManager.cs
It supports only a single position that is preventing from some trading signals being executed, that may distort the picture of trading signals robustness caused by the order they appear in history.

## MVP Goal
Update OrderPositionManager to handle order relations:
Single entry order - (parent: market/pending) and multiple exit orders - (children: pending, optional).

The updated interface should operate with @StockSharp.AdvancedBacktest.Core\OrderManagement\OrderRequest.cs (a renamed and transformed TradingSignal class).
I also want to segregate the actual order execution



## Side requirements: order-checking timeframe lower than lowest working TF

/speckit.specify Please see @StockSharp.AdvancedBacktest.Core\OrderManagement\OrderPositionManager.cs - this is a primitive single position management system, however I want to develop an advanced system of order tracking for a single position per security. It     
  needs to support order groups with a single opening order that can have multiple closing orders with fractions of the initial volume (for smoother equity). There can be multiple simultaneous order groups for one security, tracked in memory (and written on disk as  
  JSON during live mode). The abstraction for tracking should be in Core project, the implementations -- in Infrastructure. There should be configurable order group limits by max number and max risk percentage from the current equity. There should be state
  tracking of the orders and order groups (activation/cancellation hooks, closing orders placement when the opening was activated). The interface should allow creation of 1 opening and many closing orders with matching volume of opening/closing by a TradingSignal    
  (should be extended); adjustment of a single order activation price, closing of an order group (if there are activated orders affecting the position size, this impact should be undone - closed equally, canelling the rest of pending orders). The interface should
  have throwIfNotMatchingVolume. Order types should be picket automatically for non-market orders. Market closing orders can have optional closing orders, all of them are in one group. Order GroupId is optional, by default it's a
  SecurityId_DateTimeWithMs_OpenPrice. If I missed something, feel free to ask now
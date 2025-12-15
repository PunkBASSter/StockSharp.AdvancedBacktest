using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.Algo.Candles;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.OrderManagement;

/// <summary>
/// Manages orders and positions for a trading strategy with manual stop-loss and take-profit handling.
/// MVP implementation: supports single position and single order at a time.
/// </summary>
/// <remarks>
/// Creates a new OrderPositionManager for the specified strategy.
/// </remarks>
/// <param name="strategy">The parent strategy operations.</param>
public class OrderPositionManager(CustomStrategyBase _strategy, OrderRegistry _orderRegistry)
{
    //TODO implement an order state machine to track multiple orders and positions
    // What we hanlde:
    // 1. Filled entry orders: place protection orders (SL/TP) or monitor levels on candles (if OrderRequest.UseMarketProtectiveOrders is true)
    // 2. Filled protection order: cancel other protection order from pair 

    //         -> cancelled
    //         -> expired
    // pending -> filled -> protection set (sl/tp) -> protection filled
    //                                             -> protection partially filled ()
    //                   -> forced close (close by market, cancel protection) 

    // pending
    //     candelled
    //     expired
    //     filled
    //         protection set 
    //             protection filled
    //             protection partially filled
    //         forced close

    // Current active order with associated SL/TP orders    private OrderRequest? _lastSignal;



    private ICandleMessage? _lastCandle;

    public void HandleOrderRequest(OrderRequest? orderRequest)
    {
        //TODO: Reimplement from scratch
        if (orderRequest == null)
        {
            CancelAllOrders();
            return;
        }

        //var existingOrder = _orderRegistry.GetOrderRequest();

        if (order is null)
        {
            orderRequest.Validate();
            PlaceEntryOrder(orderRequest);
            // Store SL/TP levels for protection checking
            _currentStopLoss = orderRequest.StopLoss;
            _currentTakeProfit = orderRequest.TakeProfit;
            return;
        }

        orderRequest.Validate();
        if (HasSignalChanged(orderRequest) && IsOrderActive(_order.EntryOrder))
        {
            _strategy.LogInfo("Canceling existing entry order - signal levels changed");
            CancelAllOrders();
            PlaceEntryOrder(orderRequest);
            // Update SL/TP levels for new signal
            _currentStopLoss = orderRequest.StopLoss;
            _currentTakeProfit = orderRequest.TakeProfit;
        }
    }

    /// <summary>
    /// Checks if stop-loss or take-profit levels have been hit by the current candle.
    /// If hit, closes the position with a market order.
    /// This method should be called on each candle update BEFORE checking for new signals.
    /// </summary>
    /// <param name="candle">The current candle to check against protection levels.</param>
    /// <returns>True if position was closed due to SL/TP hit, false otherwise.</returns>
    public bool CheckProtectionLevels(ICandleMessage candle)//MAYBE CAN BE REMOVED IF BACKTEST IS FIXED
    {
        // Cache the candle for later use (e.g., after entry fills)
        _lastCandle = candle;

        // Only check if we have an open position and protection levels are set
        if (_strategy.Position == 0 || (_currentStopLoss == null && _currentTakeProfit == null))
            return false;

        return CheckProtectionLevelsInternal(candle);
    }

    /// <summary>
    /// Internal method to check protection levels against a candle.
    /// </summary>
    private bool CheckProtectionLevelsInternal(ICandleMessage candle)
    {
        // Check if stop-loss was hit (use candle low for more accurate checking)
        if (_currentStopLoss.HasValue && candle.LowPrice <= _currentStopLoss.Value)
        {
            _strategy.LogInfo("Stop-loss hit at candle low {0:F2} (SL level: {1:F2}), closing position",
                candle.LowPrice, _currentStopLoss.Value);
            CloseAllPositions();
            _currentStopLoss = null;
            _currentTakeProfit = null;
            return true;
        }

        // Check if take-profit was hit (use candle high for more accurate checking)
        if (_currentTakeProfit.HasValue && candle.HighPrice >= _currentTakeProfit.Value)
        {
            _strategy.LogInfo("Take-profit hit at candle high {0:F2} (TP level: {1:F2}), closing position",
                candle.HighPrice, _currentTakeProfit.Value);
            CloseAllPositions();
            _currentStopLoss = null;
            _currentTakeProfit = null;
            return true;
        }

        return false;
    }

    public void CloseAllPositions()
    {
        //Emergency market close of all positions
    }

    public void OnOwnTradeReceived(MyTrade trade)
    {
        //TODO: rewrite
        var order = trade.Order;

        if (_order?.EntryOrder == order)
        {
            HandleEntryFill(trade);
        }
        else if (order == _order?.SlOrder)
        {
            HandleStopLossFill(trade);
        }
        else if (order == _order?.TpOrder)
        {
            HandleTakeProfitFill(trade);
        }
    }

    /// <summary>
    /// Resets the manager state (call from strategy OnReseted).
    /// </summary>
    public void Reset()
    {
        //TODO: implement reset logic
    }

    #region Private Helper Methods

    private void PlaceEntryOrder(OrderRequest signal)
    {
        //TODO: implement placing entry order via strategy connector
    }

    private void PlaceProtectionOrders(OrderRequest signal)
    {
        //TODO: implement placing SL/TP orders for all pairs, take into account partial fills, etc. and simulation mode (market orders vs limit orders)
    }

    private void HandleEntryFill(MyTrade trade)
    {
        //TODO: rewrite from scratch

        _strategy.LogInfo("Entry order filled at {0:F2}, Position: {1}",
            trade.Trade.Price, _strategy.Position);

        if (_lastSignal == null)
            return;

        // CRITICAL FIX: Check if SL/TP were hit in the SAME candle that filled the entry
        // This handles the case where entry fills and TP/SL hit in one big candle
        if (_lastCandle != null && _strategy.Position != 0)
        {
            _strategy.LogInfo("Checking if protection levels hit in same candle as entry fill...");
            if (CheckProtectionLevelsInternal(_lastCandle))
            {
                _strategy.LogInfo("Protection hit immediately after entry - position closed");
                return; // Position was closed, don't place protection orders
            }
        }

        // NOTE: We don't place limit protection orders here because CheckProtectionLevels()
        // handles SL/TP checking on each candle. Using both would cause double closes.
        // If you want to use limit orders instead of candle-based checking, uncomment:
        // PlaceProtectionOrders(_lastSignal);
    }

    private bool HasSignalChanged(OrderRequest currentOrderRequest)
    {
        //TODO: move this to comparison of EntryOrderGroup or OrderRegistry
        return false;
    }

    private static bool IsOrderActive(Order? order)
    {
        return order != null && order.State == OrderStates.Active;
    }

    #endregion
}

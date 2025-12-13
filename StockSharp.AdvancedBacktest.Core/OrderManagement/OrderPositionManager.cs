using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.Algo.Candles;

namespace StockSharp.AdvancedBacktest.OrderManagement;

/// <summary>
/// Manages orders and positions for a trading strategy with manual stop-loss and take-profit handling.
/// MVP implementation: supports single position and single order at a time.
/// </summary>
/// <remarks>
/// Creates a new OrderPositionManager for the specified strategy.
/// </remarks>
/// <param name="strategy">The parent strategy operations.</param>
public class OrderPositionManager(IStrategyOrderOperations strategy)
{
    //TODO implement an order state machine to track multiple orders and positions
    

    private readonly IStrategyOrderOperations _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

    private MyOrder? _order;
    public MyOrder? Order => _order;

    private OrderRequest? _lastSignal;

    // Track current stop-loss and take-profit levels for protection
    private decimal? _currentStopLoss;
    private decimal? _currentTakeProfit;

    // Cache the last candle for protection checking after entry fills
    private ICandleMessage? _lastCandle;

    public MyOrder[] ActiveOrders()
    {
        if (_order is null || !IsOrderActive(_order.EntryOrder))
            return [];

        return [_order];
    }

    public void HandleSignal(OrderRequest? signal)
    {
        if (signal == null)
        {
            CancelAllOrders();
            return;
        }

        if (_order is null)
        {
            signal.Validate();
            PlaceEntryOrder(signal);
            // Store SL/TP levels for protection checking
            _currentStopLoss = signal.StopLoss;
            _currentTakeProfit = signal.TakeProfit;
            return;
        }

        signal.Validate();
        if (HasSignalChanged(signal) && IsOrderActive(_order.EntryOrder))
        {
            _strategy.LogInfo("Canceling existing entry order - signal levels changed");
            CancelAllOrders();
            PlaceEntryOrder(signal);
            // Update SL/TP levels for new signal
            _currentStopLoss = signal.StopLoss;
            _currentTakeProfit = signal.TakeProfit;
        }
    }

    /// <summary>
    /// Checks if stop-loss or take-profit levels have been hit by the current candle.
    /// If hit, closes the position with a market order.
    /// This method should be called on each candle update BEFORE checking for new signals.
    /// </summary>
    /// <param name="candle">The current candle to check against protection levels.</param>
    /// <returns>True if position was closed due to SL/TP hit, false otherwise.</returns>
    public bool CheckProtectionLevels(ICandleMessage candle)
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
        if (_strategy.Position == 0)
            return;

        _strategy.LogInfo("Closing all positions - current position: {0}", _strategy.Position);

        CancelProtectionOrders();

        var closeVolume = Math.Abs(_strategy.Position);
        if (_strategy.Position > 0)
        {
            _strategy.SellMarket(closeVolume);
        }
        else
        {
            _strategy.BuyMarket(closeVolume);
        }

        // Clear order state to allow new orders to be placed
        _order = null;
        _lastSignal = null;
    }

    public void OnOwnTradeReceived(MyTrade trade)
    {
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
        _order = null;
        _lastSignal = null;
        _currentStopLoss = null;
        _currentTakeProfit = null;
        _lastCandle = null;
    }

    #region Private Helper Methods

    private void PlaceEntryOrder(OrderRequest signal)
    {
        _strategy.LogInfo("Placing {0} entry order at {1:F2} Volume:{2}",
            signal.Direction, signal.Price, signal.Volume);

        _lastSignal = signal;

        if (signal.Direction == Sides.Buy)
        {
            var entryOrder = _strategy.BuyLimit(signal.Price, signal.Volume);
            _order = new MyOrder(entryOrder, null, null);
        }
        else
        {
            var entryOrder = _strategy.SellLimit(signal.Price, signal.Volume);
            _order = new MyOrder(entryOrder, null, null);
        }
    }

    private void PlaceProtectionOrders(OrderRequest signal)
    {
        var volume = Math.Abs(_strategy.Position);

        if (volume == 0)
        {
            _strategy.LogWarning("Cannot place protection orders - no position");
            return;
        }

        var exitDirection = _strategy.Position > 0 ? Sides.Sell : Sides.Buy;

        Order? slOrder = null;
        if (signal.StopLoss.HasValue)
        {
            _strategy.LogInfo("Placing stop-loss order at {0:F2}", signal.StopLoss.Value);

            if (exitDirection == Sides.Sell)
                slOrder = _strategy.SellLimit(signal.StopLoss.Value, volume);
            else
                slOrder = _strategy.BuyLimit(signal.StopLoss.Value, volume);
        }

        Order? tpOrder = null;
        if (signal.TakeProfit.HasValue)
        {
            _strategy.LogInfo("Placing take-profit order at {0:F2}", signal.TakeProfit.Value);

            if (exitDirection == Sides.Sell)
                tpOrder = _strategy.SellLimit(signal.TakeProfit.Value, volume);
            else
                tpOrder = _strategy.BuyLimit(signal.TakeProfit.Value, volume);
        }

        _order = _order! with { SlOrder = slOrder, TpOrder = tpOrder };
        return;
    }

    private void HandleEntryFill(MyTrade trade)
    {
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

    private void HandleStopLossFill(MyTrade trade)
    {
        _strategy.LogInfo("Stop-loss filled at {0:F2}, Position: {1}",
            trade.Trade.Price, _strategy.Position);

        if (IsOrderActive(_order?.TpOrder))
        {
            _strategy.LogInfo("Canceling take-profit order");
            _strategy.CancelOrder(_order!.TpOrder!);
        }

        // Clear state to allow new orders after SL closes the position
        _order = null;
        _lastSignal = null;
    }

    private void HandleTakeProfitFill(MyTrade trade)
    {
        _strategy.LogInfo("Take-profit filled at {0:F2}, Position: {1}",
            trade.Trade.Price, _strategy.Position);

        if (IsOrderActive(_order?.SlOrder))
        {
            _strategy.LogInfo("Canceling stop-loss order");
            _strategy.CancelOrder(_order!.SlOrder!);
        }

        // Clear state to allow new orders after TP closes the position
        _order = null;
        _lastSignal = null;
    }

    private void CancelAllOrders()
    {
        if (IsOrderActive(_order?.EntryOrder))
        {
            _strategy.LogInfo("Canceling entry order");
            _strategy.CancelOrder(_order!.EntryOrder);
            _order = null;
        }

        CancelProtectionOrders();
    }

    private void CancelProtectionOrders()
    {
        if (IsOrderActive(_order?.SlOrder))
        {
            _strategy.LogInfo("Canceling stop-loss order");
            _strategy.CancelOrder(_order!.SlOrder!);
        }

        if (IsOrderActive(_order?.TpOrder))
        {
            _strategy.LogInfo("Canceling take-profit order");
            _strategy.CancelOrder(_order!.TpOrder!);
        }
    }

    private bool HasSignalChanged(OrderRequest newSignal)
    {
        if (_lastSignal == null)
            return true;

        var priceStep = PriceStepHelper.GetPriceStep(_strategy.Security);

        // Check if entry price changed
        if (Math.Abs(_lastSignal.Price - newSignal.Price) > priceStep)
            return true;

        // Check if stop-loss changed
        var oldSL = _lastSignal.StopLoss ?? 0;
        var newSL = newSignal.StopLoss ?? 0;
        if (Math.Abs(oldSL - newSL) > priceStep)
            return true;

        // Check if take-profit changed
        var oldTP = _lastSignal.TakeProfit ?? 0;
        var newTP = newSignal.TakeProfit ?? 0;
        if (Math.Abs(oldTP - newTP) > priceStep)
            return true;

        return false;
    }

    private static bool IsOrderActive(Order? order)
    {
        return order != null && order.State == OrderStates.Active;
    }

    #endregion
}

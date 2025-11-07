using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

/// <summary>
/// Manages orders and positions for a trading strategy with manual stop-loss and take-profit handling.
/// MVP implementation: supports single position and single order at a time.
/// </summary>
/// <remarks>
/// Creates a new OrderPositionManager for the specified strategy.
/// </remarks>
/// <param name="strategy">The parent strategy.</param>
public class OrderPositionManager(CustomStrategyBase strategy)
{
    private readonly CustomStrategyBase _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

    // Order tracking
    private Order? _entryOrder;
    private Order? _stopLossOrder;
    private Order? _takeProfitOrder;

    // Signal tracking
    private TradeSignal? _currentSignal;
    public Order? EntryOrder => _entryOrder;
    public Order? StopLossOrder => _stopLossOrder;
    public Order? TakeProfitOrder => _takeProfitOrder;
    public TradeSignal? CurrentSignal => _currentSignal;

    public bool CanTrade()
    {
        if (_strategy.Position != 0)
            return false;

        if (IsOrderActive(_entryOrder) || IsOrderActive(_stopLossOrder) || IsOrderActive(_takeProfitOrder))
            return false;

        return true;
    }

    /// <summary>
    /// Handles a trading signal from the strategy.
    /// If signal is null, cancels all pending orders.
    /// If signal is provided, validates it and places/updates entry order.
    /// </summary>
    /// <param name="signal">The trading signal (null to cancel all orders).</param>
    public void HandleSignal(TradeSignal? signal)
    {
        // Null signal means cancel all pending orders
        if (signal == null)
        {
            CancelAllOrders();
            _currentSignal = null;
            return;
        }

        // Validate signal
        signal.Validate();

        // Check if signal levels changed significantly
        bool levelsChanged = HasSignalChanged(signal);

        // Cancel existing entry order if levels changed
        if (levelsChanged && IsOrderActive(_entryOrder))
        {
            _strategy.LogInfo("Canceling existing entry order - signal levels changed");
            _strategy.CancelOrder(_entryOrder);
            _entryOrder = null;
        }

        // Place new entry order if no active entry order exists
        if (_entryOrder == null)
        {
            _currentSignal = signal;
            PlaceEntryOrder(signal);
        }
    }

    /// <summary>
    /// Closes all open positions by placing market orders.
    /// </summary>
    public void CloseAllPositions()
    {
        if (_strategy.Position == 0)
            return;

        _strategy.LogInfo("Closing all positions - current position: {0}", _strategy.Position);

        // Cancel any active protection orders first
        CancelProtectionOrders();

        // Close position with market order
        var closeVolume = Math.Abs(_strategy.Position);
        if (_strategy.Position > 0)
        {
            // Close long position
            _strategy.SellMarket(closeVolume);
        }
        else
        {
            // Close short position
            _strategy.BuyMarket(closeVolume);
        }
    }

    /// <summary>
    /// Handles own trade execution events from the strategy.
    /// Activates protection orders when entry fills, cancels opposite order when SL/TP fills.
    /// </summary>
    /// <param name="trade">The executed trade.</param>
    public void OnOwnTradeReceived(MyTrade trade)
    {
        var order = trade.Order;

        // Entry order filled - activate protection
        if (order == _entryOrder)
        {
            HandleEntryFill(trade);
        }
        // Stop-loss filled - cancel take-profit
        else if (order == _stopLossOrder)
        {
            HandleStopLossFill(trade);
        }
        // Take-profit filled - cancel stop-loss
        else if (order == _takeProfitOrder)
        {
            HandleTakeProfitFill(trade);
        }
    }

    /// <summary>
    /// Resets the manager state (call from strategy OnReseted).
    /// </summary>
    public void Reset()
    {
        _entryOrder = null;
        _stopLossOrder = null;
        _takeProfitOrder = null;
        _currentSignal = null;
    }

    #region Private Helper Methods

    private void PlaceEntryOrder(TradeSignal signal)
    {
        _strategy.LogInfo("Placing {0} entry order at {1:F2} Volume:{2}",
            signal.Direction, signal.EntryPrice, signal.Volume);

        if (signal.Direction == Sides.Buy)
        {
            _entryOrder = _strategy.BuyLimit(signal.EntryPrice, signal.Volume);
        }
        else
        {
            _entryOrder = _strategy.SellLimit(signal.EntryPrice, signal.Volume);
        }
    }

    private void HandleEntryFill(MyTrade trade)
    {
        _strategy.LogInfo("Entry order filled at {0:F2}, Position: {1}",
            trade.Trade.Price, _strategy.Position);

        _entryOrder = null;

        // Place protection orders if signal has SL/TP levels
        if (_currentSignal == null)
            return;

        PlaceProtectionOrders(_currentSignal);
    }

    private void PlaceProtectionOrders(TradeSignal signal)
    {
        var volume = Math.Abs(_strategy.Position);

        if (volume == 0)
        {
            _strategy.LogWarning("Cannot place protection orders - no position");
            return;
        }

        // Determine exit direction (opposite of entry)
        var exitDirection = _strategy.Position > 0 ? Sides.Sell : Sides.Buy;

        // Place stop-loss order
        if (signal.StopLoss.HasValue)
        {
            _strategy.LogInfo("Placing stop-loss order at {0:F2}", signal.StopLoss.Value);

            if (exitDirection == Sides.Sell)
                _stopLossOrder = _strategy.SellLimit(signal.StopLoss.Value, volume);
            else
                _stopLossOrder = _strategy.BuyLimit(signal.StopLoss.Value, volume);
        }

        // Place take-profit order
        if (signal.TakeProfit.HasValue)
        {
            _strategy.LogInfo("Placing take-profit order at {0:F2}", signal.TakeProfit.Value);

            if (exitDirection == Sides.Sell)
                _takeProfitOrder = _strategy.SellLimit(signal.TakeProfit.Value, volume);
            else
                _takeProfitOrder = _strategy.BuyLimit(signal.TakeProfit.Value, volume);
        }
    }

    private void HandleStopLossFill(MyTrade trade)
    {
        _strategy.LogInfo("Stop-loss filled at {0:F2}, Position: {1}",
            trade.Trade.Price, _strategy.Position);

        _stopLossOrder = null;

        // Cancel take-profit order
        if (IsOrderActive(_takeProfitOrder))
        {
            _strategy.LogInfo("Canceling take-profit order");
            _strategy.CancelOrder(_takeProfitOrder);
            _takeProfitOrder = null;
        }

        // Clear current signal
        _currentSignal = null;
    }

    private void HandleTakeProfitFill(MyTrade trade)
    {
        _strategy.LogInfo("Take-profit filled at {0:F2}, Position: {1}",
            trade.Trade.Price, _strategy.Position);

        _takeProfitOrder = null;

        // Cancel stop-loss order
        if (IsOrderActive(_stopLossOrder))
        {
            _strategy.LogInfo("Canceling stop-loss order");
            _strategy.CancelOrder(_stopLossOrder);
            _stopLossOrder = null;
        }

        // Clear current signal
        _currentSignal = null;
    }

    private void CancelAllOrders()
    {
        if (IsOrderActive(_entryOrder))
        {
            _strategy.LogInfo("Canceling entry order");
            _strategy.CancelOrder(_entryOrder);
            _entryOrder = null;
        }

        CancelProtectionOrders();
    }

    private void CancelProtectionOrders()
    {
        if (IsOrderActive(_stopLossOrder))
        {
            _strategy.LogInfo("Canceling stop-loss order");
            _strategy.CancelOrder(_stopLossOrder);
            _stopLossOrder = null;
        }

        if (IsOrderActive(_takeProfitOrder))
        {
            _strategy.LogInfo("Canceling take-profit order");
            _strategy.CancelOrder(_takeProfitOrder);
            _takeProfitOrder = null;
        }
    }

    private bool HasSignalChanged(TradeSignal newSignal)
    {
        if (_currentSignal == null)
            return true;

        var priceStep = PriceStepHelper.GetPriceStep(_strategy.Security);

        // Check if entry price changed
        if (Math.Abs(_currentSignal.EntryPrice - newSignal.EntryPrice) > priceStep)
            return true;

        // Check if stop-loss changed
        var oldSL = _currentSignal.StopLoss ?? 0;
        var newSL = newSignal.StopLoss ?? 0;
        if (Math.Abs(oldSL - newSL) > priceStep)
            return true;

        // Check if take-profit changed
        var oldTP = _currentSignal.TakeProfit ?? 0;
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

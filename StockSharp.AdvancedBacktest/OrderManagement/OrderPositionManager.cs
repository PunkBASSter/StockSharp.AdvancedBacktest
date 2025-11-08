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
    public record MyOrder(Order EntryOrder, Order? SlOrder, Order? TpOrder);

    private readonly CustomStrategyBase _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

    private MyOrder? _order;
    public MyOrder? Order => _order;

    private TradeSignal? _lastSignal;

    public MyOrder[] ActiveOrders()
    {
        if (_order is null || !IsOrderActive(_order.EntryOrder))
            return [];

        return [_order];
    }

    public void HandleSignal(TradeSignal? signal)
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
            return;
        }

        signal.Validate();
        if (HasSignalChanged(signal) && IsOrderActive(_order.EntryOrder))
        {
            _strategy.LogInfo("Canceling existing entry order - signal levels changed");
            CancelAllOrders();
            PlaceEntryOrder(signal);
        }
    }

    public void CloseAllPositions() //TODO: what to do with existing orders? _order=null?
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
    }

    #region Private Helper Methods

    private void PlaceEntryOrder(TradeSignal signal)
    {
        _strategy.LogInfo("Placing {0} entry order at {1:F2} Volume:{2}",
            signal.Direction, signal.EntryPrice, signal.Volume);

        _lastSignal = signal;

        if (signal.Direction == Sides.Buy)
        {
            var entryOrder = _strategy.BuyLimit(signal.EntryPrice, signal.Volume);
            _order = new MyOrder(entryOrder, null, null);
        }
        else
        {
            var entryOrder = _strategy.SellLimit(signal.EntryPrice, signal.Volume);
            _order = new MyOrder(entryOrder, null, null);
        }
    }

    private void PlaceProtectionOrders(TradeSignal signal)
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

        PlaceProtectionOrders(_lastSignal);
    }

    private void HandleStopLossFill(MyTrade trade)
    {
        _strategy.LogInfo("Stop-loss filled at {0:F2}, Position: {1}",
            trade.Trade.Price, _strategy.Position);

        if (IsOrderActive(_order!.TpOrder))
        {
            _strategy.LogInfo("Canceling take-profit order");
            _strategy.CancelOrder(_order.TpOrder);
            _order = null;
        }

        _lastSignal = null;
    }

    private void HandleTakeProfitFill(MyTrade trade)
    {
        _strategy.LogInfo("Take-profit filled at {0:F2}, Position: {1}",
            trade.Trade.Price, _strategy.Position);

        if (IsOrderActive(_order!.SlOrder))
        {
            _strategy.LogInfo("Canceling stop-loss order");
            _strategy.CancelOrder(_order.SlOrder);
            _order = null;
        }

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
            _strategy.CancelOrder(_order!.SlOrder);
        }

        if (IsOrderActive(_order?.TpOrder))
        {
            _strategy.LogInfo("Canceling take-profit order");
            _strategy.CancelOrder(_order!.TpOrder);
        }
    }

    private bool HasSignalChanged(TradeSignal newSignal)
    {
        if (_lastSignal == null)
            return true;

        var priceStep = PriceStepHelper.GetPriceStep(_strategy.Security);

        // Check if entry price changed
        if (Math.Abs(_lastSignal.EntryPrice - newSignal.EntryPrice) > priceStep)
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

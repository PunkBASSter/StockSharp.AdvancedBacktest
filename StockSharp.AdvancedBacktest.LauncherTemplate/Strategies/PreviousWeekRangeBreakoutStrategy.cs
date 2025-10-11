using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Strategies.Modules;
using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;
using StockSharp.AdvancedBacktest.Strategies.Modules.StopLoss;
using StockSharp.AdvancedBacktest.Strategies.Modules.TakeProfit;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using ModulesIndicatorType = StockSharp.AdvancedBacktest.Strategies.Modules.IndicatorType;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;

public class PreviousWeekRangeBreakoutStrategy : CustomStrategyBase
{
    private IPositionSizer? _positionSizer;
    private IStopLossCalculator? _stopLossCalculator;
    private ITakeProfitCalculator? _takeProfitCalculator;

    private decimal? _previousWeekHigh;
    private decimal? _previousWeekLow;
    private DateTimeOffset? _currentWeekStartTime;
    private decimal _weekHigh;
    private decimal _weekLow;
    private IIndicator? _trendFilter;
    private AverageTrueRange? _atr;
    private bool _hasBreakoutOccurred;
    private ModulesIndicatorType _trendFilterType;

    private class ProtectiveOrders
    {
        public required Order StopLoss { get; set; }
        public required Order TakeProfit { get; set; }
    }

    private readonly Dictionary<Order, ProtectiveOrders> _protectiveOrdersMap = new();

    protected override void OnReseted()
    {
        base.OnReseted();

        _currentWeekStartTime = null;
        _previousWeekHigh = null;
        _previousWeekLow = null;
        _weekHigh = 0;
        _weekLow = 0;
        _hasBreakoutOccurred = false;
        _protectiveOrdersMap.Clear();
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Read configuration from ParamsContainer
        _trendFilterType = GetParam<ModulesIndicatorType>("TrendFilter.Type");
        var trendFilterPeriod = GetParam<int>("TrendFilter.Period");
        var atrPeriod = GetParam<int>("ATR.Period");

        // Create indicators
        _trendFilter = _trendFilterType == ModulesIndicatorType.SMA
            ? new SimpleMovingAverage { Length = trendFilterPeriod }
            : new ExponentialMovingAverage { Length = trendFilterPeriod };

        _atr = new AverageTrueRange { Length = atrPeriod };

        // Create modules dynamically based on parameters
        _positionSizer = CreatePositionSizer();
        _stopLossCalculator = CreateStopLossCalculator();
        _takeProfitCalculator = CreateTakeProfitCalculator();

        var subscription = new Subscription(TimeSpan.FromDays(1).TimeFrame(), Security)
        {
            MarketData =
            {
                IsFinishedOnly = true,
                BuildMode = MarketDataBuildModes.LoadAndBuild,
            }
        };

        SubscribeCandles(subscription)
            .Bind(_trendFilter, _atr, OnProcess)
            .Start();
    }

    protected override void OnOwnTradeReceived(MyTrade trade)
    {
        base.OnOwnTradeReceived(trade);

        var order = trade.Order;

        this.LogInfo("Trade filled: {0} {1} @ {2:F2}, Position: {3}",
            order.Side, trade.Trade.Volume, trade.Trade.Price, Position);

        // Check if this is a protective order fill
        foreach (var kvp in _protectiveOrdersMap.ToList())
        {
            var entryOrder = kvp.Key;
            var protective = kvp.Value;

            if (order == protective.StopLoss || order == protective.TakeProfit)
            {
                // One protective filled, cancel the other
                var orderToCancel = (order == protective.StopLoss)
                    ? protective.TakeProfit
                    : protective.StopLoss;

                if (orderToCancel.State == OrderStates.Active)
                {
                    this.LogInfo("Canceling opposite protective order: {0}",
                        orderToCancel.TransactionId);
                    CancelOrder(orderToCancel);
                }

                // Clean up tracking
                _protectiveOrdersMap.Remove(entryOrder);
                break;
            }
        }
    }

    private IPositionSizer CreatePositionSizer()
    {
        var method = GetParam<PositionSizingMethod>("PositionSizing.Method");

        return method switch
        {
            PositionSizingMethod.Fixed => new FixedPositionSizer(
                GetParam<decimal>("PositionSizing.FixedSize")),

            PositionSizingMethod.PercentOfEquity => new PercentEquityPositionSizer(
                GetParam<decimal>("PositionSizing.EquityPercent")),

            PositionSizingMethod.ATRBased => new ATRBasedPositionSizer(
                GetParam<decimal>("PositionSizing.EquityPercent"),
                GetParam<decimal>("PositionSizing.ATRMultiplier")),

            _ => throw new InvalidOperationException($"Unknown position sizing method: {method}")
        };
    }

    private IStopLossCalculator CreateStopLossCalculator()
    {
        var method = GetParam<StopLossMethod>("StopLoss.Method");

        return method switch
        {
            StopLossMethod.Percentage => new PercentageStopLoss(
                GetParam<decimal>("StopLoss.Percentage")),

            StopLossMethod.ATR => new ATRStopLoss(
                GetParam<decimal>("StopLoss.ATRMultiplier")),

            _ => throw new InvalidOperationException($"Unknown stop-loss method: {method}")
        };
    }

    private ITakeProfitCalculator CreateTakeProfitCalculator()
    {
        var method = GetParam<TakeProfitMethod>("TakeProfit.Method");

        return method switch
        {
            TakeProfitMethod.Percentage => new PercentageTakeProfit(
                GetParam<decimal>("TakeProfit.Percentage")),

            TakeProfitMethod.ATR => new ATRTakeProfit(
                GetParam<decimal>("TakeProfit.ATRMultiplier")),

            TakeProfitMethod.RiskReward => new RiskRewardTakeProfit(
                GetParam<decimal>("TakeProfit.RiskRewardRatio")),

            _ => throw new InvalidOperationException($"Unknown take-profit method: {method}")
        };
    }

    private void OnProcess(ICandleMessage candle, decimal trendValue, decimal atrValue)
    {
        if (candle.State != CandleStates.Finished)
            return;

        if (IsNewWeek(candle.OpenTime))
        {
            if (_currentWeekStartTime != null)
            {
                _previousWeekHigh = _weekHigh;
                _previousWeekLow = _weekLow;
            }

            _currentWeekStartTime = GetWeekStart(candle.OpenTime);
            _weekHigh = candle.HighPrice;
            _weekLow = candle.LowPrice;
            _hasBreakoutOccurred = false;
        }
        else
        {
            _weekHigh = Math.Max(_weekHigh, candle.HighPrice);

            if (_weekLow == 0 || candle.LowPrice < _weekLow)
                _weekLow = candle.LowPrice;
        }

        var signal = CheckForBreakoutSignal(candle, trendValue);
        if (signal != null)
        {
            LogSignal(signal.Value, candle, trendValue);
            ExecuteBreakoutTrade(signal.Value, candle);
            _hasBreakoutOccurred = true;
        }
    }

    private bool IsNewWeek(DateTimeOffset candleTime)
    {
        if (_currentWeekStartTime == null)
            return true;

        var currentWeekStart = GetWeekStart(candleTime);
        var trackedWeekStart = GetWeekStart(_currentWeekStartTime.Value);

        return currentWeekStart != trackedWeekStart;
    }

    private DateTimeOffset GetWeekStart(DateTimeOffset time)
    {
        var daysFromMonday = ((int)time.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return new DateTimeOffset(time.Date.AddDays(-daysFromMonday), time.Offset);
    }

    private Sides? CheckForBreakoutSignal(ICandleMessage candle, decimal trendValue)
    {
        if (_previousWeekHigh == null || _previousWeekLow == null)
            return null;

        if (_hasBreakoutOccurred)
            return null;

        if (Position != 0)
            return null;

        var closePrice = candle.ClosePrice;

        if (closePrice > _previousWeekHigh.Value && closePrice > trendValue)
            return Sides.Buy;

        if (closePrice < _previousWeekLow.Value && closePrice < trendValue)
            return Sides.Sell;

        return null;
    }

    private void LogSignal(Sides signal, ICandleMessage candle, decimal trendValue)
    {
        var breakoutLevel = signal == Sides.Buy ? _previousWeekHigh : _previousWeekLow;
        var direction = signal == Sides.Buy ? "LONG" : "SHORT";

        this.LogInfo($"Breakout signal detected: {direction} at {candle.CloseTime:yyyy-MM-dd HH:mm:ss}");
        this.LogInfo($"  Close Price: {candle.ClosePrice:F2}");
        this.LogInfo($"  Breakout Level: {breakoutLevel:F2}");
        this.LogInfo($"  Trend Filter ({_trendFilterType}): {trendValue:F2}");
        this.LogInfo($"  Position: {Position}");
    }

    private decimal CalculatePositionSize(decimal price)
    {
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));

        if (_positionSizer == null)
            throw new InvalidOperationException("Position sizer is not initialized");

        var atrValue = GetCurrentATRValue();
        var positionSize = _positionSizer.Calculate(price, atrValue, Portfolio);

        const decimal minimumPositionSize = 0.01m;
        if (positionSize < minimumPositionSize)
            throw new InvalidOperationException(
                $"Calculated position size {positionSize} is below minimum {minimumPositionSize}");

        return positionSize;
    }

    private decimal CalculateStopLoss(Sides side, decimal entryPrice)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        if (_stopLossCalculator == null)
            throw new InvalidOperationException("Stop-loss calculator is not initialized");

        var atrValue = GetCurrentATRValue();
        return _stopLossCalculator.Calculate(side, entryPrice, atrValue);
    }

    private decimal CalculateTakeProfit(Sides side, decimal entryPrice, decimal stopLoss)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        if (stopLoss <= 0)
            throw new ArgumentException("Stop-loss must be greater than zero", nameof(stopLoss));

        if (_takeProfitCalculator == null)
            throw new InvalidOperationException("Take-profit calculator is not initialized");

        var atrValue = GetCurrentATRValue();
        return _takeProfitCalculator.Calculate(side, entryPrice, stopLoss, atrValue);
    }

    private void ExecuteBreakoutTrade(Sides signal, ICandleMessage candle)
    {
        if (Position != 0)
        {
            this.LogWarning("Cannot execute trade: existing position {0}", Position);
            return;
        }

        if (!IsFormedAndOnlineAndAllowTrading())
        {
            this.LogWarning("Cannot execute trade: strategy not ready");
            return;
        }

        try
        {
            var entryPrice = candle.ClosePrice;

            // Reuse existing calculation modules
            var positionSize = CalculatePositionSize(entryPrice);
            var stopLossPrice = CalculateStopLoss(signal, entryPrice);
            var takeProfitPrice = CalculateTakeProfit(signal, entryPrice, stopLossPrice);

            this.LogInfo("Executing {0} breakout trade:", signal);
            this.LogInfo("  Entry Price: {0:F2}", entryPrice);
            this.LogInfo("  Position Size: {0:F4}", positionSize);
            this.LogInfo("  Stop Loss: {0:F2}", stopLossPrice);
            this.LogInfo("  Take Profit: {0:F2}", takeProfitPrice);
            this.LogInfo("  ATR: {0:F2}", GetCurrentATRValue());

            // Create entry order (limit order at breakout price)
            Order entryOrder;
            if (signal == Sides.Buy)
                entryOrder = BuyLimit(entryPrice, positionSize);
            else
                entryOrder = SellLimit(entryPrice, positionSize);

            this.LogInfo("Entry order {0} registered at {1:F2}", entryOrder.TransactionId, entryPrice);

            // Register protective orders immediately
            RegisterProtectiveOrders(entryOrder, signal, positionSize, stopLossPrice, takeProfitPrice);
        }
        catch (Exception ex)
        {
            this.LogError("Trade execution failed: {0}", ex.Message);
        }
    }

    private void RegisterProtectiveOrders(Order entryOrder, Sides entrySide, decimal volume,
        decimal stopLossPrice, decimal takeProfitPrice)
    {
        var exitSide = entrySide.Invert();

        // Stop-loss limit order
        Order stopOrder;
        if (exitSide == Sides.Buy)
            stopOrder = BuyLimit(stopLossPrice, volume);
        else
            stopOrder = SellLimit(stopLossPrice, volume);

        // Take-profit limit order
        Order tpOrder;
        if (exitSide == Sides.Buy)
            tpOrder = BuyLimit(takeProfitPrice, volume);
        else
            tpOrder = SellLimit(takeProfitPrice, volume);

        // Track for later cancellation
        _protectiveOrdersMap[entryOrder] = new ProtectiveOrders
        {
            StopLoss = stopOrder,
            TakeProfit = tpOrder
        };

        this.LogInfo("Protective orders registered:");
        this.LogInfo("  Stop-Loss: {0} at {1:F2}", stopOrder.TransactionId, stopLossPrice);
        this.LogInfo("  Take-Profit: {0} at {1:F2}", tpOrder.TransactionId, takeProfitPrice);
    }

    private decimal GetCurrentATRValue()
    {
        if (_atr == null)
            throw new InvalidOperationException("ATR indicator is not initialized");

        if (!_atr.IsFormed)
            throw new InvalidOperationException("ATR indicator is not yet formed - insufficient data");

        var atrValue = _atr.GetCurrentValue();

        if (atrValue <= 0)
            throw new InvalidOperationException($"ATR value must be greater than zero, got {atrValue}");

        return atrValue;
    }
}

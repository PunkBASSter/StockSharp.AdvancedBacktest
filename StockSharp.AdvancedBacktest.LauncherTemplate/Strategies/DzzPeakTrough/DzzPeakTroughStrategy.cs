using StockSharp.AdvancedBacktest.Indicators;
using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.DzzPeakTrough;

public class DzzPeakTroughStrategy : CustomStrategyBase
{
    private DeltaZzPeak? _peakIndicator;
    private DeltaZzTrough? _troughIndicator;
    private DzzPeakTroughConfig? _config;
    private OrderPositionManager? _orderManager;
    private IRiskAwarePositionSizer? _positionSizer;
    private SignalDeduplicator _signalDeduplicator = new();

    // Combined history of peak/trough values (FR-002: DzzPeakTroughHistory)
    // Stores: (value, isUp=true for peak/false for trough, time)
    private readonly List<(decimal value, bool isUp, DateTimeOffset time)> _dzzHistory = [];

    public override IEnumerable<(Security sec, DataType dt)> GetWorkingSecurities()
    {
        return Securities.SelectMany(kvp =>
            kvp.Value.Select(timespan => (kvp.Key, timespan.TimeFrame())));
    }

    protected override void OnReseted()
    {
        base.OnReseted();
        _orderManager?.Reset();
        _signalDeduplicator.Reset();
        _dzzHistory.Clear();
    }

    protected override void OnStarted2(DateTime time)
    {
        _config = new DzzPeakTroughConfig
        {
            DzzDepth = GetParam<decimal>("DzzDepth")
        };

        // Initialize position sizer with fixed risk calculation
        _positionSizer = new FixedRiskPositionSizer(
            _config.RiskPercentPerTrade,
            _config.MinPositionSize,
            _config.MaxPositionSize);

        // Initialize order manager
        _orderManager = new OrderPositionManager(this, Security, Name);

        var delta = _config.DzzDepth / 10m;
        var minThreshold = _config.MinimumThreshold ?? PriceStepHelper.GetDefaultDelta(Security, multiplier: 10);

        // Initialize Peak indicator
        _peakIndicator = new DeltaZzPeak
        {
            Delta = delta,
            MinimumThreshold = minThreshold
        };

        // Initialize Trough indicator
        _troughIndicator = new DeltaZzTrough
        {
            Delta = delta,
            MinimumThreshold = minThreshold
        };

        // Register indicators BEFORE calling base.OnStarted2 so debug mode can subscribe to them
        Indicators.Add(_peakIndicator);
        Indicators.Add(_troughIndicator);

        // Now call base to initialize debug mode with the indicators already registered
        base.OnStarted2(time);

        var timeframe = Securities.First().Value.First();

        var subscription = new Subscription(timeframe.TimeFrame(), Security)
        {
            MarketData =
            {
                IsFinishedOnly = true,
                BuildMode = MarketDataBuildModes.LoadAndBuild,
            }
        };

        SubscribeCandles(subscription)
            .BindWithEmpty(_peakIndicator, _troughIndicator, OnProcessCandle)
            .Start();
    }

    private void OnProcessCandle(ICandleMessage candle, decimal? peakValue, decimal? troughValue)
    {
        if (candle.State != CandleStates.Finished)
            return;

        // FR-002: Add non-empty Peak values to _dzzHistory (satisfies DzzPeakTroughHistory requirement)
        if (peakValue.HasValue && peakValue.Value != 0)
        {
            _dzzHistory.Add((peakValue.Value, isUp: true, candle.OpenTime));
        }

        // FR-002: Add non-empty Trough values to _dzzHistory (chronologically ordered)
        if (troughValue.HasValue && troughValue.Value != 0)
        {
            _dzzHistory.Add((troughValue.Value, isUp: false, candle.OpenTime));
        }

        // Check stop-loss and take-profit BEFORE checking for new signals
        if (_orderManager!.CheckProtectionLevels(candle))
        {
            // Position was closed, reset deduplicator to allow re-entry
            _signalDeduplicator.Reset();
            return;
        }

        // Don't process new signals if we already have a position
        if (Position > 0)
            return;

        var signalData = TryGetBuyOrder();

        // If no valid signal, cancel any pending entry orders
        if (!signalData.HasValue)
        {
            _orderManager.HandleOrderRequest(null);
            return;
        }

        var (price, sl, tp) = signalData.Value;

        // Check for duplicate signal (FR-004, FR-005)
        if (_signalDeduplicator.IsDuplicate(price, sl, tp))
            return;

        var volume = CalculatePositionSize(price, sl);

        var entryOrder = new Order
        {
            Side = Sides.Buy,
            Price = price,
            Volume = volume,
            Security = Security,
            Portfolio = Portfolio,
            Type = OrderTypes.Limit
        };

        var protectivePair = new ProtectivePair(sl, tp, volume);

        this.LogInfo("Signal: BUY LIMIT at {0:F2} SL:{1:F2} TP:{2:F2} Volume:{3}", price, sl, tp, volume);
        var orderToRegister = _orderManager.HandleOrderRequest(new OrderRequest(entryOrder, [protectivePair]));
        if (orderToRegister != null)
            RegisterOrder(orderToRegister);
    }

    private (decimal price, decimal sl, decimal tp)? TryGetBuyOrder()
    {
        if (_config == null)
            return null;

        // Need at least 3 zigzag points to detect pattern
        if (_dzzHistory.Count < 3)
            return null;

        // Extract last 3 non-zero points from history
        var nonZeroPoints = _dzzHistory
            .Where(h => h.value != 0)
            .TakeLast(3)
            .ToArray();

        if (nonZeroPoints.Length < 3)
            return null;

        var sl = nonZeroPoints[0].value;
        var price = nonZeroPoints[1].value;
        var l1 = nonZeroPoints[2].value;

        // Check pattern for breakout:
        // - price > sl: resistance level above stop loss (valid risk/reward)
        // - l1 < price: current swing is below resistance (waiting for breakout)
        if (price > sl && l1 < price)
        {
            var tp = price + Math.Abs(price - sl);
            return (price, sl, tp);
        }

        return null;
    }

    private decimal CalculatePositionSize(decimal entryPrice, decimal stopLoss)
    {
        if (_positionSizer == null || _config == null)
            return _config?.MinPositionSize ?? 0.01m;

        var volume = _positionSizer.Calculate(entryPrice, stopLoss, Portfolio, Security);

        this.LogInfo("Position sizing - Account:{0:F2} Risk:{1}% SL Distance:{2:F4} Volume:{3}",
            Portfolio.CurrentValue ?? Portfolio.BeginValue ?? 0,
            _config.RiskPercentPerTrade,
            Math.Abs(entryPrice - stopLoss),
            volume);

        return volume;
    }

    protected override void OnOwnTradeReceived(MyTrade trade)
    {
        base.OnOwnTradeReceived(trade);
        _orderManager?.OnOwnTradeReceived(trade);

        // Reset deduplicator when position closes (position goes to 0)
        if (Position == 0)
            _signalDeduplicator.Reset();
    }

    protected override void OnAuxiliaryCandle(ICandleMessage candle)
    {
        // Check protection levels on auxiliary TF candles for more granular SL/TP checking
        if (candle.State == CandleStates.Finished)
        {
            if (_orderManager?.CheckProtectionLevels(candle) == true)
                _signalDeduplicator.Reset();
        }
    }
}

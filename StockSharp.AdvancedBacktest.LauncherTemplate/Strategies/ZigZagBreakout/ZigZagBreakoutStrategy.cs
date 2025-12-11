using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;

public class ZigZagBreakout : CustomStrategyBase
{
    private DeltaZigZag? _dzz;
    private ZigZagBreakoutConfig? _config;
    private OrderPositionManager? _orderManager;
    private IRiskAwarePositionSizer? _positionSizer;
    private readonly List<IIndicatorValue> _dzzHistory = [];
    private TimeSpan? _candleInterval;

    public override IEnumerable<(Security sec, DataType dt)> GetWorkingSecurities()
    {
        // Return securities from the Securities dictionary with their candle types
        return Securities.SelectMany(kvp =>
            kvp.Value.Select(timespan => (kvp.Key, timespan.TimeFrame())));
    }

    protected override void OnReseted()
    {
        base.OnReseted();
        _orderManager?.Reset();
        _dzzHistory.Clear();
    }

    protected override void OnStarted2(DateTime time)
    {
        _config = new ZigZagBreakoutConfig
        {
            DzzDepth = GetParam<decimal>("DzzDepth")
        };

        // Initialize position sizer with fixed risk calculation
        _positionSizer = new FixedRiskPositionSizer(
            _config.RiskPercentPerTrade,
            _config.MinPositionSize,
            _config.MaxPositionSize);

        // Initialize order manager
        _orderManager = new OrderPositionManager(this);

        _dzz = new DeltaZigZag
        {
            Delta = _config.DzzDepth / 10m,  // Divide by 10 like Python (5 -> 0.5)
            // Set minimum threshold based on price step for initial swings
            MinimumThreshold = PriceStepHelper.GetDefaultDelta(Security, multiplier: 10)
        };

        // Register indicators BEFORE calling base.OnStarted2 so debug mode can subscribe to them
        Indicators.Add(_dzz);

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
            .BindWithEmpty(_dzz, OnProcessCandle)
            .Start();
    }

    private void OnProcessCandle(ICandleMessage candle, decimal? dzzValue)
    {
        if (candle.OpenTime == new DateTimeOffset(2020, 1, 03, 2, 0, 0, TimeSpan.Zero))
            LogDebug("last order timestamp"); //Stops were not placed here - custom order management was used
        
        //A special bar that triggers open order and tp at the same time for testing
        if (candle.OpenTime == new DateTimeOffset(2020, 4, 29, 1, 0, 0, TimeSpan.Zero))
            LogDebug("last order timestamp"); // Stops from StandardProtection were placed but never triggered (as price moved too far)
                                              //TODO: Also check how DZZ handles big candle with both local peak and trough

        if (candle.OpenTime == new DateTimeOffset(2020, 2, 06, 9, 0, 0, TimeSpan.Zero))
            LogDebug("last order timestamp"); //Stops were not placed here - custom order management was used

        if (candle.State != CandleStates.Finished)
            return;

        if (!_candleInterval.HasValue && _dzzHistory.Count > 0)
        {
            var lastTime = _dzzHistory[^1].Time;
            _candleInterval = candle.OpenTime - lastTime;
        }

        if (_dzz!.IsFormed)
        {
            var dzzIndicatorValue = _dzz.Container.GetValue(0).output;
            _dzzHistory.Add(dzzIndicatorValue);
        }

        // Check stop-loss and take-profit BEFORE checking for new signals
        // This ensures SL/TP can execute in the same candle as entry if needed
        if (_orderManager!.CheckProtectionLevels(candle))
            return; // Position was closed, no need to check for new signals

        // Don't process new signals if we already have a position
        if (Position > 0)
            return;

        var signalData = TryGetBuyOrder();

        // If no valid signal, cancel any pending entry orders
        if (!signalData.HasValue)
        {
            _orderManager.HandleSignal(null);
            return;
        }

        // Don't place new orders if there's already an active pending order with same signal
        var activeOrders = _orderManager.ActiveOrders();
        if (activeOrders.Length > 0)
        {
            // Let HandleSignal decide if the signal changed enough to replace
        }

        var (price, sl, tp) = signalData.Value;
        var volume = CalculatePositionSize(price, sl);

        var signal = new TradeSignal
        {
            Direction = Sides.Buy,
            EntryPrice = price,
            Volume = volume,
            StopLoss = sl,
            TakeProfit = tp,
            OrderType = OrderTypes.Limit
        };

        this.LogInfo("Signal: BUY LIMIT at {0:F2} SL:{1:F2} TP:{2:F2} Volume:{3}", price, sl, tp, volume);
        _orderManager.HandleSignal(signal);
    }

    private (decimal price, decimal sl, decimal tp)? TryGetBuyOrder()
    {
        if (_dzz == null || _config == null)
            return null;

        if (!_dzz.IsFormed)
            return null;

        // Need at least 20 values to extract pattern
        if (_dzzHistory.Count < 20)
            return null;

        // Extract last 3 non-zero zigzag points from last 20 values
        // Filter out empty indicator values before extracting decimal values
        var nonZeroPoints = _dzzHistory
            .Skip(Math.Max(0, _dzzHistory.Count - 20))
            .Where(h => !h.IsEmpty)
            .Select(h => h.GetValue<decimal>())
            .Where(v => v != 0)
            .TakeLast(3)
            .ToArray();

        if (nonZeroPoints.Length < 3)
            return null;

        var sl = nonZeroPoints[0];
        var price = nonZeroPoints[1];
        var l1 = nonZeroPoints[2];

        // Check pattern: sl < l1 < price and price > sl
        if (price > sl && sl < l1 && l1 < price)
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
    }
}

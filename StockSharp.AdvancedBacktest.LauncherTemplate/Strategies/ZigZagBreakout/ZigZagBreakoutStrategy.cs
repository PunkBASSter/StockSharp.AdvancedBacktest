using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;

public class ZigZagBreakout : CustomStrategyBase
{
    private DeltaZigZag? _dzz;
    private ZigZagBreakoutConfig? _config;
    private OrderPositionManager? _orderManager;
    private readonly List<IIndicatorValue> _dzzHistory = [];
    private TimeSpan? _candleInterval;

    protected override void OnReseted()
    {
        base.OnReseted();
        _orderManager?.Reset();
        _dzzHistory.Clear();
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        _config = new ZigZagBreakoutConfig
        {
            DzzDepth = GetParam<decimal>("DzzDepth")
        };

        // Initialize order manager
        _orderManager = new OrderPositionManager(this);

        _dzz = new DeltaZigZag
        {
            Delta = _config.DzzDepth / 10m,  // Divide by 10 like Python (5 -> 0.5)
            // Set minimum threshold based on price step for initial swings
            MinimumThreshold = PriceStepHelper.GetDefaultDelta(Security, multiplier: 10)
        };

        // Register indicators BEFORE calling base.OnStarted so debug mode can subscribe to them
        Indicators.Add(_dzz);

        // Now call base to initialize debug mode with the indicators already registered
        base.OnStarted(time);

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
            .Bind(_dzz, OnProcessCandle)
            .Start();
    }

    private void OnProcessCandle(ICandleMessage candle, decimal dzzValue)
    {
        //A special bar that triggers open order and tp at the same time for testing
        if (candle.OpenTime == new DateTimeOffset(2020, 4, 29, 1, 0, 0, TimeSpan.Zero))
            LogDebug("last order timestamp");

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

        // Check if we can trade (no position, no active orders)
        if (!_orderManager!.CanTrade())
            return;

        // Try to get a buy signal from ZigZag pattern
        var signalData = TryGetBuyOrder();

        if (signalData == null)
        {
            // No valid signal - cancel any pending orders
            _orderManager!.HandleSignal(null);
            return;
        }

        var (price, sl, tp) = signalData.Value;

        // Calculate position size based on risk
        var volume = CalculatePositionSize(price, sl);

        // Create and handle the signal
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
        var last20 = _dzzHistory
            .Skip(Math.Max(0, _dzzHistory.Count - 20))
            .Select(h => h.GetValue<decimal>())
            .ToList();
        var nonZeroPoints = last20
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
        if (_config == null)
            return 1m;

        // Calculate risk amount in currency
        var accountSize = Portfolio.CurrentValue ?? 10000m; // Default to 10000 if null
        var riskAmount = accountSize * _config.RiskPercentPerTrade;

        // Calculate stop loss distance
        var stopDistance = Math.Abs(entryPrice - stopLoss);

        if (stopDistance == 0)
            return 1m;

        // Calculate position size: riskAmount / stopDistance
        var volume = riskAmount / stopDistance;

        // Round to valid lot size (minimum 1)
        volume = Math.Max(1m, Math.Floor(volume));

        this.LogInfo("Position sizing - Account:{0:F2} Risk:{1:F2} SL Distance:{2:F4} Volume:{3}",
            accountSize, riskAmount, stopDistance, volume);

        return volume;
    }

    protected override void OnOwnTradeReceived(MyTrade trade)
    {
        base.OnOwnTradeReceived(trade);

        // Delegate to order manager to handle entry fills and protection order management
        _orderManager?.OnOwnTradeReceived(trade);
    }
}

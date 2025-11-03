using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout.TrendFiltering;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;

public class ZigZagBreakout : CustomStrategyBase
{
    private DeltaZigZag? _dzz;
    private Jma? _jma;
    private ZigZagBreakoutConfig? _config;
    private Order? _currentBuyOrder;
    private Order? _currentStopLoss;
    private Order? _currentTakeProfit;
    private readonly List<IIndicatorValue> _dzzHistory = [];
    private readonly List<IIndicatorValue> _jmaHistory = [];
    private TimeSpan? _candleInterval;

    protected override void OnReseted()
    {
        base.OnReseted();
        _currentBuyOrder = null;
        _currentStopLoss = null;
        _currentTakeProfit = null;
        _dzzHistory.Clear();
        _jmaHistory.Clear();
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        _config = new ZigZagBreakoutConfig
        {
            DzzDepth = GetParam<decimal>("DzzDepth"),
            JmaLength = GetParam<int>("JmaLength"),
            JmaPhase = GetParam<int>("JmaPhase"),
            JmaUsage = GetParam<int>("JmaUsage")
        };

        _dzz = new DeltaZigZag
        {
            Delta = _config.DzzDepth / 10m,  // Divide by 10 like Python (5 -> 0.5)
            // Set minimum threshold based on price step for initial swings
            MinimumThreshold = PriceStepHelper.GetDefaultDelta(Security, multiplier: 10)
        };

        _jma = new Jma
        {
            Length = _config.JmaLength,
            Phase = _config.JmaPhase
        };

        // Register indicators BEFORE calling base.OnStarted so debug mode can subscribe to them
        Indicators.Add(_dzz);
        Indicators.Add(_jma);

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
            .Bind(_dzz, _jma, OnProcessCandle)
            .Start();
    }

    private void OnProcessCandle(ICandleMessage candle, decimal dzzValue, decimal jmaValue)
    {
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

        if (_jma!.IsFormed)
        {
            var jmaIndicatorValue = _jma.Container.GetValue(0).output;
            _jmaHistory.Add(jmaIndicatorValue);
        }

        var signal = TryGetBuyOrder();
        if (signal == null)
        {
            return;
        }

        var (price, sl, tp) = signal.Value;

        // Simple order management: place limit order at breakout price
        if (_currentBuyOrder != null && _currentBuyOrder.State == OrderStates.Active)
        {
            // Cancel existing order if price changed
            if (Math.Abs(_currentBuyOrder.Price - price) > PriceStepHelper.GetPriceStep(Security))
            {
                this.LogInfo("Canceling existing order due to price change");
                CancelOrder(_currentBuyOrder);
                _currentBuyOrder = null;
            }
        }

        if (_currentBuyOrder == null && Position == 0)
        {
            // No existing order, create new one
            this.LogInfo("BUY LIMIT at {0:F2} SL:{1:F2} TP:{2:F2}", price, sl, tp);
            _currentBuyOrder = BuyLimit(price, 1m);
        }
    }

    private (decimal price, decimal sl, decimal tp)? TryGetBuyOrder()
    {
        if (_dzz == null || _jma == null || _config == null)
            return null;

        if (!_dzz.IsFormed || !_jma.IsFormed)
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

        // JMA trend filtering
        if (_config.JmaUsage != 0 && _jmaHistory.Count >= 2)
        {
            var jma1 = _jmaHistory[^1].GetValue<decimal>();
            var jma2 = _jmaHistory[^2].GetValue<decimal>();

            bool trendOk = _config.JmaUsage switch
            {
                1 => jma1 >= jma2,   // Bullish: JMA rising
                -1 => jma1 <= jma2,  // Bearish: JMA falling
                _ => true
            };

            if (!trendOk)
                return null;
        }

        // Check pattern: sl < l1 < price and price > sl
        if (price > sl && sl < l1 && l1 < price)
        {
            var tp = price + Math.Abs(price - sl);
            return (price, sl, tp);
        }

        return null;
    }

    protected override void OnOwnTradeReceived(MyTrade trade)
    {
        base.OnOwnTradeReceived(trade);

        var order = trade.Order;

        // Check if this was our buy order
        if (order == _currentBuyOrder)
        {
            this.LogInfo("Buy order filled at {0:F2}, Position: {1}", trade.Trade.Price, Position);
            _currentBuyOrder = null;

            // Create protective orders based on the signal
            var signal = TryGetBuyOrder();
            if (signal != null)
            {
                var (_, sl, tp) = signal.Value;

                // Create stop-loss order
                _currentStopLoss = SellLimit(sl, Math.Abs(Position));
                this.LogInfo("Stop-Loss order created at {0:F2}", sl);

                // Create take-profit order
                _currentTakeProfit = SellLimit(tp, Math.Abs(Position));
                this.LogInfo("Take-Profit order created at {0:F2}", tp);
            }
        }
        // Check if stop-loss was filled
        else if (order == _currentStopLoss)
        {
            this.LogInfo("Stop-Loss filled at {0:F2}, Position: {1}", trade.Trade.Price, Position);
            _currentStopLoss = null;

            // Cancel the take-profit order
            if (_currentTakeProfit != null && _currentTakeProfit.State == OrderStates.Active)
            {
                this.LogInfo("Canceling Take-Profit order");
                CancelOrder(_currentTakeProfit);
                _currentTakeProfit = null;
            }
        }
        // Check if take-profit was filled
        else if (order == _currentTakeProfit)
        {
            this.LogInfo("Take-Profit filled at {0:F2}, Position: {1}", trade.Trade.Price, Position);
            _currentTakeProfit = null;

            // Cancel the stop-loss order
            if (_currentStopLoss != null && _currentStopLoss.State == OrderStates.Active)
            {
                this.LogInfo("Canceling Stop-Loss order");
                CancelOrder(_currentStopLoss);
                _currentStopLoss = null;
            }
        }
    }
}

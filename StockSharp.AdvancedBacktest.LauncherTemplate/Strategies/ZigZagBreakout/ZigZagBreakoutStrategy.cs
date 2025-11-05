using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;

public class ZigZagBreakout : CustomStrategyBase
{
    private DeltaZigZag? _dzz;
    private ZigZagBreakoutConfig? _config;
    private Order? _currentBuyOrder;
    private readonly List<IIndicatorValue> _dzzHistory = [];
    private TimeSpan? _candleInterval;

    // Track current signal levels for protection and order management
    private decimal _currentStopLoss;
    private decimal _currentTakeProfit;
    private decimal _currentEntryPrice;

    protected override void OnReseted()
    {
        base.OnReseted();
        _currentBuyOrder = null;
        _currentStopLoss = 0;
        _currentTakeProfit = 0;
        _currentEntryPrice = 0;
        _dzzHistory.Clear();
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        _config = new ZigZagBreakoutConfig
        {
            DzzDepth = GetParam<decimal>("DzzDepth")
        };

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

        // Don't place new orders if we have a position
        if (Position != 0)
            return;

        var signal = TryGetBuyOrder();
        if (signal == null)
        {
            // No valid signal - cancel any pending entry order
            if (_currentBuyOrder != null && _currentBuyOrder.State == OrderStates.Active)
            {
                this.LogInfo("Canceling order - no valid signal");
                CancelOrder(_currentBuyOrder);
                _currentBuyOrder = null;
            }
            return;
        }

        var (price, sl, tp) = signal.Value;

        // Check if signal levels changed significantly
        bool levelsChanged =
            Math.Abs(_currentEntryPrice - price) > PriceStepHelper.GetPriceStep(Security) ||
            Math.Abs(_currentStopLoss - sl) > PriceStepHelper.GetPriceStep(Security) ||
            Math.Abs(_currentTakeProfit - tp) > PriceStepHelper.GetPriceStep(Security);

        // Cancel existing order if levels changed
        if (_currentBuyOrder != null && _currentBuyOrder.State == OrderStates.Active && levelsChanged)
        {
            this.LogInfo("Canceling existing order - ZigZag levels changed");
            CancelOrder(_currentBuyOrder);
            _currentBuyOrder = null;
        }

        // Place new order if no active order exists
        if (_currentBuyOrder == null)
        {
            // Update tracked levels
            _currentEntryPrice = price;
            _currentStopLoss = sl;
            _currentTakeProfit = tp;

            // Calculate position size based on risk
            var volume = CalculatePositionSize(price, sl);

            this.LogInfo("BUY LIMIT at {0:F2} SL:{1:F2} TP:{2:F2} Volume:{3}", price, sl, tp, volume);
            _currentBuyOrder = BuyLimit(price, volume);
        }
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

        var order = trade.Order;

        // Check if this was our entry order
        if (order == _currentBuyOrder)
        {
            this.LogInfo("Entry order filled at {0:F2}, Position: {1}", trade.Trade.Price, Position);
            _currentBuyOrder = null;

            // Activate native protection using tracked SL/TP levels
            if (_config?.UseNativeProtection == true && _currentStopLoss != 0 && _currentTakeProfit != 0)
            {
                this.LogInfo("Activating native protection - SL:{0:F2} TP:{1:F2}", _currentStopLoss, _currentTakeProfit);

                StartProtection(
                    takeProfit: new Unit(_currentTakeProfit, UnitTypes.Limit),
                    stopLoss: new Unit(_currentStopLoss, UnitTypes.Limit),
                    isStopTrailing: false,
                    useMarketOrders: false
                );
            }
        }
    }
}

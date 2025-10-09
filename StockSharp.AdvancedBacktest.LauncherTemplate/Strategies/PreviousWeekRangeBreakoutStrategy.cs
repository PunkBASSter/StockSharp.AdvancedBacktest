using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Strategies;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Strategies.Modules;
using StockSharp.AdvancedBacktest.Strategies.Modules.Factories;
using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;
using StockSharp.AdvancedBacktest.Strategies.Modules.StopLoss;
using StockSharp.AdvancedBacktest.Strategies.Modules.TakeProfit;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;

public class PreviousWeekRangeBreakoutStrategy : CustomStrategyBase
{
    private readonly StrategyOptions _options;
    private readonly IPositionSizer _positionSizer;
    private readonly IStopLossCalculator _stopLossCalculator;
    private readonly ITakeProfitCalculator _takeProfitCalculator;

    private decimal? _previousWeekHigh;
    private decimal? _previousWeekLow;
    private DateTimeOffset? _currentWeekStartTime;
    private decimal _weekHigh;
    private decimal _weekLow;
    private IIndicator? _trendFilter;
    private AverageTrueRange? _atr;
    private bool _hasBreakoutOccurred;

    public PreviousWeekRangeBreakoutStrategy(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));

        // Get options
        var optionsAccessor = serviceProvider.GetRequiredService<IOptions<StrategyOptions>>();
        _options = optionsAccessor.Value;

        // Get factories
        var positionSizerFactory = serviceProvider.GetRequiredService<PositionSizerFactory>();
        var stopLossFactory = serviceProvider.GetRequiredService<StopLossFactory>();
        var takeProfitFactory = serviceProvider.GetRequiredService<TakeProfitFactory>();

        // Resolve specific implementations based on options
        _positionSizer = positionSizerFactory.Create(_options.SizingMethod);
        _stopLossCalculator = stopLossFactory.Create(_options.StopLossMethodValue);
        _takeProfitCalculator = takeProfitFactory.Create(_options.TakeProfitMethodValue);
    }

    protected override void OnReseted()
    {
        base.OnReseted();

        _currentWeekStartTime = null;
        _previousWeekHigh = null;
        _previousWeekLow = null;
        _weekHigh = 0;
        _weekLow = 0;
        _hasBreakoutOccurred = false;
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        _trendFilter = _options.TrendFilterType == StockSharp.AdvancedBacktest.Strategies.Modules.IndicatorType.SMA
            ? new SimpleMovingAverage { Length = _options.TrendFilterPeriod }
            : new ExponentialMovingAverage { Length = _options.TrendFilterPeriod };

        _atr = new AverageTrueRange { Length = _options.ATRPeriod };

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
        this.LogInfo($"  Trend Filter ({_options.TrendFilterType}): {trendValue:F2}");
        this.LogInfo($"  Position: {Position}");
    }

    private decimal CalculatePositionSize(decimal price)
    {
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));

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

        var atrValue = GetCurrentATRValue();
        return _stopLossCalculator.Calculate(side, entryPrice, atrValue);
    }

    private decimal CalculateTakeProfit(Sides side, decimal entryPrice, decimal stopLoss)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        if (stopLoss <= 0)
            throw new ArgumentException("Stop-loss must be greater than zero", nameof(stopLoss));

        var atrValue = GetCurrentATRValue();
        return _takeProfitCalculator.Calculate(side, entryPrice, stopLoss, atrValue);
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

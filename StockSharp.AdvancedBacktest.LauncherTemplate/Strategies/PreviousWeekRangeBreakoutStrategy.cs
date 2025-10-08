using System;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Strategies;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;

public enum IndicatorType
{
    SMA,
    EMA
}

public enum StopLossMethod
{
    Percentage,
    ATR
}

public enum TakeProfitMethod
{
    Percentage,
    ATR,
    RiskReward
}

public enum PositionSizingMethod
{
    Fixed,
    PercentOfEquity,
    ATRBased
}

public class PreviousWeekRangeBreakoutStrategy : CustomStrategyBase
{
    private readonly StrategyParam<int> _trendFilterPeriod;
    private readonly StrategyParam<IndicatorType> _trendFilterType;
    private readonly StrategyParam<StopLossMethod> _stopLossMethod;
    private readonly StrategyParam<decimal> _stopLossPercentage;
    private readonly StrategyParam<decimal> _stopLossATRMultiplier;
    private readonly StrategyParam<int> _atrPeriod;
    private readonly StrategyParam<TakeProfitMethod> _takeProfitMethod;
    private readonly StrategyParam<decimal> _takeProfitPercentage;
    private readonly StrategyParam<decimal> _takeProfitATRMultiplier;
    private readonly StrategyParam<decimal> _riskRewardRatio;
    private readonly StrategyParam<PositionSizingMethod> _sizingMethod;
    private readonly StrategyParam<decimal> _fixedPositionSize;
    private readonly StrategyParam<decimal> _equityPercentage;

    public int TrendFilterPeriod
    {
        get => _trendFilterPeriod.Value;
        set => _trendFilterPeriod.Value = value;
    }

    public IndicatorType TrendFilterType
    {
        get => _trendFilterType.Value;
        set => _trendFilterType.Value = value;
    }

    public StopLossMethod StopLossMethodValue
    {
        get => _stopLossMethod.Value;
        set => _stopLossMethod.Value = value;
    }

    public decimal StopLossPercentage
    {
        get => _stopLossPercentage.Value;
        set => _stopLossPercentage.Value = value;
    }

    public decimal StopLossATRMultiplier
    {
        get => _stopLossATRMultiplier.Value;
        set => _stopLossATRMultiplier.Value = value;
    }

    public int ATRPeriod
    {
        get => _atrPeriod.Value;
        set => _atrPeriod.Value = value;
    }

    public TakeProfitMethod TakeProfitMethodValue
    {
        get => _takeProfitMethod.Value;
        set => _takeProfitMethod.Value = value;
    }

    public decimal TakeProfitPercentage
    {
        get => _takeProfitPercentage.Value;
        set => _takeProfitPercentage.Value = value;
    }

    public decimal TakeProfitATRMultiplier
    {
        get => _takeProfitATRMultiplier.Value;
        set => _takeProfitATRMultiplier.Value = value;
    }

    public decimal RiskRewardRatio
    {
        get => _riskRewardRatio.Value;
        set => _riskRewardRatio.Value = value;
    }

    public PositionSizingMethod SizingMethod
    {
        get => _sizingMethod.Value;
        set => _sizingMethod.Value = value;
    }

    public decimal FixedPositionSize
    {
        get => _fixedPositionSize.Value;
        set => _fixedPositionSize.Value = value;
    }

    public decimal EquityPercentage
    {
        get => _equityPercentage.Value;
        set => _equityPercentage.Value = value;
    }

    private decimal? _previousWeekHigh;
    private decimal? _previousWeekLow;
    private DateTimeOffset? _currentWeekStartTime;
    private decimal _weekHigh;
    private decimal _weekLow;
    private IIndicator? _trendFilter;
    private AverageTrueRange? _atr;
    private bool _hasBreakoutOccurred;

    public PreviousWeekRangeBreakoutStrategy()
    {
        _trendFilterPeriod = Param(nameof(TrendFilterPeriod), 50);
        _trendFilterType = Param(nameof(TrendFilterType), IndicatorType.SMA);
        _stopLossMethod = Param(nameof(StopLossMethodValue), Strategies.StopLossMethod.Percentage);
        _stopLossPercentage = Param(nameof(StopLossPercentage), 2.0m);
        _stopLossATRMultiplier = Param(nameof(StopLossATRMultiplier), 2.0m);
        _atrPeriod = Param(nameof(ATRPeriod), 14);
        _takeProfitMethod = Param(nameof(TakeProfitMethodValue), Strategies.TakeProfitMethod.Percentage);
        _takeProfitPercentage = Param(nameof(TakeProfitPercentage), 4.0m);
        _takeProfitATRMultiplier = Param(nameof(TakeProfitATRMultiplier), 3.0m);
        _riskRewardRatio = Param(nameof(RiskRewardRatio), 2.0m);
        _sizingMethod = Param(nameof(SizingMethod), PositionSizingMethod.Fixed);
        _fixedPositionSize = Param(nameof(FixedPositionSize), 0.01m);
        _equityPercentage = Param(nameof(EquityPercentage), 2.0m);
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

        _trendFilter = TrendFilterType == IndicatorType.SMA
            ? new SimpleMovingAverage { Length = TrendFilterPeriod }
            : new ExponentialMovingAverage { Length = TrendFilterPeriod };

        _atr = new AverageTrueRange { Length = ATRPeriod };

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
        this.LogInfo($"  Trend Filter ({TrendFilterType}): {trendValue:F2}");
        this.LogInfo($"  Position: {Position}");
    }

    private decimal CalculatePositionSize(decimal price)
    {
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));

        var positionSize = SizingMethod switch
        {
            PositionSizingMethod.Fixed => FixedPositionSize,
            
            PositionSizingMethod.PercentOfEquity => CalculatePercentOfEquitySize(price),
            
            PositionSizingMethod.ATRBased => CalculateATRBasedSize(price),
            
            _ => throw new InvalidOperationException($"Unknown position sizing method: {SizingMethod}")
        };

        const decimal minimumPositionSize = 0.01m;
        if (positionSize < minimumPositionSize)
            throw new InvalidOperationException(
                $"Calculated position size {positionSize} is below minimum {minimumPositionSize}");

        return positionSize;
    }

    private decimal CalculatePercentOfEquitySize(decimal price)
    {
        var equity = Portfolio?.CurrentValue ?? Portfolio?.BeginValue ?? 0;
        
        if (equity <= 0)
            throw new InvalidOperationException("Portfolio equity must be greater than zero for PercentOfEquity sizing");

        var riskAmount = equity * (EquityPercentage / 100m);
        return riskAmount / price;
    }

    private decimal CalculateATRBasedSize(decimal price)
    {
        var atrValue = GetCurrentATRValue();
        var equity = Portfolio?.CurrentValue ?? Portfolio?.BeginValue ?? 0;
        
        if (equity <= 0)
            throw new InvalidOperationException("Portfolio equity must be greater than zero for ATRBased sizing");

        var riskAmount = equity * (EquityPercentage / 100m);
        var riskPerShare = atrValue * StopLossATRMultiplier;
        
        if (riskPerShare <= 0)
            throw new InvalidOperationException("Risk per share must be greater than zero");

        return riskAmount / riskPerShare;
    }

    private decimal CalculateStopLoss(Sides side, decimal entryPrice)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        var stopLoss = StopLossMethodValue switch
        {
            StopLossMethod.Percentage => CalculatePercentageStopLoss(side, entryPrice),
            
            StopLossMethod.ATR => CalculateATRStopLoss(side, entryPrice),
            
            _ => throw new InvalidOperationException($"Unknown stop-loss method: {StopLossMethodValue}")
        };

        ValidateStopLoss(side, entryPrice, stopLoss);
        return stopLoss;
    }

    private decimal CalculatePercentageStopLoss(Sides side, decimal entryPrice)
    {
        return side == Sides.Buy
            ? entryPrice * (1 - StopLossPercentage / 100m)
            : entryPrice * (1 + StopLossPercentage / 100m);
    }

    private decimal CalculateATRStopLoss(Sides side, decimal entryPrice)
    {
        var atrValue = GetCurrentATRValue();
        
        return side == Sides.Buy
            ? entryPrice - (atrValue * StopLossATRMultiplier)
            : entryPrice + (atrValue * StopLossATRMultiplier);
    }

    private decimal CalculateTakeProfit(Sides side, decimal entryPrice, decimal stopLoss)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        if (stopLoss <= 0)
            throw new ArgumentException("Stop-loss must be greater than zero", nameof(stopLoss));

        var takeProfit = TakeProfitMethodValue switch
        {
            TakeProfitMethod.Percentage => CalculatePercentageTakeProfit(side, entryPrice),
            
            TakeProfitMethod.ATR => CalculateATRTakeProfit(side, entryPrice),
            
            TakeProfitMethod.RiskReward => CalculateRiskRewardTakeProfit(side, entryPrice, stopLoss),
            
            _ => throw new InvalidOperationException($"Unknown take-profit method: {TakeProfitMethodValue}")
        };

        ValidateTakeProfit(side, entryPrice, takeProfit);
        return takeProfit;
    }

    private decimal CalculatePercentageTakeProfit(Sides side, decimal entryPrice)
    {
        return side == Sides.Buy
            ? entryPrice * (1 + TakeProfitPercentage / 100m)
            : entryPrice * (1 - TakeProfitPercentage / 100m);
    }

    private decimal CalculateATRTakeProfit(Sides side, decimal entryPrice)
    {
        var atrValue = GetCurrentATRValue();
        
        return side == Sides.Buy
            ? entryPrice + (atrValue * TakeProfitATRMultiplier)
            : entryPrice - (atrValue * TakeProfitATRMultiplier);
    }

    private decimal CalculateRiskRewardTakeProfit(Sides side, decimal entryPrice, decimal stopLoss)
    {
        var risk = Math.Abs(entryPrice - stopLoss);
        
        return side == Sides.Buy
            ? entryPrice + (risk * RiskRewardRatio)
            : entryPrice - (risk * RiskRewardRatio);
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

    private void ValidateStopLoss(Sides side, decimal entryPrice, decimal stopLoss)
    {
        if (stopLoss <= 0)
            throw new InvalidOperationException("Stop-loss must be greater than zero");

        if (side == Sides.Buy && stopLoss >= entryPrice)
            throw new InvalidOperationException(
                $"For long position, stop-loss ({stopLoss}) must be below entry price ({entryPrice})");

        if (side == Sides.Sell && stopLoss <= entryPrice)
            throw new InvalidOperationException(
                $"For short position, stop-loss ({stopLoss}) must be above entry price ({entryPrice})");
    }

    private void ValidateTakeProfit(Sides side, decimal entryPrice, decimal takeProfit)
    {
        if (takeProfit <= 0)
            throw new InvalidOperationException("Take-profit must be greater than zero");

        if (side == Sides.Buy && takeProfit <= entryPrice)
            throw new InvalidOperationException(
                $"For long position, take-profit ({takeProfit}) must be above entry price ({entryPrice})");

        if (side == Sides.Sell && takeProfit >= entryPrice)
            throw new InvalidOperationException(
                $"For short position, take-profit ({takeProfit}) must be below entry price ({entryPrice})");
    }
}

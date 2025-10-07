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
}

using System;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Indicators;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.StrategyLauncher;
using StockSharp.StrategyLauncher.CustomStrategy;

namespace StockSharp.Samples.MaCrossoverBacktester.CustomStrategy;

public class MaCrossoverStrategy : CustomStrategyBase
{
	// Define strategy parameters for fast and slow MA periods:
	private readonly StrategyParam<int> _fastPeriod;
	private readonly StrategyParam<int> _slowPeriod;
	private readonly StrategyParam<DataType> _candleType;
	private readonly StrategyParam<TimeSpan?> _candleTimeFrame;
	private readonly StrategyParam<Unit> _stopLoss;
	private readonly StrategyParam<Unit> _takeProfit;

	public int FastPeriod
	{
		get => _fastPeriod.Value;
		set => _fastPeriod.Value = value;
	}

	public int SlowPeriod
	{
		get => _slowPeriod.Value;
		set => _slowPeriod.Value = value;
	}

	public DataType CandleType
	{
		get => _candleType.Value;
		set => _candleType.Value = value;
	}

	public TimeSpan? CandleTimeFrame
	{
		get => _candleTimeFrame.Value;
		set => _candleTimeFrame.Value = value;
	}

	public Unit StopLoss
	{
		get => _stopLoss.Value;
		set => _stopLoss.Value = value;
	}

	public Unit TakeProfit
	{
		get => _takeProfit.Value;
		set => _takeProfit.Value = value;
	}

	// Moving average indicators:
	private SimpleMovingAverage _fastMa;
	private SimpleMovingAverage _slowMa;
	private bool? _isShortLessThanLong;

	public MaCrossoverStrategy()
	{
		// Initialize strategy parameters with default values:
		_fastPeriod = Param(nameof(FastPeriod), 50);
		_slowPeriod = Param(nameof(SlowPeriod), 200);
		_candleType = Param(nameof(CandleType), TimeSpan.FromHours(1).TimeFrame());
		_candleTimeFrame = Param<TimeSpan?>(nameof(CandleTimeFrame));
		_stopLoss = Param(nameof(StopLoss), new Unit(2, UnitTypes.Percent));
		_takeProfit = Param(nameof(TakeProfit), new Unit(4, UnitTypes.Percent));
	}

	protected override void OnReseted()
	{
		base.OnReseted();
		_isShortLessThanLong = null;
	}

	protected override void OnStarted(DateTimeOffset time)
	{
		base.OnStarted(time);

		// Initialize moving average indicators with the selected periods:
		_fastMa = new SimpleMovingAverage { Length = FastPeriod };
		_slowMa = new SimpleMovingAverage { Length = SlowPeriod };

		// Create subscription for candles
		var dt = CandleTimeFrame is null
			? CandleType
			: DataType.Create(CandleType.MessageType, CandleTimeFrame);

		var subscription = new Subscription(dt, Security)
		{
			MarketData =
			{
				IsFinishedOnly = true,
				BuildMode = MarketDataBuildModes.LoadAndBuild,
			}
		};

		// Subscribe to candles and bind indicators
		SubscribeCandles(subscription)
			.Bind(_fastMa, _slowMa, OnProcess)
			.Start();

		// Start position protection with stop loss and take profit
		StartProtection(TakeProfit, StopLoss);
	}

	private void OnProcess(ICandleMessage candle, decimal fastValue, decimal slowValue)
	{
		// Only process finished candles
		if (candle.State != CandleStates.Finished)
			return;

		//Console.WriteLine($"{candle.OpenTime:yy.MM.dd HH:mm:ss.fff} candle O:{candle.OpenPrice} H:{candle.HighPrice} L:{candle.LowPrice} C:{candle.ClosePrice} V:{candle.TotalVolume}");
		//Console.WriteLine($"Fast MA: {fastValue}, Slow MA: {slowValue}");

		// Calculate if short MA is less than long MA
		var isShortLessThanLong = fastValue < slowValue;

		if (_isShortLessThanLong == null)
		{
			_isShortLessThanLong = isShortLessThanLong;
		}
		else if (_isShortLessThanLong != isShortLessThanLong) // Crossover occurred
		{
			// Determine trade direction: if fast > slow, buy; if fast < slow, sell
			var direction = isShortLessThanLong ? Sides.Sell : Sides.Buy;            // Calculate volume for position sizing or reversal
			var volume = Position == 0 ? Volume : Math.Min(Math.Abs(Position), Volume) * 2;

			// Use candle close price for order
			var price = candle.ClosePrice;

			if (direction == Sides.Buy)
				BuyLimit(price, volume);
			else
				SellLimit(price, volume);

			// Update crossover state
			_isShortLessThanLong = isShortLessThanLong;
		}
	}
}

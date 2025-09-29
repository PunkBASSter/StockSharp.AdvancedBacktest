using System;
using System.Collections.Generic;
using System.Linq;
using Ecng.Common;
using StockSharp.Algo.Indicators;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.StrategyLauncher.Common;
using StockSharp.StrategyLauncher.CustomStrategy;

namespace StockSharp.Samples.MaCrossoverBacktester.CustomStrategy;

public class MultiSecurityMaCrossoverStrategy : CustomStrategyBase
{
	private readonly Dictionary<Security, SMA> _fastSmas = new(new SecurityIdComparer());
	private readonly Dictionary<Security, SMA> _slowSmas = new(new SecurityIdComparer());
	private readonly Dictionary<Security, bool?> _isShortLessThanLong = new(new SecurityIdComparer());

	// Strategy parameters
	public Unit StopLoss { get; set; }
	public Unit TakeProfit { get; set; }

	protected override void OnStarted(DateTimeOffset time)
	{
		base.OnStarted(time);

		foreach (var security in Securities.Keys)
		{
			_fastSmas[security] = new SMA { Length = (int)CustomParams["FastPeriod"].Value };
			_slowSmas[security] = new SMA { Length = (int)CustomParams["SlowPeriod"].Value };

			var dt = DataType.Create(DataType.CandleTimeFrame.MessageType, Securities[security].Min());

			var subscription = new Subscription(dt, security)
			{
				MarketData =
				{
					IsFinishedOnly = true,
					BuildMode = MarketDataBuildModes.LoadAndBuild,
				}
			};

			SubscribeCandles(subscription)
				.Bind(_fastSmas[security], _slowSmas[security], ProcessCandle)
				.Start();

			StartProtection(TakeProfit, StopLoss);
		}
	}

	private void ProcessCandle(ICandleMessage candle, decimal fastValue, decimal slowValue)
	{
		if (candle.State != CandleStates.Finished)
			return;

		var secStrId = candle.SecurityId.ToStringId();
		//Console.WriteLine($"{candle.OpenTime:yy.MM.dd HH:mm:ss.fff} candle O:{candle.OpenPrice} H:{candle.HighPrice} L:{candle.LowPrice} C:{candle.ClosePrice} V:{candle.TotalVolume}");
		Console.WriteLine($"{secStrId} Fast MA: {fastValue}, Slow MA: {slowValue}");


		//TODO CONCURRENCY: If multiple securities are processed concurrently on candle messages processing.
		Security = new Security { Id = secStrId };

		var isShortLessThanLong = fastValue < slowValue;

		if (!_isShortLessThanLong.ContainsKey(Security))
		{
			_isShortLessThanLong[Security] = isShortLessThanLong;
		}
		else if (_isShortLessThanLong[Security] != isShortLessThanLong) // Crossover occurred
		{
			var direction = isShortLessThanLong ? Sides.Sell : Sides.Buy;            // Calculate volume for position sizing or reversal
			var volume = Position == 0 ? Volume : Math.Min(Math.Abs(Position), Volume) * 2;

			var price = candle.ClosePrice;

			if (direction == Sides.Buy)
				BuyLimit(price, volume, Security);
			else
				SellLimit(price, volume, Security);

			_isShortLessThanLong[Security] = isShortLessThanLong;
		}
	}
}
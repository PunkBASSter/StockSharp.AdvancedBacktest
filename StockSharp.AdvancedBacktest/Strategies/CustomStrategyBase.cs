using Ecng.Collections;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StockSharp.AdvancedBacktest.Strategies;

public abstract class CustomStrategyBase : Strategy
{
	public string Hash => $"{GetType().Name}V{Version}_{SecuritiesHash}_{ParamsHash}";
	public PerformanceMetrics? PerformanceMetrics { get; protected set; }
	public DateTimeOffset MetricWindowStart { get; set; }
	public DateTimeOffset MetricWindowEnd { get; set; }

	public virtual string Version { get; set; } = "1.0.0"; //TODO integrate with Git versioning
	public virtual string ParamsHash
	{
		get
		{
			var hash = string.Join(";", CustomParams.Select(p => $"{p.Key}={p.Value}"));
			return hash.GetHashCode().ToString("X");
		}
	}

	public virtual string SecuritiesHash
	{
		get
		{
			var hash = string.Join(";", Securities.Select(s => $"{s.Key.Id}={string.Join(",", s.Value)}"));
			return hash.GetHashCode().ToString("X");
		}
	}

	public virtual Dictionary<Security, IEnumerable<TimeSpan>> Securities { get; set; } = new(new SecurityIdComparer());

	public CachedSynchronizedDictionary<string, ICustomParam> CustomParams { get; set; } = [];

	public static T Create<T>(List<ICustomParam> paramSet) where T : CustomStrategyBase, new()
	{
		var strategy = new T();

		var secparams = paramSet.Where(p => p is SecurityParam)
			.Cast<SecurityParam>()
			.ToDictionary(sp => sp.Value.Key, sp => sp.Value.AsEnumerable());
		strategy.Securities.AddRange(secparams);

		var nonsecparams = paramSet.Where(p => p is not SecurityParam)
			.ToDictionary(p => p.Id, p => p);
		strategy.CustomParams.AddRange(nonsecparams);

		//TODO subscribe to multiple securities
		//foreach (var security in strategy.Securities.Keys)
		//{
		//	var subscription = new Subscription(timeFrame.TimeFrame(), security);
		//		SetCandleSubscriptionDetails(subscription);
		//		DataConnector.Subscribe(subscription);
		//}
		return strategy;
	}

	//TODO hanle more elegantly, not it serves as a temp param storage
	public List<ICustomParam> ParamsBackup { get; set; } = [];

	protected override void OnStopping()
	{
		PerformanceMetrics = MetricsCalculator.CalculateMetrics(this, MetricWindowStart, MetricWindowEnd);
		base.OnStopping();
	}
}

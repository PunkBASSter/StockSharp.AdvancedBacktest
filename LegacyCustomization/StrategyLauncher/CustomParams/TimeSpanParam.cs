using System;
using System.Collections.Generic;

namespace StockSharp.StrategyLauncher.CustomParams;

public class TimeSpanParam : CustomParam<TimeSpan>
{
	public TimeSpanParam(string id, TimeSpan defaultValue, TimeSpan optimizeFrom = default, TimeSpan optimizeTo = default, TimeSpan optimizeStep = default)
		: base(id, defaultValue, optimizeFrom, optimizeTo, optimizeStep)
	{
	}

	public override IEnumerable<TimeSpan> OptimizationRange
	{
		get
		{
			for (var value = (TimeSpan)OptimizeFrom; value <= (TimeSpan)OptimizeTo; value += (TimeSpan)OptimizeStep)
			{
				yield return value;
			}
		}
	}

	public override IEnumerable<ICustomParam> OptimizationRangeParams
	{
		get
		{
			for (var value = (TimeSpan)OptimizeFrom; value <= (TimeSpan)OptimizeTo; value += (TimeSpan)OptimizeStep)
			{
				yield return new TimeSpanParam(Id, value);
			}
		}
	}
}
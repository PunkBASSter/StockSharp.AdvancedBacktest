using System;
using System.Collections.Generic;
using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Parameters;

public abstract class CustomParam<T> : StrategyParam<T>, ICustomParam
{
	protected CustomParam(string id, T defaultValue, T optimizeFrom = default, T optimizeTo = default, T optimizeStep = default)
		: base(id, defaultValue)
	{
		CanOptimize = true;
		OptimizeFrom = optimizeFrom;
		OptimizeTo = optimizeTo;
		OptimizeStep = optimizeStep;
	}

	public abstract IEnumerable<T> OptimizationRange { get; }
	public abstract IEnumerable<ICustomParam> OptimizationRangeParams { get; }

	public Type ParamType => typeof(T);
}

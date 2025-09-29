using System;
using System.Collections.Generic;
using StockSharp.Algo.Strategies;

namespace StockSharp.StrategyLauncher.CustomParams;

public interface ICustomParam : IStrategyParam
{
	IEnumerable<ICustomParam> OptimizationRangeParams { get; }
	Type ParamType { get; }
}

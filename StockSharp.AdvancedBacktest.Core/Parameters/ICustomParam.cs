using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Parameters;

public interface ICustomParam : IStrategyParam
{
	IEnumerable<ICustomParam> OptimizationRangeParams { get; }
	Type ParamType { get; }
}

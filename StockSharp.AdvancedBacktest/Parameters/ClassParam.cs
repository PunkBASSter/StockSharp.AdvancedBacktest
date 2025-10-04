using System.Collections.Generic;
using System.Linq;

namespace StockSharp.AdvancedBacktest.Parameters;

public class ClassParam<T> : CustomParam<T>
	where T : class
{
	protected IList<T> Values { get; }

	public override IEnumerable<T> OptimizationRange => Values;

	public override IEnumerable<ICustomParam> OptimizationRangeParams => Values.Select(value => new ClassParam<T>(Id, [value]));

	public ClassParam(string id, IList<T> values)
		: base(id, values.FirstOrDefault())
	{
		Values = values;
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Parameters;

public class SecurityTimeframes : IGrouping<Security, TimeSpan>
{
	public SecurityTimeframes(Security security, IEnumerable<TimeSpan> timeFrames)
	{
		Security = security;
		TimeFrames = timeFrames;
	}

	public Security Security { get; set; }
	public IEnumerable<TimeSpan> TimeFrames { get; set; }

	public Security Key => Security;

	public IEnumerator<TimeSpan> GetEnumerator()
	{
		return TimeFrames.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

public class SecurityParam : ClassParam<SecurityTimeframes>
{
	public SecurityParam(string id, IList<SecurityTimeframes> values)
		: base(id, values)
	{
	}

	public override IEnumerable<ICustomParam> OptimizationRangeParams
	{
		get
		{
			return Values.Select(value => new SecurityParam(Id, [value]));
		}
	}
}

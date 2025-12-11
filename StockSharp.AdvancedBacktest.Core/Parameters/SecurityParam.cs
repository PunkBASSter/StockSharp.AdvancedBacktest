using System.Collections;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Parameters;

public class SecurityTimeframes(Security security, IEnumerable<TimeSpan> timeFrames) : IGrouping<Security, TimeSpan>
{
    public Security Security { get; set; } = security;
    public IEnumerable<TimeSpan> TimeFrames { get; set; } = timeFrames;

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

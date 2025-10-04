using System;
using System.Collections.Generic;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Utilities;

public class SecurityIdComparer : IEqualityComparer<Security>
{
	public bool Equals(Security x, Security y)
	{
		if (ReferenceEquals(x, y))
			return true;

		if (x is null || y is null)
			return false;

		if (x.IsAllSecurity() || y.IsAllSecurity())
			return x.IsAllSecurity() && y.IsAllSecurity();

		return string.Equals(x.Id, y.Id, StringComparison.InvariantCultureIgnoreCase);
	}

	public int GetHashCode(Security obj)
	{
		if (obj is null)
			return 0;

		if (obj.IsAllSecurity())
			return typeof(Security).GetHashCode();

		return obj.Id?.GetHashCode(StringComparison.InvariantCultureIgnoreCase) ?? 0;
	}
}

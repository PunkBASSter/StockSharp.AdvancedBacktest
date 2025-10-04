using System;
using System.Collections.Generic;

namespace StockSharp.AdvancedBacktest.Parameters;

public class CustomParamsContainer
{
	public List<ICustomParam> CustomParams { get; set; } = [];
	public List<Func<IDictionary<string, ICustomParam>, bool>> ValidationRules { get; set; } = new();
}

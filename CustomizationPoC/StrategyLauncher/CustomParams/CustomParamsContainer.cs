using System;
using System.Collections.Generic;
using StockSharp.StrategyLauncher.CustomParams;

namespace StockSharp.Samples.MaCrossoverBacktester.CustomParams;

public class CustomParamsContainer
{
	public List<ICustomParam> CustomParams { get; set; } = [];
	public List<Func<IDictionary<string, ICustomParam>, bool>> ValidationRules { get; set; } = new();
}

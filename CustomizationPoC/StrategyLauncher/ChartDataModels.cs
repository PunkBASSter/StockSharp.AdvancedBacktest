using System.Collections.Generic;

namespace StockSharp.Samples.MaCrossoverBacktester;

public class ChartDataModel
{
	public List<CandleDataPoint> Candles { get; set; } = [];
	public List<IndicatorDataSeries> Indicators { get; set; } = [];
	public List<TradeDataPoint> Trades { get; set; } = [];
}

public class CandleDataPoint
{
	public long Time { get; set; }
	public double Open { get; set; }
	public double High { get; set; }
	public double Low { get; set; }
	public double Close { get; set; }
	public double Volume { get; set; }
}

public class IndicatorDataSeries
{
	public string Name { get; set; } = string.Empty;
	public string Color { get; set; } = "#2196F3";
	public List<IndicatorDataPoint> Values { get; set; } = [];
}

public class IndicatorDataPoint
{
	public long Time { get; set; }
	public double Value { get; set; }
}

public class TradeDataPoint
{
	public long Time { get; set; }
	public double Price { get; set; }
	public double Volume { get; set; }
	public string Side { get; set; } = string.Empty;
	public double PnL { get; set; }
}

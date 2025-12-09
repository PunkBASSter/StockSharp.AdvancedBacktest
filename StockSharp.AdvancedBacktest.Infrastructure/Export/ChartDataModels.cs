namespace StockSharp.AdvancedBacktest.Export;

public class ChartDataModel
{
	public List<CandleDataPoint> Candles { get; set; } = [];
	public List<string> IndicatorFiles { get; set; } = [];
	public List<TradeDataPoint> Trades { get; set; } = [];
	public WalkForwardDataModel? WalkForward { get; set; }
}

public class CandleDataPoint
{
	public long Time { get; set; }
	public double Open { get; set; }
	public double High { get; set; }
	public double Low { get; set; }
	public double Close { get; set; }
	public double Volume { get; set; }

	public long? SequenceNumber { get; set; }
	public string? SecurityId { get; set; }
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

	public long? SequenceNumber { get; set; }
}

public class TradeDataPoint
{
	public long Time { get; set; }
	public double Price { get; set; }
	public double Volume { get; set; }
	public string Side { get; set; } = string.Empty;
	public double PnL { get; set; }

	public long? SequenceNumber { get; set; }
	public long? OrderId { get; set; }
}

public class StateDataPoint
{
	public long Time { get; set; }
	public double Position { get; set; }
	public double PnL { get; set; }
	public double UnrealizedPnL { get; set; }
	public string ProcessState { get; set; } = string.Empty;
	public long? SequenceNumber { get; set; }
}

public class WalkForwardDataModel
{
	public double WalkForwardEfficiency { get; set; }
	public double Consistency { get; set; }
	public int TotalWindows { get; set; }
	public List<WalkForwardWindowData> Windows { get; set; } = [];
}

public class WalkForwardWindowData
{
	public int WindowNumber { get; set; }
	public long TrainingStart { get; set; }
	public long TrainingEnd { get; set; }
	public long TestingStart { get; set; }
	public long TestingEnd { get; set; }
	public WalkForwardMetricsData TrainingMetrics { get; set; } = new();
	public WalkForwardMetricsData TestingMetrics { get; set; } = new();
	public double PerformanceDegradation { get; set; }
}

public class WalkForwardMetricsData
{
	public double TotalReturn { get; set; }
	public double SharpeRatio { get; set; }
	public double SortinoRatio { get; set; }
	public double MaxDrawdown { get; set; }
	public double WinRate { get; set; }
	public double ProfitFactor { get; set; }
	public int TotalTrades { get; set; }
}

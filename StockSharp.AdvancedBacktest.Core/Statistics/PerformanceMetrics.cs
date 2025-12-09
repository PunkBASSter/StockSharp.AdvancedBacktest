namespace StockSharp.AdvancedBacktest.Statistics;

public class PerformanceMetrics
{
	public DateTimeOffset StartTime { get; set; }
	public DateTimeOffset EndTime { get; set; }
	public int TotalTrades { get; set; }
	public int WinningTrades { get; set; }
	public int LosingTrades { get; set; }
	public double TotalReturn { get; set; }
	public double AnnualizedReturn { get; set; }
	public double SharpeRatio { get; set; }
	public double SortinoRatio { get; set; }
	public double MaxDrawdown { get; set; }
	public double WinRate { get; set; }
	public double ProfitFactor { get; set; }
	public double AverageWin { get; set; }
	public double AverageLoss { get; set; }
	public double GrossProfit { get; set; }
	public double GrossLoss { get; set; }
	public double NetProfit { get; set; }
	public double InitialCapital { get; set; }
	public double FinalValue { get; set; }
	public int TradingPeriodDays { get; set; }
	public double AverageTradesPerDay { get; set; }

	public override string ToString()
	{
		return $"Total Return: {TotalReturn:F2}%, Sharpe: {SharpeRatio:F2}, Max DD: {MaxDrawdown:F2}%, " +
			   $"Trades: {TotalTrades}, Win Rate: {WinRate:F1}%, PF: {ProfitFactor:F2}";
	}

	public string ToDetailedString()
	{
		return $@"
=== Performance Metrics ===
Trading Performance:
  Total Trades: {TotalTrades}
  Winning Trades: {WinningTrades}
  Losing Trades: {LosingTrades}
  Win Rate: {WinRate:F1}%

Returns:
  Total Return: {TotalReturn:F2}%
  Annualized Return: {AnnualizedReturn:F2}%
  Net Profit: ${NetProfit:F2}
  Gross Profit: ${GrossProfit:F2}
  Gross Loss: ${GrossLoss:F2}

Risk Metrics:
  Sharpe Ratio: {SharpeRatio:F2}
  Sortino Ratio: {SortinoRatio:F2}
  Maximum Drawdown: {MaxDrawdown:F2}%
  Profit Factor: {ProfitFactor:F2}

Trade Analysis:
  Average Win: ${AverageWin:F2}
  Average Loss: ${AverageLoss:F2}
  Average Trades/Day: {AverageTradesPerDay:F2}

Capital:
  Initial Capital: ${InitialCapital:F2}
  Final Value: ${FinalValue:F2}
  Trading Period: {TradingPeriodDays} days
";
	}
}

namespace StockSharp.AdvancedBacktest.Utilities;

public static class StatisticsCalculator
{
	public static double CalculateMaxDrawdown(IList<decimal> equityCurve)
	{
		if (equityCurve == null || equityCurve.Count == 0) return 0;

		var peak = 0m;
		var maxDrawdown = 0m;

		foreach (var value in equityCurve)
		{
			if (value > peak)
				peak = value;

			if (peak > 0)
			{
				var drawdown = (peak - value) / peak;
				if (drawdown > maxDrawdown)
					maxDrawdown = drawdown;
			}
		}

		return (double)(maxDrawdown * 100);
	}

	public static double CalculateSharpeRatio(IList<double> returns, double riskFreeRate = 0.02)
	{
		if (returns == null || returns.Count == 0) return 0;

		var averageReturn = returns.Average();
		var stdDev = Math.Sqrt(returns.Sum(r => Math.Pow(r - averageReturn, 2)) / returns.Count);

		var dailyRiskFreeRate = riskFreeRate / 365;
		return stdDev > 0 ? (averageReturn - dailyRiskFreeRate) / stdDev * Math.Sqrt(365) : 0;
	}

	public static double CalculateSortinoRatio(IList<double> returns, double riskFreeRate = 0.02)
	{
		if (returns == null || returns.Count == 0) return 0;

		var averageReturn = returns.Average();
		var negativeReturns = returns.Where(r => r < 0).ToList();

		if (negativeReturns.Count == 0) return double.PositiveInfinity;

		var downwardStdDev = Math.Sqrt(negativeReturns.Sum(r => Math.Pow(r, 2)) / negativeReturns.Count);
		var dailyRiskFreeRate = riskFreeRate / 365;

		return downwardStdDev > 0 ? (averageReturn - dailyRiskFreeRate) / downwardStdDev * Math.Sqrt(365) : 0;
	}

	public static double CalculateWinRate(int winningTrades, int totalTrades)
	{
		return totalTrades > 0 ? (double)winningTrades / totalTrades * 100 : 0;
	}

	public static double CalculateProfitFactor(double grossProfit, double grossLoss)
	{
		return grossLoss > 0 ? grossProfit / grossLoss : double.PositiveInfinity;
	}
}

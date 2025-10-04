using System;
using System.Collections.Generic;
using System.Linq;
using StockSharp.Algo.PnL;
using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Statistics;

public class MetricsCalculator
{
	public static double RiskFreeRate { get; set; } = 0.02; // 2% annual risk-free rate

	/// <summary>
	/// Calculate comprehensive performance metrics for a strategy
	/// </summary>
	public static PerformanceMetrics CalculateMetrics(Strategy strategy, DateTimeOffset startDate, DateTimeOffset endDate)
	{
		var trades = strategy.MyTrades.ToList();

		// Get PnL changes - note that PnLManager doesn't have PnLChanges property
		// We'll use MyTrades instead for calculations
		var pnlChanges = new List<PnLInfo>();
		if (trades.Count == 0)
		{
			return new PerformanceMetrics
			{
				TotalTrades = 0,
				TotalReturn = 0,
				AnnualizedReturn = 0,
				SharpeRatio = 0,
				MaxDrawdown = 0,
				WinRate = 0,
				ProfitFactor = 0,
				AverageWin = 0,
				AverageLoss = 0
			};
		}

		var totalPnL = trades.Sum(t => t.PnL ?? 0);
		var dbgPnL = strategy.PnLManager?.RealizedPnL + strategy.PnLManager?.UnrealizedPnL ?? 0;
		strategy.Portfolio.CurrentValue = strategy.Portfolio.BeginValue + totalPnL;
		var totalDays = (endDate - startDate).TotalDays;
		var initialCapital = strategy.Portfolio.BeginValue;
		var finalValue = strategy.Portfolio.CurrentValue;

		var totalReturn = (finalValue - initialCapital) / initialCapital * 100;
		var annualizedReturn = Math.Pow((double)(finalValue / initialCapital), 365.0 / totalDays) - 1;
		var winningTrades = trades.Where(t => t.PnL != null && t.PnL.Value > 0).ToList();
		var losingTrades = trades.Where(t => t.PnL != null && t.PnL.Value < 0).ToList();
		var winRate = trades.Count > 0 ? (double)winningTrades.Count / trades.Count * 100 : 0;
		var averageWin = winningTrades.Any() ? (double)winningTrades.Average(t => t.PnL!.Value) : 0;
		var averageLoss = losingTrades.Any() ? (double)losingTrades.Average(t => t.PnL!.Value) : 0;

		var grossProfit = winningTrades.Sum(t => t.PnL!.Value);
		var grossLoss = Math.Abs(losingTrades.Sum(t => t.PnL!.Value));
		var profitFactor = grossLoss > 0 ? (double)(grossProfit / grossLoss) : double.PositiveInfinity;
		decimal cumulativePnL = 0;
		foreach (var trade in trades.OrderBy(t => t.Trade.ServerTime))
		{
			if (trade.PnL != null)
			{
				cumulativePnL += trade.PnL.Value;
				pnlChanges.Add(new PnLInfo(trade.Trade.ServerTime, Math.Abs(trade.Trade.Volume), cumulativePnL));
			}
		}

		var maxDrawdown = CalculateMaxDrawdown(pnlChanges);
		var sharpeRatio = CalculateSharpeRatio(pnlChanges, totalDays);
		var sortinoRatio = CalculateSortinoRatio(pnlChanges, totalDays);

		return new PerformanceMetrics
		{
			TotalTrades = trades.Count,
			WinningTrades = winningTrades.Count,
			LosingTrades = losingTrades.Count,
			TotalReturn = (double)totalReturn,
			AnnualizedReturn = annualizedReturn * 100,
			SharpeRatio = sharpeRatio,
			SortinoRatio = sortinoRatio,
			MaxDrawdown = maxDrawdown,
			WinRate = winRate,
			ProfitFactor = profitFactor,
			AverageWin = averageWin,
			AverageLoss = averageLoss,
			GrossProfit = (double)grossProfit,
			GrossLoss = (double)grossLoss,
			NetProfit = (double)(grossProfit - grossLoss),
			InitialCapital = (double)initialCapital,
			FinalValue = (double)finalValue,
			TradingPeriodDays = (int)totalDays,
			AverageTradesPerDay = totalDays > 0 ? trades.Count / totalDays : 0,
			//FastMaPeriod = strategy is MaCrossoverStrategy maStrategy ? maStrategy.FastPeriod : 0,
			//SlowMaPeriod = strategy is MaCrossoverStrategy maStrategy2 ? maStrategy2.SlowPeriod : 0
		};
	}

	private static double CalculateMaxDrawdown(IList<PnLInfo> pnlChanges)
	{
		if (!pnlChanges.Any()) return 0;

		var peak = 0m;
		var maxDrawdown = 0m;

		foreach (var pnl in pnlChanges)
		{
			var currentValue = pnl.PnL;
			if (currentValue > peak)
				peak = currentValue;

			if (peak > 0)
			{
				var drawdown = (peak - currentValue) / peak;
				if (drawdown > maxDrawdown)
					maxDrawdown = drawdown;
			}
		}

		return (double)(maxDrawdown * 100);
	}

	private static double CalculateSharpeRatio(IList<PnLInfo> pnlChanges, double totalDays)
	{
		if (!pnlChanges.Any() || totalDays <= 0) return 0;

		var returns = new List<double>();
		decimal previousValue = 0;

		// Sort by time to ensure correct sequence
		var orderedPnLChanges = pnlChanges.OrderBy(p => p.ServerTime).ToList();

		foreach (var pnl in orderedPnLChanges)
		{
			if (previousValue > 0) // Avoid division by zero
			{
				var dailyReturn = (double)((pnl.PnL - previousValue) / previousValue);
				returns.Add(dailyReturn);
			}
			previousValue = pnl.PnL;
		}

		if (!returns.Any()) return 0;

		var averageReturn = returns.Average();
		var returnStdDev = Math.Sqrt(returns.Sum(r => Math.Pow(r - averageReturn, 2)) / returns.Count);

		// Assuming risk-free rate of 2% annually
		var riskFreeRate = 0.02 / 365; // Daily risk-free rate

		return returnStdDev > 0 ? (averageReturn - riskFreeRate) / returnStdDev * Math.Sqrt(365) : 0;
	}

	private static double CalculateSortinoRatio(IList<PnLInfo> pnlChanges, double totalDays)
	{
		if (!pnlChanges.Any() || totalDays <= 0) return 0;

		var returns = new List<double>();
		decimal previousValue = 0;

		// Sort by time to ensure correct sequence
		var orderedPnLChanges = pnlChanges.OrderBy(p => p.ServerTime).ToList();

		foreach (var pnl in orderedPnLChanges)
		{
			if (previousValue > 0) // Avoid division by zero
			{
				var dailyReturn = (double)((pnl.PnL - previousValue) / previousValue);
				returns.Add(dailyReturn);
			}
			previousValue = pnl.PnL;
		}

		if (!returns.Any()) return 0;

		var averageReturn = returns.Average();
		var negativeReturns = returns.Where(r => r < 0).ToList();

		if (!negativeReturns.Any()) return double.PositiveInfinity;

		var downwardStdDev = Math.Sqrt(negativeReturns.Sum(r => Math.Pow(r, 2)) / negativeReturns.Count);

		// Assuming risk-free rate of 2% annually
		RiskFreeRate = 0.02 / 365; // Approximate daily risk-free rate

		return downwardStdDev > 0 ? (averageReturn - RiskFreeRate) / downwardStdDev * Math.Sqrt(365) : 0;
	}
}

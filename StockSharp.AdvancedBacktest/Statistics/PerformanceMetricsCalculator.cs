using StockSharp.Algo.PnL;
using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Statistics;

public class PerformanceMetricsCalculator(double riskFreeRate = 0.02) : IPerformanceMetricsCalculator
{
    public double RiskFreeRate { get; set; } = riskFreeRate;

    public PerformanceMetrics CalculateMetrics(Strategy strategy, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        var trades = strategy.MyTrades
            .Where(t => t.Trade.ServerTime >= startDate && t.Trade.ServerTime <= endDate)
            .ToList();

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
        strategy.Portfolio.CurrentValue = strategy.Portfolio.BeginValue + totalPnL;

        var totalDays = (endDate - startDate).TotalDays;
        var initialCapital = strategy.Portfolio.BeginValue ?? 0;
        var finalValue = strategy.Portfolio.CurrentValue ?? 0;

        var totalReturn = initialCapital != 0 ? (finalValue - initialCapital) / initialCapital * 100 : 0;
        var annualizedReturn = initialCapital != 0 && totalDays > 0
            ? Math.Pow((double)(finalValue / initialCapital), 365.0 / totalDays) - 1
            : 0;

        var winningTrades = trades.Where(t => t.PnL != null && t.PnL.Value > 0).ToList();
        var losingTrades = trades.Where(t => t.PnL != null && t.PnL.Value < 0).ToList();

        var winRate = CalculateWinRate(winningTrades.Count, trades.Count);
        var averageWin = winningTrades.Count > 0 ? (double)winningTrades.Average(t => t.PnL!.Value) : 0;
        var averageLoss = losingTrades.Count > 0 ? (double)losingTrades.Average(t => t.PnL!.Value) : 0;

        var grossProfit = winningTrades.Sum(t => t.PnL!.Value);
        var grossLoss = Math.Abs(losingTrades.Sum(t => t.PnL!.Value));
        var profitFactor = CalculateProfitFactor((double)grossProfit, (double)grossLoss);

        var pnlChanges = new List<PnLInfo>();
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
            AverageTradesPerDay = totalDays > 0 ? trades.Count / totalDays : 0
        };
    }

    private static double CalculateMaxDrawdown(List<PnLInfo> pnlChanges)
    {
        if (pnlChanges.Count == 0)
            return 0;

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

    private double CalculateSharpeRatio(List<PnLInfo> pnlChanges, double totalDays)
    {
        if (pnlChanges.Count == 0 || totalDays <= 0)
            return 0;

        var returns = new List<double>();
        decimal previousValue = 0;

        var orderedPnLChanges = pnlChanges.OrderBy(p => p.ServerTime).ToList();

        foreach (var pnl in orderedPnLChanges)
        {
            if (previousValue > 0)
            {
                var dailyReturn = (double)((pnl.PnL - previousValue) / previousValue);
                returns.Add(dailyReturn);
            }
            previousValue = pnl.PnL;
        }

        if (returns.Count == 0)
            return 0;

        var averageReturn = returns.Average();
        var returnStdDev = Math.Sqrt(returns.Sum(r => Math.Pow(r - averageReturn, 2)) / returns.Count);

        var dailyRiskFreeRate = RiskFreeRate / 365;

        return returnStdDev > 0
            ? (averageReturn - dailyRiskFreeRate) / returnStdDev * Math.Sqrt(365)
            : 0;
    }

    private double CalculateSortinoRatio(List<PnLInfo> pnlChanges, double totalDays)
    {
        if (pnlChanges.Count == 0 || totalDays <= 0)
            return 0;

        var returns = new List<double>();
        decimal previousValue = 0;

        var orderedPnLChanges = pnlChanges.OrderBy(p => p.ServerTime).ToList();

        foreach (var pnl in orderedPnLChanges)
        {
            if (previousValue > 0)
            {
                var dailyReturn = (double)((pnl.PnL - previousValue) / previousValue);
                returns.Add(dailyReturn);
            }
            previousValue = pnl.PnL;
        }

        if (returns.Count == 0)
            return 0;

        var averageReturn = returns.Average();
        var negativeReturns = returns.Where(r => r < 0).ToList();

        if (negativeReturns.Count == 0)
            return double.PositiveInfinity;

        var downwardStdDev = Math.Sqrt(negativeReturns.Sum(r => Math.Pow(r, 2)) / negativeReturns.Count);
        var dailyRiskFreeRate = RiskFreeRate / 365;

        return downwardStdDev > 0
            ? (averageReturn - dailyRiskFreeRate) / downwardStdDev * Math.Sqrt(365)
            : 0;
    }

    private static double CalculateWinRate(int winningTrades, int totalTrades)
    {
        return totalTrades > 0 ? (double)winningTrades / totalTrades * 100 : 0;
    }

    private static double CalculateProfitFactor(double grossProfit, double grossLoss)
    {
        return grossLoss > 0 ? grossProfit / grossLoss : double.PositiveInfinity;
    }
}

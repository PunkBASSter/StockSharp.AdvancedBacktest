using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Statistics;

public class PerformanceMetricsCalculator(double riskFreeRate = 0.02) : IPerformanceMetricsCalculator
{
    public double RiskFreeRate { get; set; } = riskFreeRate;

    public PerformanceMetrics CalculateMetrics(Strategy strategy, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        // Single pass: filter by date and sort
        var trades = strategy.MyTrades
            .Where(t => t.Trade.ServerTime >= startDate && t.Trade.ServerTime <= endDate)
            .OrderBy(t => t.Trade.ServerTime)
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

        var totalDays = (endDate - startDate).TotalDays;
        var initialCapital = strategy.Portfolio.BeginValue ?? 0;

        // Single pass to compute all metrics
        var stats = ComputeTradeStatistics(trades, initialCapital);

        strategy.Portfolio.CurrentValue = initialCapital + stats.TotalPnL;
        var finalValue = strategy.Portfolio.CurrentValue ?? 0;

        var totalReturn = initialCapital != 0 ? (finalValue - initialCapital) / initialCapital * 100 : 0;
        var annualizedReturn = initialCapital != 0 && totalDays > 0
            ? Math.Pow((double)(finalValue / initialCapital), 365.0 / totalDays) - 1
            : 0;

        var winRate = stats.CompletedTradesCount > 0
            ? (double)stats.WinningTradesCount / stats.CompletedTradesCount * 100
            : 0;

        var profitFactor = stats.GrossLoss > 0
            ? stats.GrossProfit / stats.GrossLoss
            : double.PositiveInfinity;

        // Compute risk metrics from pre-computed returns
        var (sharpeRatio, sortinoRatio) = ComputeRiskMetrics(stats.Returns, totalDays);

        return new PerformanceMetrics
        {
            TotalTrades = trades.Count,
            WinningTrades = stats.WinningTradesCount,
            LosingTrades = stats.LosingTradesCount,
            TotalReturn = (double)totalReturn,
            AnnualizedReturn = annualizedReturn * 100,
            SharpeRatio = sharpeRatio,
            SortinoRatio = sortinoRatio,
            MaxDrawdown = stats.MaxDrawdown,
            WinRate = winRate,
            ProfitFactor = profitFactor,
            AverageWin = stats.AverageWin,
            AverageLoss = stats.AverageLoss,
            GrossProfit = stats.GrossProfit,
            GrossLoss = stats.GrossLoss,
            NetProfit = stats.GrossProfit - stats.GrossLoss,
            InitialCapital = (double)initialCapital,
            FinalValue = (double)finalValue,
            TradingPeriodDays = (int)totalDays,
            AverageTradesPerDay = totalDays > 0 ? trades.Count / totalDays : 0
        };
    }

    private static TradeStatistics ComputeTradeStatistics(List<StockSharp.BusinessEntities.MyTrade> trades, decimal initialCapital)
    {
        var stats = new TradeStatistics();

        decimal cumulativePnL = 0;
        decimal previousCumulativePnL = 0;
        decimal peakEquity = initialCapital;
        decimal maxDrawdown = 0;

        decimal winningSum = 0;
        decimal losingSum = 0;

        foreach (var trade in trades)
        {
            var pnl = trade.PnL;
            if (pnl == null) continue;

            var pnlValue = pnl.Value;
            stats.TotalPnL += pnlValue;
            cumulativePnL += pnlValue;

            // Classify trade
            if (pnlValue > 0)
            {
                stats.WinningTradesCount++;
                winningSum += pnlValue;
            }
            else if (pnlValue < 0)
            {
                stats.LosingTradesCount++;
                losingSum += pnlValue;
            }

            // Track max drawdown
            var currentEquity = initialCapital + cumulativePnL;
            if (currentEquity > peakEquity)
                peakEquity = currentEquity;

            if (peakEquity > 0)
            {
                var drawdown = (peakEquity - currentEquity) / peakEquity;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            // Compute return for Sharpe/Sortino (need at least 2 data points)
            if (previousCumulativePnL > 0)
            {
                var periodReturn = (double)((cumulativePnL - previousCumulativePnL) / previousCumulativePnL);
                stats.Returns.Add(periodReturn);
            }
            previousCumulativePnL = cumulativePnL;
        }

        stats.CompletedTradesCount = stats.WinningTradesCount + stats.LosingTradesCount;
        stats.GrossProfit = (double)winningSum;
        stats.GrossLoss = (double)Math.Abs(losingSum);
        stats.AverageWin = stats.WinningTradesCount > 0 ? (double)(winningSum / stats.WinningTradesCount) : 0;
        stats.AverageLoss = stats.LosingTradesCount > 0 ? (double)(losingSum / stats.LosingTradesCount) : 0;
        stats.MaxDrawdown = (double)(maxDrawdown * 100);

        return stats;
    }

    private (double sharpe, double sortino) ComputeRiskMetrics(List<double> returns, double totalDays)
    {
        if (returns.Count == 0 || totalDays <= 0)
            return (0, 0);

        // Compute mean and variance in single pass
        double sum = 0;
        double sumSq = 0;
        double negSumSq = 0;
        int negCount = 0;

        foreach (var r in returns)
        {
            sum += r;
            sumSq += r * r;
            if (r < 0)
            {
                negSumSq += r * r;
                negCount++;
            }
        }

        var n = returns.Count;
        var mean = sum / n;
        var variance = (sumSq / n) - (mean * mean);
        var stdDev = Math.Sqrt(Math.Max(0, variance));

        var dailyRiskFreeRate = RiskFreeRate / 365;

        var sharpe = stdDev > 0
            ? (mean - dailyRiskFreeRate) / stdDev * Math.Sqrt(365)
            : 0;

        var sortino = 0.0;
        if (negCount > 0)
        {
            var downwardStdDev = Math.Sqrt(negSumSq / negCount);
            sortino = downwardStdDev > 0
                ? (mean - dailyRiskFreeRate) / downwardStdDev * Math.Sqrt(365)
                : 0;
        }
        else
        {
            sortino = double.PositiveInfinity;
        }

        return (sharpe, sortino);
    }

    private class TradeStatistics
    {
        public decimal TotalPnL { get; set; }
        public int WinningTradesCount { get; set; }
        public int LosingTradesCount { get; set; }
        public int CompletedTradesCount { get; set; }
        public double GrossProfit { get; set; }
        public double GrossLoss { get; set; }
        public double AverageWin { get; set; }
        public double AverageLoss { get; set; }
        public double MaxDrawdown { get; set; }
        public List<double> Returns { get; } = new();
    }
}

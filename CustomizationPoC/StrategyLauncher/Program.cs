using System;
using System.Collections.Generic;
using StockSharp.Algo.Commissions;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.StrategyLauncher;
using StockSharp.StrategyLauncher.Config;
using StockSharp.Samples.MaCrossoverBacktester.CustomStrategy;
using StockSharp.StrategyLauncher.CustomParams;
using StockSharp.Samples.MaCrossoverBacktester.OptimizerFactory;
using StockSharp.Samples.MaCrossoverBacktester.CustomParams;

namespace StockSharp.Samples.MaCrossoverBacktester
{
	public class Program
	{
		static int Main(string[] args)
		{
			//var trainingPeriod = new TrainingPeriodConfig
			//{
			//	TrainingStartDate = new DateTime(2023, 1, 1),
			//	TrainingEndDate = new DateTime(2023, 6, 30),
			//	ValidationStartDate = new DateTime(2024, 7, 1),
			//	ValidationEndDate = new DateTime(2025, 7, 13)
			//};
			var trainingPeriod = new TrainingPeriodConfig
			{
				TrainingStartDate = new DateTime(2025, 3, 1),
				TrainingEndDate = new DateTime(2025, 5, 7),
				ValidationStartDate = new DateTime(2025, 5, 8),
				ValidationEndDate = new DateTime(2025, 7, 14)
			};
			var secs = new List<SecurityTimeframes>
			{
				new (new Security { Id = "BTCUSDT@BNB" }, [TimeSpan.FromHours(1)]),
				new (new Security { Id = "ETHUSDT@BNB" }, [TimeSpan.FromHours(1)]),
			};

			using var launcher = new OptimizationLauncher<MultiSecurityMaCrossoverStrategy>
				(trainingPeriod, new CustomOptimizer<MultiSecurityMaCrossoverStrategy>())
				.WithCommissionRules([new CommissionTradeRule { Value = 0.01m }])
				.WithPortfolio(new Portfolio { Name = "Test Portfolio", BeginValue = 10000 })
				.WithStrategyParams(
					new SecurityParam("Asset", secs),
					new NumberParam<int>("FastPeriod", 10, 10, 11, 2),
					new NumberParam<int>("SlowPeriod", 20, 40, 41, 2),
					//new TimeSpanParam("CandleTimeFrame", TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromHours(4), TimeSpan.FromHours(3)),
					new ClassParam<Unit>("StopLoss", [new Unit(0.02m, UnitTypes.Percent)]),
					new ClassParam<Unit>("TakeProfit", [new Unit(0.05m, UnitTypes.Percent)]))
				.WithParamValidation(p => (int)p["FastPeriod"].Value < (int)p["SlowPeriod"].Value)

				.WithMetricsFilter(metrics => metrics.TotalReturn > 5.0)    // Minimum 5% return
				.WithMetricsFilter(metrics => metrics.SharpeRatio > 0.5)    // Minimum Sharpe ratio
				.WithMetricsFilter(metrics => metrics.MaxDrawdown < 15.0)   // Maximum drawdown
				.WithMetricsFilter(metrics => metrics.WinRate > 40.0)       // Minimum win rate
				.WithMetricsFilter(metrics => metrics.ProfitFactor > 1.2)   // Minimum profit factor
																			//.WithResultsExport("results.csv")

				.WithOptimizationThreads(1)
				.Launch();

			Console.CancelKeyPress += (_, e) => { e.Cancel = true; launcher.CancellationTokenSrc.Cancel(); };
			Console.WriteLine("Press Ctrl-C to stop …");

			launcher.StrategyTask.GetAwaiter().GetResult();
			Console.WriteLine("Finished.");

			return 0;
		}
	}
}
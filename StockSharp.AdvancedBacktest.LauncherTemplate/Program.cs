using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.PreviousWeekRangeBreakout;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Strategies.Modules;

namespace StockSharp.AdvancedBacktest.LauncherTemplate;

// Entry point for the LauncherTemplate console application.
// Uses CustomParams pattern for configuration instead of Dependency Injection.
public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("StockSharp Advanced Backtest - Launcher Template");

        // Determine mode
        var mode = args.Length > 0 && args[0] == "--live" ? "live" : "optimization";
        Console.WriteLine($"Running in {mode} mode");

        if (mode == "live")
        {
            RunLiveMode();
        }
        else
        {
            RunOptimizationMode();
        }

        return 0;
    }

    private static void RunLiveMode()
    {
        Console.WriteLine("Live trading mode - creating strategy instance");

        // Build configuration using type-safe builder
        var config = new PreviousWeekRangeBreakoutConfigBuilder()
            .WithTrendFilter(IndicatorType.SMA, 20)
            .WithATRPeriod(14)
            .WithATRBasedPositionSizing(equityPercent: 2m, atrMultiplier: 2m)
            .WithATRStopLoss(2m)
            .WithRiskRewardTakeProfit(2m)
            .Build();

        // Create strategy instance using CustomStrategyBase factory method
        var strategy = CustomStrategyBase.Create<PreviousWeekRangeBreakoutStrategy>(config);

        // TODO: Configure connector, portfolio, security, etc.
        // TODO: Start strategy

        Console.WriteLine("Strategy created successfully");
        Console.WriteLine($"  Position Sizing: ATRBased");
        Console.WriteLine($"  Stop Loss Method: ATR");
        Console.WriteLine($"  Take Profit Method: RiskReward");
    }

    private static void RunOptimizationMode()
    {
        Console.WriteLine("Optimization mode - demonstrating parameter iteration");

        // Define parameter combinations to test
        var positionSizingMethods = new[]
        {
            PositionSizingMethod.Fixed,
            PositionSizingMethod.PercentOfEquity,
            PositionSizingMethod.ATRBased
        };

        var stopLossMethods = new[]
        {
            StopLossMethod.Percentage,
            StopLossMethod.ATR
        };

        int iteration = 0;

        foreach (var posMethod in positionSizingMethods)
        {
            foreach (var slMethod in stopLossMethods)
            {
                iteration++;
                Console.WriteLine($"\nIteration {iteration}:");
                Console.WriteLine($"  Position Sizing: {posMethod}");
                Console.WriteLine($"  Stop Loss: {slMethod}");

                // Build configuration for this iteration
                var configBuilder = new PreviousWeekRangeBreakoutConfigBuilder()
                    .WithTrendFilter(IndicatorType.SMA, 20)
                    .WithATRPeriod(14);

                // Configure position sizing based on method
                configBuilder = posMethod switch
                {
                    PositionSizingMethod.Fixed =>
                        configBuilder.WithFixedPositionSizing(1m),

                    PositionSizingMethod.PercentOfEquity =>
                        configBuilder.WithPercentEquityPositionSizing(2m),

                    PositionSizingMethod.ATRBased =>
                        configBuilder.WithATRBasedPositionSizing(equityPercent: 2m, atrMultiplier: 2m),

                    _ => throw new InvalidOperationException($"Unknown position sizing method: {posMethod}")
                };

                // Configure stop loss based on method
                configBuilder = slMethod switch
                {
                    StopLossMethod.Percentage =>
                        configBuilder.WithPercentageStopLoss(2m),

                    StopLossMethod.ATR =>
                        configBuilder.WithATRStopLoss(2m),

                    _ => throw new InvalidOperationException($"Unknown stop loss method: {slMethod}")
                };

                // Always use RiskReward take profit for this example
                var config = configBuilder
                    .WithRiskRewardTakeProfit(2m)
                    .Build();

                // Create strategy instance with current parameters
                var strategy = CustomStrategyBase.Create<PreviousWeekRangeBreakoutStrategy>(config);

                // TODO: Configure connector, portfolio, security for backtest
                // TODO: Run backtest with this strategy configuration
                // TODO: Collect metrics (Sharpe ratio, drawdown, etc.)

                Console.WriteLine($"  Strategy instance created for iteration {iteration}");
            }
        }

        Console.WriteLine("\nOptimization complete - would collect and rank results by metrics");
        Console.WriteLine("Next steps:");
        Console.WriteLine("  - Run each strategy instance through backtester");
        Console.WriteLine("  - Collect performance metrics");
        Console.WriteLine("  - Rank configurations by Sharpe ratio / drawdown");
        Console.WriteLine("  - Export results to JSON for web visualization");
    }
}

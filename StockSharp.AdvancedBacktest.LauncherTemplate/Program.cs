using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;
using StockSharp.AdvancedBacktest.Strategies.Modules;
using StockSharp.AdvancedBacktest.Strategies.Modules.Factories;

namespace StockSharp.AdvancedBacktest.LauncherTemplate;

/// <summary>
/// Entry point for the LauncherTemplate console application with Dependency Injection.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("StockSharp Advanced Backtest - Launcher Template with DI");

        // Build DI container
        var services = ConfigureServices();
        var serviceProvider = services.BuildServiceProvider();

        // Determine mode
        var mode = args.Length > 0 && args[0] == "--live" ? "live" : "optimization";
        Console.WriteLine($"Running in {mode} mode");

        if (mode == "live")
        {
            RunLiveMode(serviceProvider);
        }
        else
        {
            RunOptimizationMode(serviceProvider);
        }

        return 0;
    }

    private static ServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configure strategy options
        services.Configure<StrategyOptions>(options =>
        {
            // Trend Filter settings
            options.TrendFilterType = IndicatorType.SMA;
            options.TrendFilterPeriod = 20;

            // ATR settings
            options.ATRPeriod = 14;

            // Position sizing
            options.SizingMethod = PositionSizingMethod.ATRBased;
            options.FixedPositionSize = 1m;
            options.EquityPercentage = 2m;

            // Stop loss settings
            options.StopLossMethodValue = StopLossMethod.ATR;
            options.StopLossPercentage = 2m;
            options.StopLossATRMultiplier = 2m;

            // Take profit settings
            options.TakeProfitMethodValue = TakeProfitMethod.RiskReward;
            options.TakeProfitPercentage = 4m;
            options.TakeProfitATRMultiplier = 3m;
            options.RiskRewardRatio = 2m;
        });

        // Register factories (scoped to allow options updates per optimization iteration)
        services.AddScoped<PositionSizerFactory>();
        services.AddScoped<StopLossFactory>();
        services.AddScoped<TakeProfitFactory>();

        // Register strategy (scoped to create new instances per optimization iteration)
        services.AddScoped<PreviousWeekRangeBreakoutStrategy>();

        return services;
    }

    private static void RunLiveMode(ServiceProvider serviceProvider)
    {
        Console.WriteLine("Live trading mode - creating strategy instance");

        var strategy = serviceProvider.GetRequiredService<PreviousWeekRangeBreakoutStrategy>();

        // TODO: Configure connector, portfolio, security, etc.
        // TODO: Start strategy

        Console.WriteLine("Strategy created successfully");
        Console.WriteLine($"  Position Sizing: {serviceProvider.GetRequiredService<IOptions<StrategyOptions>>().Value.SizingMethod}");
        Console.WriteLine($"  Stop Loss Method: {serviceProvider.GetRequiredService<IOptions<StrategyOptions>>().Value.StopLossMethodValue}");
        Console.WriteLine($"  Take Profit Method: {serviceProvider.GetRequiredService<IOptions<StrategyOptions>>().Value.TakeProfitMethodValue}");
    }

    private static void RunOptimizationMode(ServiceProvider serviceProvider)
    {
        Console.WriteLine("Optimization mode - demonstrating parameter iteration");

        // Example: Iterate through different combinations
        var positionSizingMethods = new[] { PositionSizingMethod.Fixed, PositionSizingMethod.PercentOfEquity, PositionSizingMethod.ATRBased };
        var stopLossMethods = new[] { StopLossMethod.Percentage, StopLossMethod.ATR };

        int iteration = 0;
        foreach (var posMethod in positionSizingMethods)
        {
            foreach (var slMethod in stopLossMethods)
            {
                iteration++;
                Console.WriteLine($"\nIteration {iteration}:");
                Console.WriteLine($"  Position Sizing: {posMethod}");
                Console.WriteLine($"  Stop Loss: {slMethod}");

                // Create a new scope for this optimization iteration
                using var scope = serviceProvider.CreateScope();

                // Update options for this iteration
                var optionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<StrategyOptions>>();

                // Note: In a real implementation, you would use IOptionsSnapshot or a custom mechanism
                // to update options per scope. For this example, we're demonstrating the pattern.

                // Create strategy instance with current parameters
                var strategy = scope.ServiceProvider.GetRequiredService<PreviousWeekRangeBreakoutStrategy>();

                // TODO: Run backtest with this strategy configuration
                // TODO: Collect metrics (Sharpe ratio, drawdown, etc.)

                Console.WriteLine($"  Strategy instance created for iteration {iteration}");
            }
        }

        Console.WriteLine("\nOptimization complete - would collect and rank results by metrics");
    }
}

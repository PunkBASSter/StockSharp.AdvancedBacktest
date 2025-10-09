using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockSharp.AdvancedBacktest.Strategies.Modules;
using StockSharp.AdvancedBacktest.Strategies.Modules.Factories;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Optimization;

public class OptimizerRunner<TStrategy> where TStrategy : class
{
    private readonly IServiceProvider _rootServiceProvider;

    public OptimizerRunner(IServiceProvider rootServiceProvider)
    {
        _rootServiceProvider = rootServiceProvider ?? throw new ArgumentNullException(nameof(rootServiceProvider));
    }

    public void RunOptimization(IEnumerable<StrategyOptions> parameterCombinations)
    {
        int iteration = 0;

        foreach (var paramSet in parameterCombinations)
        {
            iteration++;
            Console.WriteLine($"\nOptimization Iteration {iteration}:");
            Console.WriteLine($"  Position Sizing: {paramSet.SizingMethod}");
            Console.WriteLine($"  Stop Loss: {paramSet.StopLossMethodValue}");
            Console.WriteLine($"  Take Profit: {paramSet.TakeProfitMethodValue}");

            // Create a new scope for this iteration
            using var scope = _rootServiceProvider.CreateScope();

            // Configure options for this specific iteration
            var services = new ServiceCollection();
            services.AddSingleton(Options.Create(paramSet));

            // Add factories and strategy with the scoped options
            services.AddScoped(sp => new PositionSizerFactory(sp.GetRequiredService<IOptions<StrategyOptions>>()));
            services.AddScoped(sp => new StopLossFactory(sp.GetRequiredService<IOptions<StrategyOptions>>()));
            services.AddScoped(sp => new TakeProfitFactory(sp.GetRequiredService<IOptions<StrategyOptions>>()));
            services.AddScoped<TStrategy>();

            var iterationProvider = services.BuildServiceProvider();

            try
            {
                // Resolve strategy with current parameters
                var strategy = iterationProvider.GetRequiredService<TStrategy>();

                // TODO: Run backtest with this strategy configuration
                // TODO: Calculate metrics (Sharpe ratio, drawdown, etc.)

                Console.WriteLine($"  Strategy instance created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error in iteration {iteration}: {ex.Message}");
            }
        }
    }

    public static IEnumerable<StrategyOptions> GenerateParameterCombinations()
    {
        var positionSizingMethods = new[] { PositionSizingMethod.Fixed, PositionSizingMethod.PercentOfEquity, PositionSizingMethod.ATRBased };
        var stopLossMethods = new[] { StopLossMethod.Percentage, StopLossMethod.ATR };
        var takeProfitMethods = new[] { TakeProfitMethod.Percentage, TakeProfitMethod.ATR, TakeProfitMethod.RiskReward };

        foreach (var posMethod in positionSizingMethods)
        {
            foreach (var slMethod in stopLossMethods)
            {
                foreach (var tpMethod in takeProfitMethods)
                {
                    yield return new StrategyOptions
                    {
                        // Trend Filter settings
                        TrendFilterType = IndicatorType.SMA,
                        TrendFilterPeriod = 20,

                        // ATR settings
                        ATRPeriod = 14,

                        // Position sizing
                        SizingMethod = posMethod,
                        FixedPositionSize = 1m,
                        EquityPercentage = 2m,

                        // Stop loss settings
                        StopLossMethodValue = slMethod,
                        StopLossPercentage = 2m,
                        StopLossATRMultiplier = 2m,

                        // Take profit settings
                        TakeProfitMethodValue = tpMethod,
                        TakeProfitPercentage = 4m,
                        TakeProfitATRMultiplier = 3m,
                        RiskRewardRatio = 2m
                    };
                }
            }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;
using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;

namespace StockSharp.AdvancedBacktest.LauncherTemplate;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Register global service collection (static?)
        var services = new ServiceCollection();
        //services.AddSingleton<ILogger, ConsoleLogger>();
        //services.AddSingleton<IConfigurationLoader, JsonConfigurationLoader>();
        //services.AddSingleton<IBacktestRunner, BacktestRunner>();
        var serviceProvider = services.BuildServiceProvider();

        return 0;
    }

    private static async Task<int> RunSingleMode(string configPath)
    {
        ConsoleLogger.LogInfo($"Loading single-run configuration from: {configPath}");

        if (!File.Exists(configPath))
        {
            ConsoleLogger.LogError($"Configuration file not found: {configPath}");
            return 1;
        }

        var configJson = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializationHelper.Deserialize<BacktestConfiguration>(configJson);

        if (config == null)
        {
            ConsoleLogger.LogError("Failed to parse configuration file");
            return 1;
        }

        if (config.RunMode != Configuration.Models.RunMode.Single)
        {
            ConsoleLogger.LogWarning($"Configuration RunMode is set to '{config.RunMode}', but --single-run flag was specified.");
            ConsoleLogger.LogInfo("Overriding RunMode to 'Single' to match CLI flag.");
            config.RunMode = Configuration.Models.RunMode.Single;
        }

        var runner = new BacktestRunner<PreviousWeekRangeBreakoutStrategy>(config)
        {
            VerboseLogging = true
        };

        return await runner.RunAsync();
    }

    private static string? FindDefaultConfigFile()
    {
        var candidates = new[]
        {
            "single-run-btcusdt.json",
            "config.json",
            "backtest.json",
            "ConfigFiles/config.json"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                ConsoleLogger.LogInfo($"Using configuration file: {candidate}");
                return candidate;
            }
        }

        return null;
    }
}

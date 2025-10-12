using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.PreviousWeekRangeBreakout;
using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Strategies.Modules;

namespace StockSharp.AdvancedBacktest.LauncherTemplate;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("StockSharp Advanced Backtest - Launcher Template");
        Console.WriteLine();

        try
        {
            if (args.Length == 0)
            {
                var configPath = FindDefaultConfigFile();
                if (configPath == null)
                {
                    ConsoleLogger.LogError("No configuration file found");
                    ConsoleLogger.LogError("Tried: config.json, backtest.json, ConfigFiles/config.json");
                    ConsoleLogger.LogInfo("Use --config <path> to specify a configuration file");
                    return 1;
                }

                return await RunBacktestMode(configPath);
            }

            var mode = args[0].ToLowerInvariant();

            return mode switch
            {
                "--live" => RunLiveMode(),
                "--validate-data" => await RunValidationMode(args),
                "--config" when args.Length > 1 => await RunBacktestMode(args[1]),
                "--single-run" when args.Length > 1 => await RunSingleMode(args[1]),
                "--help" or "-h" => ShowHelpAndExit(),
                _ => ShowHelpAndExit()
            };
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunBacktestMode(string configPath)
    {
        ConsoleLogger.LogInfo($"Loading configuration from: {configPath}");

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

        var runner = new BacktestRunner<PreviousWeekRangeBreakoutStrategy>(config)
        {
            VerboseLogging = true
        };

        return await runner.RunAsync();
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

    private static async Task<int> RunValidationMode(string[] args)
    {
        var configPath = args.Length > 1 && !args[1].StartsWith("--")
            ? args[1]
            : "ConfigFiles/test-backtest-btcusdt.json";

        ConsoleLogger.LogInfo($"Validating history data");
        ConsoleLogger.LogInfo($"Configuration: {configPath}");

        if (!File.Exists(configPath))
        {
            ConsoleLogger.LogError($"Configuration file not found: {configPath}");
            return 1;
        }

        var configJson = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializationHelper.Deserialize<BacktestConfiguration>(configJson);

        if (config == null)
        {
            ConsoleLogger.LogError("Failed to parse configuration");
            return 1;
        }

        var validator = new HistoryDataValidator(config.HistoryPath);
        var timeFrames = config.TimeFrames.Select(tf => ParseTimeFrame(tf)).ToList();
        var report = validator.Validate(config.Securities, timeFrames);

        report.PrintToConsole();

        return report.IsSuccess ? 0 : 1;
    }

    private static int RunLiveMode()
    {
        Console.WriteLine("Live trading mode - creating strategy instance");

        var config = new PreviousWeekRangeBreakoutConfigBuilder()
            .WithTrendFilter(IndicatorType.SMA, 20)
            .WithATRPeriod(14)
            .WithATRBasedPositionSizing(equityPercent: 2m, atrMultiplier: 2m)
            .WithATRStopLoss(2m)
            .WithRiskRewardTakeProfit(2m)
            .Build();

        var strategy = CustomStrategyBase.Create<PreviousWeekRangeBreakoutStrategy>(config);

        Console.WriteLine("Strategy created successfully");
        Console.WriteLine($"  Position Sizing: ATRBased");
        Console.WriteLine($"  Stop Loss Method: ATR");
        Console.WriteLine($"  Take Profit Method: RiskReward");
        Console.WriteLine();
        Console.WriteLine("NOTE: Live trading mode is not fully implemented yet.");
        Console.WriteLine("      This is a placeholder for future live trading integration.");

        return 0;
    }

    private static int ShowHelpAndExit()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run                                    Run backtest (searches for config file)");
        Console.WriteLine("  dotnet run --config <path>                    Run backtest with specified config");
        Console.WriteLine("  dotnet run --single-run <config>              Run single backtest (no optimization)");
        Console.WriteLine("  dotnet run --validate-data [config]           Validate history data availability");
        Console.WriteLine("  dotnet run --live                             Run live trading mode (not implemented)");
        Console.WriteLine("  dotnet run --help                             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Default Config Search:");
        Console.WriteLine("  When no --config argument is provided, the following files are searched in order:");
        Console.WriteLine("    1. config.json (current directory)");
        Console.WriteLine("    2. backtest.json (current directory)");
        Console.WriteLine("    3. ConfigFiles/config.json (project structure)");
        Console.WriteLine();
        Console.WriteLine("Testing:");
        Console.WriteLine("  dotnet test                                   Run all unit and integration tests");
        Console.WriteLine("  dotnet test --filter Category!=E2E            Run only fast tests (skip E2E)");
        Console.WriteLine("  dotnet test --filter Category=Integration     Run integration tests");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --config ConfigFiles/test-backtest-btcusdt.json");
        Console.WriteLine("  dotnet run --single-run ConfigFiles/single-run-btcusdt.json");
        Console.WriteLine("  dotnet run --validate-data");
        Console.WriteLine("  dotnet test");

        return 0;
    }

    private static TimeSpan ParseTimeFrame(string timeFrameStr)
    {
        if (string.IsNullOrWhiteSpace(timeFrameStr))
            throw new ArgumentException("Timeframe string cannot be empty");

        var timeFrameLower = timeFrameStr.ToLowerInvariant().Trim();

        if (timeFrameLower.Length < 2)
            throw new ArgumentException($"Invalid timeframe format: {timeFrameStr}");

        var unitChar = timeFrameLower[^1];
        var valueStr = timeFrameLower[..^1];

        if (!int.TryParse(valueStr, out var value) || value <= 0)
            throw new ArgumentException($"Invalid timeframe value: {timeFrameStr}");

        return unitChar switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            'w' => TimeSpan.FromDays(value * 7),
            _ => throw new ArgumentException($"Invalid timeframe unit '{unitChar}' in: {timeFrameStr}")
        };
    }

    private static string? FindDefaultConfigFile()
    {
        var candidates = new[]
        {
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

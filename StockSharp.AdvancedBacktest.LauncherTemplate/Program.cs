using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using StockSharp.AdvancedBacktest.LauncherTemplate.Launchers;

namespace StockSharp.AdvancedBacktest.LauncherTemplate;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var aiDebugOption = new Option<bool>(
            name: "--ai-debug",
            description: "Enable AI agentic debug mode (disables web app launcher)",
            getDefaultValue: () => false);

        var strategyOption = new Option<string>(
            name: "--strategy",
            description: "Strategy to run (ZigZagBreakout, DzzPeakTrough)",
            getDefaultValue: () => "ZigZagBreakout");

        var rootCommand = new RootCommand("Strategy Backtest Launcher");
        rootCommand.AddOption(aiDebugOption);
        rootCommand.AddOption(strategyOption);

        rootCommand.SetHandler(async (bool aiDebug, string strategy) =>
        {
            var services = ConfigureServices();
            var launcher = ResolveLauncher(services, strategy);

            if (launcher == null)
            {
                Console.WriteLine($"ERROR: Unknown strategy '{strategy}'");
                Console.WriteLine("Available strategies: ZigZagBreakout, DzzPeakTrough");
                Environment.ExitCode = 1;
                return;
            }

            Environment.ExitCode = await launcher.RunAsync(aiDebug);
        }, aiDebugOption, strategyOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register launchers
        services.AddSingleton<ZigZagBreakoutLauncher>();
        services.AddSingleton<DzzPeakTroughLauncher>();

        return services.BuildServiceProvider();
    }

    private static IStrategyLauncher? ResolveLauncher(IServiceProvider services, string strategyName)
    {
        return strategyName.ToLowerInvariant() switch
        {
            "zigzagbreakout" => services.GetRequiredService<ZigZagBreakoutLauncher>(),
            "dzzpeaktrough" => services.GetRequiredService<DzzPeakTroughLauncher>(),
            _ => null
        };
    }
}

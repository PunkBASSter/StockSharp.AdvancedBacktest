using StockSharp.AdvancedBacktest.Backtest;
using StockSharp.AdvancedBacktest.DebugMode;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Launchers;

/// <summary>
/// Configuration for a strategy backtest run.
/// </summary>
public class LauncherConfig
{
    public string HistoryPath { get; set; } = @".\History";
    public StorageFormats StorageFormat { get; set; } = StorageFormats.Binary;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public decimal InitialCapital { get; set; } = 10000m;
    public Security Security { get; set; } = null!;
    public string PortfolioName { get; set; } = "Backtest";
    public string WebAppRelativePath { get; set; } = @"..\..\..\..\StockSharp.AdvancedBacktest.Web";
}

/// <summary>
/// Base class for strategy launchers providing common backtest infrastructure.
/// </summary>
/// <typeparam name="TStrategy">The strategy type to launch (must inherit from CustomStrategyBase)</typeparam>
public abstract class StrategyLauncherBase<TStrategy> : IStrategyLauncher
    where TStrategy : CustomStrategyBase, new()
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// Gets the default launcher configuration. Override to customize.
    /// </summary>
    protected virtual LauncherConfig GetDefaultConfig()
    {
        var historyPath = Environment.GetEnvironmentVariable("StockSharp__HistoryPath") ?? @".\History";
        var storageFormatEnv = Environment.GetEnvironmentVariable("StockSharp__StorageFormat");
        var storageFormat = Enum.TryParse<StorageFormats>(storageFormatEnv, ignoreCase: true, out var parsed)
            ? parsed
            : StorageFormats.Binary;

        return new LauncherConfig
        {
            HistoryPath = historyPath,
            StorageFormat = storageFormat,
            StartDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2023, 12, 31, 23, 59, 59, TimeSpan.Zero),
            InitialCapital = 10000m,
            Security = CreateDefaultSecurity(),
            PortfolioName = Name
        };
    }

    /// <summary>
    /// Creates the default security. Override to customize.
    /// </summary>
    protected virtual Security CreateDefaultSecurity()
    {
        return new Security
        {
            Id = "BTCUSDT@BNB",
            Code = "BTCUSDT",
            Board = ExchangeBoard.Binance,
            PriceStep = 0.01m,
            Decimals = 2,
            VolumeStep = 0.001m,
            MinVolume = 0.001m,
            MaxVolume = 9000m
        };
    }

    /// <summary>
    /// Creates and configures the strategy instance.
    /// </summary>
    protected abstract TStrategy CreateStrategy(LauncherConfig config, Security security, Portfolio portfolio);

    /// <summary>
    /// Gets the strategy parameters for display and configuration.
    /// </summary>
    protected abstract IList<ICustomParam> GetParameters();

    /// <summary>
    /// Gets the auxiliary timeframe for the strategy.
    /// </summary>
    protected virtual TimeSpan GetAuxiliaryTimeframe() => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the primary timeframes for the security.
    /// </summary>
    protected virtual TimeSpan[] GetPrimaryTimeframes() => [TimeSpan.FromHours(1)];

    /// <inheritdoc />
    public async Task<int> RunAsync(RunFlags flags)
    {
        var aiDebug = flags.HasFlag(RunFlags.AiDebug);
        var visualDebug = flags.HasFlag(RunFlags.VisualDebug);

        Console.WriteLine($"=== {Name} Strategy Backtest ===");
        Console.WriteLine();

        PrintDebugModeInfo(aiDebug, visualDebug);

        DebugWebAppLauncher? webAppLauncher = null;

        try
        {
            var config = GetDefaultConfig();
            PrintConfiguration(config);

            var portfolio = CreatePortfolio(config);
            var backtestConfig = CreateBacktestConfig(config, aiDebug);

            // Start visual debug web app if requested
            if (visualDebug)
            {
                webAppLauncher = await StartVisualDebugAsync(config);
            }

            var strategy = CreateStrategy(config, config.Security, portfolio);
            ConfigureStrategy(strategy, config);

            PrintStrategyParameters();

            var result = await RunBacktestAsync(backtestConfig, strategy);

            await ProcessResultAsync(result, strategy, config);

            return result.IsSuccessful ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return 1;
        }
        finally
        {
            webAppLauncher?.Dispose();
        }
    }

    private void PrintDebugModeInfo(bool aiDebug, bool visualDebug)
    {
        if (aiDebug)
        {
            Console.WriteLine("[AI Debug Mode Enabled]");
            Console.WriteLine("  - Using SQLite event repository");
            Console.WriteLine("  - Web app launcher disabled");
            Console.WriteLine();
        }

        if (visualDebug)
        {
            Console.WriteLine("[Visual Debug Mode Enabled]");
            Console.WriteLine("  - Web app for visual debugging will be started");
            Console.WriteLine();
        }
    }

    private void PrintConfiguration(LauncherConfig config)
    {
        Console.WriteLine($"History Path: {config.HistoryPath}");
        Console.WriteLine($"Storage Format: {config.StorageFormat}");
        Console.WriteLine($"Period: {config.StartDate:yyyy-MM-dd} to {config.EndDate:yyyy-MM-dd}");
        Console.WriteLine($"Initial Capital: {config.InitialCapital:N2}");
        Console.WriteLine();
    }

    private Portfolio CreatePortfolio(LauncherConfig config)
    {
        var portfolio = Portfolio.CreateSimulator();
        portfolio.BeginValue = config.InitialCapital;
        portfolio.Name = config.PortfolioName;
        return portfolio;
    }

    private BacktestConfig CreateBacktestConfig(LauncherConfig config, bool aiDebug)
    {
        var backtestConfig = new BacktestConfig
        {
            ValidationPeriod = new PeriodConfig
            {
                StartDate = config.StartDate,
                EndDate = config.EndDate
            },
            HistoryPath = config.HistoryPath,
            StorageFormat = config.StorageFormat,
            MatchOnTouch = false
        };

        if (aiDebug)
        {
            var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
            var debugDbPath = Path.Combine(solutionRoot, "debug", "events.db");
            backtestConfig.AgenticLogging = new AgenticLoggingSettings
            {
                Enabled = true,
                DatabasePath = debugDbPath,
                BatchSize = 1000,
                FlushInterval = TimeSpan.FromSeconds(30),
                LogIndicators = true,
                LogTrades = true,
                LogMarketData = false
            };
        }
        else
        {
            backtestConfig.DebugMode = new DebugModeSettings
            {
                Enabled = true,
                OutputDirectory = GetWebAppPath(config, @"public\debug-mode"),
                FlushIntervalMs = 800,
                WebAppPath = GetWebAppPath(config),
                WebAppUrl = "http://localhost:3000",
                DebugPagePath = "/debug-mode"
            };
        }

        return backtestConfig;
    }

    private async Task<DebugWebAppLauncher?> StartVisualDebugAsync(LauncherConfig config)
    {
        var webAppLauncher = new DebugWebAppLauncher(
            GetWebAppPath(config),
            "http://localhost:3000",
            "/debug-mode");

        var serverStarted = await webAppLauncher.EnsureServerRunningAndOpenAsync();
        if (!serverStarted)
        {
            Console.WriteLine("Warning: Could not start debug web server, continuing without visual debug...");
        }

        return webAppLauncher;
    }

    private void ConfigureStrategy(TStrategy strategy, LauncherConfig config)
    {
        strategy.AuxiliaryTimeframe = GetAuxiliaryTimeframe();
        strategy.Securities[config.Security] = GetPrimaryTimeframes();

        var parameters = GetParameters();
        strategy.ParamsContainer = new CustomParamsContainer(parameters);
    }

    private void PrintStrategyParameters()
    {
        var parameters = GetParameters();
        Console.WriteLine("Strategy Parameters:");
        foreach (var param in parameters)
        {
            Console.WriteLine($"  {param.Id}: {param.Value}");
        }
        Console.WriteLine();
    }

    private async Task<BacktestResult<TStrategy>> RunBacktestAsync(BacktestConfig config, TStrategy strategy)
    {
        Console.WriteLine("Starting backtest...");
        Console.WriteLine();

        var metricsCalculator = new PerformanceMetricsCalculator();
        using var runner = new BacktestRunner<TStrategy>(config, strategy, metricsCalculator);
        return await runner.RunAsync();
    }

    private async Task ProcessResultAsync(BacktestResult<TStrategy> result, TStrategy strategy, LauncherConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("=== Backtest Results ===");
        Console.WriteLine();

        if (result.IsSuccessful)
        {
            PrintSuccessfulResult(result);
            await GenerateReportAsync(result, strategy, config);
        }
        else
        {
            Console.WriteLine($"Status: FAILED");
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }
    }

    private void PrintSuccessfulResult(BacktestResult<TStrategy> result)
    {
        var metrics = result.Metrics;

        Console.WriteLine($"Status: SUCCESS");
        Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2} seconds");
        Console.WriteLine();

        Console.WriteLine("Trading Performance:");
        Console.WriteLine($"  Total Trades: {metrics.TotalTrades}");
        Console.WriteLine($"  Winning Trades: {metrics.WinningTrades}");
        Console.WriteLine($"  Losing Trades: {metrics.LosingTrades}");
        Console.WriteLine($"  Win Rate: {metrics.WinRate:F1}%");
        Console.WriteLine();

        Console.WriteLine("Returns:");
        Console.WriteLine($"  Total Return: {metrics.TotalReturn:F2}%");
        Console.WriteLine($"  Annualized Return: {metrics.AnnualizedReturn:F2}%");
        Console.WriteLine($"  Net Profit: ${metrics.NetProfit:N2}");
        Console.WriteLine($"  Gross Profit: ${metrics.GrossProfit:N2}");
        Console.WriteLine($"  Gross Loss: ${metrics.GrossLoss:N2}");
        Console.WriteLine();

        Console.WriteLine("Risk Metrics:");
        Console.WriteLine($"  Sharpe Ratio: {metrics.SharpeRatio:F2}");
        Console.WriteLine($"  Sortino Ratio: {metrics.SortinoRatio:F2}");
        Console.WriteLine($"  Maximum Drawdown: {metrics.MaxDrawdown:F2}%");
        Console.WriteLine($"  Profit Factor: {metrics.ProfitFactor:F2}");
        Console.WriteLine();

        Console.WriteLine("Trade Analysis:");
        Console.WriteLine($"  Average Win: ${metrics.AverageWin:N2}");
        Console.WriteLine($"  Average Loss: ${metrics.AverageLoss:N2}");
        Console.WriteLine($"  Average Trades/Day: {metrics.AverageTradesPerDay:F2}");
        Console.WriteLine();

        Console.WriteLine("Capital:");
        Console.WriteLine($"  Initial Capital: ${metrics.InitialCapital:N2}");
        Console.WriteLine($"  Final Value: ${metrics.FinalValue:N2}");
        Console.WriteLine($"  Trading Period: {metrics.TradingPeriodDays} days");
        Console.WriteLine();
    }

    private async Task GenerateReportAsync(BacktestResult<TStrategy> result, TStrategy strategy, LauncherConfig config)
    {
        Console.WriteLine("Generating HTML report...");

        var strategyVersion = strategy.Version;
        var paramsHash = strategy.ParamsHash;

        var securityCode = config.Security.Code;
        var fromDate = config.StartDate.ToString("yyyyMMdd");
        var toDate = config.EndDate.ToString("yyyyMMdd");

        var reportPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "StockSharp",
            "Reports",
            $"{strategy.GetType().Name}_v{strategyVersion}",
            paramsHash,
            $"backtest_{securityCode}_{fromDate}_{toDate}");

        Console.WriteLine($"Report path: {reportPath}");

        var chartModel = new StrategySecurityChartModel
        {
            Strategy = strategy,
            HistoryPath = config.HistoryPath,
            Security = config.Security,
            OutputPath = reportPath,
            Metrics = result.Metrics,
            StartDate = config.StartDate,
            EndDate = config.EndDate,
            WalkForwardResult = null
        };

        var reportBuilder = new ReportBuilder<TStrategy>();
        await reportBuilder.GenerateReportAsync(chartModel, reportPath);

        Console.WriteLine();
        Console.WriteLine("=== Report Generated Successfully ===");
        Console.WriteLine($"Location: {Path.Combine(reportPath, "index.html")}");
        Console.WriteLine("Open in browser to view interactive charts");
        Console.WriteLine();
    }

    private string GetWebAppPath(LauncherConfig config, string subdirectory = "")
    {
        var basePath = AppContext.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(basePath, config.WebAppRelativePath, subdirectory));

        Console.WriteLine($"[DEBUG] AppContext.BaseDirectory: {basePath}");
        Console.WriteLine($"[DEBUG] WebAppPath result: {fullPath}");

        return fullPath;
    }
}

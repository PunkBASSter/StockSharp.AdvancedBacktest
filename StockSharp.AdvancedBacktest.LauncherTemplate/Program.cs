using System.CommandLine;
using StockSharp.AdvancedBacktest.Backtest;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.Algo.Storages;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.LauncherTemplate;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var aiDebugOption = new Option<bool>(
            name: "--ai-debug",
            description: "Enable AI agentic debug mode (disables web app launcher)",
            getDefaultValue: () => false);

        var rootCommand = new RootCommand("ZigZag Breakout Strategy Backtest");
        rootCommand.AddOption(aiDebugOption);

        rootCommand.SetHandler(async (bool aiDebug) =>
        {
            await RunBacktestAsync(aiDebug);
        }, aiDebugOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> RunBacktestAsync(bool aiDebug)
    {
        Console.WriteLine("=== ZigZag Breakout Strategy Backtest ===");
        Console.WriteLine();

        if (aiDebug)
        {
            Console.WriteLine("[AI Debug Mode Enabled]");
            Console.WriteLine("  - Using SQLite event repository");
            Console.WriteLine("  - Web app launcher disabled");
            Console.WriteLine();
        }

        try
        {
            // Configuration
            var historyPath = Environment.GetEnvironmentVariable("StockSharp__HistoryPath")
                ?? @".\History";
            var storageFormatEnv = Environment.GetEnvironmentVariable("StockSharp__StorageFormat");
            var storageFormat = Enum.TryParse<StorageFormats>(storageFormatEnv, ignoreCase: true, out var parsed)
                ? parsed
                : StorageFormats.Binary;
            var startDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var endDate = new DateTimeOffset(2023, 12, 31, 23, 59, 59, TimeSpan.Zero);
            const decimal initialCapital = 10000m;

            Console.WriteLine($"History Path: {historyPath}");
            Console.WriteLine($"Storage Format: {storageFormat}");
            Console.WriteLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Console.WriteLine($"Initial Capital: {initialCapital:N2}");
            Console.WriteLine();

            // Create Security
            var security = new Security
            {
                Id = "BTCUSDT@BNB",
                Code = "BTCUSDT",
                Board = ExchangeBoard.Binance,
                PriceStep = 0.01m,  // BTCUSDT typically trades with 2 decimal places
                Decimals = 2,
                VolumeStep = 0.001m,  // Binance BTCUSDT lot size
                MinVolume = 0.001m,   // Minimum order size
                MaxVolume = 9000m     // Maximum order size
            };

            // Create Portfolio
            var portfolio = Portfolio.CreateSimulator();
            portfolio.BeginValue = initialCapital;
            portfolio.Name = "ZigZagBreakout";

            // Create Backtest Configuration
            var config = new BacktestConfig
            {
                ValidationPeriod = new PeriodConfig
                {
                    StartDate = startDate,
                    EndDate = endDate
                },
                HistoryPath = historyPath,
                StorageFormat = storageFormat,
                MatchOnTouch = false
            };

            // Configure debug mode based on --ai-debug flag
            if (aiDebug)
            {
                // AI Agentic Debug Mode - SQLite event repository for AI agent analysis
                // Path is relative to solution root for MCP server compatibility
                var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
                var debugDbPath = Path.Combine(solutionRoot, "debug", "events.db");
                config.AgenticLogging = new AgenticLoggingSettings
                {
                    Enabled = true,
                    DatabasePath = debugDbPath,
                    BatchSize = 1000,
                    FlushInterval = TimeSpan.FromSeconds(30),
                    LogIndicators = true,
                    LogTrades = true,
                    LogMarketData = false  // Disable to reduce database size
                };
            }
            else
            {
                // Standard Debug Mode - Real-time browser visualization
                config.DebugMode = new DebugModeSettings
                {
                    Enabled = true,
                    OutputDirectory = WebAppPath(@"public\debug-mode"),
                    FlushIntervalMs = 800,
                    WebAppPath = WebAppPath(),
                    WebAppUrl = "http://localhost:3000",
                    DebugPagePath = "/debug-mode"
                };
            }

            // Create Strategy Instance
            var strategy = new ZigZagBreakout
            {
                Security = security,
                Portfolio = portfolio,
                // Set auxiliary timeframe for more granular SL/TP checking during backtests
                // This is invisible in all outputs - purely internal implementation
                AuxiliaryTimeframe = TimeSpan.FromMinutes(5)
            };

            // Set timeframe - using 1 hour candles for this backtest
            strategy.Securities[security] = [TimeSpan.FromHours(1)];

            // Set Strategy Parameters using CustomParams
            var parameters = new List<ICustomParam>
            {
                new NumberParam<decimal>("DzzDepth", 5m)
            };
            strategy.ParamsContainer = new CustomParamsContainer(parameters);

            Console.WriteLine("Strategy Parameters:");
            Console.WriteLine($"  DzzDepth: 5");
            Console.WriteLine();

            // Create and Run Backtest
            Console.WriteLine("Starting backtest...");
            Console.WriteLine();

            var metricsCalculator = new Statistics.PerformanceMetricsCalculator();
            using var runner = new BacktestRunner<ZigZagBreakout>(config, strategy, metricsCalculator);
            var result = await runner.RunAsync();

            // Print Results
            Console.WriteLine();
            Console.WriteLine("=== Backtest Results ===");
            Console.WriteLine();

            if (result.IsSuccessful)
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

                // Generate HTML Report
                Console.WriteLine("Generating HTML report...");

                // Build hierarchical folder structure
                var strategyVersion = strategy.Version; // Uses strategy's Version property
                var paramsHash = strategy.ParamsHash; // Uses SHA256-based hash from CustomStrategyBase
                var securityCode = security.Code; // "BTCUSDT"
                var fromDate = startDate.ToString("yyyyMMdd");
                var toDate = endDate.ToString("yyyyMMdd");

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
                    HistoryPath = historyPath,
                    Security = security,
                    OutputPath = reportPath,
                    Metrics = result.Metrics,
                    StartDate = startDate,
                    EndDate = endDate,
                    WalkForwardResult = null  // No walk-forward in this basic backtest
                };

                var reportBuilder = new ReportBuilder<ZigZagBreakout>();
                await reportBuilder.GenerateReportAsync(chartModel, reportPath);

                Console.WriteLine();
                Console.WriteLine("=== Report Generated Successfully ===");
                Console.WriteLine($"Location: {Path.Combine(reportPath, "index.html")}");
                Console.WriteLine("Open in browser to view interactive charts");
                Console.WriteLine();

                return 0;
            }
            else
            {
                Console.WriteLine($"Status: FAILED");
                Console.WriteLine($"Error: {result.ErrorMessage}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return 1;
        }
    }

    private static string WebAppPath(string subdirectory = "")
    {
        var basePath = AppContext.BaseDirectory;
        var fullPath = Path.GetFullPath(Path.Combine(basePath,
            @"..\..\..\..\StockSharp.AdvancedBacktest.Web", subdirectory));

        Console.WriteLine($"[DEBUG] AppContext.BaseDirectory: {basePath}");
        Console.WriteLine($"[DEBUG] WebAppPath result: {fullPath}");

        return fullPath;
    }
}

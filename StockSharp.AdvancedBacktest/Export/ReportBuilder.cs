using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Strategies;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.PerformanceValidation;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Export;

public class ReportBuilder<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    private readonly ILogger<ReportBuilder<TStrategy>>? _logger;
    private readonly BacktestExporter _backtestExporter;
    private readonly string _webTemplatePath;

    public ReportBuilder(
        BacktestExporter? backtestExporter = null,
        ILogger<ReportBuilder<TStrategy>>? logger = null,
        string? webTemplatePath = null)
    {
        _logger = logger;
        _backtestExporter = backtestExporter ?? new BacktestExporter(logger: null);
        _webTemplatePath = webTemplatePath ?? FindWebTemplatePath();
    }

    /// <summary>
    /// Finds the web template path by searching upward from the base directory for the solution root
    /// </summary>
    private static string FindWebTemplatePath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var currentDir = new DirectoryInfo(baseDir);

        // Search upward for the solution root (directory containing .slnx file)
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "StockSharp.AdvancedBacktest.slnx")))
            {
                // Found solution root
                return Path.Combine(currentDir.FullName, "StockSharp.AdvancedBacktest.Web", "out");
            }
            currentDir = currentDir.Parent;
        }

        // Fallback to old relative path if solution root not found
        return Path.Combine(baseDir, "..", "..", "..", "..", "StockSharp.AdvancedBacktest.Web", "out");
    }

    /// <summary>
    /// Generates a static HTML report by copying the Next.js template and writing chartData.json
    /// </summary>
    /// <param name="model">Strategy and chart configuration</param>
    /// <param name="outputPath">Directory where the report should be generated</param>
    /// <exception cref="InvalidOperationException">Thrown when web template is not found or report generation fails</exception>
    public async Task GenerateReportAsync(StrategySecurityChartModel model, string outputPath)
    {
        try
        {
            _logger?.LogInformation("Starting report generation for {OutputPath}", outputPath);

            // 1. Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                _logger?.LogDebug("Created output directory: {OutputPath}", outputPath);
            }

            // 2. Export indicators to separate files
            var indicatorSeries = ExtractIndicatorData(model.Strategy);
            var indicatorFiles = await ExportIndicatorsToFilesAsync(indicatorSeries, outputPath);

            // 3. Export chartData.json (with indicator file references)
            var chartData = new ChartDataModel
            {
                Candles = ExtractCandleData(model),
                IndicatorFiles = indicatorFiles,
                Trades = ExtractTradeData(model.Strategy),
                WalkForward = ExtractWalkForwardData(model.WalkForwardResult)
            };

            var chartDataPath = Path.Combine(outputPath, "chartData.json");
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            await File.WriteAllTextAsync(chartDataPath, JsonSerializer.Serialize(chartData, jsonOptions));
            _logger?.LogDebug("Chart data written to {ChartDataPath}", chartDataPath);

            // 3. Verify web template exists
            if (!Directory.Exists(_webTemplatePath))
            {
                throw new InvalidOperationException(
                    $"Web template not found at {_webTemplatePath}. " +
                    "Please run 'npm run build' in StockSharp.AdvancedBacktest.Web directory.");
            }

            // 4. Copy pre-built template
            CopyDirectory(_webTemplatePath, outputPath, overwrite: true);
            _logger?.LogDebug("Copied web template from {TemplatePath} to {OutputPath}", _webTemplatePath, outputPath);

            // 5. Verify index.html exists
            var indexPath = Path.Combine(outputPath, "index.html");
            if (!File.Exists(indexPath))
            {
                throw new InvalidOperationException(
                    $"Web report generation failed: index.html not found at {indexPath}");
            }

            // 6. Run fix-paths.mjs to fix Next.js paths and embed chartData.json
            await RunFixPathsScript(outputPath);
            _logger?.LogDebug("Fixed paths and embedded chart data via fix-paths.mjs");

            // 7. Export trades to CSV
            await ExportTradesToCsvAsync(chartData.Trades, outputPath);
            _logger?.LogDebug("Exported trades to CSV");

            _logger?.LogInformation("Report generated successfully at {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate report at {OutputPath}", outputPath);
            throw;
        }
    }

    public void GenerateInteractiveChart(StrategySecurityChartModel model, bool openInBrowser = false)
    {
        var chartData = new ChartDataModel();
        chartData.Candles = ExtractCandleData(model);
        chartData.Trades = ExtractTradeData(model.Strategy);
        chartData.WalkForward = ExtractWalkForwardData(model.WalkForwardResult);
        var htmlContent = GenerateChartHtml(chartData);
        var outputDir = Path.GetDirectoryName(model.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        File.WriteAllText(model.OutputPath, htmlContent, System.Text.Encoding.UTF8);

        if (openInBrowser)
        {
            var browserPath = Environment.GetEnvironmentVariable("BROWSER_PATH") ?? "chrome";
            var filePath = Path.GetFullPath(model.OutputPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = filePath,
                UseShellExecute = true
            });
        }
    }

    private List<CandleDataPoint> ExtractCandleData(StrategySecurityChartModel model)
    {
        var historyPath = model.HistoryPath;
        var securities = model.Strategy.Securities;
        using var dataDrive = new LocalMarketDataDrive(historyPath);
        using var tempRegistry = new StorageRegistry { DefaultDrive = dataDrive };
        var candles = new List<CandleDataPoint>();
        var security = model.Strategy.Security ?? securities.Keys.FirstOrDefault();
        if (security == null)
            return candles;
        //foreach (var security in securities.Keys)
        {
            var lowestTimeFrame = securities[security].FirstOrDefault();
            var securityId = security.Id.ToSecurityId();
            var candleStorage = tempRegistry.GetCandleMessageStorage(
                typeof(TimeFrameCandleMessage),
                securityId,
                lowestTimeFrame,
                format: StorageFormats.Binary);

            // Filter dates to only include those within the StartDate to EndDate range
            var startDate = model.StartDate.Date;
            var endDate = model.EndDate.Date;
            var dates = candleStorage.Dates
                .Where(d => d >= startDate && d <= endDate)
                .ToArray();

            candles = dates.SelectMany(date => candleStorage.Load(date))
                .Where(c => c.OpenTime >= model.StartDate && c.OpenTime <= model.EndDate)
                .Select(c => new CandleDataPoint
                {
                    Time = c.OpenTime.ToUnixTimeSeconds(),
                    Open = (double)c.OpenPrice,
                    High = (double)c.HighPrice,
                    Low = (double)c.LowPrice,
                    Close = (double)c.ClosePrice,
                    Volume = (double)c.TotalVolume
                })
                .OrderBy(c => c.Time)
                .ToList();
        }
        return candles;
    }

    private List<IndicatorDataSeries> ExtractIndicatorData(CustomStrategyBase strategy)
    {
        _logger?.LogDebug("Extracting indicator data from {StrategyType}", strategy.GetType().Name);

        // Extract candle interval from strategy configuration
        var candleInterval = ExtractCandleInterval(strategy);

        // Use BacktestExporter to extract indicators directly
        var indicators = _backtestExporter.ExtractIndicators(strategy.Indicators, candleInterval);

        _logger?.LogDebug("Extracted {Count} indicator series from strategy", indicators.Count);
        return indicators;
    }

    private TimeSpan? ExtractCandleInterval(CustomStrategyBase strategy)
    {
        var firstSecurity = strategy.Securities.FirstOrDefault();
        var candleInterval = firstSecurity.Value?.FirstOrDefault();

        _logger?.LogDebug("Extracted candle interval: {Interval}", candleInterval);
        return candleInterval;
    }

    private async Task<List<string>> ExportIndicatorsToFilesAsync(List<IndicatorDataSeries> indicators, string outputPath)
    {
        var indicatorFiles = new List<string>();
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        foreach (var indicator in indicators)
        {
            // Create safe filename from indicator name
            var safeFileName = string.Join("_", indicator.Name.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"indicator_{safeFileName}.json";
            var filePath = Path.Combine(outputPath, fileName);

            // Write indicator data to file
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(indicator, jsonOptions));

            indicatorFiles.Add(fileName);
            _logger?.LogDebug("Exported indicator {Name} to {FileName} ({ValueCount} values)",
                indicator.Name, fileName, indicator.Values.Count);
        }

        _logger?.LogInformation("Exported {Count} indicators to separate files", indicators.Count);
        return indicatorFiles;
    }

    private List<TradeDataPoint> ExtractTradeData(Strategy strategy)
    {
        var trades = new List<TradeDataPoint>();

        foreach (var myTrade in strategy.MyTrades)
        {
            // Determine trade side from order side or volume sign
            var side = "buy"; // Default
            if (myTrade.Order?.Side != null)
            {
                side = myTrade.Order.Side == Sides.Buy ? "buy" : "sell";
            }
            else if (myTrade.Trade.Volume != 0)
            {
                // If no order side, infer from volume (positive = buy, negative = sell)
                side = myTrade.Trade.Volume > 0 ? "buy" : "sell";
            }

            trades.Add(new TradeDataPoint
            {
                Time = myTrade.Trade.ServerTime.ToUnixTimeSeconds(),
                Price = (double)myTrade.Trade.Price,
                Volume = (double)Math.Abs(myTrade.Trade.Volume),
                Side = side,
                PnL = (double)(myTrade.PnL ?? 0)
            });
        }

        return trades.OrderBy(t => t.Time).ToList();
    }

    private WalkForwardDataModel? ExtractWalkForwardData(WalkForwardResult? wfResult)
    {
        if (wfResult == null)
            return null;

        return new WalkForwardDataModel
        {
            WalkForwardEfficiency = wfResult.WalkForwardEfficiency,
            Consistency = wfResult.Consistency,
            TotalWindows = wfResult.TotalWindows,
            Windows = wfResult.Windows.Select(w => new WalkForwardWindowData
            {
                WindowNumber = w.WindowNumber,
                TrainingStart = w.TrainingPeriod.start.ToUnixTimeSeconds(),
                TrainingEnd = w.TrainingPeriod.end.ToUnixTimeSeconds(),
                TestingStart = w.TestingPeriod.start.ToUnixTimeSeconds(),
                TestingEnd = w.TestingPeriod.end.ToUnixTimeSeconds(),
                TrainingMetrics = new WalkForwardMetricsData
                {
                    TotalReturn = w.TrainingMetrics.TotalReturn,
                    SharpeRatio = w.TrainingMetrics.SharpeRatio,
                    SortinoRatio = w.TrainingMetrics.SortinoRatio,
                    MaxDrawdown = w.TrainingMetrics.MaxDrawdown,
                    WinRate = w.TrainingMetrics.WinRate,
                    ProfitFactor = w.TrainingMetrics.ProfitFactor,
                    TotalTrades = w.TrainingMetrics.TotalTrades
                },
                TestingMetrics = new WalkForwardMetricsData
                {
                    TotalReturn = w.TestingMetrics.TotalReturn,
                    SharpeRatio = w.TestingMetrics.SharpeRatio,
                    SortinoRatio = w.TestingMetrics.SortinoRatio,
                    MaxDrawdown = w.TestingMetrics.MaxDrawdown,
                    WinRate = w.TestingMetrics.WinRate,
                    ProfitFactor = w.TestingMetrics.ProfitFactor,
                    TotalTrades = w.TestingMetrics.TotalTrades
                },
                PerformanceDegradation = w.PerformanceDegradation
            }).ToList()
        };
    }

    /// <summary>
    /// Get appropriate color for indicator based on type
    /// </summary>
    private static string GetIndicatorColor(IIndicator indicator)
    {
        // Simple color assignment based on indicator type
        return indicator.Name.ToLower() switch
        {
            var name when name.Contains("sma") || name.Contains("simple") => "#2196F3",
            var name when name.Contains("ema") || name.Contains("exponential") => "#FF9800",
            var name when name.Contains("rsi") => "#9C27B0",
            var name when name.Contains("macd") => "#4CAF50",
            var name when name.Contains("bollinger") => "#F44336",
            _ => "#607D8B"
        };
    }

    /// <summary>
    /// Generate HTML content with TradingView chart
    /// </summary>
    private string GenerateChartHtml(ChartDataModel chartData)
    {
        var template = GetHtmlTemplate();
        var chartDataJson = JsonSerializer.Serialize(chartData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return template.Replace("{{ CHART_DATA }}", chartDataJson);
    }

    /// <summary>
    /// Get HTML template with TradingView Lightweight Charts
    /// </summary>
    private static string GetHtmlTemplate()
    {
        return File.Exists("chart-template.html")
            ? File.ReadAllText("chart-template.html")
            : throw new FileNotFoundException("chart-template.html not found");
    }

    /// <summary>
    /// Recursively copies all files and subdirectories from source to destination
    /// </summary>
    /// <param name="sourceDir">Source directory path</param>
    /// <param name="destinationDir">Destination directory path</param>
    /// <param name="overwrite">Whether to overwrite existing files</param>
    private void CopyDirectory(string sourceDir, string destinationDir, bool overwrite = false)
    {
        var dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        // Create destination directory if it doesn't exist
        Directory.CreateDirectory(destinationDir);

        // Copy all files
        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);

            // Skip chartData.json to avoid overwriting it
            if (file.Name.Equals("chartData.json", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("Skipping {FileName} from template copy", file.Name);
                continue;
            }

            file.CopyTo(targetFilePath, overwrite);
            _logger?.LogTrace("Copied file {FileName} to {TargetPath}", file.Name, targetFilePath);
        }

        // Recursively copy all subdirectories
        foreach (var subDir in dir.GetDirectories())
        {
            var targetSubDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, targetSubDir, overwrite);
        }
    }

    /// <summary>
    /// Runs the fix-paths.mjs Node.js script to fix Next.js paths and embed chartData.json
    /// </summary>
    /// <param name="reportPath">Path to the report directory containing index.html and chartData.json</param>
    private async Task RunFixPathsScript(string reportPath)
    {
        // Path to fix-paths.mjs in the Web project
        var fixPathsScript = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "StockSharp.AdvancedBacktest.Web", "fix-paths.mjs");

        if (!File.Exists(fixPathsScript))
        {
            throw new InvalidOperationException(
                $"fix-paths.mjs not found at {fixPathsScript}. " +
                "Ensure StockSharp.AdvancedBacktest.Web project is in the expected location.");
        }

        _logger?.LogDebug("Running fix-paths.mjs from {ScriptPath}", fixPathsScript);

        var processInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{fixPathsScript}\"",
            WorkingDirectory = reportPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(processInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Node.js process for fix-paths.mjs");
        }

        // Capture output for logging
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger?.LogError("fix-paths.mjs failed with exit code {ExitCode}", process.ExitCode);
            _logger?.LogError("STDERR: {Error}", error);
            throw new InvalidOperationException(
                $"fix-paths.mjs failed with exit code {process.ExitCode}. Error: {error}");
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            _logger?.LogDebug("fix-paths.mjs output: {Output}", output.Trim());
        }
    }

    /// <summary>
    /// Exports trade data to a CSV file
    /// </summary>
    /// <param name="trades">List of trade data points</param>
    /// <param name="outputPath">Directory where the CSV file should be saved</param>
    private async Task ExportTradesToCsvAsync(List<TradeDataPoint> trades, string outputPath)
    {
        var csvPath = Path.Combine(outputPath, "trades.csv");

        using var writer = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8);

        // Write header
        await writer.WriteLineAsync("Timestamp,DateTime,Price,Volume,Side,PnL");

        // Write trade data
        foreach (var trade in trades)
        {
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(trade.Time).ToString("yyyy-MM-dd HH:mm:ss");
            await writer.WriteLineAsync(
                $"{trade.Time},{dateTime},{trade.Price},{trade.Volume},{trade.Side},{trade.PnL}");
        }

        _logger?.LogDebug("Exported {TradeCount} trades to {CsvPath}", trades.Count, csvPath);
    }
}

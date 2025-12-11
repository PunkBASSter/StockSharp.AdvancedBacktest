using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Strategies;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.PerformanceValidation;
using StockSharp.AdvancedBacktest.Storages;
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

    private static string FindWebTemplatePath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var currentDir = new DirectoryInfo(baseDir);

        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "StockSharp.AdvancedBacktest.slnx")))
            {
                return Path.Combine(currentDir.FullName, "StockSharp.AdvancedBacktest.Web", "out");
            }
            currentDir = currentDir.Parent;
        }

        return Path.Combine(baseDir, "..", "..", "..", "..", "StockSharp.AdvancedBacktest.Web", "out");
    }

    public async Task GenerateReportAsync(StrategySecurityChartModel model, string outputPath)
    {
        try
        {
            _logger?.LogInformation("Starting report generation for {OutputPath}", outputPath);

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                _logger?.LogDebug("Created output directory: {OutputPath}", outputPath);
            }

            var indicatorSeries = ExtractIndicatorData(model.Strategy);
            var indicatorFiles = await ExportIndicatorsToFilesAsync(indicatorSeries, outputPath);

            var chartData = new ChartDataModel
            {
                Candles = await ExtractCandleDataAsync(model),
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

            if (!Directory.Exists(_webTemplatePath))
            {
                throw new InvalidOperationException(
                    $"Web template not found at {_webTemplatePath}. " +
                    "Please run 'npm run build' in StockSharp.AdvancedBacktest.Web directory.");
            }

            CopyDirectory(_webTemplatePath, outputPath, overwrite: true);
            _logger?.LogDebug("Copied web template from {TemplatePath} to {OutputPath}", _webTemplatePath, outputPath);

            var indexPath = Path.Combine(outputPath, "index.html");
            if (!File.Exists(indexPath))
            {
                throw new InvalidOperationException(
                    $"Web report generation failed: index.html not found at {indexPath}");
            }

            await RunFixPathsScript(outputPath);
            _logger?.LogDebug("Fixed paths and embedded chart data via fix-paths.mjs");

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
        chartData.Candles = ExtractCandleDataAsync(model).GetAwaiter().GetResult();
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

    private async Task<List<CandleDataPoint>> ExtractCandleDataAsync(StrategySecurityChartModel model, CancellationToken cancellationToken = default)
    {
        var historyPath = model.HistoryPath;
        var securities = model.Strategy.Securities;
        using var dataDrive = new LocalMarketDataDrive(historyPath);
        using var innerRegistry = new StorageRegistry { DefaultDrive = dataDrive };
        var tempRegistry = new SharedStorageRegistry(innerRegistry);
        var candles = new List<CandleDataPoint>();
        var security = model.Strategy.Security ?? securities.Keys.FirstOrDefault();
        if (security == null)
            return candles;

        var lowestTimeFrame = securities[security].FirstOrDefault();
        var securityId = security.Id.ToSecurityId();
        var candleStorage = tempRegistry.GetCandleMessageStorage(
            securityId,
            DataType.Create<TimeFrameCandleMessage>(lowestTimeFrame),
            format: StorageFormats.Binary);

        var startDate = model.StartDate.Date;
        var endDate = model.EndDate.Date;
        var allDates = await candleStorage.GetDatesAsync(cancellationToken);
        var dates = allDates
            .Where(d => d >= startDate && d <= endDate)
            .ToArray();

        var candleList = new List<CandleMessage>();
        foreach (var date in dates)
        {
            await foreach (var candle in candleStorage.LoadAsync(date, cancellationToken))
            {
                candleList.Add(candle);
            }
        }

        candles = candleList
            .Where(c => c.OpenTime >= model.StartDate && c.OpenTime <= model.EndDate)
            .Select(c => new CandleDataPoint
            {
                Time = new DateTimeOffset(c.OpenTime, TimeSpan.Zero).ToUnixTimeSeconds(),
                Open = (double)c.OpenPrice,
                High = (double)c.HighPrice,
                Low = (double)c.LowPrice,
                Close = (double)c.ClosePrice,
                Volume = (double)c.TotalVolume
            })
            .OrderBy(c => c.Time)
            .ToList();

        return candles;
    }

    private List<IndicatorDataSeries> ExtractIndicatorData(CustomStrategyBase strategy)
    {
        _logger?.LogDebug("Extracting indicator data from {StrategyType}", strategy.GetType().Name);

        var candleInterval = ExtractCandleInterval(strategy);
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
            var safeFileName = string.Join("_", indicator.Name.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"indicator_{safeFileName}.json";
            var filePath = Path.Combine(outputPath, fileName);

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
            var side = "buy";
            if (myTrade.Order?.Side != null)
            {
                side = myTrade.Order.Side == Sides.Buy ? "buy" : "sell";
            }
            else if (myTrade.Trade.Volume != 0)
            {
                side = myTrade.Trade.Volume > 0 ? "buy" : "sell";
            }

            trades.Add(new TradeDataPoint
            {
                Time = new DateTimeOffset(myTrade.Trade.ServerTime, TimeSpan.Zero).ToUnixTimeSeconds(),
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

    private static string GetIndicatorColor(IIndicator indicator)
    {
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

    private static string GetHtmlTemplate()
    {
        return File.Exists("chart-template.html")
            ? File.ReadAllText("chart-template.html")
            : throw new FileNotFoundException("chart-template.html not found");
    }

    private void CopyDirectory(string sourceDir, string destinationDir, bool overwrite = false)
    {
        var dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);

            if (file.Name.Equals("chartData.json", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("Skipping {FileName} from template copy", file.Name);
                continue;
            }

            file.CopyTo(targetFilePath, overwrite);
            _logger?.LogTrace("Copied file {FileName} to {TargetPath}", file.Name, targetFilePath);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            var targetSubDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, targetSubDir, overwrite);
        }
    }

    private async Task RunFixPathsScript(string reportPath)
    {
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

    private async Task ExportTradesToCsvAsync(List<TradeDataPoint> trades, string outputPath)
    {
        var csvPath = Path.Combine(outputPath, "trades.csv");

        using var writer = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8);

        await writer.WriteLineAsync("Timestamp,DateTime,Price,Volume,Side,PnL");

        foreach (var trade in trades)
        {
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(trade.Time).ToString("yyyy-MM-dd HH:mm:ss");
            await writer.WriteLineAsync(
                $"{trade.Time},{dateTime},{trade.Price},{trade.Volume},{trade.Side},{trade.PnL}");
        }

        _logger?.LogDebug("Exported {TradeCount} trades to {CsvPath}", trades.Count, csvPath);
    }
}

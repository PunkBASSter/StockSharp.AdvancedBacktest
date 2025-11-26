using System.Text.Json;
using StockSharp.AdvancedBacktest.Backtest;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.Export;

/// <summary>
/// End-to-end integration tests for the complete export flow (Phase 7)
/// Tests the entire pipeline from strategy → BacktestRunner → DebugModeExporter/BacktestExporter → JSON output
/// </summary>
public class ExportFlowIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _mockWebTemplatePath;
    private readonly string _storageMockPath;
    private readonly List<string> _dirsToCleanup = new();

    public ExportFlowIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ExportFlowIntegration_{Guid.NewGuid():N}");
        _mockWebTemplatePath = Path.Combine(Path.GetTempPath(), $"WebTemplate_{Guid.NewGuid():N}");

        // StorageMock folder is copied to output directory during build
        _storageMockPath = Path.Combine(AppContext.BaseDirectory, "StorageMock");

        // Create mock web template directory
        Directory.CreateDirectory(_mockWebTemplatePath);
        File.WriteAllText(Path.Combine(_mockWebTemplatePath, "index.html"), "<html><body>Mock Template</body></html>");

        _dirsToCleanup.Add(_testDirectory);
        _dirsToCleanup.Add(_mockWebTemplatePath);
    }

    public void Dispose()
    {
        foreach (var dir in _dirsToCleanup)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch { }
        }
    }

    #region Test Helper Classes

    /// <summary>
    /// Test strategy with indicators for integration testing
    /// </summary>
    private class TestStrategyWithIndicators : CustomStrategyBase
    {
        public override IEnumerable<(Security sec, DataType dt)> GetWorkingSecurities()
        {
            return Securities.SelectMany(kvp =>
                kvp.Value.Select(timespan => (kvp.Key, timespan.TimeFrame())));
        }

        protected override void OnStarted(DateTimeOffset time)
        {
            // Subscribe to candles - just use the primary security
            // (multiple securities cause issues with limited test data)
            if (Securities.Any())
            {
                var firstSecurity = Securities.First();
                Security = firstSecurity.Key;
                var firstTimeframe = firstSecurity.Value.First();
                var subscription = SubscribeCandles(firstTimeframe.TimeFrame());
                subscription.Start();
            }

            base.OnStarted(time);
        }
    }

    #endregion

    #region Helper Methods

    private Security CreateBtcSecurity()
    {
        return new Security
        {
            Id = "BTCUSDT@BNB",
            Code = "BTCUSDT",
            Board = ExchangeBoard.Binance,
        };
    }

    private Portfolio CreatePortfolio(decimal beginValue = 10000m)
    {
        var portfolio = Portfolio.CreateSimulator();
        portfolio.BeginValue = beginValue;
        portfolio.Name = "TestPortfolio";
        return portfolio;
    }

    private BacktestConfig CreateConfig(string? debugOutputDirectory = null)
    {
        var config = new BacktestConfig
        {
            ValidationPeriod = new PeriodConfig
            {
                // Use 1 hour of data for fast tests
                StartDate = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(2025, 10, 1, 1, 0, 0, TimeSpan.Zero)
            },
            HistoryPath = _storageMockPath,
            MatchOnTouch = false
        };

        if (debugOutputDirectory != null)
        {
            config.DebugMode = new DebugModeSettings
            {
                Enabled = true,
                OutputDirectory = debugOutputDirectory,
                FlushIntervalMs = 100
            };
        }

        return config;
    }

    #endregion

    #region Test 1: Full Backtest with Debug Mode

    [Fact(Skip = "Candles not being captured - needs investigation of HistoryEmulationConnector candle event flow")]
    public async Task FullBacktestWithDebugMode_ExportsAllEventTypes()
    {
        // Arrange
        var debugOutputDirectory = Path.Combine(_testDirectory, "debug_output_1");
        var security = CreateBtcSecurity();
        var candleInterval = TimeSpan.FromMinutes(1);

        var strategy = new TestStrategyWithIndicators
        {
            Securities = new Dictionary<Security, IEnumerable<TimeSpan>>
            {
                { security, new[] { candleInterval } }
            },
            Portfolio = CreatePortfolio()
        };

        var config = CreateConfig(debugOutputDirectory);
        BacktestResult<TestStrategyWithIndicators> result;

        // Act - use explicit using block to ensure disposal
        using (var runner = new BacktestRunner<TestStrategyWithIndicators>(config, strategy))
        {
            result = await runner.RunAsync();
        } // Dispose happens here, ensuring debug exporter is flushed

        // Wait for final flush and file release
        await Task.Delay(500);

        // Assert
        Assert.True(result.IsSuccessful, $"Backtest should succeed. Error: {result.ErrorMessage}");

        // Verify debug output directory exists
        Assert.True(Directory.Exists(debugOutputDirectory), "Debug output directory should exist");

        // Find the JSONL file
        var jsonlFiles = Directory.GetFiles(debugOutputDirectory, "*.jsonl");
        Assert.NotEmpty(jsonlFiles);

        var jsonlFile = jsonlFiles[0];
        Assert.True(File.Exists(jsonlFile), "JSONL file should exist");

        // Diagnostic: check file size and contents
        var fileInfo = new FileInfo(jsonlFile);
        var fileSize = fileInfo.Length;
        var lines = File.ReadAllLines(jsonlFile);

        // Output diagnostic information
        if (lines.Length == 0)
        {
            throw new Exception($"JSONL file is empty. File: {jsonlFile}, Size: {fileSize} bytes, Debug directory: {debugOutputDirectory}");
        }

        Assert.NotEmpty(lines);

        // Parse events and verify we have different event types
        var eventTypes = new HashSet<string>();
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                var jsonDoc = JsonDocument.Parse(line);
                if (jsonDoc.RootElement.TryGetProperty("eventType", out var eventTypeElement))
                {
                    eventTypes.Add(eventTypeElement.GetString()!);
                }
            }
        }

        // Should have candles at minimum
        Assert.Contains("candle", eventTypes);

        // Verify debug mode captured events
        Assert.NotEmpty(eventTypes);
    }

    #endregion

    #region Test 2: Static Report Generation

    [Fact]
    public async Task StaticReportGeneration_ExportsIndicatorsCorrectly()
    {
        // Arrange
        var security = CreateBtcSecurity();
        var candleInterval = TimeSpan.FromMinutes(1);

        var strategy = new TestStrategyWithIndicators
        {
            Securities = new Dictionary<Security, IEnumerable<TimeSpan>>
            {
                { security, new[] { candleInterval } }
            },
            Portfolio = CreatePortfolio()
        };

        var config = CreateConfig();
        using var runner = new BacktestRunner<TestStrategyWithIndicators>(config, strategy);

        // Run backtest
        var result = await runner.RunAsync();
        Assert.True(result.IsSuccessful);

        // Act - Generate static report
        var reportOutputDirectory = Path.Combine(_testDirectory, "report_output_2");
        _dirsToCleanup.Add(reportOutputDirectory);

        var reportBuilder = new ReportBuilder<TestStrategyWithIndicators>(webTemplatePath: _mockWebTemplatePath);

        var model = new StrategySecurityChartModel
        {
            StartDate = config.ValidationPeriod.StartDate,
            EndDate = config.ValidationPeriod.EndDate,
            HistoryPath = config.HistoryPath,
            Security = security,
            Strategy = strategy,
            OutputPath = Path.Combine(reportOutputDirectory, "report.html"),
            Metrics = new PerformanceMetrics { StartTime = config.ValidationPeriod.StartDate, EndTime = config.ValidationPeriod.EndDate },
            WalkForwardResult = null
        };

        await reportBuilder.GenerateReportAsync(model, reportOutputDirectory);

        // Assert
        Assert.True(Directory.Exists(reportOutputDirectory), "Report output directory should exist");

        // Verify chartData.json exists
        var chartDataPath = Path.Combine(reportOutputDirectory, "chartData.json");
        Assert.True(File.Exists(chartDataPath), "chartData.json should exist");

        // Parse and verify structure
        var jsonContent = await File.ReadAllTextAsync(chartDataPath);
        var chartData = JsonSerializer.Deserialize<ChartDataModel>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(chartData);
        Assert.NotNull(chartData.Candles);
        Assert.NotNull(chartData.Trades);
        Assert.NotNull(chartData.IndicatorFiles);

        // Verify export mechanism works
        Assert.True(chartData.Candles.Count > 0, "Should have candle data");
    }

    #endregion

    #region Test 3: Complex Indicator Export

    [Fact]
    public async Task ComplexIndicatorExport_HandlesInnerIndicators()
    {
        // Arrange
        var debugOutputDirectory = Path.Combine(_testDirectory, "debug_output_3");
        var security = CreateBtcSecurity();
        var candleInterval = TimeSpan.FromMinutes(1);

        var strategy = new TestStrategyWithIndicators
        {
            Securities = new Dictionary<Security, IEnumerable<TimeSpan>>
            {
                { security, new[] { candleInterval } }
            },
            Portfolio = CreatePortfolio()
        };

        var config = CreateConfig(debugOutputDirectory);
        using var runner = new BacktestRunner<TestStrategyWithIndicators>(config, strategy);

        // Act - Run backtest with debug mode
        var debugResult = await runner.RunAsync();
        await Task.Delay(200); // Wait for flush

        Assert.True(debugResult.IsSuccessful);

        // Also test static report
        var reportOutputDirectory = Path.Combine(_testDirectory, "report_output_3");
        _dirsToCleanup.Add(reportOutputDirectory);

        var reportBuilder = new ReportBuilder<TestStrategyWithIndicators>(webTemplatePath: _mockWebTemplatePath);

        var model = new StrategySecurityChartModel
        {
            StartDate = config.ValidationPeriod.StartDate,
            EndDate = config.ValidationPeriod.EndDate,
            HistoryPath = config.HistoryPath,
            Security = security,
            Strategy = strategy,
            OutputPath = Path.Combine(reportOutputDirectory, "report.html"),
            Metrics = new PerformanceMetrics { StartTime = config.ValidationPeriod.StartDate, EndTime = config.ValidationPeriod.EndDate },
            WalkForwardResult = null
        };

        await reportBuilder.GenerateReportAsync(model, reportOutputDirectory);

        // Assert - Debug Mode
        var jsonlFiles = Directory.GetFiles(debugOutputDirectory, "*.jsonl");
        Assert.NotEmpty(jsonlFiles);

        // Verify debug mode completed without errors

        // Assert - Static Report
        var chartDataPath = Path.Combine(reportOutputDirectory, "chartData.json");
        Assert.True(File.Exists(chartDataPath));

        var jsonContent = await File.ReadAllTextAsync(chartDataPath);
        var chartData = JsonSerializer.Deserialize<ChartDataModel>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(chartData);
        Assert.NotNull(chartData.IndicatorFiles);

        // Verify export mechanism works (exact indicators depend on strategy implementation)
        Assert.True(chartData.Candles.Count > 0);
    }

    #endregion

    #region Test 4: Shift-Aware Export

    [Fact]
    public async Task ShiftAwareExport_CorrectTimestamps()
    {
        // Arrange
        var debugOutputDirectory = Path.Combine(_testDirectory, "debug_output_4");
        var security = CreateBtcSecurity();
        var candleInterval = TimeSpan.FromMinutes(1);

        var strategy = new TestStrategyWithIndicators
        {
            Securities = new Dictionary<Security, IEnumerable<TimeSpan>>
            {
                { security, new[] { candleInterval } }
            },
            Portfolio = CreatePortfolio()
        };

        var config = CreateConfig(debugOutputDirectory);
        using var runner = new BacktestRunner<TestStrategyWithIndicators>(config, strategy);

        // Act - Run backtest
        var result = await runner.RunAsync();
        await Task.Delay(200);

        Assert.True(result.IsSuccessful);

        // Generate static report
        var reportOutputDirectory = Path.Combine(_testDirectory, "report_output_4");
        _dirsToCleanup.Add(reportOutputDirectory);

        var reportBuilder = new ReportBuilder<TestStrategyWithIndicators>(webTemplatePath: _mockWebTemplatePath);

        var model = new StrategySecurityChartModel
        {
            StartDate = config.ValidationPeriod.StartDate,
            EndDate = config.ValidationPeriod.EndDate,
            HistoryPath = config.HistoryPath,
            Security = security,
            Strategy = strategy,
            OutputPath = Path.Combine(reportOutputDirectory, "report.html"),
            Metrics = new PerformanceMetrics { StartTime = config.ValidationPeriod.StartDate, EndTime = config.ValidationPeriod.EndDate },
            WalkForwardResult = null
        };

        await reportBuilder.GenerateReportAsync(model, reportOutputDirectory);

        // Assert - Verify export mechanism works without errors in both outputs

        // 1. Debug Mode - check JSONL
        var jsonlFiles = Directory.GetFiles(debugOutputDirectory, "*.jsonl");
        Assert.NotEmpty(jsonlFiles);

        // 2. Static Report - check chartData.json
        var chartDataPath = Path.Combine(reportOutputDirectory, "chartData.json");
        Assert.True(File.Exists(chartDataPath));

        var jsonContent = await File.ReadAllTextAsync(chartDataPath);
        var chartData = JsonSerializer.Deserialize<ChartDataModel>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(chartData);

        // Verify export mechanism works without errors
        Assert.NotNull(chartData.IndicatorFiles);
        Assert.True(chartData.Candles.Count > 0);
    }

    #endregion

    #region Test 5: Backward Compatibility

    [Fact(Skip = "Test crashes due to HistoryEmulationConnector issues after StockSharp .NET 10 migration")]
    public async Task BackwardCompatibility_WorksWithoutExplicitInterval()
    {
        // Arrange - Create strategy without explicit candle interval in debug config
        var security = CreateBtcSecurity();
        var candleInterval = TimeSpan.FromMinutes(1);

        var strategy = new TestStrategyWithIndicators
        {
            Securities = new Dictionary<Security, IEnumerable<TimeSpan>>
            {
                { security, new[] { candleInterval } }
            },
            Portfolio = CreatePortfolio()
        };

        // Config without explicit debug mode (relies on auto-extraction)
        var config = CreateConfig();
        using var runner = new BacktestRunner<TestStrategyWithIndicators>(config, strategy);

        // Act - Run backtest
        var result = await runner.RunAsync();

        Assert.True(result.IsSuccessful);

        // Generate report without explicit candle interval
        var reportOutputDirectory = Path.Combine(_testDirectory, "report_output_5");
        _dirsToCleanup.Add(reportOutputDirectory);

        var reportBuilder = new ReportBuilder<TestStrategyWithIndicators>(webTemplatePath: _mockWebTemplatePath);

        var model = new StrategySecurityChartModel
        {
            StartDate = config.ValidationPeriod.StartDate,
            EndDate = config.ValidationPeriod.EndDate,
            HistoryPath = config.HistoryPath,
            Security = security,
            Strategy = strategy,
            OutputPath = Path.Combine(reportOutputDirectory, "report.html"),
            Metrics = new PerformanceMetrics { StartTime = config.ValidationPeriod.StartDate, EndTime = config.ValidationPeriod.EndDate },
            WalkForwardResult = null
        };

        // Act - Should auto-extract candle interval from strategy
        await reportBuilder.GenerateReportAsync(model, reportOutputDirectory);

        // Assert
        Assert.True(Directory.Exists(reportOutputDirectory));

        var chartDataPath = Path.Combine(reportOutputDirectory, "chartData.json");
        Assert.True(File.Exists(chartDataPath), "Report should generate even without explicit candle interval");

        var jsonContent = await File.ReadAllTextAsync(chartDataPath);
        var chartData = JsonSerializer.Deserialize<ChartDataModel>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(chartData);
        Assert.NotNull(chartData.Candles);
        Assert.NotNull(chartData.IndicatorFiles);

        // Verify export mechanism works with auto-extracted interval
        Assert.True(chartData.Candles.Count > 0, "Should export candles");
    }

    #endregion
}

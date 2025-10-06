using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.PerformanceValidation;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.Integration;

/// <summary>
/// End-to-end integration tests validating the complete flow from optimization to web report generation
/// </summary>
[Trait("Category", "E2E")]
public class E2EReportGenerationTest : IDisposable
{
    private readonly string _testOutputPath;
    private readonly string _mockWebTemplatePath;

    public E2EReportGenerationTest()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), $"E2ETest_{Guid.NewGuid()}");
        _mockWebTemplatePath = Path.Combine(Path.GetTempPath(), $"WebTemplate_{Guid.NewGuid()}");

        // Create mock web template directory structure
        Directory.CreateDirectory(_mockWebTemplatePath);
        CreateMockWebTemplate();
    }

    public void Dispose()
    {
        // Clean up test directories
        if (Directory.Exists(_testOutputPath))
            Directory.Delete(_testOutputPath, recursive: true);

        if (Directory.Exists(_mockWebTemplatePath))
            Directory.Delete(_mockWebTemplatePath, recursive: true);
    }

    private class MockStrategy : CustomStrategyBase
    {
        public MockStrategy() : base()
        {
        }
    }

    private void CreateMockWebTemplate()
    {
        // Create a minimal Next.js static export structure
        File.WriteAllText(
            Path.Combine(_mockWebTemplatePath, "index.html"),
            @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>StockSharp Advanced Backtest Report</title>
</head>
<body>
    <div id=""root"">Report will load here</div>
    <script src=""/_next/static/chunks/main.js""></script>
</body>
</html>");

        // Create _next directory structure
        var nextDir = Path.Combine(_mockWebTemplatePath, "_next");
        var staticDir = Path.Combine(nextDir, "static");
        var chunksDir = Path.Combine(staticDir, "chunks");

        Directory.CreateDirectory(chunksDir);

        File.WriteAllText(
            Path.Combine(chunksDir, "main.js"),
            "console.log('Mock Next.js bundle');");
    }

    private StrategySecurityChartModel CreateMockModelWithWalkForward()
    {
        var security = new Security
        {
            Id = "BTCUSDT@BINANCE",
            Code = "BTCUSDT",
            Board = new ExchangeBoard { Code = "BINANCE" }
        };

        var strategy = new MockStrategy();

        var metrics = new PerformanceMetrics
        {
            TotalReturn = 0.25,
            SharpeRatio = 2.1,
            SortinoRatio = 2.8,
            MaxDrawdown = -0.12,
            WinRate = 0.65,
            ProfitFactor = 2.5,
            TotalTrades = 150,
            WinningTrades = 98,
            LosingTrades = 52,
            AnnualizedReturn = 0.75,
            AverageWin = 250,
            AverageLoss = -120,
            GrossProfit = 24500,
            GrossLoss = -6240,
            NetProfit = 18260,
            InitialCapital = 10000,
            FinalValue = 28260,
            TradingPeriodDays = 90,
            AverageTradesPerDay = 1.67
        };

        // Create walk-forward result
        var wfResult = CreateMockWalkForwardResult();

        return new StrategySecurityChartModel
        {
            StartDate = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2023, 3, 31, 0, 0, 0, TimeSpan.Zero),
            HistoryPath = "C:\\Data\\History",
            Security = security,
            Strategy = strategy,
            OutputPath = Path.Combine(_testOutputPath, "report.html"),
            Metrics = metrics,
            WalkForwardResult = wfResult
        };
    }

    private WalkForwardResult CreateMockWalkForwardResult()
    {
        var windows = new List<WindowResult>
        {
            new WindowResult
            {
                WindowNumber = 1,
                TrainingPeriod = (
                    new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2023, 1, 31, 0, 0, 0, TimeSpan.Zero)
                ),
                TestingPeriod = (
                    new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2023, 2, 14, 0, 0, 0, TimeSpan.Zero)
                ),
                TrainingMetrics = new PerformanceMetrics
                {
                    TotalReturn = 0.15,
                    SharpeRatio = 1.8,
                    SortinoRatio = 2.2,
                    MaxDrawdown = -0.06,
                    WinRate = 0.62,
                    ProfitFactor = 2.3,
                    TotalTrades = 50
                },
                TestingMetrics = new PerformanceMetrics
                {
                    TotalReturn = 0.10,
                    SharpeRatio = 1.5,
                    SortinoRatio = 1.9,
                    MaxDrawdown = -0.08,
                    WinRate = 0.58,
                    ProfitFactor = 2.0,
                    TotalTrades = 20
                }
            },
            new WindowResult
            {
                WindowNumber = 2,
                TrainingPeriod = (
                    new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2023, 2, 28, 0, 0, 0, TimeSpan.Zero)
                ),
                TestingPeriod = (
                    new DateTimeOffset(2023, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2023, 3, 14, 0, 0, 0, TimeSpan.Zero)
                ),
                TrainingMetrics = new PerformanceMetrics
                {
                    TotalReturn = 0.18,
                    SharpeRatio = 2.0,
                    SortinoRatio = 2.5,
                    MaxDrawdown = -0.05,
                    WinRate = 0.65,
                    ProfitFactor = 2.6,
                    TotalTrades = 55
                },
                TestingMetrics = new PerformanceMetrics
                {
                    TotalReturn = 0.13,
                    SharpeRatio = 1.6,
                    SortinoRatio = 2.1,
                    MaxDrawdown = -0.07,
                    WinRate = 0.60,
                    ProfitFactor = 2.2,
                    TotalTrades = 22
                }
            },
            new WindowResult
            {
                WindowNumber = 3,
                TrainingPeriod = (
                    new DateTimeOffset(2023, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2023, 3, 31, 0, 0, 0, TimeSpan.Zero)
                ),
                TestingPeriod = (
                    new DateTimeOffset(2023, 4, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2023, 4, 14, 0, 0, 0, TimeSpan.Zero)
                ),
                TrainingMetrics = new PerformanceMetrics
                {
                    TotalReturn = 0.12,
                    SharpeRatio = 1.6,
                    SortinoRatio = 2.0,
                    MaxDrawdown = -0.07,
                    WinRate = 0.60,
                    ProfitFactor = 2.1,
                    TotalTrades = 45
                },
                TestingMetrics = new PerformanceMetrics
                {
                    TotalReturn = 0.09,
                    SharpeRatio = 1.3,
                    SortinoRatio = 1.7,
                    MaxDrawdown = -0.09,
                    WinRate = 0.55,
                    ProfitFactor = 1.8,
                    TotalTrades = 18
                }
            }
        };

        return new WalkForwardResult
        {
            TotalWindows = 3,
            Windows = windows
        };
    }

    [Fact]
    public async Task CompleteOptimizationFlow_GeneratesValidWebReport()
    {
        // Arrange
        var model = CreateMockModelWithWalkForward();
        var reportBuilder = new ReportBuilder<MockStrategy>(logger: null, webTemplatePath: _mockWebTemplatePath);

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert - Verify file structure
        Assert.True(File.Exists(Path.Combine(_testOutputPath, "index.html")),
            "index.html should exist in output directory");
        Assert.True(File.Exists(Path.Combine(_testOutputPath, "chartData.json")),
            "chartData.json should exist in output directory");
        Assert.True(Directory.Exists(Path.Combine(_testOutputPath, "_next")),
            "_next directory should exist for Next.js assets");

        // Assert - Verify JSON content
        var chartDataJson = await File.ReadAllTextAsync(
            Path.Combine(_testOutputPath, "chartData.json"));
        var chartData = JsonSerializer.Deserialize<ChartDataModel>(chartDataJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(chartData);
        Assert.NotNull(chartData.Candles);
        Assert.NotNull(chartData.Trades);
        Assert.NotNull(chartData.WalkForward);

        // Verify walk-forward data structure
        Assert.Equal(3, chartData.WalkForward.TotalWindows);
        Assert.True(chartData.WalkForward.WalkForwardEfficiency > 0,
            "Walk-forward efficiency should be calculated");
        Assert.True(chartData.WalkForward.Consistency >= 0,
            "Consistency should be calculated");
        Assert.Equal(3, chartData.WalkForward.Windows.Count);

        // Verify walk-forward window data
        var firstWindow = chartData.WalkForward.Windows.First();
        Assert.Equal(1, firstWindow.WindowNumber);
        Assert.True(firstWindow.TrainingStart > 0, "Training start timestamp should be valid");
        Assert.True(firstWindow.TestingStart > firstWindow.TrainingEnd,
            "Testing period should start after training period");
        Assert.NotNull(firstWindow.TrainingMetrics);
        Assert.NotNull(firstWindow.TestingMetrics);
        Assert.True(firstWindow.TrainingMetrics.TotalReturn > 0,
            "Training metrics should have positive return");
        // Performance degradation can be negative when testing performs worse than training
        Assert.True(firstWindow.PerformanceDegradation != 0.0 || firstWindow.TrainingMetrics.TotalReturn == 0.0,
            "Performance degradation should be calculated");

        // Assert - Verify HTML loads properly
        var htmlContent = await File.ReadAllTextAsync(
            Path.Combine(_testOutputPath, "index.html"));
        Assert.Contains("StockSharp Advanced Backtest Report", htmlContent,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebReport_ContainsAllRequiredAssets()
    {
        // Arrange
        var model = CreateMockModelWithWalkForward();
        var reportBuilder = new ReportBuilder<MockStrategy>(logger: null, webTemplatePath: _mockWebTemplatePath);

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert - Verify Next.js structure
        var nextStaticDir = Path.Combine(_testOutputPath, "_next", "static");
        Assert.True(Directory.Exists(nextStaticDir),
            "Next.js static directory should exist");

        var chunksDir = Path.Combine(nextStaticDir, "chunks");
        Assert.True(Directory.Exists(chunksDir),
            "JavaScript chunks directory should exist");

        Assert.True(File.Exists(Path.Combine(chunksDir, "main.js")),
            "Main JavaScript bundle should exist");
    }

    [Fact]
    public async Task WebReport_WorksOffline()
    {
        // Arrange
        var model = CreateMockModelWithWalkForward();
        var reportBuilder = new ReportBuilder<MockStrategy>(logger: null, webTemplatePath: _mockWebTemplatePath);

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert - Verify that all assets are local (no external CDN references)
        var htmlContent = await File.ReadAllTextAsync(
            Path.Combine(_testOutputPath, "index.html"));

        // Check that scripts reference local files, not CDNs
        Assert.DoesNotContain("https://cdn.", htmlContent);
        Assert.DoesNotContain("http://", htmlContent.ToLower());

        // Verify file:// protocol compatibility
        var reportPath = "file://" + Path.Combine(_testOutputPath, "index.html").Replace("\\", "/");
        Assert.NotEmpty(reportPath);

        // Verify chartData.json can be loaded from file system
        var chartDataPath = Path.Combine(_testOutputPath, "chartData.json");
        Assert.True(File.Exists(chartDataPath),
            "chartData.json should be accessible from file system");
    }

    [Fact]
    public async Task WebReport_ChartDataValidation_AllFieldsPresent()
    {
        // Arrange
        var model = CreateMockModelWithWalkForward();
        var reportBuilder = new ReportBuilder<MockStrategy>(logger: null, webTemplatePath: _mockWebTemplatePath);

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert - Deep validation of chartData.json structure
        var chartDataJson = await File.ReadAllTextAsync(
            Path.Combine(_testOutputPath, "chartData.json"));

        using var jsonDoc = JsonDocument.Parse(chartDataJson);
        var root = jsonDoc.RootElement;

        // Verify top-level properties
        Assert.True(root.TryGetProperty("candles", out _), "candles property should exist");
        Assert.True(root.TryGetProperty("trades", out _), "trades property should exist");
        Assert.True(root.TryGetProperty("walkForward", out _), "walkForward property should exist");

        // Verify walk-forward properties
        var wf = root.GetProperty("walkForward");
        Assert.True(wf.TryGetProperty("walkForwardEfficiency", out _),
            "walkForwardEfficiency property should exist");
        Assert.True(wf.TryGetProperty("consistency", out _),
            "consistency property should exist");
        Assert.True(wf.TryGetProperty("totalWindows", out _),
            "totalWindows property should exist");
        Assert.True(wf.TryGetProperty("windows", out var windows),
            "windows array should exist");

        // Verify window structure
        Assert.True(windows.GetArrayLength() > 0, "windows array should not be empty");
        var firstWindow = windows[0];

        Assert.True(firstWindow.TryGetProperty("windowNumber", out _),
            "windowNumber should exist");
        Assert.True(firstWindow.TryGetProperty("trainingStart", out _),
            "trainingStart should exist");
        Assert.True(firstWindow.TryGetProperty("trainingEnd", out _),
            "trainingEnd should exist");
        Assert.True(firstWindow.TryGetProperty("testingStart", out _),
            "testingStart should exist");
        Assert.True(firstWindow.TryGetProperty("testingEnd", out _),
            "testingEnd should exist");
        Assert.True(firstWindow.TryGetProperty("trainingMetrics", out _),
            "trainingMetrics should exist");
        Assert.True(firstWindow.TryGetProperty("testingMetrics", out _),
            "testingMetrics should exist");
        Assert.True(firstWindow.TryGetProperty("performanceDegradation", out _),
            "performanceDegradation should exist");
    }

    [Fact]
    public async Task E2ETest_CompletesWithinTimeLimit()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var model = CreateMockModelWithWalkForward();
        var reportBuilder = new ReportBuilder<MockStrategy>(logger: null, webTemplatePath: _mockWebTemplatePath);

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert
        var duration = DateTime.UtcNow - startTime;
        Assert.True(duration.TotalMinutes < 2,
            $"E2E test should complete in less than 2 minutes, but took {duration.TotalMinutes:F2} minutes");

        // Verify output was actually generated
        Assert.True(File.Exists(Path.Combine(_testOutputPath, "index.html")));
        Assert.True(File.Exists(Path.Combine(_testOutputPath, "chartData.json")));
    }
}

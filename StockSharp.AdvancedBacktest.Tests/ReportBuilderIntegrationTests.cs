using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests;

public class ReportBuilderIntegrationTests : IDisposable
{
    private readonly string _testOutputPath;
    private readonly string _mockWebTemplatePath;

    public ReportBuilderIntegrationTests()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), $"ReportBuilderTest_{Guid.NewGuid()}");
        _mockWebTemplatePath = Path.Combine(Path.GetTempPath(), $"WebTemplate_{Guid.NewGuid()}");

        // Create mock web template directory with index.html
        Directory.CreateDirectory(_mockWebTemplatePath);
        File.WriteAllText(Path.Combine(_mockWebTemplatePath, "index.html"), "<html><body>Mock Template</body></html>");
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

    private StrategySecurityChartModel CreateMockModel()
    {
        var security = new Security
        {
            Id = "AAPL@NASDAQ",
            Code = "AAPL",
            Board = new ExchangeBoard { Code = "NASDAQ" }
        };

        var strategy = new MockStrategy();

        var metrics = new PerformanceMetrics
        {
            TotalReturn = 0.15,
            SharpeRatio = 1.5,
            MaxDrawdown = -0.08
        };

        return new StrategySecurityChartModel
        {
            StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            HistoryPath = "C:\\Data\\History",
            Security = security,
            Strategy = strategy,
            OutputPath = Path.Combine(_testOutputPath, "report.html"),
            Metrics = metrics,
            WalkForwardResult = null
        };
    }

    [Fact]
    public async Task GenerateReportAsync_CreatesOutputDirectory()
    {
        // Arrange
        var reportBuilder = new ReportBuilder<MockStrategy>(webTemplatePath: _mockWebTemplatePath);
        var model = CreateMockModel();

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert
        Assert.True(Directory.Exists(_testOutputPath), "Output directory should be created");
    }

    [Fact]
    public async Task GenerateReportAsync_WritesChartDataJson()
    {
        // Arrange
        var reportBuilder = new ReportBuilder<MockStrategy>(webTemplatePath: _mockWebTemplatePath);
        var model = CreateMockModel();

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert
        var chartDataPath = Path.Combine(_testOutputPath, "chartData.json");
        Assert.True(File.Exists(chartDataPath), "chartData.json should be created");

        // Verify JSON structure
        var jsonContent = await File.ReadAllTextAsync(chartDataPath);
        var chartData = JsonSerializer.Deserialize<ChartDataModel>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(chartData);
        Assert.NotNull(chartData.Candles);
        Assert.NotNull(chartData.Trades);
    }

    [Fact]
    public async Task GenerateReportAsync_CopiesIndexHtml()
    {
        // Arrange
        var reportBuilder = new ReportBuilder<MockStrategy>(webTemplatePath: _mockWebTemplatePath);
        var model = CreateMockModel();

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert
        var indexPath = Path.Combine(_testOutputPath, "index.html");
        Assert.True(File.Exists(indexPath), "index.html should be copied from template");

        var content = await File.ReadAllTextAsync(indexPath);
        Assert.Contains("Mock Template", content);
    }

    [Fact]
    public async Task GenerateReportAsync_ThrowsWhenTemplateNotFound()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid()}");
        var reportBuilder = new ReportBuilder<MockStrategy>(webTemplatePath: nonExistentPath);
        var model = CreateMockModel();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reportBuilder.GenerateReportAsync(model, _testOutputPath));
    }

    [Fact]
    public async Task GenerateReportAsync_SkipsChartDataJsonFromTemplate()
    {
        // Arrange
        // Add a chartData.json to the template
        var templateChartData = new { test = "template data" };
        await File.WriteAllTextAsync(
            Path.Combine(_mockWebTemplatePath, "chartData.json"),
            JsonSerializer.Serialize(templateChartData));

        var reportBuilder = new ReportBuilder<MockStrategy>(webTemplatePath: _mockWebTemplatePath);
        var model = CreateMockModel();

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert
        var chartDataPath = Path.Combine(_testOutputPath, "chartData.json");
        var jsonContent = await File.ReadAllTextAsync(chartDataPath);

        // Should contain generated data, not template data
        Assert.DoesNotContain("template data", jsonContent);
        Assert.Contains("candles", jsonContent);
    }

    [Fact]
    public async Task GenerateReportAsync_WorksWithNullLogger()
    {
        // Arrange
        var reportBuilder = new ReportBuilder<MockStrategy>(logger: null, webTemplatePath: _mockWebTemplatePath);
        var model = CreateMockModel();

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert - test should complete without exceptions when logger is null
        Assert.True(File.Exists(Path.Combine(_testOutputPath, "index.html")));
    }

    [Fact]
    public async Task GenerateReportAsync_HandlesSubdirectories()
    {
        // Arrange
        var subDir = Path.Combine(_mockWebTemplatePath, "_next", "static");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "test.js"), "console.log('test');");

        var reportBuilder = new ReportBuilder<MockStrategy>(webTemplatePath: _mockWebTemplatePath);
        var model = CreateMockModel();

        // Act
        await reportBuilder.GenerateReportAsync(model, _testOutputPath);

        // Assert
        var copiedFilePath = Path.Combine(_testOutputPath, "_next", "static", "test.js");
        Assert.True(File.Exists(copiedFilePath), "Subdirectory files should be copied");
    }
}

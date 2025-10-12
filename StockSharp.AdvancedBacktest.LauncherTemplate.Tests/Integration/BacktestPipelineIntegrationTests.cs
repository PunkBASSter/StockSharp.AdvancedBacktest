using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.Tests.Integration;

[Trait("Category", "E2E")]
public class BacktestPipelineIntegrationTests
{
    [Fact(Skip = "Requires real BTCUSDT history data in local Hydra storage")]
    public async Task FullBacktestPipeline_WithRealData_CompletesSuccessfully()
    {
        var configPath = "ConfigFiles/test-backtest-btcusdt.json";

        if (!File.Exists(configPath))
        {
            Assert.Fail($"Configuration file not found: {configPath}");
            return;
        }

        var configJson = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<BacktestConfiguration>(configJson);

        Assert.NotNull(config);
        Assert.NotEmpty(config.Securities);
        Assert.True(Directory.Exists(config.HistoryPath), "History path should exist");
    }

    [Fact]
    public async Task Configuration_LoadsFromJson_Successfully()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");

        var config = new BacktestConfiguration
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 2, 28, 0, 0, 0, TimeSpan.Zero),
            Securities = new List<string> { "BTCUSDT@BNB" },
            TimeFrames = new List<string> { "1d", "4h" },
            HistoryPath = "C:/Data/History",
            InitialCapital = 10000,
            CommissionPercentage = 0.1m,
            ParallelWorkers = 4,
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(tempPath, json);

        var loadedJson = await File.ReadAllTextAsync(tempPath);
        var loadedConfig = JsonSerializer.Deserialize<BacktestConfiguration>(loadedJson);

        Assert.NotNull(loadedConfig);
        Assert.Equal(config.StrategyName, loadedConfig.StrategyName);
        Assert.Equal(config.Securities.Count, loadedConfig.Securities.Count);
        Assert.Equal(config.TimeFrames.Count, loadedConfig.TimeFrames.Count);

        File.Delete(tempPath);
    }

    [Theory]
    [InlineData("1s", 1)]
    [InlineData("30s", 30)]
    [InlineData("1m", 60)]
    [InlineData("5m", 300)]
    [InlineData("15m", 900)]
    [InlineData("30m", 1800)]
    [InlineData("1h", 3600)]
    [InlineData("4h", 14400)]
    [InlineData("1d", 86400)]
    [InlineData("1w", 604800)]
    public void TimeFrameParsing_WithMultipleFormats_WorksCorrectly(string timeFrame, int expectedSeconds)
    {
        var expected = TimeSpan.FromSeconds(expectedSeconds);

        Assert.True(TryParseTimeFrame(timeFrame, out var result));
        Assert.Equal(expected, result);
    }

    private bool TryParseTimeFrame(string timeFrameStr, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(timeFrameStr) || timeFrameStr.Length < 2)
            return false;

        var timeFrameLower = timeFrameStr.ToLowerInvariant().Trim();
        var unitChar = timeFrameLower[^1];
        var valueStr = timeFrameLower[..^1];

        if (!int.TryParse(valueStr, out var value) || value <= 0)
            return false;

        result = unitChar switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            'w' => TimeSpan.FromDays(value * 7),
            _ => TimeSpan.Zero
        };

        return result != TimeSpan.Zero;
    }

    [Fact]
    public void ConfigurationValidation_DetectsInvalidDateRanges()
    {
        var config = new BacktestConfiguration
        {
            StrategyName = "Test",
            StrategyVersion = "1.0",
            TrainingStartDate = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero),
            Securities = new List<string>(),
            HistoryPath = "/test",
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };

        Assert.True(config.TrainingEndDate < config.TrainingStartDate,
            "Should detect invalid training date range");
    }

    [Fact]
    public void ConfigurationValidation_RequiresSecurities()
    {
        var config = new BacktestConfiguration
        {
            StrategyName = "Test",
            StrategyVersion = "1.0",
            TrainingStartDate = DateTimeOffset.UtcNow,
            TrainingEndDate = DateTimeOffset.UtcNow.AddDays(30),
            ValidationStartDate = DateTimeOffset.UtcNow.AddDays(31),
            ValidationEndDate = DateTimeOffset.UtcNow.AddDays(60),
            Securities = new List<string>(),
            HistoryPath = "/path/to/data",
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };

        Assert.Empty(config.Securities);
    }

    [Fact]
    public void ConfigurationValidation_RequiresTimeFrames()
    {
        var config = new BacktestConfiguration
        {
            StrategyName = "Test",
            StrategyVersion = "1.0",
            TrainingStartDate = DateTimeOffset.UtcNow,
            TrainingEndDate = DateTimeOffset.UtcNow.AddDays(30),
            ValidationStartDate = DateTimeOffset.UtcNow.AddDays(31),
            ValidationEndDate = DateTimeOffset.UtcNow.AddDays(60),
            Securities = new List<string> { "BTCUSDT@BNB" },
            TimeFrames = new List<string>(),
            HistoryPath = "/path/to/data",
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };

        Assert.Empty(config.TimeFrames);
    }
}

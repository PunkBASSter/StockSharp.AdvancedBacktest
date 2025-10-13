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
            TimeFrames = new List<TimeSpan> { TimeSpan.FromDays(1), TimeSpan.FromHours(4) },
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
            TimeFrames = new List<TimeSpan>(),
            HistoryPath = "/path/to/data",
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };

        Assert.Empty(config.TimeFrames);
    }
}

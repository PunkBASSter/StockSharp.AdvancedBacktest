using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;

namespace StockSharp.AdvancedBacktest.Tests.Integration;

[Trait("Category", "Integration")]
public class BacktestRunnerIntegrationTests
{
    private readonly string _testConfigPath;

    public BacktestRunnerIntegrationTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
    }

    private BacktestConfiguration CreateMinimalConfig()
    {
        return new BacktestConfiguration
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 2, 28, 0, 0, 0, TimeSpan.Zero),
            Securities = new List<string> { "BTCUSDT@BNB" },
            TimeFrames = new List<TimeSpan> { TimeSpan.FromDays(1) },
            OptimizableParameters = new Dictionary<string, ParameterDefinition>
            {
                ["TestParam"] = new ParameterDefinition
                {
                    Name = "TestParam",
                    Type = "int",
                    MinValue = JsonSerializer.SerializeToElement(1),
                    MaxValue = JsonSerializer.SerializeToElement(5),
                    StepValue = JsonSerializer.SerializeToElement(1),
                    DefaultValue = JsonSerializer.SerializeToElement(3)
                }
            },
            HistoryPath = Path.GetTempPath(),
            InitialCapital = 10000,
            CommissionPercentage = 0.1m,
            ParallelWorkers = 1
        };
    }


    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BacktestRunner<PreviousWeekRangeBreakoutStrategy>(null!));
    }

    [Fact]
    public void Constructor_WithValidConfig_SetsProperties()
    {
        var config = CreateMinimalConfig();
        config.ParallelWorkers = 4;

        var runner = new BacktestRunner<PreviousWeekRangeBreakoutStrategy>(config);

        Assert.Equal(4, runner.ParallelThreads);
        Assert.Equal("./output", runner.OutputDirectory);
        Assert.False(runner.VerboseLogging);
    }

    [Fact]
    public void Configuration_CanBeSerializedAndDeserialized()
    {
        var config = CreateMinimalConfig();
        var json = JsonSerializer.Serialize(config);

        Assert.NotNull(json);
        Assert.NotEmpty(json);

        var deserialized = JsonSerializer.Deserialize<BacktestConfiguration>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(config.StrategyName, deserialized.StrategyName);
        Assert.Equal(config.Securities.Count, deserialized.Securities.Count);
    }
}

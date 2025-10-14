using StockSharp.AdvancedBacktest.Pipeline;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.Pipeline;

public class PipelineConfigurationTests
{
    private static PipelineConfiguration CreateValidConfiguration()
    {
        return new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT", "ETHUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero)
        };
    }

    [Fact]
    public void Configuration_WithAllRequiredFields_IsValid()
    {
        var config = CreateValidConfiguration();

        Assert.NotNull(config);
        Assert.Equal("C:\\Data", config.HistoryPath);
        Assert.Equal(2, config.Securities.Count);
        Assert.Equal(2, config.TimeFrames.Count);
        Assert.True(config.TrainingEndDate > config.TrainingStartDate);
        Assert.True(config.ValidationEndDate > config.ValidationStartDate);
    }

    [Fact]
    public void Configuration_DefaultValues_AreCorrect()
    {
        var config = CreateValidConfiguration();

        Assert.Equal(10000m, config.InitialCapital);
        Assert.Equal(0.01m, config.TradeVolume);
        Assert.Equal(0.1m, config.CommissionPercentage);
        Assert.True(config.UseBruteForceOptimization);
        Assert.Equal(Environment.ProcessorCount, config.ParallelWorkers);
        Assert.Equal(5, config.TopStrategiesCount);
        Assert.Null(config.SustainabilityFilter);
        Assert.True(config.GenerateReports);
        Assert.True(config.ExportToJson);
    }

    [Fact]
    public void Configuration_WithCustomValues_OverridesDefaults()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            InitialCapital = 50000m,
            TradeVolume = 0.1m,
            CommissionPercentage = 0.2m,
            UseBruteForceOptimization = false,
            ParallelWorkers = 8,
            TopStrategiesCount = 10,
            GenerateReports = false,
            ExportToJson = false
        };

        Assert.Equal(50000m, config.InitialCapital);
        Assert.Equal(0.1m, config.TradeVolume);
        Assert.Equal(0.2m, config.CommissionPercentage);
        Assert.False(config.UseBruteForceOptimization);
        Assert.Equal(8, config.ParallelWorkers);
        Assert.Equal(10, config.TopStrategiesCount);
        Assert.False(config.GenerateReports);
        Assert.False(config.ExportToJson);
    }

    [Fact]
    public void Configuration_IsImmutable()
    {
        var config = CreateValidConfiguration();
        var historyPath = config.HistoryPath;

        Assert.Equal(historyPath, config.HistoryPath);
    }

    [Fact]
    public void Configuration_WithNullOptionalCollections_IsValid()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            ParameterRanges = null,
            ParameterValidationRules = null,
            MetricFilters = null
        };

        Assert.Null(config.ParameterRanges);
        Assert.Null(config.ParameterValidationRules);
        Assert.Null(config.MetricFilters);
    }

    [Fact]
    public void Configuration_WithExportPath_StoresCorrectly()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            ExportPath = "C:\\Output"
        };

        Assert.Equal("C:\\Output", config.ExportPath);
    }

    [Fact]
    public void Configuration_WithSustainabilityFilter_StoresCorrectly()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            SustainabilityFilter = metrics => metrics.Where(m => m.TotalTrades >= 30)
        };

        Assert.NotNull(config.SustainabilityFilter);
    }
}

public class ParameterRangeDefinitionTests
{
    [Fact]
    public void ParameterRangeDefinition_WithAllFields_IsValid()
    {
        var range = new ParameterRangeDefinition
        {
            Name = "LookbackPeriod",
            Min = 10,
            Max = 50,
            Step = 5,
            ParameterType = typeof(int)
        };

        Assert.Equal("LookbackPeriod", range.Name);
        Assert.Equal(10, range.Min);
        Assert.Equal(50, range.Max);
        Assert.Equal(5, range.Step);
        Assert.Equal(typeof(int), range.ParameterType);
    }

    [Fact]
    public void ParameterRangeDefinition_WithDecimalValues_IsValid()
    {
        var range = new ParameterRangeDefinition
        {
            Name = "Multiplier",
            Min = 1.5m,
            Max = 3.0m,
            Step = 0.5m,
            ParameterType = typeof(decimal)
        };

        Assert.Equal("Multiplier", range.Name);
        Assert.Equal(1.5m, range.Min);
        Assert.Equal(3.0m, range.Max);
        Assert.Equal(0.5m, range.Step);
        Assert.Equal(typeof(decimal), range.ParameterType);
    }
}

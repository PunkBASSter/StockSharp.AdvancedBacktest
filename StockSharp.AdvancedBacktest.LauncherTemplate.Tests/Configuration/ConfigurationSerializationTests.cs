using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.Validation;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.Configuration;

public class ConfigurationSerializationTests
{
    [Fact]
    public void RiskLimitsConfig_SerializesAndDeserializes()
    {
        var config = new RiskLimitsConfig
        {
            MaxPositionSize = 5000m,
            MaxDailyLoss = 1000m,
            MaxDailyLossIsPercentage = true,
            MaxDrawdownPercentage = 15m,
            MaxTradesPerDay = 50,
            CircuitBreakerEnabled = true,
            CircuitBreakerThresholdPercentage = 10m,
            CircuitBreakerCooldownMinutes = 60
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<RiskLimitsConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(config.MaxPositionSize, deserialized.MaxPositionSize);
        Assert.Equal(config.MaxDailyLoss, deserialized.MaxDailyLoss);
        Assert.Equal(config.CircuitBreakerEnabled, deserialized.CircuitBreakerEnabled);
    }

    [Fact]
    public void StrategyParametersConfig_SerializesAndDeserializes()
    {
        var config = new StrategyParametersConfig
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            StrategyHash = "abc123def456abc123def456abc12345",
            OptimizationDate = DateTimeOffset.UtcNow,
            Parameters = new Dictionary<string, JsonElement>
            {
                ["Period"] = JsonSerializer.SerializeToElement(20),
                ["Threshold"] = JsonSerializer.SerializeToElement(0.5)
            },
            InitialCapital = 50000m,
            TradeVolume = 1.0m,
            Securities = ["AAPL", "MSFT"]
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<StrategyParametersConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(config.StrategyName, deserialized.StrategyName);
        Assert.Equal(config.Parameters.Count, deserialized.Parameters.Count);
        Assert.Equal(2, deserialized.Securities.Count);
    }

    [Fact]
    public void BacktestConfiguration_SerializesAndDeserializes()
    {
        var config = new BacktestConfiguration
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            Securities = ["AAPL"],
            OptimizableParameters = new Dictionary<string, ParameterDefinition>
            {
                ["Period"] = new ParameterDefinition
                {
                    Name = "Period",
                    Type = "int",
                    MinValue = JsonSerializer.SerializeToElement(10),
                    MaxValue = JsonSerializer.SerializeToElement(50),
                    StepValue = JsonSerializer.SerializeToElement(5)
                }
            },
            HistoryPath = "C:\\Data\\History",
            WalkForwardConfig = new WalkForwardConfig
            {
                WindowSize = TimeSpan.FromDays(90),
                StepSize = TimeSpan.FromDays(30),
                ValidationSize = TimeSpan.FromDays(30)
            }
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<BacktestConfiguration>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(config.StrategyName, deserialized.StrategyName);
        Assert.NotNull(deserialized.WalkForwardConfig);
        Assert.Equal(config.WalkForwardConfig.WindowSize, deserialized.WalkForwardConfig.WindowSize);
    }

    [Fact]
    public void LiveTradingConfiguration_SerializesAndDeserializes()
    {
        var config = new LiveTradingConfiguration
        {
            StrategyConfigPath = "C:\\Config\\strategy.json",
            BrokerConfigPath = "C:\\Config\\broker.json",
            RiskLimits = new RiskLimitsConfig
            {
                MaxPositionSize = 10000m,
                MaxDailyLoss = 2000m
            },
            EnableAlerts = true,
            AlertEmail = "trader@example.com",
            TradingSessions =
            [
                new TradingSession
                {
                    Name = "US Market Hours",
                    StartTime = new TimeOnly(9, 30),
                    EndTime = new TimeOnly(16, 0),
                    DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]
                }
            ]
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<LiveTradingConfiguration>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(config.StrategyConfigPath, deserialized.StrategyConfigPath);
        Assert.NotNull(deserialized.RiskLimits);
        Assert.Single(deserialized.TradingSessions);
        Assert.Equal("US Market Hours", deserialized.TradingSessions[0].Name);
    }
}

using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.Validation;
using Xunit;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.BacktestMode;

public class ConfigurationLoaderTests : IDisposable
{
    private readonly string _testDirectory;

    public ConfigurationLoaderTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ConfigLoaderTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region LoadBacktestConfigAsync Tests

    [Fact]
    public async Task LoadBacktestConfigAsync_ValidFile_LoadsSuccessfully()
    {
        // Arrange
        var config = CreateValidBacktestConfiguration();
        var filePath = Path.Combine(_testDirectory, "backtest.json");
        await SaveToFile(config, filePath);

        // Act
        var loaded = await ConfigurationLoader.LoadBacktestConfigAsync(filePath);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(config.StrategyName, loaded.StrategyName);
        Assert.Equal(config.StrategyVersion, loaded.StrategyVersion);
        Assert.Equal(config.Securities.Count, loaded.Securities.Count);
        Assert.Equal(config.TrainingStartDate, loaded.TrainingStartDate);
        Assert.Equal(config.OptimizableParameters.Count, loaded.OptimizableParameters.Count);
    }

    [Fact]
    public async Task LoadBacktestConfigAsync_FileNotFound_ThrowsConfigurationLoadException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadBacktestConfigAsync(filePath));

        Assert.Contains("not found", exception.Message);
        Assert.Contains(filePath, exception.Message);
    }

    [Fact]
    public async Task LoadBacktestConfigAsync_InvalidJson_ThrowsConfigurationLoadException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "invalid.json");
        await File.WriteAllTextAsync(filePath, "{ this is not valid json }");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadBacktestConfigAsync(filePath));

        Assert.Contains("Invalid JSON", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task LoadBacktestConfigAsync_EmptyFile_ThrowsConfigurationLoadException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "empty.json");
        await File.WriteAllTextAsync(filePath, "");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadBacktestConfigAsync(filePath));

        Assert.Contains("Invalid JSON", exception.Message);
    }

    [Fact]
    public async Task LoadBacktestConfigAsync_NullContent_ThrowsConfigurationLoadException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "null.json");
        await File.WriteAllTextAsync(filePath, "null");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadBacktestConfigAsync(filePath));

        Assert.Contains("Failed to deserialize", exception.Message);
    }

    [Fact]
    public async Task LoadBacktestConfigAsync_NullOrWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => ConfigurationLoader.LoadBacktestConfigAsync(""));

        await Assert.ThrowsAsync<ArgumentException>(
            () => ConfigurationLoader.LoadBacktestConfigAsync("   "));
    }

    [Fact]
    public async Task LoadBacktestConfigAsync_InvalidTrainingDates_ThrowsConfigurationLoadException()
    {
        // Arrange
        var config = CreateValidBacktestConfiguration();
        config.TrainingEndDate = config.TrainingStartDate.AddDays(-1); // Invalid: end before start
        var filePath = Path.Combine(_testDirectory, "invalid_dates.json");
        await SaveToFile(config, filePath);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadBacktestConfigAsync(filePath));

        Assert.Contains("validation failed", exception.Message);
        Assert.Contains("Training end date must be after training start date", exception.Message);
    }

    [Fact]
    public async Task LoadBacktestConfigAsync_NoSecurities_ThrowsConfigurationLoadException()
    {
        // Arrange
        var config = CreateValidBacktestConfiguration();
        config.Securities = [];
        var filePath = Path.Combine(_testDirectory, "no_securities.json");
        await SaveToFile(config, filePath);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadBacktestConfigAsync(filePath));

        Assert.Contains("At least one security must be specified", exception.Message);
    }

    [Fact]
    public async Task LoadBacktestConfigAsync_WithWalkForward_LoadsSuccessfully()
    {
        // Arrange
        var config = CreateValidBacktestConfiguration();
        config.WalkForwardConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(90),
            StepSize = TimeSpan.FromDays(30),
            ValidationSize = TimeSpan.FromDays(30),
            Mode = WindowGenerationMode.Anchored
        };
        var filePath = Path.Combine(_testDirectory, "walkforward.json");
        await SaveToFile(config, filePath);

        // Act
        var loaded = await ConfigurationLoader.LoadBacktestConfigAsync(filePath);

        // Assert
        Assert.NotNull(loaded.WalkForwardConfig);
        Assert.Equal(TimeSpan.FromDays(90), loaded.WalkForwardConfig.WindowSize);
    }

    #endregion

    #region LoadStrategyConfigAsync Tests

    [Fact]
    public async Task LoadStrategyConfigAsync_ValidFile_LoadsSuccessfully()
    {
        // Arrange
        var config = CreateValidStrategyConfiguration();
        var filePath = Path.Combine(_testDirectory, "strategy.json");
        await SaveToFile(config, filePath);

        // Act
        var loaded = await ConfigurationLoader.LoadStrategyConfigAsync(filePath);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(config.StrategyName, loaded.StrategyName);
        Assert.Equal(config.StrategyVersion, loaded.StrategyVersion);
        Assert.Equal(config.StrategyHash, loaded.StrategyHash);
        Assert.Equal(config.Parameters.Count, loaded.Parameters.Count);
    }

    [Fact]
    public async Task LoadStrategyConfigAsync_FileNotFound_ThrowsConfigurationLoadException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent_strategy.json");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadStrategyConfigAsync(filePath));

        Assert.Contains("not found", exception.Message);
        Assert.Contains(filePath, exception.Message);
    }

    [Fact]
    public async Task LoadStrategyConfigAsync_InvalidJson_ThrowsConfigurationLoadException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "invalid_strategy.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json content");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadStrategyConfigAsync(filePath));

        Assert.Contains("Invalid JSON", exception.Message);
    }

    [Fact]
    public async Task LoadStrategyConfigAsync_NoParameters_ThrowsConfigurationLoadException()
    {
        // Arrange
        var config = CreateValidStrategyConfiguration();
        config.Parameters = new Dictionary<string, JsonElement>();
        var filePath = Path.Combine(_testDirectory, "no_params.json");
        await SaveToFile(config, filePath);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadStrategyConfigAsync(filePath));

        Assert.Contains("At least one parameter must be specified", exception.Message);
    }

    [Fact]
    public async Task LoadStrategyConfigAsync_InvalidInitialCapital_ThrowsConfigurationLoadException()
    {
        // Arrange
        var config = CreateValidStrategyConfiguration();
        config.InitialCapital = -100m;
        var filePath = Path.Combine(_testDirectory, "invalid_capital.json");
        await SaveToFile(config, filePath);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadStrategyConfigAsync(filePath));

        Assert.Contains("Initial capital must be greater than 0", exception.Message);
    }

    #endregion

    #region LoadLiveConfigAsync Tests

    [Fact]
    public async Task LoadLiveConfigAsync_ValidFile_LoadsSuccessfully()
    {
        // Arrange
        var config = CreateValidLiveConfiguration();
        var filePath = Path.Combine(_testDirectory, "live.json");
        await SaveToFile(config, filePath);

        // Act
        var loaded = await ConfigurationLoader.LoadLiveConfigAsync(filePath);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(config.StrategyConfigPath, loaded.StrategyConfigPath);
        Assert.Equal(config.BrokerConfigPath, loaded.BrokerConfigPath);
        Assert.NotNull(loaded.RiskLimits);
        Assert.Equal(config.RiskLimits.MaxPositionSize, loaded.RiskLimits.MaxPositionSize);
    }

    [Fact]
    public async Task LoadLiveConfigAsync_FileNotFound_ThrowsConfigurationLoadException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent_live.json");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadLiveConfigAsync(filePath));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task LoadLiveConfigAsync_InvalidJson_ThrowsConfigurationLoadException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "invalid_live.json");
        await File.WriteAllTextAsync(filePath, "not json at all!");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadLiveConfigAsync(filePath));

        Assert.Contains("Invalid JSON", exception.Message);
    }

    [Fact]
    public async Task LoadLiveConfigAsync_NullRiskLimits_ThrowsConfigurationLoadException()
    {
        // Arrange
        var json = @"{
            ""strategyConfigPath"": ""C:\\config\\strategy.json"",
            ""brokerConfigPath"": ""C:\\config\\broker.json"",
            ""riskLimits"": null
        }";
        var filePath = Path.Combine(_testDirectory, "null_risk.json");
        await File.WriteAllTextAsync(filePath, json);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConfigurationLoadException>(
            () => ConfigurationLoader.LoadLiveConfigAsync(filePath));

        Assert.Contains("Risk limits must be specified", exception.Message);
    }

    [Fact]
    public async Task LoadLiveConfigAsync_WithTradingSessions_LoadsSuccessfully()
    {
        // Arrange
        var config = CreateValidLiveConfiguration();
        config.TradingSessions =
        [
            new TradingSession
            {
                Name = "US Market Hours",
                StartTime = new TimeOnly(9, 30),
                EndTime = new TimeOnly(16, 0),
                DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                Enabled = true
            }
        ];
        var filePath = Path.Combine(_testDirectory, "sessions.json");
        await SaveToFile(config, filePath);

        // Act
        var loaded = await ConfigurationLoader.LoadLiveConfigAsync(filePath);

        // Assert
        Assert.Single(loaded.TradingSessions);
        Assert.Equal("US Market Hours", loaded.TradingSessions[0].Name);
    }

    [Fact]
    public async Task LoadLiveConfigAsync_CancellationToken_CanBeCancelled()
    {
        // Arrange
        var config = CreateValidLiveConfiguration();
        var filePath = Path.Combine(_testDirectory, "cancel_test.json");
        await SaveToFile(config, filePath);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ConfigurationLoader.LoadLiveConfigAsync(filePath, cts.Token));
    }

    #endregion

    #region Helper Methods

    private static BacktestConfiguration CreateValidBacktestConfiguration()
    {
        return new BacktestConfiguration
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            StrategyDescription = "Test strategy for unit testing",
            TrainingStartDate = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2023, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2023, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2023, 12, 31, 0, 0, 0, TimeSpan.Zero),
            Securities = ["AAPL", "MSFT"],
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
            InitialCapital = 10000m,
            TradeVolume = 0.01m
        };
    }

    private static StrategyParametersConfig CreateValidStrategyConfiguration()
    {
        return new StrategyParametersConfig
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
            InitialCapital = 10000m,
            TradeVolume = 0.01m
        };
    }

    private static LiveTradingConfiguration CreateValidLiveConfiguration()
    {
        return new LiveTradingConfiguration
        {
            StrategyConfigPath = "C:\\Config\\strategy.json",
            BrokerConfigPath = "C:\\Config\\broker.json",
            RiskLimits = new RiskLimitsConfig
            {
                MaxPositionSize = 10000m,
                MaxDailyLoss = 2000m,
                MaxDailyLossIsPercentage = false,
                MaxDrawdownPercentage = 20m,
                MaxTradesPerDay = 100,
                CircuitBreakerEnabled = true,
                CircuitBreakerThresholdPercentage = 10m,
                CircuitBreakerCooldownMinutes = 30
            },
            SafetyCheckIntervalSeconds = 10,
            EnableAlerts = true
        };
    }

    private static async Task SaveToFile<T>(T config, string filePath)
    {
        var json = JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(filePath, json);
    }

    #endregion
}

using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;
using StockSharp.AdvancedBacktest.Validation;
using Xunit;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.Configuration;

public class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator;

    public ConfigurationValidatorTests()
    {
        _validator = new ConfigurationValidator();
    }

    #region BacktestConfiguration Tests

    [Fact]
    public void ValidateBacktestConfiguration_ValidConfig_ReturnsNoErrors()
    {
        var config = CreateValidBacktestConfiguration();

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateBacktestConfiguration_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.ValidateBacktestConfiguration(null!));
    }

    [Fact]
    public void ValidateBacktestConfiguration_MissingStrategyName_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.StrategyName = "";

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Strategy name is required"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_EmptySecurities_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.Securities = [];

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("At least one security must be specified"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_DuplicateSecurities_ReturnsWarning()
    {
        var config = CreateValidBacktestConfiguration();
        config.Securities = ["AAPL", "MSFT", "AAPL"];

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.True(result.IsValid); // Duplicates are a warning, not an error
        Assert.Contains(result.Warnings, w => w.Message.Contains("Duplicate securities"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_NegativeInitialCapital_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.InitialCapital = -1000;

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Initial capital must be greater than 0"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_VeryLowInitialCapital_ReturnsWarning()
    {
        var config = CreateValidBacktestConfiguration();
        config.InitialCapital = 50;

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.True(result.IsValid); // Low capital is a warning, not an error
        Assert.Contains(result.Warnings, w => w.Message.Contains("Initial capital is very low"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_TrainingEndBeforeStart_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.TrainingEndDate = config.TrainingStartDate.AddDays(-1);

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Training end date must be after training start date"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_ValidationOverlapsTraining_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.ValidationStartDate = config.TrainingEndDate.AddDays(-10);

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Validation period overlaps with training period"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_ShortTrainingPeriod_ReturnsWarning()
    {
        var config = CreateValidBacktestConfiguration();
        config.TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        config.TrainingEndDate = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero); // 4 days
        // Update validation dates to be after training dates
        config.ValidationStartDate = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero);
        config.ValidationEndDate = new DateTimeOffset(2024, 2, 5, 0, 0, 0, TimeSpan.Zero);

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Training period is less than 7 days"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_FutureDates_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.TrainingEndDate = DateTimeOffset.UtcNow.AddDays(30);
        config.ValidationEndDate = DateTimeOffset.UtcNow.AddDays(60);

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("cannot be in the future"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_NonexistentHistoryPath_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.HistoryPath = @"C:\NonexistentPath\Data";

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("History path does not exist"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_ExcessiveCommission_ReturnsWarning()
    {
        var config = CreateValidBacktestConfiguration();
        config.CommissionPercentage = 15;

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Commission percentage is unusually high"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_NoOptimizableParameters_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>();

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("At least one optimizable parameter must be specified"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_InvalidIntegerParameter_ReturnsError()
    {
        var config = CreateValidBacktestConfiguration();
        config.OptimizableParameters["Period"] = new ParameterDefinition
        {
            Name = "Period",
            Type = "int",
            MinValue = JsonSerializer.SerializeToElement(50),
            MaxValue = JsonSerializer.SerializeToElement(10), // Max < Min
            StepValue = JsonSerializer.SerializeToElement(5)
        };

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("MinValue") && e.Message.Contains("must be less than MaxValue"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_ExcessiveParameterRange_ReturnsWarning()
    {
        var config = CreateValidBacktestConfiguration();
        config.OptimizableParameters["Period"] = new ParameterDefinition
        {
            Name = "Period",
            Type = "int",
            MinValue = JsonSerializer.SerializeToElement(1),
            MaxValue = JsonSerializer.SerializeToElement(10000),
            StepValue = JsonSerializer.SerializeToElement(1) // Will generate 10,000 values
        };

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Range will generate") && w.Message.Contains("values"));
    }

    [Fact]
    public void ValidateBacktestConfiguration_ManyParameters_ReturnsWarning()
    {
        var config = CreateValidBacktestConfiguration();
        for (int i = 0; i < 15; i++)
        {
            config.OptimizableParameters[$"Param{i}"] = new ParameterDefinition
            {
                Name = $"Param{i}",
                Type = "int",
                MinValue = JsonSerializer.SerializeToElement(1),
                MaxValue = JsonSerializer.SerializeToElement(10),
                StepValue = JsonSerializer.SerializeToElement(1)
            };
        }

        var result = _validator.ValidateBacktestConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("optimizable parameters") && w.Message.Contains("long optimization times"));
    }

    #endregion

    #region StrategyParametersConfig Tests

    [Fact]
    public void ValidateStrategyParametersConfig_ValidConfig_ReturnsNoErrors()
    {
        var config = CreateValidStrategyParametersConfig();

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateStrategyParametersConfig_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.ValidateStrategyParametersConfig(null!));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_MissingStrategyName_ReturnsError()
    {
        var config = CreateValidStrategyParametersConfig();
        config.StrategyName = "";

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Strategy name is required"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_NoParameters_ReturnsError()
    {
        var config = CreateValidStrategyParametersConfig();
        config.Parameters = new Dictionary<string, JsonElement>();

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("At least one parameter must be specified"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_NegativeStopLoss_ReturnsError()
    {
        var config = CreateValidStrategyParametersConfig();
        config.Parameters["StopLossPercentage"] = JsonSerializer.SerializeToElement(-5.0m);

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("StopLossPercentage cannot be negative"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_ExcessiveStopLoss_ReturnsWarning()
    {
        var config = CreateValidStrategyParametersConfig();
        config.Parameters["StopLossPercentage"] = JsonSerializer.SerializeToElement(60m);

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("StopLossPercentage is very high"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_StopLossGreaterThanTakeProfit_ReturnsWarning()
    {
        var config = CreateValidStrategyParametersConfig();
        config.Parameters["StopLossPercentage"] = JsonSerializer.SerializeToElement(10m);
        config.Parameters["TakeProfitPercentage"] = JsonSerializer.SerializeToElement(5m);

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("StopLossPercentage is greater than or equal to TakeProfitPercentage"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_PoorRiskRewardRatio_ReturnsWarning()
    {
        var config = CreateValidStrategyParametersConfig();
        config.Parameters["StopLossPercentage"] = JsonSerializer.SerializeToElement(10m);
        config.Parameters["TakeProfitPercentage"] = JsonSerializer.SerializeToElement(5m); // 0.5:1 ratio

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Risk-reward ratio is less than 1:1"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_NegativePeriod_ReturnsError()
    {
        var config = CreateValidStrategyParametersConfig();
        config.Parameters["TrendFilterPeriod"] = JsonSerializer.SerializeToElement(-10);

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("TrendFilterPeriod") && e.Message.Contains("must be greater than 0"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_ExcessivePeriod_ReturnsWarning()
    {
        var config = CreateValidStrategyParametersConfig();
        config.Parameters["TrendFilterPeriod"] = JsonSerializer.SerializeToElement(2000);

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("TrendFilterPeriod") && w.Message.Contains("very large"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_OldOptimization_ReturnsWarning()
    {
        var config = CreateValidStrategyParametersConfig();
        config.OptimizationDate = DateTimeOffset.UtcNow.AddYears(-2);

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("over 1 year old"));
    }

    [Fact]
    public void ValidateStrategyParametersConfig_NoMetrics_ReturnsWarning()
    {
        var config = CreateValidStrategyParametersConfig();
        config.TrainingMetrics = null;
        config.ValidationMetrics = null;

        var result = _validator.ValidateStrategyParametersConfig(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("No performance metrics"));
    }

    #endregion

    #region LiveTradingConfiguration Tests

    [Fact]
    public void ValidateLiveTradingConfiguration_ValidConfig_ReturnsNoErrors()
    {
        var config = CreateValidLiveTradingConfiguration();

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.ValidateLiveTradingConfiguration(null!));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_NonexistentStrategyConfig_ReturnsError()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.StrategyConfigPath = @"C:\Nonexistent\strategy.json";

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Strategy configuration file does not exist"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_NonexistentBrokerConfig_ReturnsError()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.BrokerConfigPath = @"C:\Nonexistent\broker.json";

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Broker configuration file does not exist"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_NullRiskLimits_ReturnsError()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.RiskLimits = null!;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Risk limits configuration is required"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_NegativeMaxPositionSize_ReturnsError()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.RiskLimits.MaxPositionSize = -1000;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Max position size must be greater than 0"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_ExcessiveMaxPositionSize_ReturnsWarning()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.RiskLimits.MaxPositionSize = 2000000;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Max position size is very large"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_ExcessiveDailyLossPercentage_ReturnsWarning()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.RiskLimits.MaxDailyLoss = 30;
        config.RiskLimits.MaxDailyLossIsPercentage = true;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Max daily loss percentage is very high"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_DailyLossPercentageOver100_ReturnsError()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.RiskLimits.MaxDailyLoss = 150;
        config.RiskLimits.MaxDailyLossIsPercentage = true;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Max daily loss percentage cannot exceed 100%"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_ExcessiveDrawdown_ReturnsWarning()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.RiskLimits.MaxDrawdownPercentage = 60;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Max drawdown percentage is very high"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_HighLeverage_ReturnsWarning()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.RiskLimits.MaxLeverageRatio = 15;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Max leverage ratio is very high"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_InvalidTradingSession_ReturnsError()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.TradingSessions =
        [
            new TradingSession
            {
                Name = "Invalid Session",
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(9, 0), // End before start
                DaysOfWeek = [DayOfWeek.Monday]
            }
        ];

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("End time must be after start time"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_SessionWithNoDays_ReturnsWarning()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.TradingSessions =
        [
            new TradingSession
            {
                Name = "No Days Session",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(16, 0),
                DaysOfWeek = []
            }
        ];

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("No days of week specified"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_AlertsEnabledButNoDestination_ReturnsWarning()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.EnableAlerts = true;
        config.AlertEmail = null;
        config.AlertWebhookUrl = null;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Alerts are enabled but no alert email or webhook URL"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_NoFileLogging_ReturnsWarning()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.EnableFileLogging = false;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("File logging is disabled"));
    }

    [Fact]
    public void ValidateLiveTradingConfiguration_LiveWithoutSafeguards_ReturnsWarning()
    {
        var config = CreateValidLiveTradingConfiguration();
        config.EnableDryRun = false;
        config.RequireManualApproval = false;

        var result = _validator.ValidateLiveTradingConfiguration(config);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Live trading is enabled without dry run or manual approval"));
    }

    #endregion

    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_AddError_IncreasesErrorCount()
    {
        var result = new ValidationResult();
        result.AddError("Test error");

        Assert.Single(result.Errors);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidationResult_AddWarning_DoesNotAffectValidity()
    {
        var result = new ValidationResult();
        result.AddWarning("Test warning");

        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
        Assert.True(result.IsValid);
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void ValidationResult_GetFormattedMessages_IncludesErrorsAndWarnings()
    {
        var result = new ValidationResult();
        result.AddError("Error 1");
        result.AddError("Error 2");
        result.AddWarning("Warning 1");

        var formatted = result.GetFormattedMessages();

        Assert.Contains("Errors:", formatted);
        Assert.Contains("Error 1", formatted);
        Assert.Contains("Error 2", formatted);
        Assert.Contains("Warnings:", formatted);
        Assert.Contains("Warning 1", formatted);
    }

    [Fact]
    public void ValidationResult_ThrowIfInvalid_ThrowsWhenInvalid()
    {
        var result = new ValidationResult();
        result.AddError("Test error");

        var exception = Assert.Throws<ConfigurationValidationException>(() =>
            result.ThrowIfInvalid("TestConfig"));

        Assert.Contains("TestConfig", exception.Message);
        Assert.Contains("Test error", exception.Message);
    }

    [Fact]
    public void ValidationResult_ThrowIfInvalid_DoesNotThrowWhenValid()
    {
        var result = new ValidationResult();
        result.AddWarning("Just a warning");

        // Should not throw
        result.ThrowIfInvalid("TestConfig");
    }

    #endregion

    #region Helper Methods

    private BacktestConfiguration CreateValidBacktestConfiguration()
    {
        return new BacktestConfiguration
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
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
                    MaxValue = JsonSerializer.SerializeToElement(100),
                    StepValue = JsonSerializer.SerializeToElement(10)
                }
            },
            HistoryPath = Path.GetTempPath(), // Use temp path as it exists
            InitialCapital = 10000,
            TradeVolume = 0.01m,
            CommissionPercentage = 0.1m
        };
    }

    private StrategyParametersConfig CreateValidStrategyParametersConfig()
    {
        return new StrategyParametersConfig
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            StrategyHash = "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6",
            OptimizationDate = DateTimeOffset.UtcNow.AddMonths(-1),
            Parameters = new Dictionary<string, JsonElement>
            {
                ["TrendFilterPeriod"] = JsonSerializer.SerializeToElement(50),
                ["StopLossPercentage"] = JsonSerializer.SerializeToElement(5.0m),
                ["TakeProfitPercentage"] = JsonSerializer.SerializeToElement(10.0m)
            },
            InitialCapital = 10000,
            TradeVolume = 0.01m,
            Securities = ["AAPL"]
        };
    }

    private LiveTradingConfiguration CreateValidLiveTradingConfiguration()
    {
        // Create temporary files for testing
        var strategyConfigPath = Path.GetTempFileName();
        var brokerConfigPath = Path.GetTempFileName();

        return new LiveTradingConfiguration
        {
            StrategyConfigPath = strategyConfigPath,
            BrokerConfigPath = brokerConfigPath,
            RiskLimits = new RiskLimitsConfig
            {
                MaxPositionSize = 10000,
                MaxDailyLoss = 2000,
                MaxDailyLossIsPercentage = false,
                MaxDrawdownPercentage = 20,
                MaxTradesPerDay = 100,
                CircuitBreakerEnabled = true,
                CircuitBreakerThresholdPercentage = 5,
                CircuitBreakerCooldownMinutes = 30,
                MaxLeverageRatio = 1.0m,
                MaxPositionConcentrationPercentage = 20
            },
            EnableAlerts = true,
            AlertEmail = "test@example.com"
        };
    }

    #endregion
}

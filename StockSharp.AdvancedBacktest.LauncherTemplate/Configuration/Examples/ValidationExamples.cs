using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Examples;

/// <summary>
/// Examples demonstrating the new FluentValidation-based validation
/// </summary>
public static class ValidationExamples
{
    /// <summary>
    /// Example 1: Basic validation of BacktestConfiguration
    /// </summary>
    public static void Example1_BasicValidation()
    {
        var config = new BacktestConfiguration
        {
            StrategyName = "MyStrategy",
            StrategyVersion = "1.0",
            TrainingStartDate = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero),
            Securities = new List<string> { "AAPL", "MSFT" },
            HistoryPath = "C:\\Data\\History",
            OptimizableParameters = new Dictionary<string, ParameterDefinition>
            {
                ["Period"] = new ParameterDefinition
                {
                    Name = "Period",
                    Type = "int",
                    MinValue = System.Text.Json.JsonDocument.Parse("10").RootElement,
                    MaxValue = System.Text.Json.JsonDocument.Parse("50").RootElement,
                    StepValue = System.Text.Json.JsonDocument.Parse("5").RootElement
                }
            }
        };

        var validator = new ConfigurationValidator();
        var result = validator.ValidateBacktestConfiguration(config);

        if (result.IsValid)
        {
            Console.WriteLine("✓ Configuration is valid!");
        }
        else
        {
            Console.WriteLine("✗ Configuration has errors:");
            Console.WriteLine(result.GetFormattedMessages());
        }
    }

    /// <summary>
    /// Example 2: Validation with warnings
    /// </summary>
    public static void Example2_ValidationWithWarnings()
    {
        var config = new BacktestConfiguration
        {
            StrategyName = "MyStrategy",
            StrategyVersion = "1.0",
            TrainingStartDate = DateTimeOffset.UtcNow.AddMonths(-6),
            TrainingEndDate = DateTimeOffset.UtcNow.AddMonths(-3),
            ValidationStartDate = DateTimeOffset.UtcNow.AddMonths(-3),
            ValidationEndDate = DateTimeOffset.UtcNow.AddMonths(-1),
            Securities = new List<string> { "AAPL", "MSFT", "AAPL" }, // Duplicate!
            HistoryPath = "C:\\Data\\History",
            InitialCapital = 50m, // Very low - will trigger warning
            CommissionPercentage = 15m, // Very high - will trigger warning
            OptimizableParameters = new Dictionary<string, ParameterDefinition>
            {
                ["Period"] = new ParameterDefinition
                {
                    Name = "Period",
                    Type = "int",
                    MinValue = System.Text.Json.JsonDocument.Parse("1").RootElement,
                    MaxValue = System.Text.Json.JsonDocument.Parse("5000").RootElement, // Large range - will trigger warning
                    StepValue = System.Text.Json.JsonDocument.Parse("1").RootElement
                }
            }
        };

        var validator = new ConfigurationValidator();
        var result = validator.ValidateBacktestConfiguration(config);

        Console.WriteLine($"Is Valid: {result.IsValid}");
        Console.WriteLine($"Has Warnings: {result.HasWarnings}");
        Console.WriteLine($"Errors: {result.Errors.Count}");
        Console.WriteLine($"Warnings: {result.Warnings.Count}");
        Console.WriteLine("\n" + result.GetFormattedMessages());
    }

    /// <summary>
    /// Example 3: Direct FluentValidation usage (advanced)
    /// </summary>
    public static void Example3_DirectFluentValidation()
    {
        var config = new BacktestConfiguration
        {
            StrategyName = "", // Invalid - empty
            StrategyVersion = "1.0",
            TrainingStartDate = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero),
            Securities = new List<string>(),
            HistoryPath = "",
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };

        // Direct use of FluentValidation validator
        var fluentValidator = new BacktestConfigurationValidator();
        var fluentResult = fluentValidator.Validate(config);

        Console.WriteLine($"Is Valid: {fluentResult.IsValid}");

        foreach (var error in fluentResult.Errors)
        {
            Console.WriteLine($"[{error.Severity}] {error.PropertyName}: {error.ErrorMessage}");
        }
    }

    /// <summary>
    /// Example 4: Live trading configuration validation
    /// </summary>
    public static void Example4_LiveTradingValidation()
    {
        var config = new LiveTradingConfiguration
        {
            StrategyConfigPath = "strategy.json",
            BrokerConfigPath = "broker.json",
            RiskLimits = new RiskLimitsConfig
            {
                MaxPositionSize = 10000m,
                MaxDailyLoss = 500m,
                MaxDailyLossIsPercentage = false,
                MaxDrawdownPercentage = 10m,
                CircuitBreakerEnabled = true,
                CircuitBreakerThresholdPercentage = 5m,
                CircuitBreakerCooldownMinutes = 30
            },
            EnableDryRun = false, // Will trigger warning
            RequireManualApproval = false, // Will trigger warning with EnableDryRun = false
            EnableFileLogging = false // Will trigger warning
        };

        var validator = new ConfigurationValidator();
        var result = validator.ValidateLiveTradingConfiguration(config);

        Console.WriteLine(result.GetFormattedMessages());
    }

    /// <summary>
    /// Example 5: Handling validation exceptions
    /// </summary>
    public static void Example5_ValidationExceptions()
    {
        var config = new BacktestConfiguration
        {
            StrategyName = "",
            StrategyVersion = "1.0",
            TrainingStartDate = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero),
            Securities = new List<string>(),
            HistoryPath = "",
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };

        var validator = new ConfigurationValidator();
        var result = validator.ValidateBacktestConfiguration(config);

        try
        {
            // Throws exception if validation failed
            result.ThrowIfInvalid("Backtest Configuration");
        }
        catch (ConfigurationValidationException ex)
        {
            Console.WriteLine($"Validation failed: {ex.Message}");
            Console.WriteLine($"Error count: {ex.ValidationResult.Errors.Count}");

            foreach (var error in ex.ValidationResult.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }
    }
}

using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;

public static class ConfigurationLoader
{
    public static async Task<BacktestConfiguration> LoadBacktestConfigAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        if (!File.Exists(filePath))
        {
            throw new ConfigurationLoadException(
                $"Backtest configuration file not found: '{filePath}'. " +
                "Please ensure the file exists and the path is correct.");
        }

        var options = JsonSerializationHelper.CreateStandardOptions();
        BacktestConfiguration? config;

        try
        {
            await using var stream = File.OpenRead(filePath);
            config = await JsonSerializer.DeserializeAsync<BacktestConfiguration>(
                stream,
                options,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new ConfigurationLoadException(
                $"Invalid JSON in backtest configuration file '{filePath}': {ex.Message}. " +
                "Please verify the JSON syntax is correct.",
                ex);
        }

        if (config == null)
        {
            throw new ConfigurationLoadException(
                $"Failed to deserialize backtest configuration from '{filePath}'. " +
                "The file may be empty or contain null content.");
        }

        ValidateBacktestConfiguration(config, filePath);

        return config;
    }

    public static async Task<StrategyParametersConfig> LoadStrategyConfigAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        if (!File.Exists(filePath))
        {
            throw new ConfigurationLoadException(
                $"Strategy configuration file not found: '{filePath}'. " +
                "Please ensure the file exists and the path is correct.");
        }

        var options = JsonSerializationHelper.CreateStandardOptions();
        StrategyParametersConfig? config;

        try
        {
            await using var stream = File.OpenRead(filePath);
            config = await JsonSerializer.DeserializeAsync<StrategyParametersConfig>(
                stream,
                options,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new ConfigurationLoadException(
                $"Invalid JSON in strategy configuration file '{filePath}': {ex.Message}. " +
                "Please verify the JSON syntax is correct.",
                ex);
        }

        if (config == null)
        {
            throw new ConfigurationLoadException(
                $"Failed to deserialize strategy configuration from '{filePath}'. " +
                "The file may be empty or contain null content.");
        }

        ValidateStrategyConfiguration(config, filePath);

        return config;
    }

    public static async Task<LiveTradingConfiguration> LoadLiveConfigAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

        if (!File.Exists(filePath))
        {
            throw new ConfigurationLoadException(
                $"Live trading configuration file not found: '{filePath}'. " +
                "Please ensure the file exists and the path is correct.");
        }

        var options = JsonSerializationHelper.CreateStandardOptions();
        LiveTradingConfiguration? config;

        try
        {
            await using var stream = File.OpenRead(filePath);
            config = await JsonSerializer.DeserializeAsync<LiveTradingConfiguration>(
                stream,
                options,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new ConfigurationLoadException(
                $"Invalid JSON in live trading configuration file '{filePath}': {ex.Message}. " +
                "Please verify the JSON syntax is correct.",
                ex);
        }

        if (config == null)
        {
            throw new ConfigurationLoadException(
                $"Failed to deserialize live trading configuration from '{filePath}'. " +
                "The file may be empty or contain null content.");
        }

        ValidateLiveConfiguration(config, filePath);

        return config;
    }

    private static void ValidateBacktestConfiguration(BacktestConfiguration config, string filePath)
    {
        var errors = new List<string>();

        if (config.TrainingEndDate <= config.TrainingStartDate)
        {
            errors.Add("Training end date must be after training start date.");
        }

        if (config.ValidationEndDate <= config.ValidationStartDate)
        {
            errors.Add("Validation end date must be after validation start date.");
        }

        if (config.ValidationStartDate < config.TrainingEndDate)
        {
            errors.Add("Validation start date should not be before training end date.");
        }

        if (config.Securities.Count == 0)
        {
            errors.Add("At least one security must be specified.");
        }

        if (config.OptimizableParameters.Count == 0)
        {
            errors.Add("At least one optimizable parameter must be specified.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationLoadException(
                $"Backtest configuration validation failed for '{filePath}':\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    private static void ValidateStrategyConfiguration(StrategyParametersConfig config, string filePath)
    {
        var errors = new List<string>();

        if (config.Parameters.Count == 0)
        {
            errors.Add("At least one parameter must be specified.");
        }

        if (config.InitialCapital <= 0)
        {
            errors.Add("Initial capital must be greater than 0.");
        }

        if (config.TradeVolume <= 0)
        {
            errors.Add("Trade volume must be greater than 0.");
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationLoadException(
                $"Strategy configuration validation failed for '{filePath}':\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    private static void ValidateLiveConfiguration(LiveTradingConfiguration config, string filePath)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.StrategyConfigPath))
        {
            errors.Add("Strategy configuration path must be specified.");
        }

        if (string.IsNullOrWhiteSpace(config.BrokerConfigPath))
        {
            errors.Add("Broker configuration path must be specified.");
        }

        if (config.RiskLimits == null)
        {
            errors.Add("Risk limits must be specified.");
        }
        else
        {
            if (config.RiskLimits.MaxPositionSize <= 0)
            {
                errors.Add("Max position size must be greater than 0.");
            }

            if (config.RiskLimits.MaxDailyLoss <= 0)
            {
                errors.Add("Max daily loss must be greater than 0.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationLoadException(
                $"Live trading configuration validation failed for '{filePath}':\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }
}

public class ConfigurationLoadException : Exception
{
    public ConfigurationLoadException(string message) : base(message)
    {
    }

    public ConfigurationLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

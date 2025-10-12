using System.Text.Json;
using FluentValidation;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class LiveTradingConfigurationValidator : AbstractValidator<LiveTradingConfiguration>
{
    public LiveTradingConfigurationValidator()
    {
        // Strategy config path validation
        RuleFor(x => x.StrategyConfigPath)
            .NotEmpty().WithMessage("Strategy configuration path is required.")
            .Must(File.Exists).WithMessage(x => $"Strategy configuration file does not exist: '{x.StrategyConfigPath}'")
                .When(x => !string.IsNullOrWhiteSpace(x.StrategyConfigPath));

        // Broker config path validation
        RuleFor(x => x.BrokerConfigPath)
            .NotEmpty().WithMessage("Broker configuration path is required.")
            .Must(File.Exists).WithMessage(x => $"Broker configuration file does not exist: '{x.BrokerConfigPath}'")
                .When(x => !string.IsNullOrWhiteSpace(x.BrokerConfigPath));

        // Binance connector configuration validation
        RuleFor(x => x.BrokerConfigPath)
            .Must(IsValidBinanceConfig).WithMessage(x => $"Invalid Binance connector configuration in: '{x.BrokerConfigPath}'")
                .When(x => !string.IsNullOrWhiteSpace(x.BrokerConfigPath) && File.Exists(x.BrokerConfigPath));

        RuleFor(x => x.BrokerConfigPath)
            .Must(HasBinanceAdapter).WithMessage(x => $"Binance adapter not found in connector configuration: '{x.BrokerConfigPath}'")
                .When(x => !string.IsNullOrWhiteSpace(x.BrokerConfigPath) && File.Exists(x.BrokerConfigPath));

        RuleFor(x => x.BrokerConfigPath)
            .Must(HasValidBinanceCredentials).WithMessage(x => $"Binance API credentials (Key/Secret) are missing or invalid in: '{x.BrokerConfigPath}'")
                .When(x => !string.IsNullOrWhiteSpace(x.BrokerConfigPath) && File.Exists(x.BrokerConfigPath) && HasBinanceAdapter(x.BrokerConfigPath));

        RuleFor(x => x.BrokerConfigPath)
            .Must(path => HasValidBinanceSections(path)).WithMessage(x => $"Binance sections configuration is invalid in: '{x.BrokerConfigPath}'. Expected format: 'Spot,Margin,Futures,FuturesCoin'")
                .WithSeverity(Severity.Warning)
                .When(x => !string.IsNullOrWhiteSpace(x.BrokerConfigPath) && File.Exists(x.BrokerConfigPath) && HasBinanceAdapter(x.BrokerConfigPath));

        // Risk limits validation
        RuleFor(x => x.RiskLimits)
            .NotNull().WithMessage("Risk limits configuration is required for live trading.");

        RuleFor(x => x.RiskLimits)
            .SetValidator(new RiskLimitsConfigValidator())
                .When(x => x.RiskLimits != null);

        // Log file path validation
        RuleFor(x => x.LogFilePath)
            .Must(path => IsValidLogPath(path))
                .WithMessage(x => $"Invalid log file path: '{x.LogFilePath}'")
                .When(x => !string.IsNullOrWhiteSpace(x.LogFilePath));

        RuleFor(x => x.LogFilePath)
            .Must(path => LogDirectoryExistsOrCanBeCreated(path))
                .WithMessage(x => $"Log file directory does not exist and will be created: '{Path.GetDirectoryName(x.LogFilePath)}'")
                .WithSeverity(Severity.Warning)
                .When(x => !string.IsNullOrWhiteSpace(x.LogFilePath));

        // Safety check interval validation
        RuleFor(x => x.SafetyCheckIntervalSeconds)
            .GreaterThanOrEqualTo(1).WithMessage("Safety check interval must be at least 1 second.")
            .LessThanOrEqualTo(300).WithMessage("Safety check interval is more than 5 minutes. Consider reducing for better monitoring.")
                .WithSeverity(Severity.Warning);

        // Alert configuration validation
        When(x => x.EnableAlerts, () =>
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.AlertEmail) || !string.IsNullOrWhiteSpace(x.AlertWebhookUrl))
                    .WithMessage("Alerts are enabled but no alert email or webhook URL is configured.")
                    .WithSeverity(Severity.Warning);
        });

        // Auto recovery validation
        When(x => x.EnableAutoRecovery, () =>
        {
            RuleFor(x => x.MaxRecoveryAttempts)
                .InclusiveBetween(1, 10).WithMessage("Max recovery attempts should be between 1 and 10.")
                    .WithSeverity(Severity.Warning);

            RuleFor(x => x.RecoveryDelaySeconds)
                .GreaterThanOrEqualTo(1).WithMessage("Recovery delay must be at least 1 second.");
        });

        // Trading sessions validation
        RuleForEach(x => x.TradingSessions)
            .SetValidator(new TradingSessionValidator());

        // Check for overlapping sessions
        RuleFor(x => x.TradingSessions)
            .Custom((sessions, context) =>
            {
                if (sessions == null || sessions.Count == 0) return;

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];

                    for (int j = i + 1; j < sessions.Count; j++)
                    {
                        var otherSession = sessions[j];
                        if (HasOverlappingDays(session, otherSession) && HasOverlappingTime(session, otherSession))
                        {
                            var failure = new FluentValidation.Results.ValidationFailure("TradingSessions",
                                $"Trading sessions '{session.Name}' and '{otherSession.Name}' have overlapping time periods.")
                            {
                                Severity = Severity.Warning
                            };
                            context.AddFailure(failure);
                        }
                    }
                }
            });

        // Session timeout validation
        RuleFor(x => x.SessionTimeoutHours)
            .LessThanOrEqualTo(12).WithMessage(x => $"Session timeout is set to {x.SessionTimeoutHours} hours. Consider if this is appropriate for your trading schedule.")
                .WithSeverity(Severity.Warning);

        // Live trading safety warnings
        RuleFor(x => x)
            .Must(x => x.EnableDryRun || x.RequireManualApproval)
                .WithMessage("Live trading is enabled without dry run or manual approval. Ensure you have thoroughly tested your strategy.")
                .WithSeverity(Severity.Warning);

        RuleFor(x => x.EnableFileLogging)
            .Equal(true).WithMessage("File logging is disabled. You will have no persistent log records of trading activity.")
                .WithSeverity(Severity.Warning);

        RuleFor(x => x.EnablePerformanceMonitoring)
            .Equal(true).WithMessage("Performance monitoring is disabled. You will not be able to track strategy performance in real-time.")
                .WithSeverity(Severity.Warning);
    }

    private bool IsValidLogPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        try
        {
            Path.GetDirectoryName(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool LogDirectoryExistsOrCanBeCreated(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        try
        {
            var directory = Path.GetDirectoryName(path);
            return string.IsNullOrWhiteSpace(directory) || Directory.Exists(directory);
        }
        catch
        {
            return false;
        }
    }

    private bool HasOverlappingDays(TradingSession session1, TradingSession session2)
    {
        return session1.DaysOfWeek.Intersect(session2.DaysOfWeek).Any();
    }

    private bool HasOverlappingTime(TradingSession session1, TradingSession session2)
    {
        return session1.StartTime < session2.EndTime && session2.StartTime < session1.EndTime;
    }

    private bool IsValidBinanceConfig(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return true;

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonDocument.Parse(json);

            // Check for required structure
            if (!document.RootElement.TryGetProperty("Adapter", out var adapter))
                return false;

            if (!adapter.TryGetProperty("InnerAdapters", out var innerAdapters))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool HasBinanceAdapter(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return true;

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonDocument.Parse(json);

            var adapter = document.RootElement.GetProperty("Adapter");
            var innerAdapters = adapter.GetProperty("InnerAdapters");

            foreach (var innerAdapter in innerAdapters.EnumerateArray())
            {
                if (innerAdapter.TryGetProperty("AdapterType", out var adapterType))
                {
                    var typeValue = adapterType.GetString();
                    if (typeValue?.Contains("BinanceMessageAdapter") == true)
                        return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool HasValidBinanceCredentials(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return true;

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonDocument.Parse(json);

            var adapter = document.RootElement.GetProperty("Adapter");
            var innerAdapters = adapter.GetProperty("InnerAdapters");

            foreach (var innerAdapter in innerAdapters.EnumerateArray())
            {
                if (innerAdapter.TryGetProperty("AdapterType", out var adapterType))
                {
                    var typeValue = adapterType.GetString();
                    if (typeValue?.Contains("BinanceMessageAdapter") == true)
                    {
                        var adapterSettings = innerAdapter.GetProperty("AdapterSettings");

                        if (!adapterSettings.TryGetProperty("Key", out var key) || string.IsNullOrWhiteSpace(key.GetString()))
                            return false;

                        if (!adapterSettings.TryGetProperty("Secret", out var secret) || string.IsNullOrWhiteSpace(secret.GetString()))
                            return false;

                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool HasValidBinanceSections(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return true;

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonDocument.Parse(json);

            var adapter = document.RootElement.GetProperty("Adapter");
            var innerAdapters = adapter.GetProperty("InnerAdapters");

            foreach (var innerAdapter in innerAdapters.EnumerateArray())
            {
                if (innerAdapter.TryGetProperty("AdapterType", out var adapterType))
                {
                    var typeValue = adapterType.GetString();
                    if (typeValue?.Contains("BinanceMessageAdapter") == true)
                    {
                        var adapterSettings = innerAdapter.GetProperty("AdapterSettings");

                        if (!adapterSettings.TryGetProperty("Sections", out var sections))
                            return true; // Optional field

                        var sectionsValue = sections.GetString();
                        if (string.IsNullOrWhiteSpace(sectionsValue))
                            return true;

                        // Validate format: should be comma-separated values
                        var validSections = new[] { "Spot", "Margin", "Futures", "FuturesCoin" };
                        var configuredSections = sectionsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        return configuredSections.All(s => validSections.Contains(s));
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}

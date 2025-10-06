using FluentValidation;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class ConfigurationValidator
{
    private readonly BacktestConfigurationValidator _backtestValidator;
    private readonly StrategyParametersConfigValidator _strategyParametersValidator;
    private readonly LiveTradingConfigurationValidator _liveTradingValidator;

    public ConfigurationValidator()
    {
        _backtestValidator = new BacktestConfigurationValidator();
        _strategyParametersValidator = new StrategyParametersConfigValidator();
        _liveTradingValidator = new LiveTradingConfigurationValidator();
    }

    public ValidationResult ValidateBacktestConfiguration(BacktestConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        var fluentResult = _backtestValidator.Validate(config);
        return ConvertFluentValidationResult(fluentResult);
    }

    public ValidationResult ValidateStrategyParametersConfig(StrategyParametersConfig config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        var fluentResult = _strategyParametersValidator.Validate(config);
        return ConvertFluentValidationResult(fluentResult);
    }

    public ValidationResult ValidateLiveTradingConfiguration(LiveTradingConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        var fluentResult = _liveTradingValidator.Validate(config);
        return ConvertFluentValidationResult(fluentResult);
    }

    private ValidationResult ConvertFluentValidationResult(FluentValidation.Results.ValidationResult fluentResult)
    {
        var result = new ValidationResult();

        foreach (var failure in fluentResult.Errors)
        {
            if (failure.Severity == Severity.Warning)
            {
                result.AddWarning(failure.ErrorMessage, failure.PropertyName);
            }
            else
            {
                result.AddError(failure.ErrorMessage, failure.PropertyName);
            }
        }

        return result;
    }
}

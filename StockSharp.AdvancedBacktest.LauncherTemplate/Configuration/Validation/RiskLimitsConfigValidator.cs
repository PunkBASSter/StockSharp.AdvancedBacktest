using FluentValidation;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class RiskLimitsConfigValidator : AbstractValidator<RiskLimitsConfig>
{
    public RiskLimitsConfigValidator()
    {
        // Max position size validation
        RuleFor(x => x.MaxPositionSize)
            .GreaterThan(0).WithMessage("Max position size must be greater than 0.")
            .LessThanOrEqualTo(1000000).WithMessage(x => $"Max position size is very large ({x.MaxPositionSize:N0}). Ensure this is appropriate for your account.")
                .WithSeverity(Severity.Warning);

        // Max daily loss validation
        RuleFor(x => x.MaxDailyLoss)
            .GreaterThan(0).WithMessage("Max daily loss must be greater than 0.");

        RuleFor(x => x.MaxDailyLoss)
            .LessThanOrEqualTo(100).WithMessage("Max daily loss percentage cannot exceed 100%.")
                .When(x => x.MaxDailyLossIsPercentage)
            .LessThanOrEqualTo(20).WithMessage(x => $"Max daily loss percentage is very high ({x.MaxDailyLoss}%). This could result in significant losses.")
                .WithSeverity(Severity.Warning)
                .When(x => x.MaxDailyLossIsPercentage);

        // Max drawdown validation
        RuleFor(x => x.MaxDrawdownPercentage)
            .GreaterThan(0).WithMessage("Max drawdown percentage must be greater than 0.")
            .LessThanOrEqualTo(100).WithMessage("Max drawdown percentage cannot exceed 100%.")
            .LessThanOrEqualTo(50).WithMessage(x => $"Max drawdown percentage is very high ({x.MaxDrawdownPercentage}%). Consider reducing this for better risk management.")
                .WithSeverity(Severity.Warning);

        // Max trades per day validation
        RuleFor(x => x.MaxTradesPerDay)
            .GreaterThanOrEqualTo(1).WithMessage("Max trades per day must be at least 1.")
            .LessThanOrEqualTo(1000).WithMessage(x => $"Max trades per day is very high ({x.MaxTradesPerDay}). This may indicate a high-frequency strategy.")
                .WithSeverity(Severity.Warning);

        // Circuit breaker validations
        When(x => x.CircuitBreakerEnabled, () =>
        {
            RuleFor(x => x.CircuitBreakerThresholdPercentage)
                .GreaterThan(0).WithMessage("Circuit breaker threshold must be greater than 0.")
                .LessThanOrEqualTo(100).WithMessage("Circuit breaker threshold cannot exceed 100%.");

            RuleFor(x => x.CircuitBreakerCooldownMinutes)
                .GreaterThanOrEqualTo(0).WithMessage("Circuit breaker cooldown cannot be negative.")
                .LessThanOrEqualTo(1440).WithMessage(x => $"Circuit breaker cooldown is more than 24 hours ({x.CircuitBreakerCooldownMinutes} minutes). This may be excessive.")
                    .WithSeverity(Severity.Warning);
        });

        // Max leverage ratio validation
        RuleFor(x => x.MaxLeverageRatio)
            .GreaterThanOrEqualTo(1).WithMessage("Max leverage ratio must be at least 1.")
            .LessThanOrEqualTo(10).WithMessage(x => $"Max leverage ratio is very high ({x.MaxLeverageRatio}). High leverage increases risk significantly.")
                .WithSeverity(Severity.Warning);

        // Max position concentration validation
        RuleFor(x => x.MaxPositionConcentrationPercentage)
            .InclusiveBetween(0, 100).WithMessage("Max position concentration must be between 0 and 100%.")
            .LessThanOrEqualTo(50).WithMessage(x => $"Max position concentration is high ({x.MaxPositionConcentrationPercentage}%). Consider diversifying positions.")
                .WithSeverity(Severity.Warning);
    }
}

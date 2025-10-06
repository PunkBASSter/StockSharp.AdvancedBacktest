using FluentValidation;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class TradingSessionValidator : AbstractValidator<TradingSession>
{
    public TradingSessionValidator()
    {
        // Session name validation
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Trading session name is required.");

        // Time validation
        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
                .WithMessage(x => $"Trading session '{x.Name}': End time must be after start time.");

        // Days of week validation
        RuleFor(x => x.DaysOfWeek)
            .NotNull().WithMessage(x => $"Trading session '{x.Name}': Days of week cannot be null.")
            .NotEmpty().WithMessage(x => $"Trading session '{x.Name}': No days of week specified. Session will never be active.")
                .WithSeverity(Severity.Warning);
    }
}

using FluentValidation;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class StrategyParametersConfigValidator : AbstractValidator<StrategyParametersConfig>
{
    public StrategyParametersConfigValidator()
    {
        // Strategy name validation
        RuleFor(x => x.StrategyName)
            .NotEmpty().WithMessage("Strategy name is required.");

        // Strategy version validation
        RuleFor(x => x.StrategyVersion)
            .NotEmpty().WithMessage("Strategy version is required.");

        // Strategy hash validation
        RuleFor(x => x.StrategyHash)
            .NotEmpty().WithMessage("Strategy hash is required.");

        // Parameters validation
        RuleFor(x => x.Parameters)
            .NotNull().WithMessage("Parameters cannot be null.")
            .NotEmpty().WithMessage("At least one parameter must be specified.");

        // Initial capital validation
        RuleFor(x => x.InitialCapital)
            .GreaterThan(0).WithMessage("Initial capital must be greater than 0.")
            .GreaterThanOrEqualTo(100).WithMessage("Initial capital is very low (< 100). Ensure this is appropriate for your trading strategy.")
                .WithSeverity(Severity.Warning);

        // Trade volume validation
        RuleFor(x => x.TradeVolume)
            .GreaterThan(0).WithMessage("Trade volume must be greater than 0.");

        // Parameter-specific validations
        RuleFor(x => x)
            .Custom((config, context) =>
            {
                if (config.Parameters == null) return;

                // StopLoss validation
                if (config.Parameters.TryGetValue("StopLossPercentage", out var stopLossElement))
                {
                    if (stopLossElement.TryGetDecimal(out var stopLoss))
                    {
                        if (stopLoss < 0)
                        {
                            context.AddFailure("Parameters.StopLossPercentage", "StopLossPercentage cannot be negative.");
                        }
                        else if (stopLoss > 100)
                        {
                            context.AddFailure("Parameters.StopLossPercentage", "StopLossPercentage cannot exceed 100%.");
                        }
                        else if (stopLoss > 50)
                        {
                            var failure = new FluentValidation.Results.ValidationFailure("Parameters.StopLossPercentage",
                                "StopLossPercentage is very high (> 50%). This may result in large losses.")
                            {
                                Severity = Severity.Warning
                            };
                            context.AddFailure(failure);
                        }
                    }
                }

                // TakeProfit validation
                if (config.Parameters.TryGetValue("TakeProfitPercentage", out var takeProfitElement))
                {
                    if (takeProfitElement.TryGetDecimal(out var takeProfit))
                    {
                        if (takeProfit <= 0)
                        {
                            context.AddFailure("Parameters.TakeProfitPercentage", "TakeProfitPercentage must be greater than 0.");
                        }
                    }
                }

                // PositionSize validation
                if (config.Parameters.TryGetValue("PositionSize", out var positionElement))
                {
                    if (positionElement.TryGetDecimal(out var position))
                    {
                        if (position <= 0)
                        {
                            context.AddFailure("Parameters.PositionSize", "PositionSize must be greater than 0.");
                        }
                        else if (position > config.InitialCapital)
                        {
                            var failure = new FluentValidation.Results.ValidationFailure("Parameters.PositionSize",
                                "PositionSize exceeds initial capital. This requires leverage or will cause issues.")
                            {
                                Severity = Severity.Warning
                            };
                            context.AddFailure(failure);
                        }
                    }
                }

                // Risk-reward ratio check
                bool hasStopLoss = config.Parameters.TryGetValue("StopLossPercentage", out var slElement);
                bool hasTakeProfit = config.Parameters.TryGetValue("TakeProfitPercentage", out var tpElement);

                if (hasStopLoss && hasTakeProfit)
                {
                    if (slElement.TryGetDecimal(out var stopLoss) && tpElement.TryGetDecimal(out var takeProfit))
                    {
                        if (stopLoss >= takeProfit)
                        {
                            var failure = new FluentValidation.Results.ValidationFailure("Parameters",
                                "StopLossPercentage is greater than or equal to TakeProfitPercentage. This may indicate a configuration error.")
                            {
                                Severity = Severity.Warning
                            };
                            context.AddFailure(failure);
                        }

                        if (takeProfit > 0 && stopLoss > 0)
                        {
                            var riskRewardRatio = takeProfit / stopLoss;
                            if (riskRewardRatio < 1)
                            {
                                var failure = new FluentValidation.Results.ValidationFailure("Parameters",
                                    $"Risk-reward ratio is less than 1:1 (TakeProfit/StopLoss = {riskRewardRatio:F2}). Consider if this aligns with your trading strategy.")
                                {
                                    Severity = Severity.Warning
                                };
                                context.AddFailure(failure);
                            }
                        }
                    }
                }

                // Period parameters validation
                var periodParameters = config.Parameters.Where(p => p.Key.Contains("Period", StringComparison.OrdinalIgnoreCase));
                foreach (var param in periodParameters)
                {
                    if (param.Value.TryGetInt32(out var period))
                    {
                        if (period <= 0)
                        {
                            context.AddFailure($"Parameters.{param.Key}", $"Parameter '{param.Key}' must be greater than 0.");
                        }
                        else if (period > 1000)
                        {
                            var failure = new FluentValidation.Results.ValidationFailure($"Parameters.{param.Key}",
                                $"Parameter '{param.Key}' is very large ({period}). Ensure this is correct.")
                            {
                                Severity = Severity.Warning
                            };
                            context.AddFailure(failure);
                        }
                    }
                }
            });

        // Performance metrics warning
        RuleFor(x => x)
            .Must(x => x.TrainingMetrics != null || x.ValidationMetrics != null)
                .WithMessage("No performance metrics are available. This strategy configuration may not have been properly backtested.")
                .WithSeverity(Severity.Warning);

        // Securities warning
        RuleFor(x => x.Securities)
            .Must(securities => securities.Count > 0)
                .WithMessage("No securities specified. Ensure the strategy will be applied to the correct instruments.")
                .WithSeverity(Severity.Warning);

        // Optimization date warning
        RuleFor(x => x.OptimizationDate)
            .GreaterThan(DateTimeOffset.UtcNow.AddYears(-1))
                .WithMessage(x => $"This strategy configuration is over 1 year old (optimized on {x.OptimizationDate:yyyy-MM-dd}). Consider re-optimizing with recent data.")
                .WithSeverity(Severity.Warning);
    }
}

using FluentValidation;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class ParameterDefinitionValidator : AbstractValidator<ParameterDefinition>
{
    public ParameterDefinitionValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Parameter type is required.");

        RuleFor(x => x)
            .Custom((param, context) =>
            {
                var type = param.Type?.ToLowerInvariant();

                switch (type)
                {
                    case "int":
                    case "integer":
                        ValidateIntegerParameter(param, context);
                        break;
                    case "decimal":
                    case "double":
                    case "float":
                        ValidateNumericParameter(param, context);
                        break;
                    case "bool":
                    case "boolean":
                        // No additional validation needed for boolean
                        break;
                    default:
                        context.AddFailure($"Unknown parameter type '{param.Type}'.");
                        break;
                }
            });
    }

    private void ValidateIntegerParameter(ParameterDefinition param, ValidationContext<ParameterDefinition> context)
    {
        try
        {
            if (!param.MinValue.TryGetInt32(out var min))
            {
                context.AddFailure("MinValue", "MinValue must be a valid integer.");
                return;
            }

            if (!param.MaxValue.TryGetInt32(out var max))
            {
                context.AddFailure("MaxValue", "MaxValue must be a valid integer.");
                return;
            }

            if (!param.StepValue.TryGetInt32(out var step))
            {
                context.AddFailure("StepValue", "StepValue must be a valid integer.");
                return;
            }

            if (min >= max)
            {
                context.AddFailure("MinValue", $"MinValue ({min}) must be less than MaxValue ({max}).");
            }

            if (step <= 0)
            {
                context.AddFailure("StepValue", "StepValue must be greater than 0.");
            }

            var steps = (max - min) / step;
            if (steps > 1000)
            {
                var failure = new FluentValidation.Results.ValidationFailure("StepValue",
                    $"Range will generate {steps} values. Consider increasing step size.")
                {
                    Severity = Severity.Warning
                };
                context.AddFailure(failure);
            }
        }
        catch (Exception ex)
        {
            context.AddFailure($"Error validating integer parameter - {ex.Message}");
        }
    }

    private void ValidateNumericParameter(ParameterDefinition param, ValidationContext<ParameterDefinition> context)
    {
        try
        {
            if (!param.MinValue.TryGetDecimal(out var min))
            {
                context.AddFailure("MinValue", "MinValue must be a valid decimal number.");
                return;
            }

            if (!param.MaxValue.TryGetDecimal(out var max))
            {
                context.AddFailure("MaxValue", "MaxValue must be a valid decimal number.");
                return;
            }

            if (!param.StepValue.TryGetDecimal(out var step))
            {
                context.AddFailure("StepValue", "StepValue must be a valid decimal number.");
                return;
            }

            if (min >= max)
            {
                context.AddFailure("MinValue", $"MinValue ({min}) must be less than MaxValue ({max}).");
            }

            if (step <= 0)
            {
                context.AddFailure("StepValue", "StepValue must be greater than 0.");
            }

            var steps = (max - min) / step;
            if (steps > 1000)
            {
                var failure = new FluentValidation.Results.ValidationFailure("StepValue",
                    $"Range will generate approximately {(int)steps} values. Consider increasing step size.")
                {
                    Severity = Severity.Warning
                };
                context.AddFailure(failure);
            }
        }
        catch (Exception ex)
        {
            context.AddFailure($"Error validating numeric parameter - {ex.Message}");
        }
    }
}

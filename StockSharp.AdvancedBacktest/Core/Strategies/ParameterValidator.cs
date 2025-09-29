// Backward compatibility wrapper around the enhanced ParameterValidator
// This file provides aliases to maintain compatibility while new code uses the enhanced implementation

using StockSharp.AdvancedBacktest.Core.Configuration.Validation;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

/// <summary>
/// Backward compatibility wrapper around the enhanced ParameterValidator.
/// New code should use StockSharp.AdvancedBacktest.Core.Configuration.Validation.ParameterValidator.
/// </summary>
[Obsolete("Use StockSharp.AdvancedBacktest.Core.Configuration.Validation.ParameterValidator instead. This alias will be removed in v3.0.")]
public class ParameterValidator : IParameterValidator
{
    private readonly StockSharp.AdvancedBacktest.Core.Configuration.Validation.ParameterValidator _enhanced;

    public ParameterValidator()
    {
        _enhanced = new StockSharp.AdvancedBacktest.Core.Configuration.Validation.ParameterValidator();
    }

    public Models.ValidationResult ValidateParameter(string parameterName, object? value, Models.ParameterDefinition definition)
    {
        if (string.IsNullOrEmpty(parameterName))
            return Models.ValidationResult.Failure("Parameter name cannot be null or empty");

        if (definition == null)
            return Models.ValidationResult.Failure($"Parameter definition for '{parameterName}' is null");

        // Use the enhanced validator
        var result = _enhanced.ValidateParameter(definition, value);
        return Models.ValidationResult.FromEnhanced(result);
    }

    public Models.ValidationResult ValidateParameterSet(IParameterSet parameters)
    {
        if (parameters == null)
            return Models.ValidationResult.Failure("Parameter set cannot be null");

        // Convert to enhanced parameter set for validation if it's our wrapper
        if (parameters is ParameterSet legacySet)
        {
            var result = legacySet.Validate();
            return result;
        }

        // Fallback for other implementations
        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        try
        {
            // Validate each parameter using the legacy method
            foreach (var definition in parameters.Definitions)
            {
                var value = parameters.GetValue(definition.Name);
                var result = ValidateParameter(definition.Name, value, definition);

                allErrors.AddRange(result.Errors);
                allWarnings.AddRange(result.Warnings);
            }

            // Basic completeness check
            var statistics = parameters.GetStatistics();
            if (!statistics.IsComplete)
            {
                var missing = statistics.RequiredParameters - statistics.RequiredParametersSet;
                allErrors.Add($"{missing} required parameter(s) are missing");
            }

            return allErrors.Count == 0
                ? (allWarnings.Count == 0 ? Models.ValidationResult.CreateSuccess() : Models.ValidationResult.SuccessWithWarnings(allWarnings.ToArray()))
                : Models.ValidationResult.Failure(allErrors, allWarnings);
        }
        catch (Exception ex)
        {
            allErrors.Add($"Parameter set validation error: {ex.Message}");
            return Models.ValidationResult.Failure(allErrors);
        }
    }

    public Models.ValidationResult ValidateDependencies(IParameterSet parameters)
    {
        if (parameters == null)
            return Models.ValidationResult.Failure("Parameter set cannot be null");

        // Simplified dependency validation for backward compatibility
        return Models.ValidationResult.CreateSuccess();
    }
}
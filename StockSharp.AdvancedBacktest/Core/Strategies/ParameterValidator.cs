using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Text.RegularExpressions;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

/// <summary>
/// Default parameter validator implementation with comprehensive validation rules
/// </summary>
public class ParameterValidator : IParameterValidator
{
    /// <summary>
    /// Validate a single parameter
    /// </summary>
    public ValidationResult ValidateParameter(string parameterName, object? value, ParameterDefinition definition)
    {
        if (string.IsNullOrEmpty(parameterName))
            return ValidationResult.Failure("Parameter name cannot be null or empty");

        if (definition == null)
            return ValidationResult.Failure($"Parameter definition for '{parameterName}' is null");

        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Check required parameters
            if (definition.IsRequired && value == null)
            {
                errors.Add($"Required parameter '{parameterName}' is missing");
                return ValidationResult.Failure(errors);
            }

            // Allow null for non-required parameters
            if (value == null)
            {
                return ValidationResult.CreateSuccess();
            }

            // Type validation
            if (!IsTypeCompatible(value, definition.Type))
            {
                errors.Add($"Parameter '{parameterName}' expects type {definition.Type.Name} but got {value.GetType().Name}");
            }

            // Numeric range validation
            if (definition.IsNumeric && value is IComparable comparableValue)
            {
                ValidateNumericRange(parameterName, comparableValue, definition, errors, warnings);
            }

            // String validation
            if (definition.IsString && value is string stringValue)
            {
                ValidateString(parameterName, stringValue, definition, errors, warnings);
            }

            // Boolean validation
            if (definition.IsBoolean && value is bool boolValue)
            {
                ValidateBoolean(parameterName, boolValue, definition, errors, warnings);
            }

            // Enum validation
            if (definition.IsEnum)
            {
                ValidateEnum(parameterName, value, definition, errors, warnings);
            }

            return errors.Count == 0
                ? (warnings.Count == 0 ? ValidationResult.CreateSuccess() : ValidationResult.SuccessWithWarnings(warnings))
                : ValidationResult.Failure(errors, warnings);
        }
        catch (Exception ex)
        {
            errors.Add($"Validation error for parameter '{parameterName}': {ex.Message}");
            return ValidationResult.Failure(errors);
        }
    }

    /// <summary>
    /// Validate all parameters in a set
    /// </summary>
    public ValidationResult ValidateParameterSet(IParameterSet parameters)
    {
        if (parameters == null)
            return ValidationResult.Failure("Parameter set cannot be null");

        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        try
        {
            // Validate each parameter
            foreach (var definition in parameters.Definitions)
            {
                var value = parameters.GetValue(definition.Name);
                var result = ValidateParameter(definition.Name, value, definition);

                allErrors.AddRange(result.Errors);
                allWarnings.AddRange(result.Warnings);
            }

            // Validate dependencies
            var dependencyResult = ValidateDependencies(parameters);
            allErrors.AddRange(dependencyResult.Errors);
            allWarnings.AddRange(dependencyResult.Warnings);

            // Validate completeness
            var completenessResult = ValidateCompleteness(parameters);
            allErrors.AddRange(completenessResult.Errors);
            allWarnings.AddRange(completenessResult.Warnings);

            return allErrors.Count == 0
                ? (allWarnings.Count == 0 ? ValidationResult.CreateSuccess() : ValidationResult.SuccessWithWarnings(allWarnings))
                : ValidationResult.Failure(allErrors, allWarnings);
        }
        catch (Exception ex)
        {
            allErrors.Add($"Parameter set validation error: {ex.Message}");
            return ValidationResult.Failure(allErrors);
        }
    }

    /// <summary>
    /// Validate parameter dependencies and relationships
    /// </summary>
    public ValidationResult ValidateDependencies(IParameterSet parameters)
    {
        if (parameters == null)
            return ValidationResult.Failure("Parameter set cannot be null");

        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Common dependency validations
            ValidateCommonDependencies(parameters, errors, warnings);

            // Trading-specific validations
            ValidateTradingDependencies(parameters, errors, warnings);

            // Risk management validations
            ValidateRiskDependencies(parameters, errors, warnings);

            return errors.Count == 0
                ? (warnings.Count == 0 ? ValidationResult.CreateSuccess() : ValidationResult.SuccessWithWarnings(warnings))
                : ValidationResult.Failure(errors, warnings);
        }
        catch (Exception ex)
        {
            errors.Add($"Dependency validation error: {ex.Message}");
            return ValidationResult.Failure(errors);
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Check if value type is compatible with expected type
    /// </summary>
    private static bool IsTypeCompatible(object value, Type expectedType)
    {
        if (value == null)
            return !expectedType.IsValueType || Nullable.GetUnderlyingType(expectedType) != null;

        var valueType = value.GetType();

        // Direct type match
        if (expectedType.IsAssignableFrom(valueType))
            return true;

        // Numeric conversions
        if (IsNumericType(expectedType) && IsNumericType(valueType))
            return true;

        // String conversions
        if (expectedType == typeof(string))
            return true;

        return false;
    }

    /// <summary>
    /// Check if type is numeric
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(decimal) ||
               type == typeof(double) || type == typeof(float) || type == typeof(short) ||
               type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
               type == typeof(ushort) || type == typeof(sbyte) ||
               type == typeof(int?) || type == typeof(long?) || type == typeof(decimal?) ||
               type == typeof(double?) || type == typeof(float?) || type == typeof(short?) ||
               type == typeof(byte?) || type == typeof(uint?) || type == typeof(ulong?) ||
               type == typeof(ushort?) || type == typeof(sbyte?);
    }

    /// <summary>
    /// Validate numeric range
    /// </summary>
    private static void ValidateNumericRange(
        string parameterName,
        IComparable value,
        ParameterDefinition definition,
        List<string> errors,
        List<string> warnings)
    {
        if (definition.MinValue is IComparable min && value.CompareTo(min) < 0)
        {
            errors.Add($"Parameter '{parameterName}' value {value} is below minimum {min}");
        }

        if (definition.MaxValue is IComparable max && value.CompareTo(max) > 0)
        {
            errors.Add($"Parameter '{parameterName}' value {value} is above maximum {max}");
        }

        // Warning for values near limits
        if (definition.MinValue is IComparable minWarn && definition.MaxValue is IComparable maxWarn)
        {
            var range = Convert.ToDecimal(maxWarn) - Convert.ToDecimal(minWarn);
            var valueDecimal = Convert.ToDecimal(value);
            var minDecimal = Convert.ToDecimal(minWarn);
            var maxDecimal = Convert.ToDecimal(maxWarn);

            var distanceFromMin = (valueDecimal - minDecimal) / range;
            var distanceFromMax = (maxDecimal - valueDecimal) / range;

            if (distanceFromMin < 0.1m)
            {
                warnings.Add($"Parameter '{parameterName}' value {value} is very close to minimum {minWarn}");
            }
            else if (distanceFromMax < 0.1m)
            {
                warnings.Add($"Parameter '{parameterName}' value {value} is very close to maximum {maxWarn}");
            }
        }
    }

    /// <summary>
    /// Validate string parameter
    /// </summary>
    private static void ValidateString(
        string parameterName,
        string value,
        ParameterDefinition definition,
        List<string> errors,
        List<string> warnings)
    {
        // Pattern validation
        if (!string.IsNullOrEmpty(definition.ValidationPattern))
        {
            try
            {
                if (!Regex.IsMatch(value, definition.ValidationPattern))
                {
                    errors.Add($"Parameter '{parameterName}' value '{value}' does not match required pattern");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Parameter '{parameterName}' pattern validation failed: {ex.Message}");
            }
        }

        // Length validations
        if (string.IsNullOrWhiteSpace(value) && definition.IsRequired)
        {
            errors.Add($"Required parameter '{parameterName}' cannot be empty or whitespace");
        }

        if (value.Length > 1000) // Reasonable upper limit
        {
            warnings.Add($"Parameter '{parameterName}' value is very long ({value.Length} characters)");
        }
    }

    /// <summary>
    /// Validate boolean parameter
    /// </summary>
    private static void ValidateBoolean(
        string parameterName,
        bool value,
        ParameterDefinition definition,
        List<string> errors,
        List<string> warnings)
    {
        // No specific validation for boolean values
        // Could add business logic validation here if needed
    }

    /// <summary>
    /// Validate enum parameter
    /// </summary>
    private static void ValidateEnum(
        string parameterName,
        object value,
        ParameterDefinition definition,
        List<string> errors,
        List<string> warnings)
    {
        if (!Enum.IsDefined(definition.Type, value))
        {
            errors.Add($"Parameter '{parameterName}' value '{value}' is not a valid {definition.Type.Name} enum value");
        }
    }

    /// <summary>
    /// Validate common parameter dependencies
    /// </summary>
    private static void ValidateCommonDependencies(
        IParameterSet parameters,
        List<string> errors,
        List<string> warnings)
    {
        // Example validations would go here based on actual parameter names
        // For now, no generic common dependencies to validate
    }

    /// <summary>
    /// Validate trading-specific dependencies
    /// </summary>
    private static void ValidateTradingDependencies(
        IParameterSet parameters,
        List<string> errors,
        List<string> warnings)
    {
        // Example trading validations would go here based on actual parameter names
        // For now, no generic trading dependencies to validate
    }

    /// <summary>
    /// Validate risk management dependencies
    /// </summary>
    private static void ValidateRiskDependencies(
        IParameterSet parameters,
        List<string> errors,
        List<string> warnings)
    {
        // Example risk validations would go here based on actual parameter names
        // For now, no generic risk dependencies to validate
    }

    /// <summary>
    /// Validate parameter set completeness
    /// </summary>
    private static ValidationResult ValidateCompleteness(IParameterSet parameters)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check if all required parameters are set
        var statistics = ((ParameterSet)parameters).GetStatistics();
        if (!statistics.IsComplete)
        {
            var missing = statistics.RequiredParameters - statistics.RequiredParametersSet;
            errors.Add($"{missing} required parameter(s) are missing");
        }

        // Warn about optional parameters that might be important
        var setRatio = (decimal)statistics.SetParameters / statistics.TotalParameters;
        if (setRatio < 0.5m)
        {
            warnings.Add($"Only {setRatio:P0} of parameters are set - consider reviewing optional parameters");
        }

        return errors.Count == 0
            ? (warnings.Count == 0 ? ValidationResult.CreateSuccess() : ValidationResult.SuccessWithWarnings(warnings))
            : ValidationResult.Failure(errors, warnings);
    }

    #endregion
}
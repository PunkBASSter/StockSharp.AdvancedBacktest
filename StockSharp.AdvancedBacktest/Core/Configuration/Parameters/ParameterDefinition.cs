using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.Core.Configuration.Validation;

namespace StockSharp.AdvancedBacktest.Core.Configuration.Parameters;

/// <summary>
/// Strongly-typed parameter definition with C# 14 generic math support.
/// Provides type-safe parameter configuration with validation and enumeration capabilities.
/// </summary>
/// <typeparam name="T">The parameter value type, constrained to comparable numeric types</typeparam>
public sealed record ParameterDefinition<T> : ParameterDefinitionBase
    where T : struct, IComparable<T>, INumber<T>
{
    [JsonPropertyName("minValue")]
    public T? MinValue { get; init; }

    [JsonPropertyName("maxValue")]
    public T? MaxValue { get; init; }

    [JsonPropertyName("defaultValue")]
    public T? DefaultValue { get; init; }

    [JsonPropertyName("step")]
    public T? Step { get; init; }

    public ParameterDefinition(
        string name,
        T? minValue = null,
        T? maxValue = null,
        T? defaultValue = null,
        T? step = null,
        string? description = null,
        bool isRequired = false,
        string? validationPattern = null)
        : base(name, typeof(T), description, isRequired, validationPattern)
    {
        MinValue = minValue;
        MaxValue = maxValue;
        DefaultValue = defaultValue;
        Step = step;

        // Validate configuration at construction time
        ValidateConfiguration();
    }

    [JsonPropertyName("isNumeric")]
    public override bool IsNumeric => true;

    [JsonPropertyName("hasMinValue")]
    public override bool HasMinValue => MinValue.HasValue;

    [JsonPropertyName("hasMaxValue")]
    public override bool HasMaxValue => MaxValue.HasValue;

    [JsonPropertyName("hasDefaultValue")]
    public override bool HasDefaultValue => DefaultValue.HasValue;

    [JsonPropertyName("hasStep")]
    public override bool HasStep => Step.HasValue;

    public override object? GetMinValue() => MinValue;
    public override object? GetMaxValue() => MaxValue;
    public override object? GetDefaultValue() => DefaultValue;
    public override object? GetStep() => Step;

    /// <summary>
    /// Validates a typed value against this parameter definition using generic math.
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateValue(T? value)
    {
        var errors = new List<string>();

        // Check required
        if (IsRequired && !value.HasValue)
        {
            errors.Add($"Parameter '{Name}' is required but no value was provided");
            return ValidationResult.Failure(errors);
        }

        // Allow null for non-required parameters
        if (!value.HasValue)
        {
            return ValidationResult.CreateSuccess();
        }

        var actualValue = value.Value;

        // Range validation using generic math
        if (MinValue.HasValue && actualValue < MinValue.Value)
        {
            errors.Add($"Parameter '{Name}' value {actualValue} is below minimum {MinValue.Value}");
        }

        if (MaxValue.HasValue && actualValue > MaxValue.Value)
        {
            errors.Add($"Parameter '{Name}' value {actualValue} is above maximum {MaxValue.Value}");
        }

        // Step validation using generic math
        if (Step.HasValue && MinValue.HasValue)
        {
            var range = actualValue - MinValue.Value;
            var remainder = range % Step.Value;
            if (remainder != T.Zero)
            {
                errors.Add($"Parameter '{Name}' value {actualValue} is not aligned to step {Step.Value} from minimum {MinValue.Value}");
            }
        }

        return errors.Count == 0 ? ValidationResult.CreateSuccess() : ValidationResult.Failure(errors);
    }

    /// <summary>
    /// Type-erased validation implementation.
    /// </summary>
    public override ValidationResult ValidateValue(object? value)
    {
        if (value == null)
        {
            return ValidateValue(null);
        }

        if (value is T typedValue)
        {
            return ValidateValue(typedValue);
        }

        // Try to convert using generic math
        if (T.TryParse(value.ToString(), null, out var parsedValue))
        {
            return ValidateValue(parsedValue);
        }

        return ValidationResult.Failure($"Parameter '{Name}' expects type {typeof(T).Name} but got {value.GetType().Name}");
    }

    /// <summary>
    /// Generates all valid values within the defined range using streaming enumeration.
    /// Optimized for memory efficiency with O(1) space complexity.
    /// </summary>
    public override IEnumerable<object?> GenerateValidValues()
    {
        if (!MinValue.HasValue || !MaxValue.HasValue)
        {
            // Cannot enumerate without range
            if (DefaultValue.HasValue)
                yield return DefaultValue.Value;
            yield break;
        }

        var current = MinValue.Value;
        var max = MaxValue.Value;
        var step = Step ?? T.One;

        // Ensure we don't get stuck in infinite loop
        if (step <= T.Zero)
        {
            if (DefaultValue.HasValue)
                yield return DefaultValue.Value;
            yield break;
        }

        while (current <= max)
        {
            yield return current;

            // Use generic math for step increment
            var next = current + step;

            // Prevent infinite loop on overflow
            if (next <= current)
                break;

            current = next;
        }
    }

    /// <summary>
    /// Efficiently calculates the number of valid values without enumeration.
    /// </summary>
    public override long? GetValidValueCount()
    {
        if (!MinValue.HasValue || !MaxValue.HasValue)
            return null;

        var step = Step ?? T.One;
        if (step <= T.Zero)
            return 1; // Only default value

        try
        {
            // Convert to double for calculation to avoid overflow
            var min = Convert.ToDouble(MinValue.Value);
            var max = Convert.ToDouble(MaxValue.Value);
            var stepDouble = Convert.ToDouble(step);

            var count = Math.Floor((max - min) / stepDouble) + 1;
            return (long)Math.Min(count, long.MaxValue);
        }
        catch
        {
            // Fallback for types that don't convert to double
            return null;
        }
    }

    /// <summary>
    /// Creates a new parameter definition with updated range.
    /// </summary>
    public ParameterDefinition<T> WithRange(T? minValue, T? maxValue)
    {
        return this with { MinValue = minValue, MaxValue = maxValue };
    }

    /// <summary>
    /// Creates a new parameter definition with updated step.
    /// </summary>
    public ParameterDefinition<T> WithStep(T? step)
    {
        return this with { Step = step };
    }

    /// <summary>
    /// Type-erased range update implementation.
    /// </summary>
    public override ParameterDefinitionBase WithRange(object? minValue, object? maxValue)
    {
        var min = minValue switch
        {
            null => null,
            T t => (T?)t,
            _ when T.TryParse(minValue.ToString(), null, out var parsed) => parsed,
            _ => throw new ArgumentException($"Cannot convert minValue to type {typeof(T).Name}")
        };

        var max = maxValue switch
        {
            null => null,
            T t => (T?)t,
            _ when T.TryParse(maxValue.ToString(), null, out var parsed) => parsed,
            _ => throw new ArgumentException($"Cannot convert maxValue to type {typeof(T).Name}")
        };

        return WithRange(min, max);
    }

    /// <summary>
    /// Type-erased step update implementation.
    /// </summary>
    public override ParameterDefinitionBase WithStep(object? step)
    {
        var stepValue = step switch
        {
            null => null,
            T t => (T?)t,
            _ when T.TryParse(step.ToString(), null, out var parsed) => parsed,
            _ => throw new ArgumentException($"Cannot convert step to type {typeof(T).Name}")
        };

        return WithStep(stepValue);
    }

    /// <summary>
    /// Validates the parameter definition configuration for consistency.
    /// </summary>
    private void ValidateConfiguration()
    {
        if (MinValue.HasValue && MaxValue.HasValue && MinValue.Value > MaxValue.Value)
        {
            throw new ArgumentException($"Parameter '{Name}': MinValue ({MinValue.Value}) cannot be greater than MaxValue ({MaxValue.Value})");
        }

        if (Step.HasValue && Step.Value <= T.Zero)
        {
            throw new ArgumentException($"Parameter '{Name}': Step must be positive, got {Step.Value}");
        }

        if (DefaultValue.HasValue)
        {
            var validation = ValidateValue(DefaultValue.Value);
            if (!validation.IsValid)
            {
                throw new ArgumentException($"Parameter '{Name}': Default value {DefaultValue.Value} is invalid: {validation.GetFormattedIssues()}");
            }
        }
    }
}

/// <summary>
/// Factory methods for creating parameter definitions without explicit generic type parameters.
/// </summary>
public static class ParameterDefinition
{
    /// <summary>
    /// Creates a numeric parameter definition with automatic type inference.
    /// </summary>
    public static ParameterDefinition<T> CreateNumeric<T>(
        string name,
        T? minValue = null,
        T? maxValue = null,
        T? defaultValue = null,
        T? step = null,
        string? description = null,
        bool isRequired = false) where T : struct, IComparable<T>, INumber<T>
    {
        return new ParameterDefinition<T>(
            name: name,
            minValue: minValue,
            maxValue: maxValue,
            defaultValue: defaultValue,
            step: step,
            description: description,
            isRequired: isRequired
        );
    }

    /// <summary>
    /// Creates an integer parameter definition with common defaults.
    /// </summary>
    public static ParameterDefinition<int> CreateInteger(
        string name,
        int? minValue = null,
        int? maxValue = null,
        int? defaultValue = null,
        int step = 1,
        string? description = null,
        bool isRequired = false)
    {
        return CreateNumeric(name, minValue, maxValue, defaultValue, step, description, isRequired);
    }

    /// <summary>
    /// Creates a decimal parameter definition with common defaults.
    /// </summary>
    public static ParameterDefinition<decimal> CreateDecimal(
        string name,
        decimal? minValue = null,
        decimal? maxValue = null,
        decimal? defaultValue = null,
        decimal? step = null,
        string? description = null,
        bool isRequired = false)
    {
        return CreateNumeric(name, minValue, maxValue, defaultValue, step, description, isRequired);
    }

    /// <summary>
    /// Creates a double parameter definition with common defaults.
    /// </summary>
    public static ParameterDefinition<double> CreateDouble(
        string name,
        double? minValue = null,
        double? maxValue = null,
        double? defaultValue = null,
        double? step = null,
        string? description = null,
        bool isRequired = false)
    {
        return CreateNumeric(name, minValue, maxValue, defaultValue, step, description, isRequired);
    }
}
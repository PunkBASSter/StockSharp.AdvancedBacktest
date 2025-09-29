using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;

namespace StockSharp.AdvancedBacktest.Core.Configuration.Validation;

/// <summary>
/// Abstract base class for strongly-typed parameter validation rules.
/// Provides high-performance validation with compiled expression support.
/// </summary>
/// <typeparam name="T">The parameter type to validate</typeparam>
public abstract class ValidationRule<T> : IParameterValidationRule
{
    protected static readonly ValidationResult SuccessResult = ValidationResult.CreateSuccess();

    /// <summary>
    /// Gets the name of this validation rule for diagnostic purposes.
    /// </summary>
    public abstract string RuleName { get; }

    /// <summary>
    /// Gets whether this rule supports the specified parameter type.
    /// </summary>
    /// <param name="parameterType">The parameter type to check</param>
    /// <returns>True if the rule can validate this type</returns>
    public virtual bool SupportsType(Type parameterType)
    {
        return typeof(T).IsAssignableFrom(parameterType) ||
               parameterType.IsAssignableFrom(typeof(T));
    }

    /// <summary>
    /// Validates a parameter set (for cross-parameter validation).
    /// Override this method for rules that need to validate relationships between parameters.
    /// </summary>
    public virtual ValidationResult Validate(ParameterSet parameterSet)
    {
        return SuccessResult;
    }

    /// <summary>
    /// Validates a single parameter value within context.
    /// This method handles type conversion and delegates to the strongly-typed validation.
    /// </summary>
    public ValidationResult ValidateParameter(ParameterDefinitionBase definition, object? value, ParameterSet? context)
    {
        if (!SupportsType(definition.Type))
            return SuccessResult;

        try
        {
            var typedValue = ConvertValue(value);
            return ValidateTypedValue(definition, typedValue, context);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return ValidationResult.Failure($"Cannot convert value '{value}' to {typeof(T).Name} for rule '{RuleName}'");
        }
    }

    /// <summary>
    /// Validates a strongly-typed parameter value.
    /// Override this method to implement specific validation logic.
    /// </summary>
    /// <param name="definition">The parameter definition</param>
    /// <param name="value">The strongly-typed value to validate</param>
    /// <param name="context">Optional parameter set context for cross-parameter validation</param>
    /// <returns>Validation result</returns>
    protected abstract ValidationResult ValidateTypedValue(ParameterDefinitionBase definition, T value, ParameterSet? context);

    /// <summary>
    /// Fast validation without detailed error reporting.
    /// Override this method to provide optimized validation for performance-critical scenarios.
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public virtual bool IsValid(T value)
    {
        // Default implementation - create a dummy definition for validation
        var dummyDefinition = new DummyParameterDefinition(typeof(T));
        return ValidateTypedValue(dummyDefinition, value, null).IsValid;
    }

    /// <summary>
    /// Dummy parameter definition for fast validation scenarios.
    /// </summary>
    private sealed record DummyParameterDefinition : ParameterDefinitionBase
    {
        private readonly Type _type;

        public DummyParameterDefinition(Type type) : base("dummy", type)
        {
            _type = type;
        }

        public override bool IsNumeric =>
            _type == typeof(int) ||
            _type == typeof(decimal) ||
            _type == typeof(double) ||
            _type == typeof(float) ||
            _type == typeof(long);

        public override bool HasMinValue => false;
        public override bool HasMaxValue => false;
        public override bool HasDefaultValue => false;
        public override bool HasStep => false;

        public override object? GetMinValue() => null;
        public override object? GetMaxValue() => null;
        public override object? GetDefaultValue() => null;
        public override object? GetStep() => null;

        public override ValidationResult ValidateValue(object? value) => ValidationResult.CreateSuccess();
        public override IEnumerable<object?> GenerateValidValues() => Enumerable.Empty<object?>();
        public override long? GetValidValueCount() => 0;
        public override ParameterDefinitionBase WithRange(object? minValue, object? maxValue) => this;
        public override ParameterDefinitionBase WithStep(object? step) => this;
    }

    /// <summary>
    /// Converts an object value to the strongly-typed parameter value.
    /// Override this method for custom conversion logic.
    /// </summary>
    protected virtual T ConvertValue(object? value)
    {
        if (value == null)
        {
            if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                throw new InvalidCastException($"Cannot convert null to non-nullable {typeof(T).Name}");
            return default(T)!;
        }

        if (value is T directValue)
            return directValue;

        // Handle numeric conversions for INumber<T> types
        if (typeof(T).GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumber<>)))
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    /// <summary>
    /// Creates a validation result with a formatted error message for this rule.
    /// </summary>
    protected ValidationResult CreateError(string message, ParameterDefinitionBase? definition = null)
    {
        var fullMessage = definition != null
            ? $"[{RuleName}] Parameter '{definition.Name}': {message}"
            : $"[{RuleName}] {message}";

        return ValidationResult.Failure(fullMessage);
    }

    /// <summary>
    /// Creates a validation result with a formatted warning message for this rule.
    /// </summary>
    protected ValidationResult CreateWarning(string message, ParameterDefinitionBase? definition = null)
    {
        var fullMessage = definition != null
            ? $"[{RuleName}] Parameter '{definition.Name}': {message}"
            : $"[{RuleName}] {message}";

        return ValidationResult.SuccessWithWarnings(fullMessage);
    }
}

/// <summary>
/// Base class for numeric validation rules that work with INumber<T> types.
/// Provides optimized numeric operations and comparisons.
/// </summary>
/// <typeparam name="T">A numeric type implementing INumber<T></typeparam>
public abstract class NumericValidationRule<T> : ValidationRule<T>
    where T : struct, INumber<T>
{
    /// <summary>
    /// Compares two numeric values with high performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static int Compare(T left, T right)
    {
        if (left < right) return -1;
        if (left > right) return 1;
        return 0;
    }

    /// <summary>
    /// Checks if a numeric value is within the specified range (inclusive).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsInRange(T value, T min, T max)
    {
        return value >= min && value <= max;
    }

    /// <summary>
    /// Checks if a numeric value is a valid step increment from a base value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool IsValidStep(T value, T baseValue, T step)
    {
        if (step == T.Zero) return true;

        var difference = value - baseValue;
        var remainder = difference % step;
        return remainder == T.Zero;
    }
}

/// <summary>
/// Validation rule that combines multiple rules with logical AND operation.
/// All rules must pass for the validation to succeed.
/// </summary>
/// <typeparam name="T">The parameter type to validate</typeparam>
public sealed class CompositeAndValidationRule<T> : ValidationRule<T>
{
    private readonly ImmutableArray<ValidationRule<T>> _rules;

    public override string RuleName => $"CompositeAnd[{string.Join(", ", _rules.Select(r => r.RuleName))}]";

    public CompositeAndValidationRule(params ValidationRule<T>[] rules)
    {
        _rules = rules.ToImmutableArray();
    }

    public CompositeAndValidationRule(IEnumerable<ValidationRule<T>> rules)
    {
        _rules = rules.ToImmutableArray();
    }

    public override ValidationResult Validate(ParameterSet parameterSet)
    {
        var results = new List<ValidationResult>(_rules.Length);

        foreach (var rule in _rules)
        {
            var result = rule.Validate(parameterSet);
            results.Add(result);

            // Early exit on first failure for AND logic
            if (!result.IsValid)
                break;
        }

        return ValidationResult.Combine(results);
    }

    protected override ValidationResult ValidateTypedValue(ParameterDefinitionBase definition, T value, ParameterSet? context)
    {
        var results = new List<ValidationResult>(_rules.Length);

        foreach (var rule in _rules)
        {
            var result = rule.ValidateParameter(definition, value, context);
            results.Add(result);

            // Early exit on first failure for AND logic
            if (!result.IsValid)
                break;
        }

        return ValidationResult.Combine(results);
    }
}

/// <summary>
/// Validation rule that combines multiple rules with logical OR operation.
/// At least one rule must pass for the validation to succeed.
/// </summary>
/// <typeparam name="T">The parameter type to validate</typeparam>
public sealed class CompositeOrValidationRule<T> : ValidationRule<T>
{
    private readonly ImmutableArray<ValidationRule<T>> _rules;

    public override string RuleName => $"CompositeOr[{string.Join(", ", _rules.Select(r => r.RuleName))}]";

    public CompositeOrValidationRule(params ValidationRule<T>[] rules)
    {
        _rules = rules.ToImmutableArray();
    }

    public CompositeOrValidationRule(IEnumerable<ValidationRule<T>> rules)
    {
        _rules = rules.ToImmutableArray();
    }

    public override ValidationResult Validate(ParameterSet parameterSet)
    {
        var allResults = new List<ValidationResult>(_rules.Length);

        foreach (var rule in _rules)
        {
            var result = rule.Validate(parameterSet);
            allResults.Add(result);

            // Early exit on first success for OR logic
            if (result.IsValid)
                return result;
        }

        // If we get here, all rules failed - combine all errors
        return ValidationResult.Combine(allResults);
    }

    protected override ValidationResult ValidateTypedValue(ParameterDefinitionBase definition, T value, ParameterSet? context)
    {
        var allResults = new List<ValidationResult>(_rules.Length);

        foreach (var rule in _rules)
        {
            var result = rule.ValidateParameter(definition, value, context);
            allResults.Add(result);

            // Early exit on first success for OR logic
            if (result.IsValid)
                return result;
        }

        // If we get here, all rules failed - combine all errors
        return ValidationResult.Combine(allResults);
    }
}
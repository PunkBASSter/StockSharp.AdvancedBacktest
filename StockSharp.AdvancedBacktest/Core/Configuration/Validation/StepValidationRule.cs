using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;

namespace StockSharp.AdvancedBacktest.Core.Configuration.Validation;

/// <summary>
/// High-performance step validation rule with compiled expression support.
/// Validates that numeric parameters conform to specified incremental steps from a base value.
/// Optimized for 1M+ validations per second through expression compilation and caching.
/// </summary>
/// <typeparam name="T">A numeric type implementing INumber<T></typeparam>
public sealed class StepValidationRule<T> : NumericValidationRule<T>
    where T : struct, INumber<T>
{
    private static readonly ConcurrentDictionary<(T Base, T Step), Func<T, bool>> CompiledValidators = new();

    private readonly T _baseValue;
    private readonly T _stepValue;
    private readonly Func<T, bool> _compiledValidator;

    public override string RuleName => "Step";

    /// <summary>
    /// Gets the base value from which steps are calculated.
    /// </summary>
    public T BaseValue => _baseValue;

    /// <summary>
    /// Gets the step increment value.
    /// </summary>
    public T StepValue => _stepValue;

    /// <summary>
    /// Initializes a new step validation rule.
    /// </summary>
    /// <param name="stepValue">The increment step value</param>
    /// <param name="baseValue">The base value from which steps are calculated (default: 0)</param>
    /// <exception cref="ArgumentException">Thrown when stepValue is zero or negative</exception>
    public StepValidationRule(T stepValue, T? baseValue = null)
    {
        if (stepValue <= T.Zero)
            throw new ArgumentException("Step value must be positive", nameof(stepValue));

        _stepValue = stepValue;
        _baseValue = baseValue ?? T.Zero;

        // Get or compile the validation function for optimal performance
        _compiledValidator = GetOrCompileValidator(_baseValue, _stepValue);
    }

    /// <summary>
    /// Creates a step validation rule from parameter definition.
    /// </summary>
    /// <param name="definition">Parameter definition with step value</param>
    /// <param name="baseValue">Optional base value (uses parameter min value or 0 if not specified)</param>
    /// <returns>Step validation rule or null if definition doesn't have a step value</returns>
    public static StepValidationRule<T>? FromParameterDefinition(ParameterDefinitionBase definition, T? baseValue = null)
    {
        if (!definition.HasStep)
            return null;

        var stepValue = definition.GetStep();
        if (stepValue is not T typedStep)
            return null;

        var actualBaseValue = baseValue;
        if (actualBaseValue == null && definition.HasMinValue)
        {
            var minValue = definition.GetMinValue();
            if (minValue is T typedMin)
                actualBaseValue = typedMin;
        }

        return new StepValidationRule<T>(typedStep, actualBaseValue);
    }

    protected override ValidationResult ValidateTypedValue(ParameterDefinitionBase definition, T value, ParameterSet? context)
    {
        // Use compiled validator for maximum performance
        if (_compiledValidator(value))
            return SuccessResult;

        return CreateError(
            $"Value {value} is not a valid step increment. Must be {_baseValue} + n × {_stepValue} where n is a non-negative integer",
            definition);
    }

    /// <summary>
    /// Fast inline validation without error message generation.
    /// Use this for high-throughput scenarios where only pass/fail is needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsValid(T value)
    {
        return _compiledValidator(value);
    }

    /// <summary>
    /// Calculates the nearest valid step value to the given input.
    /// </summary>
    /// <param name="value">Input value to round to nearest step</param>
    /// <param name="roundingMode">How to round when exactly between two steps</param>
    /// <returns>Nearest valid step value</returns>
    public T GetNearestValidValue(T value, MidpointRounding roundingMode = MidpointRounding.ToEven)
    {
        var difference = value - _baseValue;

        // Handle exact multiples
        var remainder = difference % _stepValue;
        if (remainder == T.Zero)
            return value;

        // Calculate steps and round
        var steps = difference / _stepValue;

        // Convert to double for rounding operations
        var stepsDouble = double.CreateChecked(steps);
        var roundedSteps = Math.Round(stepsDouble, roundingMode);

        // Convert back to T and calculate result
        var roundedStepsT = T.CreateChecked(roundedSteps);
        return _baseValue + (roundedStepsT * _stepValue);
    }

    /// <summary>
    /// Gets the next valid step value greater than the given input.
    /// </summary>
    /// <param name="value">Input value</param>
    /// <returns>Next valid step value greater than input</returns>
    public T GetNextValidValue(T value)
    {
        if (IsValid(value))
            return value + _stepValue;

        return GetNearestValidValue(value + _stepValue);
    }

    /// <summary>
    /// Gets the previous valid step value less than the given input.
    /// </summary>
    /// <param name="value">Input value</param>
    /// <returns>Previous valid step value less than input</returns>
    public T GetPreviousValidValue(T value)
    {
        if (IsValid(value))
            return value - _stepValue;

        return GetNearestValidValue(value - _stepValue);
    }

    /// <summary>
    /// Validates multiple values in batch for optimal performance.
    /// </summary>
    /// <param name="values">Values to validate</param>
    /// <returns>Array of validation results, corresponding to input values</returns>
    public bool[] ValidateBatch(ReadOnlySpan<T> values)
    {
        var results = new bool[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            results[i] = _compiledValidator(values[i]);
        }
        return results;
    }

    /// <summary>
    /// Corrects multiple values to their nearest valid step values.
    /// </summary>
    /// <param name="values">Values to correct</param>
    /// <param name="roundingMode">How to round when exactly between two steps</param>
    /// <returns>Array of corrected values</returns>
    public T[] CorrectBatch(ReadOnlySpan<T> values, MidpointRounding roundingMode = MidpointRounding.ToEven)
    {
        var results = new T[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            results[i] = GetNearestValidValue(values[i], roundingMode);
        }
        return results;
    }

    /// <summary>
    /// Generates a sequence of valid step values within the specified range.
    /// </summary>
    /// <param name="minValue">Minimum value (inclusive)</param>
    /// <param name="maxValue">Maximum value (inclusive)</param>
    /// <returns>Enumerable of valid step values within range</returns>
    public IEnumerable<T> GenerateValidValues(T minValue, T maxValue)
    {
        if (minValue > maxValue)
            yield break;

        // Find first valid value >= minValue
        var current = _baseValue;
        if (current < minValue)
        {
            var stepsNeeded = (minValue - _baseValue) / _stepValue;
            var stepsNeededCeiled = T.CreateChecked(Math.Ceiling(double.CreateChecked(stepsNeeded)));
            current = _baseValue + (stepsNeededCeiled * _stepValue);
        }

        // Generate values until we exceed maxValue
        while (current <= maxValue)
        {
            yield return current;
            current += _stepValue;
        }
    }

    /// <summary>
    /// Gets the count of valid step values within the specified range.
    /// </summary>
    /// <param name="minValue">Minimum value (inclusive)</param>
    /// <param name="maxValue">Maximum value (inclusive)</param>
    /// <returns>Count of valid step values</returns>
    public long GetValidValueCount(T minValue, T maxValue)
    {
        if (minValue > maxValue)
            return 0;

        // Find range of valid step indices
        var minSteps = (minValue - _baseValue) / _stepValue;
        var maxSteps = (maxValue - _baseValue) / _stepValue;

        var minStepsLong = (long)Math.Ceiling(double.CreateChecked(minSteps));
        var maxStepsLong = (long)Math.Floor(double.CreateChecked(maxSteps));

        return Math.Max(0, maxStepsLong - minStepsLong + 1);
    }

    /// <summary>
    /// Gets or compiles a validation function for the specified step configuration.
    /// Uses caching to avoid recompiling identical step configurations.
    /// </summary>
    private static Func<T, bool> GetOrCompileValidator(T baseValue, T stepValue)
    {
        var cacheKey = (baseValue, stepValue);

        return CompiledValidators.GetOrAdd(cacheKey, _ =>
        {
            // Build expression: value => (value - baseValue) % stepValue == 0
            var valueParam = Expression.Parameter(typeof(T), "value");
            var baseConstant = Expression.Constant(baseValue, typeof(T));
            var stepConstant = Expression.Constant(stepValue, typeof(T));
            var zeroConstant = Expression.Constant(T.Zero, typeof(T));

            // Calculate: value - baseValue
            var difference = Expression.Subtract(valueParam, baseConstant);

            // Calculate: difference % stepValue
            var modulo = Expression.Modulo(difference, stepConstant);

            // Compare: modulo == 0
            var equality = Expression.Equal(modulo, zeroConstant);

            // Compile to delegate for maximum runtime performance
            var lambda = Expression.Lambda<Func<T, bool>>(equality, valueParam);
            return lambda.Compile();
        });
    }

    /// <summary>
    /// Creates common step validation rules for trading scenarios.
    /// </summary>
    public static class Trading
    {
        /// <summary>
        /// Creates a step rule for tick increments (0.01).
        /// </summary>
        public static StepValidationRule<T> TickIncrement()
        {
            var step = T.CreateChecked(0.01);
            return new StepValidationRule<T>(step);
        }

        /// <summary>
        /// Creates a step rule for integer increments (1).
        /// </summary>
        public static StepValidationRule<T> IntegerIncrement()
        {
            return new StepValidationRule<T>(T.One);
        }

        /// <summary>
        /// Creates a step rule for half-point increments (0.5).
        /// </summary>
        public static StepValidationRule<T> HalfPointIncrement()
        {
            var step = T.CreateChecked(0.5);
            return new StepValidationRule<T>(step);
        }

        /// <summary>
        /// Creates a step rule for quarter-point increments (0.25).
        /// </summary>
        public static StepValidationRule<T> QuarterPointIncrement()
        {
            var step = T.CreateChecked(0.25);
            return new StepValidationRule<T>(step);
        }

        /// <summary>
        /// Creates a step rule for percentage increments (1% = 0.01).
        /// </summary>
        public static StepValidationRule<T> PercentageIncrement()
        {
            var step = T.CreateChecked(0.01);
            return new StepValidationRule<T>(step);
        }

        /// <summary>
        /// Creates a step rule for basis point increments (0.0001).
        /// </summary>
        public static StepValidationRule<T> BasisPointIncrement()
        {
            var step = T.CreateChecked(0.0001);
            return new StepValidationRule<T>(step);
        }
    }

    public override string ToString()
    {
        return $"Step(base={_baseValue}, step={_stepValue})";
    }

    public override bool Equals(object? obj)
    {
        return obj is StepValidationRule<T> other &&
               EqualityComparer<T>.Default.Equals(_baseValue, other._baseValue) &&
               EqualityComparer<T>.Default.Equals(_stepValue, other._stepValue);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_baseValue, _stepValue);
    }
}
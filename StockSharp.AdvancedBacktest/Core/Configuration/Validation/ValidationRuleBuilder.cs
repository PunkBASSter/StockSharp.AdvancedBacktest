using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Numerics;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;

namespace StockSharp.AdvancedBacktest.Core.Configuration.Validation;

/// <summary>
/// Fluent builder for creating and composing complex validation rules.
/// Supports method chaining, conditional logic, and rule composition for maximum flexibility.
/// </summary>
/// <typeparam name="T">The parameter type to validate</typeparam>
public sealed class ValidationRuleBuilder<T>
{
    private readonly List<ValidationRule<T>> _rules = new();
    private readonly List<IParameterValidationRule> _globalRules = new();
    private string? _description;
    private CompositionMode _compositionMode = CompositionMode.And;

    public enum CompositionMode
    {
        And,    // All rules must pass
        Or      // At least one rule must pass
    }

    /// <summary>
    /// Sets the composition mode for combining multiple rules.
    /// </summary>
    public ValidationRuleBuilder<T> WithCompositionMode(CompositionMode mode)
    {
        _compositionMode = mode;
        return this;
    }

    /// <summary>
    /// Sets a description for the composed rule.
    /// </summary>
    public ValidationRuleBuilder<T> WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Adds a range validation rule (only works with numeric types).
    /// </summary>
    public ValidationRuleBuilder<T> WithRange(T minValue, T maxValue, bool minInclusive = true, bool maxInclusive = true)
    {
        // Use reflection to create the rule for numeric types
        var rangeType = typeof(RangeValidationRule<>).MakeGenericType(typeof(T));
        var rule = Activator.CreateInstance(rangeType, minValue, maxValue, minInclusive, maxInclusive);
        if (rule is ValidationRule<T> validationRule)
        {
            _rules.Add(validationRule);
        }
        return this;
    }

    /// <summary>
    /// Adds a step validation rule (only works with numeric types).
    /// </summary>
    public ValidationRuleBuilder<T> WithStep(T stepValue, object? baseValue = null)
    {
        // Use reflection to create the rule for numeric types
        var stepType = typeof(StepValidationRule<>).MakeGenericType(typeof(T));
        var rule = Activator.CreateInstance(stepType, stepValue, baseValue);
        if (rule is ValidationRule<T> validationRule)
        {
            _rules.Add(validationRule);
        }
        return this;
    }

    /// <summary>
    /// Adds a custom validation rule using a lambda expression.
    /// </summary>
    public ValidationRuleBuilder<T> WithCustom(Expression<Func<T, bool>> validator, string description, Func<T, string>? errorGenerator = null)
    {
        _rules.Add(new CustomValidationRule<T>(validator, description, errorGenerator));
        return this;
    }

    /// <summary>
    /// Adds a custom validation rule using a compiled function.
    /// </summary>
    public ValidationRuleBuilder<T> WithCustomFunc(Func<T, bool> validator, string description, Func<T, string>? errorGenerator = null)
    {
        _rules.Add(new CustomValidationRule<T>(validator, null, description, errorGenerator));
        return this;
    }

    /// <summary>
    /// Adds an existing validation rule.
    /// </summary>
    public ValidationRuleBuilder<T> WithRule(ValidationRule<T> rule)
    {
        _rules.Add(rule);
        return this;
    }

    /// <summary>
    /// Adds multiple existing validation rules.
    /// </summary>
    public ValidationRuleBuilder<T> WithRules(params ValidationRule<T>[] rules)
    {
        _rules.AddRange(rules);
        return this;
    }

    /// <summary>
    /// Adds multiple existing validation rules.
    /// </summary>
    public ValidationRuleBuilder<T> WithRules(IEnumerable<ValidationRule<T>> rules)
    {
        _rules.AddRange(rules);
        return this;
    }

    /// <summary>
    /// Adds a conditional rule that only applies when a condition is met.
    /// </summary>
    public ValidationRuleBuilder<T> When(Func<T, bool> condition, Action<ConditionalRuleBuilder<T>> configureRule)
    {
        var conditionalBuilder = new ConditionalRuleBuilder<T>(condition);
        configureRule(conditionalBuilder);
        var conditionalRule = conditionalBuilder.Build();

        if (conditionalRule != null)
            _rules.Add(conditionalRule);

        return this;
    }

    /// <summary>
    /// Adds a global rule that operates on the entire parameter set.
    /// </summary>
    public ValidationRuleBuilder<T> WithGlobalRule(IParameterValidationRule rule)
    {
        _globalRules.Add(rule);
        return this;
    }

    /// <summary>
    /// Adds a dependency rule for cross-parameter validation.
    /// </summary>
    public ValidationRuleBuilder<T> WithDependency(DependencyValidationRule dependencyRule)
    {
        _globalRules.Add(dependencyRule);
        return this;
    }

    /// <summary>
    /// Builds the final validation rule based on the configured options.
    /// </summary>
    public ValidationRule<T>? Build()
    {
        if (_rules.Count == 0)
            return null;

        if (_rules.Count == 1)
            return _rules[0];

        return _compositionMode switch
        {
            CompositionMode.And => new CompositeAndValidationRule<T>(_rules),
            CompositionMode.Or => new CompositeOrValidationRule<T>(_rules),
            _ => throw new InvalidOperationException($"Unknown composition mode: {_compositionMode}")
        };
    }

    /// <summary>
    /// Builds a parameter validator that includes both typed rules and global rules.
    /// </summary>
    public ParameterValidator BuildValidator(string? parameterName = null)
    {
        var typedRule = Build();
        var globalRules = _globalRules.ToList();

        if (typedRule != null)
            globalRules.Add(typedRule);

        var parameterSpecificRules = parameterName != null && typedRule != null
            ? new Dictionary<string, IEnumerable<IParameterValidationRule>> { [parameterName] = new[] { typedRule } }
            : null;

        return new ParameterValidator(globalRules, parameterSpecificRules);
    }

    /// <summary>
    /// Creates a new builder for the specified type.
    /// </summary>
    public static ValidationRuleBuilder<TNew> For<TNew>()
    {
        return new ValidationRuleBuilder<TNew>();
    }

    /// <summary>
    /// Creates a builder with common numeric validation rules.
    /// </summary>
    public static ValidationRuleBuilder<TNumeric> ForNumeric<TNumeric>()
    {
        return new ValidationRuleBuilder<TNumeric>();
    }

    /// <summary>
    /// Creates a builder with common trading validation rules.
    /// </summary>
    public static ValidationRuleBuilder<TNumeric> ForTrading<TNumeric>()
    {
        return new ValidationRuleBuilder<TNumeric>();
    }
}

/// <summary>
/// Builder for conditional validation rules.
/// </summary>
/// <typeparam name="T">The parameter type to validate</typeparam>
public sealed class ConditionalRuleBuilder<T>
{
    private readonly Func<T, bool> _condition;
    private readonly List<ValidationRule<T>> _thenRules = new();
    private readonly List<ValidationRule<T>> _elseRules = new();

    internal ConditionalRuleBuilder(Func<T, bool> condition)
    {
        _condition = condition;
    }

    /// <summary>
    /// Adds a rule that applies when the condition is true.
    /// </summary>
    public ConditionalRuleBuilder<T> Then(ValidationRule<T> rule)
    {
        _thenRules.Add(rule);
        return this;
    }

    /// <summary>
    /// Adds a custom rule that applies when the condition is true.
    /// </summary>
    public ConditionalRuleBuilder<T> Then(Func<T, bool> validator, string description)
    {
        _thenRules.Add(new CustomValidationRule<T>(validator, null, description));
        return this;
    }

    /// <summary>
    /// Adds a range rule that applies when the condition is true (only works with numeric types).
    /// </summary>
    public ConditionalRuleBuilder<T> ThenRange(T minValue, T maxValue)
    {
        // Use reflection to create the rule for numeric types
        var rangeType = typeof(RangeValidationRule<>).MakeGenericType(typeof(T));
        var rule = Activator.CreateInstance(rangeType, minValue, maxValue, true, true);
        if (rule is ValidationRule<T> validationRule)
        {
            _thenRules.Add(validationRule);
        }
        return this;
    }

    /// <summary>
    /// Adds a rule that applies when the condition is false.
    /// </summary>
    public ConditionalRuleBuilder<T> Else(ValidationRule<T> rule)
    {
        _elseRules.Add(rule);
        return this;
    }

    /// <summary>
    /// Adds a custom rule that applies when the condition is false.
    /// </summary>
    public ConditionalRuleBuilder<T> Else(Func<T, bool> validator, string description)
    {
        _elseRules.Add(new CustomValidationRule<T>(validator, null, description));
        return this;
    }

    /// <summary>
    /// Builds the conditional validation rule.
    /// </summary>
    internal CustomValidationRule<T>? Build()
    {
        if (_thenRules.Count == 0 && _elseRules.Count == 0)
            return null;

        return new CustomValidationRule<T>(
            value =>
            {
                if (_condition(value))
                {
                    // Condition is true, validate with 'then' rules
                    return _thenRules.Count == 0 || _thenRules.All(rule => rule.IsValid(value));
                }
                else
                {
                    // Condition is false, validate with 'else' rules
                    return _elseRules.Count == 0 || _elseRules.All(rule => rule.IsValid(value));
                }
            },
            null,
            $"Conditional validation based on custom condition"
        );
    }

    // Removed ValidationRuleExtensions from here - moved to separate file
}

/// <summary>
/// Factory for creating commonly used validation rule combinations.
/// </summary>
public static class ValidationRuleFactory
{
    /// <summary>
    /// Creates a complete numeric parameter validator with range, step, and custom validations.
    /// </summary>
    public static ValidationRule<T> CreateNumericValidator<T>(
        T minValue,
        T maxValue,
        T? stepValue = null,
        IEnumerable<Func<T, bool>>? customValidations = null)
        where T : struct, INumber<T>
    {
        var builder = ValidationRuleBuilder<T>.ForNumeric<T>()
            .WithRange(minValue, maxValue);

        if (stepValue.HasValue)
            builder.WithStep(stepValue.Value);

        if (customValidations != null)
        {
            foreach (var validation in customValidations)
            {
                builder.WithCustomFunc(validation, "Custom numeric validation");
            }
        }

        return builder.Build() ?? throw new InvalidOperationException("Failed to create numeric validator");
    }

    /// <summary>
    /// Creates a trading-specific parameter validator with reasonable defaults.
    /// </summary>
    public static ValidationRule<decimal> CreateTradingValidator(
        decimal minValue = 0m,
        decimal maxValue = 1_000_000m,
        decimal? stepValue = null,
        bool allowNegative = false)
    {
        var actualMin = allowNegative ? -Math.Abs(maxValue) : Math.Max(0, minValue);

        var builder = ValidationRuleBuilder<decimal>.ForTrading<decimal>()
            .WithRange(actualMin, maxValue);

        if (stepValue.HasValue)
            builder.WithStep(stepValue.Value);

        return builder.Build() ?? throw new InvalidOperationException("Failed to create trading validator");
    }

    /// <summary>
    /// Creates a moving average parameter validator ensuring slow > fast.
    /// </summary>
    public static ParameterValidator CreateMovingAverageValidator(
        string fastMaParam,
        string slowMaParam,
        int minPeriod = 2,
        int maxPeriod = 200)
    {
        var fastMaRule = ValidationRuleBuilder<int>.ForNumeric<int>()
            .WithRange(minPeriod, maxPeriod)
            .Build();

        var slowMaRule = ValidationRuleBuilder<int>.ForNumeric<int>()
            .WithRange(minPeriod, maxPeriod)
            .Build();

        var dependencyRule = DependencyValidationRule.Trading.MovingAverageOrder<int>(fastMaParam, slowMaParam);

        var globalRules = new List<IParameterValidationRule> { dependencyRule };
        var parameterRules = new Dictionary<string, IEnumerable<IParameterValidationRule>>();

        if (fastMaRule != null)
            parameterRules[fastMaParam] = new[] { fastMaRule };

        if (slowMaRule != null)
            parameterRules[slowMaParam] = new[] { slowMaRule };

        return new ParameterValidator(globalRules, parameterRules);
    }

    /// <summary>
    /// Creates a comprehensive risk management validator.
    /// </summary>
    public static ParameterValidator CreateRiskManagementValidator(
        string positionSizeParam,
        string stopLossParam,
        string takeProfitParam,
        string riskPercentParam,
        decimal maxRiskPercent = 0.02m)
    {
        var positionSizeRule = ValidationRuleBuilder<decimal>.ForTrading<decimal>()
            .WithRange(0.01m, 1_000_000m)
            .WithCustomFunc(size => size > 0, "Position size must be positive")
            .Build();

        var stopLossRule = ValidationRuleBuilder<decimal>.ForTrading<decimal>()
            .WithRange(-1_000_000m, 1_000_000m)
            .Build();

        var takeProfitRule = ValidationRuleBuilder<decimal>.ForTrading<decimal>()
            .WithRange(-1_000_000m, 1_000_000m)
            .Build();

        var riskPercentRule = ValidationRuleBuilder<decimal>.ForTrading<decimal>()
            .WithRange(0.0001m, 0.1m) // 0.01% to 10%
            .Build();

        var globalRules = new List<IParameterValidationRule>
        {
            DependencyValidationRule.Trading.StopLossTakeProfitLong<decimal>(stopLossParam, takeProfitParam),
            DependencyValidationRule.Trading.RiskPercentageLimit<decimal>(riskPercentParam, maxRiskPercent)
        };

        var parameterRules = new Dictionary<string, IEnumerable<IParameterValidationRule>>();

        if (positionSizeRule != null)
            parameterRules[positionSizeParam] = new[] { positionSizeRule };
        if (stopLossRule != null)
            parameterRules[stopLossParam] = new[] { stopLossRule };
        if (takeProfitRule != null)
            parameterRules[takeProfitParam] = new[] { takeProfitRule };
        if (riskPercentRule != null)
            parameterRules[riskPercentParam] = new[] { riskPercentRule };

        return new ParameterValidator(globalRules, parameterRules);
    }
}
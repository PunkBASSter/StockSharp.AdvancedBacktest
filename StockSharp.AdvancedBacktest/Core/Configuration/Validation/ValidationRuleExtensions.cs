namespace StockSharp.AdvancedBacktest.Core.Configuration.Validation;

/// <summary>
/// Extension methods for ValidationRule to support fluent composition and chaining.
/// </summary>
public static class ValidationRuleExtensions
{
    /// <summary>
    /// Combines this rule with another rule using AND logic.
    /// </summary>
    public static CompositeAndValidationRule<T> And<T>(this ValidationRule<T> first, ValidationRule<T> second)
    {
        return new CompositeAndValidationRule<T>(first, second);
    }

    /// <summary>
    /// Combines this rule with another rule using OR logic.
    /// </summary>
    public static CompositeOrValidationRule<T> Or<T>(this ValidationRule<T> first, ValidationRule<T> second)
    {
        return new CompositeOrValidationRule<T>(first, second);
    }

    /// <summary>
    /// Creates a conditional wrapper around this rule.
    /// </summary>
    public static CustomValidationRule<T> When<T>(this ValidationRule<T> rule, Func<T, bool> condition)
    {
        return new CustomValidationRule<T>(
            value => !condition(value) || rule.IsValid(value),
            null,
            $"Conditional application of {rule.RuleName}"
        );
    }
}
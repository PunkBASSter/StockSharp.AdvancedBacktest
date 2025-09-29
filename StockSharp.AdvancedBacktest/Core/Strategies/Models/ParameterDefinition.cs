// Backward compatibility alias for existing code
// This file provides aliases to maintain compatibility while new code uses the enhanced implementation

using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Configuration.Validation;
using System.Collections.Immutable;
using System.Numerics;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

/// <summary>
/// Backward compatibility alias for ParameterDefinitionBase.
/// New code should use StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterDefinitionBase.
/// </summary>
[Obsolete("Use StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterDefinitionBase instead. This alias will be removed in v3.0.")]
public abstract record ParameterDefinition : ParameterDefinitionBase
{
    protected ParameterDefinition(string name, Type type, string? description = null, bool isRequired = false, string? validationPattern = null)
        : base(name, type, description, isRequired, validationPattern)
    {
    }

    // Legacy factory methods for backward compatibility
    public static ParameterDefinition<T> CreateNumeric<T>(
        string name,
        T? minValue = null,
        T? maxValue = null,
        T? defaultValue = null,
        string? description = null,
        bool isRequired = false) where T : struct, IComparable<T>, INumber<T>
    {
        return StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterDefinition.CreateNumeric(
            name, minValue, maxValue, defaultValue, null, description, isRequired);
    }

    // Note: Non-generic factory methods removed - they were problematic with the new generic math constraints
    // Existing code using CreateString, CreateBoolean, CreateEnum should be updated to use the new typed approach
}

/// <summary>
/// Backward compatibility alias for ValidationResult.
/// New code should use StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult.
/// </summary>
[Obsolete("Use StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult instead. This alias will be removed in v3.0.")]
public sealed class ValidationResult
{
    private readonly StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult _inner;

    public ValidationResult(bool isValid, ImmutableArray<string> errors, ImmutableArray<string> warnings)
    {
        _inner = new StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult(isValid, errors, warnings);
    }

    public bool IsValid => _inner.IsValid;
    public ImmutableArray<string> Errors => _inner.Errors;
    public ImmutableArray<string> Warnings => _inner.Warnings;
    public bool HasErrors => _inner.HasErrors;
    public bool HasWarnings => _inner.HasWarnings;
    public int TotalIssues => _inner.TotalIssues;

    public static ValidationResult CreateSuccess() => new(true, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
    public static ValidationResult SuccessWithWarnings(params string[] warnings) => new(true, ImmutableArray<string>.Empty, warnings.ToImmutableArray());
    public static ValidationResult Failure(params string[] errors) => new(false, errors.ToImmutableArray(), ImmutableArray<string>.Empty);
    public static ValidationResult Failure(IEnumerable<string> errors) => new(false, errors.ToImmutableArray(), ImmutableArray<string>.Empty);
    public static ValidationResult Failure(IEnumerable<string> errors, IEnumerable<string> warnings) => new(false, errors.ToImmutableArray(), warnings.ToImmutableArray());

    public string GetFormattedIssues() => _inner.GetFormattedIssues();

    // Conversion methods instead of implicit operators
    public static ValidationResult FromEnhanced(StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult result)
    {
        return new ValidationResult(result.IsValid, result.Errors, result.Warnings);
    }

    public StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult ToEnhanced()
    {
        return _inner;
    }

    // Implicit conversion TO enhanced version only
    public static implicit operator StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult(ValidationResult result)
    {
        return result._inner;
    }
}
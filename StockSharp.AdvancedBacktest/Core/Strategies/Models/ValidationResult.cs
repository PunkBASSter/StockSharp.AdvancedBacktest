using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public record ValidationResult(
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("errors")] ImmutableArray<string> Errors,
    [property: JsonPropertyName("warnings")] ImmutableArray<string> Warnings
)
{
    /// <summary>
    /// Successful validation result
    /// </summary>
    public static readonly ValidationResult Success = new(true, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);

    /// <summary>
    /// Whether there are any errors
    /// </summary>
    [JsonPropertyName("hasErrors")]
    public bool HasErrors => !Errors.IsDefaultOrEmpty;

    /// <summary>
    /// Whether there are any warnings
    /// </summary>
    [JsonPropertyName("hasWarnings")]
    public bool HasWarnings => !Warnings.IsDefaultOrEmpty;

    /// <summary>
    /// Total number of issues (errors + warnings)
    /// </summary>
    [JsonPropertyName("totalIssues")]
    public int TotalIssues => Errors.Length + Warnings.Length;

    /// <summary>
    /// Create a successful validation result
    /// </summary>
    public static ValidationResult CreateSuccess() => Success;

    /// <summary>
    /// Create a successful validation result with warnings
    /// </summary>
    public static ValidationResult SuccessWithWarnings(params string[] warnings) =>
        new(true, ImmutableArray<string>.Empty, warnings.ToImmutableArray());

    /// <summary>
    /// Create a successful validation result with warnings
    /// </summary>
    public static ValidationResult SuccessWithWarnings(IEnumerable<string> warnings) =>
        new(true, ImmutableArray<string>.Empty, warnings.ToImmutableArray());

    /// <summary>
    /// Create a failed validation result
    /// </summary>
    public static ValidationResult Failure(params string[] errors) =>
        new(false, errors.ToImmutableArray(), ImmutableArray<string>.Empty);

    /// <summary>
    /// Create a failed validation result
    /// </summary>
    public static ValidationResult Failure(IEnumerable<string> errors) =>
        new(false, errors.ToImmutableArray(), ImmutableArray<string>.Empty);

    /// <summary>
    /// Create a failed validation result with warnings
    /// </summary>
    public static ValidationResult Failure(IEnumerable<string> errors, IEnumerable<string> warnings) =>
        new(false, errors.ToImmutableArray(), warnings.ToImmutableArray());

    /// <summary>
    /// Combine multiple validation results
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        if (results.Length == 0)
            return CreateSuccess();

        var allErrors = results
            .Where(r => !r.Errors.IsDefaultOrEmpty)
            .SelectMany(r => r.Errors)
            .ToImmutableArray();
        var allWarnings = results
            .Where(r => !r.Warnings.IsDefaultOrEmpty)
            .SelectMany(r => r.Warnings)
            .ToImmutableArray();
        var isValid = results.All(r => r.IsValid);

        return new ValidationResult(isValid, allErrors, allWarnings);
    }

    /// <summary>
    /// Combine multiple validation results
    /// </summary>
    public static ValidationResult Combine(IEnumerable<ValidationResult> results) =>
        Combine(results.ToArray());

    /// <summary>
    /// Add an error to this validation result
    /// </summary>
    public ValidationResult WithError(string error) =>
        this with { IsValid = false, Errors = Errors.IsDefault ? ImmutableArray.Create(error) : Errors.Add(error) };

    /// <summary>
    /// Add multiple errors to this validation result
    /// </summary>
    public ValidationResult WithErrors(params string[] errors) =>
        this with { IsValid = false, Errors = Errors.IsDefault ? errors.ToImmutableArray() : Errors.AddRange(errors) };

    /// <summary>
    /// Add a warning to this validation result
    /// </summary>
    public ValidationResult WithWarning(string warning) =>
        this with { Warnings = Warnings.IsDefault ? ImmutableArray.Create(warning) : Warnings.Add(warning) };

    /// <summary>
    /// Add multiple warnings to this validation result
    /// </summary>
    public ValidationResult WithWarnings(params string[] warnings) =>
        this with { Warnings = Warnings.IsDefault ? warnings.ToImmutableArray() : Warnings.AddRange(warnings) };

    /// <summary>
    /// Get all issues as a single formatted string
    /// </summary>
    public string GetFormattedIssues()
    {
        var issues = new List<string>();

        if (HasErrors)
        {
            issues.Add("Errors:");
            issues.AddRange(Errors.Select(e => $"  - {e}"));
        }

        if (HasWarnings)
        {
            if (issues.Count > 0) issues.Add("");
            issues.Add("Warnings:");
            issues.AddRange(Warnings.Select(w => $"  - {w}"));
        }

        return string.Join(Environment.NewLine, issues);
    }

    /// <summary>
    /// Implicit conversion from bool to ValidationResult
    /// </summary>
    public static implicit operator ValidationResult(bool isValid) =>
        isValid ? Success : Failure("Validation failed");

    /// <summary>
    /// Implicit conversion to bool
    /// </summary>
    public static implicit operator bool(ValidationResult result) => result.IsValid;

    /// <summary>
    /// String representation for debugging
    /// </summary>
    public override string ToString()
    {
        if (IsValid && !HasWarnings)
            return "Valid";

        if (IsValid && HasWarnings)
            return $"Valid with {Warnings.Length} warning(s)";

        return $"Invalid: {Errors.Length} error(s)" +
               (HasWarnings ? $", {Warnings.Length} warning(s)" : "");
    }

    /// <summary>
    /// Custom equality to handle ImmutableArray comparison
    /// </summary>
    public virtual bool Equals(ValidationResult? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return IsValid == other.IsValid &&
               Errors.SequenceEqual(other.Errors) &&
               Warnings.SequenceEqual(other.Warnings);
    }

    /// <summary>
    /// Custom hash code to match custom equality
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IsValid);

        foreach (var error in Errors)
            hash.Add(error);

        foreach (var warning in Warnings)
            hash.Add(warning);

        return hash.ToHashCode();
    }
}
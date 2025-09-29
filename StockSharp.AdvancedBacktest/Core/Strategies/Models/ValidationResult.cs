using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public record ValidationResult(
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("errors")] ImmutableArray<string> Errors,
    [property: JsonPropertyName("warnings")] ImmutableArray<string> Warnings
)
{
    public static readonly ValidationResult Success = new(true, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);

    [JsonPropertyName("hasErrors")]
    public bool HasErrors => !Errors.IsDefaultOrEmpty;

    [JsonPropertyName("hasWarnings")]
    public bool HasWarnings => !Warnings.IsDefaultOrEmpty;

    [JsonPropertyName("totalIssues")]
    public int TotalIssues => Errors.Length + Warnings.Length;

    public static ValidationResult CreateSuccess() => Success;

    public static ValidationResult SuccessWithWarnings(params string[] warnings) =>
        new(true, ImmutableArray<string>.Empty, warnings.ToImmutableArray());

    public static ValidationResult SuccessWithWarnings(IEnumerable<string> warnings) =>
        new(true, ImmutableArray<string>.Empty, warnings.ToImmutableArray());

    public static ValidationResult Failure(params string[] errors) =>
        new(false, errors.ToImmutableArray(), ImmutableArray<string>.Empty);

    public static ValidationResult Failure(IEnumerable<string> errors) =>
        new(false, errors.ToImmutableArray(), ImmutableArray<string>.Empty);

    public static ValidationResult Failure(IEnumerable<string> errors, IEnumerable<string> warnings) =>
        new(false, errors.ToImmutableArray(), warnings.ToImmutableArray());

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

    public static ValidationResult Combine(IEnumerable<ValidationResult> results) =>
        Combine(results.ToArray());

    public ValidationResult WithError(string error) =>
        this with { IsValid = false, Errors = Errors.IsDefault ? ImmutableArray.Create(error) : Errors.Add(error) };

    public ValidationResult WithErrors(params string[] errors) =>
        this with { IsValid = false, Errors = Errors.IsDefault ? errors.ToImmutableArray() : Errors.AddRange(errors) };

    public ValidationResult WithWarning(string warning) =>
        this with { Warnings = Warnings.IsDefault ? ImmutableArray.Create(warning) : Warnings.Add(warning) };

    public ValidationResult WithWarnings(params string[] warnings) =>
        this with { Warnings = Warnings.IsDefault ? warnings.ToImmutableArray() : Warnings.AddRange(warnings) };

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

    public static implicit operator ValidationResult(bool isValid) =>
        isValid ? Success : Failure("Validation failed");

    public static implicit operator bool(ValidationResult result) => result.IsValid;

    public override string ToString()
    {
        if (IsValid && !HasWarnings)
            return "Valid";

        if (IsValid && HasWarnings)
            return $"Valid with {Warnings.Length} warning(s)";

        return $"Invalid: {Errors.Length} error(s)" +
               (HasWarnings ? $", {Warnings.Length} warning(s)" : "");
    }

    public virtual bool Equals(ValidationResult? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return IsValid == other.IsValid &&
               Errors.SequenceEqual(other.Errors) &&
               Warnings.SequenceEqual(other.Warnings);
    }

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
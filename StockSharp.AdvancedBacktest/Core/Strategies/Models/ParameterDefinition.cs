using System.Text.Json.Serialization;
using System.Numerics;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

/// <summary>
/// Immutable record for parameter definition with generic math support
/// </summary>
/// <param name="Name">Parameter name</param>
/// <param name="Type">Parameter type</param>
/// <param name="MinValue">Minimum allowed value</param>
/// <param name="MaxValue">Maximum allowed value</param>
/// <param name="DefaultValue">Default parameter value</param>
/// <param name="Description">Parameter description</param>
/// <param name="IsRequired">Whether the parameter is required</param>
/// <param name="ValidationPattern">Regex pattern for string validation</param>
public record ParameterDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] Type Type,
    [property: JsonPropertyName("minValue")] object? MinValue = null,
    [property: JsonPropertyName("maxValue")] object? MaxValue = null,
    [property: JsonPropertyName("defaultValue")] object? DefaultValue = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("isRequired")] bool IsRequired = false,
    [property: JsonPropertyName("validationPattern")] string? ValidationPattern = null
)
{
    /// <summary>
    /// Type name for serialization
    /// </summary>
    [JsonPropertyName("typeName")]
    public string TypeName => Type.Name;

    /// <summary>
    /// Full type name for serialization
    /// </summary>
    [JsonPropertyName("fullTypeName")]
    public string FullTypeName => Type.FullName ?? Type.Name;

    /// <summary>
    /// Whether this parameter supports numeric operations
    /// </summary>
    [JsonPropertyName("isNumeric")]
    public bool IsNumeric => IsNumericType(Type);

    /// <summary>
    /// Check if a type is numeric (including nullable numeric types)
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        // Handle nullable types
        var actualType = Nullable.GetUnderlyingType(type) ?? type;

        // Check if the type implements INumber<T>
        return actualType.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumber<>));
    }

    /// <summary>
    /// Whether this parameter is a string type
    /// </summary>
    [JsonPropertyName("isString")]
    public bool IsString => Type == typeof(string);

    /// <summary>
    /// Whether this parameter is a boolean type
    /// </summary>
    [JsonPropertyName("isBoolean")]
    public bool IsBoolean => Type == typeof(bool) || Type == typeof(bool?);

    /// <summary>
    /// Whether this parameter is an enum type
    /// </summary>
    [JsonPropertyName("isEnum")]
    public bool IsEnum => Type.IsEnum;

    /// <summary>
    /// Create a numeric parameter definition
    /// </summary>
    public static ParameterDefinition CreateNumeric<T>(
        string name,
        T? minValue = null,
        T? maxValue = null,
        T? defaultValue = null,
        string? description = null,
        bool isRequired = false) where T : struct, INumber<T>
    {
        return new ParameterDefinition(
            Name: name,
            Type: typeof(T),
            MinValue: minValue,
            MaxValue: maxValue,
            DefaultValue: defaultValue,
            Description: description,
            IsRequired: isRequired
        );
    }

    /// <summary>
    /// Create a string parameter definition
    /// </summary>
    public static ParameterDefinition CreateString(
        string name,
        string? defaultValue = null,
        string? description = null,
        bool isRequired = false,
        string? validationPattern = null)
    {
        return new ParameterDefinition(
            Name: name,
            Type: typeof(string),
            DefaultValue: defaultValue,
            Description: description,
            IsRequired: isRequired,
            ValidationPattern: validationPattern
        );
    }

    /// <summary>
    /// Create a boolean parameter definition
    /// </summary>
    public static ParameterDefinition CreateBoolean(
        string name,
        bool? defaultValue = null,
        string? description = null,
        bool isRequired = false)
    {
        return new ParameterDefinition(
            Name: name,
            Type: typeof(bool),
            DefaultValue: defaultValue,
            Description: description,
            IsRequired: isRequired
        );
    }

    /// <summary>
    /// Create an enum parameter definition
    /// </summary>
    public static ParameterDefinition CreateEnum<T>(
        string name,
        T? defaultValue = null,
        string? description = null,
        bool isRequired = false) where T : struct, Enum
    {
        return new ParameterDefinition(
            Name: name,
            Type: typeof(T),
            DefaultValue: defaultValue,
            Description: description,
            IsRequired: isRequired
        );
    }

    /// <summary>
    /// Validate a value against this parameter definition
    /// </summary>
    public ValidationResult ValidateValue(object? value)
    {
        var errors = new List<string>();

        // Check required
        if (IsRequired && value == null)
        {
            errors.Add($"Parameter '{Name}' is required but no value was provided");
            return ValidationResult.Failure(errors);
        }

        // Allow null for non-required parameters
        if (value == null)
        {
            return ValidationResult.CreateSuccess();
        }

        // Check type compatibility
        if (!Type.IsAssignableFrom(value.GetType()))
        {
            errors.Add($"Parameter '{Name}' expects type {Type.Name} but got {value.GetType().Name}");
            return ValidationResult.Failure(errors);
        }

        // Numeric range validation
        if (IsNumeric && value is IComparable comparableValue)
        {
            if (MinValue is IComparable min && comparableValue.CompareTo(min) < 0)
            {
                errors.Add($"Parameter '{Name}' value {value} is below minimum {MinValue}");
            }

            if (MaxValue is IComparable max && comparableValue.CompareTo(max) > 0)
            {
                errors.Add($"Parameter '{Name}' value {value} is above maximum {MaxValue}");
            }
        }

        // String pattern validation
        if (IsString && !string.IsNullOrEmpty(ValidationPattern) && value is string stringValue)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(stringValue, ValidationPattern))
            {
                errors.Add($"Parameter '{Name}' value '{stringValue}' does not match required pattern");
            }
        }

        return errors.Count == 0 ? ValidationResult.CreateSuccess() : ValidationResult.Failure(errors);
    }
}

/// <summary>
/// Typed parameter wrapper for compile-time type safety
/// </summary>
/// <typeparam name="T">Parameter value type</typeparam>
public class TypedParameter<T> where T : struct, INumber<T>
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parameter value
    /// </summary>
    public required T Value { get; init; }

    /// <summary>
    /// Minimum allowed value
    /// </summary>
    public T? MinValue { get; init; }

    /// <summary>
    /// Maximum allowed value
    /// </summary>
    public T? MaxValue { get; init; }

    /// <summary>
    /// Validate the parameter value using generic math
    /// </summary>
    public bool IsValid()
    {
        if (MinValue is not null && Value < MinValue)
            return false;

        if (MaxValue is not null && Value > MaxValue)
            return false;

        return true;
    }

    /// <summary>
    /// Get validation error message
    /// </summary>
    public string? GetValidationError()
    {
        if (MinValue is not null && Value < MinValue)
            return $"Value {Value} is below minimum {MinValue}";

        if (MaxValue is not null && Value > MaxValue)
            return $"Value {Value} is above maximum {MaxValue}";

        return null;
    }
}
using System.Text.Json.Serialization;
using System.Numerics;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

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
    [JsonPropertyName("typeName")]
    public string TypeName => Type.Name;

    [JsonPropertyName("fullTypeName")]
    public string FullTypeName => Type.FullName ?? Type.Name;

    [JsonPropertyName("isNumeric")]
    public bool IsNumeric => IsNumericType(Type);

    private static bool IsNumericType(Type type)
    {
        // Handle nullable types
        var actualType = Nullable.GetUnderlyingType(type) ?? type;

        // Check if the type implements INumber<T>
        return actualType.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumber<>));
    }

    [JsonPropertyName("isString")]
    public bool IsString => Type == typeof(string);

    [JsonPropertyName("isBoolean")]
    public bool IsBoolean => Type == typeof(bool) || Type == typeof(bool?);

    [JsonPropertyName("isEnum")]
    public bool IsEnum => Type.IsEnum;

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

public class TypedParameter<T> where T : struct, INumber<T>
{
    public required string Name { get; init; }

    public required T Value { get; init; }

    public T? MinValue { get; init; }

    public T? MaxValue { get; init; }

    public bool IsValid()
    {
        if (MinValue is not null && Value < MinValue)
            return false;

        if (MaxValue is not null && Value > MaxValue)
            return false;

        return true;
    }

    public string? GetValidationError()
    {
        if (MinValue is not null && Value < MinValue)
            return $"Value {Value} is below minimum {MinValue}";

        if (MaxValue is not null && Value > MaxValue)
            return $"Value {Value} is above maximum {MaxValue}";

        return null;
    }
}
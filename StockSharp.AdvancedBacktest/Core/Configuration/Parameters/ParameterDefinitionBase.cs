using System.Collections.Immutable;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.Core.Configuration.Validation;

namespace StockSharp.AdvancedBacktest.Core.Configuration.Parameters;

/// <summary>
/// Type-erased base class for parameter definitions, enabling heterogeneous collections
/// while maintaining type safety through the generic derived class.
/// </summary>
public abstract record ParameterDefinitionBase(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] Type Type,
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
    public abstract bool IsNumeric { get; }

    [JsonPropertyName("isString")]
    public bool IsString => Type == typeof(string);

    [JsonPropertyName("isBoolean")]
    public bool IsBoolean => Type == typeof(bool) || Type == typeof(bool?);

    [JsonPropertyName("isEnum")]
    public bool IsEnum => Type.IsEnum;

    [JsonPropertyName("hasMinValue")]
    public abstract bool HasMinValue { get; }

    [JsonPropertyName("hasMaxValue")]
    public abstract bool HasMaxValue { get; }

    [JsonPropertyName("hasDefaultValue")]
    public abstract bool HasDefaultValue { get; }

    [JsonPropertyName("hasStep")]
    public abstract bool HasStep { get; }

    /// <summary>
    /// Gets the minimum value as an object (type-erased).
    /// </summary>
    public abstract object? GetMinValue();

    /// <summary>
    /// Gets the maximum value as an object (type-erased).
    /// </summary>
    public abstract object? GetMaxValue();

    /// <summary>
    /// Gets the default value as an object (type-erased).
    /// </summary>
    public abstract object? GetDefaultValue();

    /// <summary>
    /// Gets the step value as an object (type-erased).
    /// </summary>
    public abstract object? GetStep();

    /// <summary>
    /// Validates a value against this parameter definition.
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <returns>Validation result</returns>
    public abstract ValidationResult ValidateValue(object? value);

    /// <summary>
    /// Generates all valid values for this parameter within its range.
    /// For performance, this uses streaming enumeration.
    /// </summary>
    /// <returns>Enumerable of valid values</returns>
    public abstract IEnumerable<object?> GenerateValidValues();

    /// <summary>
    /// Gets the estimated count of valid values (for optimization planning).
    /// Returns null if the count is infinite or cannot be determined.
    /// </summary>
    public abstract long? GetValidValueCount();

    /// <summary>
    /// Creates a copy of this parameter definition with a new value range.
    /// </summary>
    /// <param name="minValue">New minimum value</param>
    /// <param name="maxValue">New maximum value</param>
    /// <returns>New parameter definition with updated range</returns>
    public abstract ParameterDefinitionBase WithRange(object? minValue, object? maxValue);

    /// <summary>
    /// Creates a copy of this parameter definition with a new step value.
    /// </summary>
    /// <param name="step">New step value</param>
    /// <returns>New parameter definition with updated step</returns>
    public abstract ParameterDefinitionBase WithStep(object? step);
}
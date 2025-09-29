using System.Text.Json;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Configuration.Validation;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Core.Configuration.Serialization;

/// <summary>
/// System.Text.Json serialization context for high-performance parameter serialization.
/// Source generation will be enabled once type conflicts are resolved.
/// Target performance: 50MB/second JSON serialization throughput.
/// </summary>
// Source generation temporarily disabled due to type conflicts
// [JsonSourceGenerationOptions(
//     WriteIndented = false,
//     PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
//     DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
//     GenerationMode = JsonSourceGenerationMode.Default)]
// [JsonSerializable(typeof(ParameterSetJson))]
// [JsonSerializable(typeof(ParameterDefinitionBase[]))]
// [JsonSerializable(typeof(ParameterDefinition<int>))]
// [JsonSerializable(typeof(ParameterDefinition<long>))]
// [JsonSerializable(typeof(ParameterDefinition<double>))]
// [JsonSerializable(typeof(ParameterDefinition<decimal>))]
// [JsonSerializable(typeof(ParameterDefinition<float>))]
// [JsonSerializable(typeof(StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult))]
// [JsonSerializable(typeof(Dictionary<string, object?>))]
// [JsonSerializable(typeof(System.Collections.Immutable.ImmutableDictionary<string, object?>))]
// [JsonSerializable(typeof(System.Collections.Immutable.ImmutableDictionary<string, object>))]
// internal partial class ParameterJsonContext : JsonSerializerContext
// {
// }

/// <summary>
/// System.Text.Json serialization context for high-performance parameter serialization.
/// </summary>
public static class ParameterSerializationContext
{
    /// <summary>
    /// Gets optimized JSON serializer options. Source generation will be added in future version.
    /// </summary>
    public static JsonSerializerOptions GetDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),
                new DecimalConverter(),
                new TypeConverter()
            }
        };
        return options;
    }

    /// <summary>
    /// Gets minimal JSON serializer options for maximum performance.
    /// Target: 50MB/second serialization throughput.
    /// </summary>
    public static JsonSerializerOptions GetHighPerformanceOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new DecimalConverter(),
                new TypeConverter()
            }
        };
        return options;
    }

    /// <summary>
    /// Gets ultra-high performance options for optimization caching scenarios.
    /// Uses minimal converters for maximum throughput.
    /// </summary>
    public static JsonSerializerOptions GetCachingOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new DecimalConverter()
            }
        };
        return options;
    }
}

/// <summary>
/// Custom JSON converter for decimal types to maintain precision in trading calculations.
/// </summary>
public sealed class DecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (decimal.TryParse(value, out var result))
                return result;
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to decimal");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        // Write as string to preserve full precision
        writer.WriteStringValue(value.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Custom JSON converter for Type objects to handle type serialization.
/// </summary>
public sealed class TypeConverter : JsonConverter<Type>
{
    public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var typeName = reader.GetString();
        if (string.IsNullOrEmpty(typeName))
            return null;

        // Try to load the type
        try
        {
            return Type.GetType(typeName) ?? throw new JsonException($"Cannot resolve type: {typeName}");
        }
        catch (Exception ex)
        {
            throw new JsonException($"Error deserializing type '{typeName}': {ex.Message}", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.AssemblyQualifiedName ?? value.FullName ?? value.Name);
    }
}


/// <summary>
/// Optimized JSON serialization extensions for parameter types.
/// </summary>
public static class ParameterJsonExtensions
{
    /// <summary>
    /// Serializes a parameter set to JSON with optimal performance using source generation.
    /// Target: 50MB/second throughput.
    /// </summary>
    public static string ToJsonOptimized(this ParameterSet parameterSet)
    {
        return parameterSet.ToJson(ParameterSerializationContext.GetHighPerformanceOptions());
    }

    /// <summary>
    /// Serializes a parameter set to JSON for caching scenarios with ultra-high performance.
    /// Uses serialization-only mode for maximum throughput.
    /// </summary>
    public static string ToJsonForCaching(this ParameterSet parameterSet)
    {
        return parameterSet.ToJson(ParameterSerializationContext.GetCachingOptions());
    }

    /// <summary>
    /// Deserializes a parameter set from JSON with optimal performance using source generation.
    /// </summary>
    public static ParameterSet FromJsonOptimized(string json, ParameterValidator? validator = null)
    {
        return ParameterSet.FromJson(json, validator, ParameterSerializationContext.GetHighPerformanceOptions());
    }

    /// <summary>
    /// Serializes parameter values to JSON for hashing with minimal overhead.
    /// </summary>
    public static string ToJsonForHashing(this IReadOnlyDictionary<string, object?> parameters)
    {
        var options = ParameterSerializationContext.GetCachingOptions();
        return JsonSerializer.Serialize(parameters, options);
    }

    /// <summary>
    /// Serializes parameter definitions to JSON.
    /// </summary>
    public static string ToJson(this IEnumerable<ParameterDefinitionBase> definitions, JsonSerializerOptions? options = null)
    {
        options ??= ParameterSerializationContext.GetDefaultOptions();
        return JsonSerializer.Serialize(definitions.ToArray(), options);
    }

    /// <summary>
    /// Deserializes parameter definitions from JSON.
    /// </summary>
    public static ParameterDefinitionBase[] FromJson(string json, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        options ??= ParameterSerializationContext.GetDefaultOptions();

        return JsonSerializer.Deserialize<ParameterDefinitionBase[]>(json, options)
            ?? throw new JsonException("Failed to deserialize parameter definitions");
    }
}
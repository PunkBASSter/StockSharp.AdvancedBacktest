using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

/// <summary>
/// Thread-safe parameter set implementation with generic math support
/// </summary>
public class ParameterSet : IParameterSet
{
    private readonly ConcurrentDictionary<string, object?> _values = new();
    private readonly ImmutableArray<ParameterDefinition> _definitions;
    private readonly IParameterValidator _validator;

    /// <summary>
    /// Number of parameters in the set
    /// </summary>
    public int Count => _definitions.Length;

    /// <summary>
    /// Get all parameter definitions
    /// </summary>
    public ImmutableArray<ParameterDefinition> Definitions => _definitions;

    /// <summary>
    /// Initialize parameter set with definitions
    /// </summary>
    public ParameterSet(IEnumerable<ParameterDefinition> definitions, IParameterValidator? validator = null)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        _definitions = definitions.ToImmutableArray();
        _validator = validator ?? new ParameterValidator();

        // Initialize with default values
        foreach (var definition in _definitions)
        {
            if (definition.DefaultValue != null)
            {
                _values[definition.Name] = definition.DefaultValue;
            }
        }
    }

    /// <summary>
    /// Get parameter value by name with generic math support
    /// </summary>
    public T GetValue<T>(string name) where T : INumber<T>
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Parameter name cannot be null or empty", nameof(name));

        if (_values.TryGetValue(name, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            // Try to convert using generic math
            if (value != null && T.TryParse(value.ToString(), null, out var parsedValue))
                return parsedValue;

            throw new InvalidCastException($"Cannot convert parameter '{name}' value '{value}' to type {typeof(T).Name}");
        }

        throw new KeyNotFoundException($"Parameter '{name}' not found");
    }

    /// <summary>
    /// Set parameter value by name with generic math support
    /// </summary>
    public void SetValue<T>(string name, T value) where T : INumber<T>
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Parameter name cannot be null or empty", nameof(name));

        var definition = GetDefinition(name);
        if (definition == null)
            throw new KeyNotFoundException($"Parameter '{name}' not found in definitions");

        // Validate type compatibility
        if (!definition.Type.IsAssignableFrom(typeof(T)))
            throw new ArgumentException($"Parameter '{name}' expects type {definition.Type.Name} but got {typeof(T).Name}");

        // Validate value
        var validationResult = definition.ValidateValue(value);
        if (!validationResult.IsValid)
            throw new ArgumentException($"Parameter '{name}' validation failed: {validationResult.GetFormattedIssues()}");

        _values[name] = value;
    }

    /// <summary>
    /// Get parameter value as object
    /// </summary>
    public object? GetValue(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Parameter name cannot be null or empty", nameof(name));

        return _values.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// Set parameter value as object
    /// </summary>
    public void SetValue(string name, object? value)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Parameter name cannot be null or empty", nameof(name));

        var definition = GetDefinition(name);
        if (definition == null)
            throw new KeyNotFoundException($"Parameter '{name}' not found in definitions");

        // Validate value
        var validationResult = definition.ValidateValue(value);
        if (!validationResult.IsValid)
            throw new ArgumentException($"Parameter '{name}' validation failed: {validationResult.GetFormattedIssues()}");

        _values[name] = value;
    }

    /// <summary>
    /// Check if parameter exists
    /// </summary>
    public bool HasParameter(string name)
    {
        return !string.IsNullOrEmpty(name) && _definitions.Any(d => d.Name == name);
    }

    /// <summary>
    /// Validate all parameters
    /// </summary>
    public ValidationResult Validate()
    {
        return _validator.ValidateParameterSet(this);
    }

    /// <summary>
    /// Create a snapshot of current parameter values
    /// </summary>
    public ImmutableDictionary<string, object?> GetSnapshot()
    {
        return _values.ToImmutableDictionary();
    }

    /// <summary>
    /// Clone the parameter set
    /// </summary>
    public IParameterSet Clone()
    {
        var clone = new ParameterSet(_definitions, _validator);

        foreach (var kvp in _values)
        {
            clone._values[kvp.Key] = kvp.Value;
        }

        return clone;
    }

    /// <summary>
    /// Get parameter definition by name
    /// </summary>
    private ParameterDefinition? GetDefinition(string name)
    {
        return _definitions.FirstOrDefault(d => d.Name == name);
    }

    /// <summary>
    /// Get all parameter names
    /// </summary>
    public IEnumerable<string> GetParameterNames()
    {
        return _definitions.Select(d => d.Name);
    }

    /// <summary>
    /// Get parameter definition by name
    /// </summary>
    public ParameterDefinition? GetParameterDefinition(string name)
    {
        return GetDefinition(name);
    }

    /// <summary>
    /// Bulk set parameters from dictionary
    /// </summary>
    public void SetValues(IReadOnlyDictionary<string, object?> values)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        var errors = new List<string>();

        foreach (var kvp in values)
        {
            try
            {
                SetValue(kvp.Key, kvp.Value);
            }
            catch (Exception ex)
            {
                errors.Add($"Error setting parameter '{kvp.Key}': {ex.Message}");
            }
        }

        if (errors.Count > 0)
            throw new ArgumentException($"Failed to set {errors.Count} parameters: {string.Join("; ", errors)}");
    }

    /// <summary>
    /// Get typed parameter value with fallback
    /// </summary>
    public T GetValue<T>(string name, T fallback) where T : INumber<T>
    {
        try
        {
            return GetValue<T>(name);
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Try to get parameter value
    /// </summary>
    public bool TryGetValue<T>(string name, out T value) where T : INumber<T>
    {
        value = default(T)!;

        try
        {
            value = GetValue<T>(name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Try to get parameter value as object
    /// </summary>
    public bool TryGetValue(string name, out object? value)
    {
        return _values.TryGetValue(name, out value);
    }

    /// <summary>
    /// Get parameter statistics
    /// </summary>
    public ParameterSetStatistics GetStatistics()
    {
        var totalParams = _definitions.Length;
        var setParams = _values.Count(kvp => kvp.Value != null);
        var requiredParams = _definitions.Count(d => d.IsRequired);
        var requiredSet = _definitions
            .Where(d => d.IsRequired)
            .Count(d => _values.ContainsKey(d.Name) && _values[d.Name] != null);

        return new ParameterSetStatistics(
            TotalParameters: totalParams,
            SetParameters: setParams,
            RequiredParameters: requiredParams,
            RequiredParametersSet: requiredSet,
            IsComplete: requiredSet == requiredParams
        );
    }

    /// <summary>
    /// Serialize parameter set to JSON string
    /// </summary>
    public string ToJson(JsonSerializerOptions? options = null)
    {
        var statistics = GetStatistics();
        var data = new ParameterSetJson
        {
            Definitions = _definitions.ToArray(),
            Values = _values.ToImmutableDictionary(),
            Statistics = new Dictionary<string, object>
            {
                ["TotalParameters"] = statistics.TotalParameters,
                ["SetParameters"] = statistics.SetParameters,
                ["RequiredParameters"] = statistics.RequiredParameters,
                ["RequiredParametersSet"] = statistics.RequiredParametersSet,
                ["IsComplete"] = statistics.IsComplete
            }.ToImmutableDictionary(),
            Timestamp = DateTimeOffset.UtcNow
        };

        options ??= GetDefaultJsonOptions();
        return JsonSerializer.Serialize(data, options);
    }

    /// <summary>
    /// Create parameter set from JSON string
    /// </summary>
    public static ParameterSet FromJson(string json, IParameterValidator? validator = null, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrEmpty(json))
            throw new ArgumentException("JSON string cannot be null or empty", nameof(json));

        options ??= GetDefaultJsonOptions();
        var data = JsonSerializer.Deserialize<ParameterSetJson>(json, options)
            ?? throw new ArgumentException("Failed to deserialize parameter set from JSON");

        var parameterSet = new ParameterSet(data.Definitions, validator);

        // Set values from JSON
        foreach (var kvp in data.Values)
        {
            if (kvp.Value != null)
            {
                try
                {
                    parameterSet.SetValue(kvp.Key, kvp.Value);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Failed to set parameter '{kvp.Key}' from JSON: {ex.Message}", ex);
                }
            }
        }

        return parameterSet;
    }

    /// <summary>
    /// Export parameter set as optimization configuration
    /// </summary>
    public ParameterOptimizationConfig ToOptimizationConfig()
    {
        var ranges = new Dictionary<string, ParameterRange>();

        foreach (var definition in _definitions)
        {
            if (definition.IsNumeric && definition.MinValue != null && definition.MaxValue != null)
            {
                ranges[definition.Name] = new ParameterRange(
                    definition.Name,
                    definition.MinValue,
                    definition.MaxValue,
                    GetValue(definition.Name) ?? definition.DefaultValue
                );
            }
        }

        return new ParameterOptimizationConfig(
            Name: "ParameterSet_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Ranges: ranges.ToImmutableDictionary(),
            CreatedAt: DateTimeOffset.UtcNow
        );
    }

    /// <summary>
    /// Create parameter set from optimization result
    /// </summary>
    public static ParameterSet FromOptimizationResult(
        IEnumerable<ParameterDefinition> definitions,
        IReadOnlyDictionary<string, object?> optimizedValues,
        IParameterValidator? validator = null)
    {
        var parameterSet = new ParameterSet(definitions, validator);
        parameterSet.SetValues(optimizedValues);
        return parameterSet;
    }

    /// <summary>
    /// Get default JSON serialization options
    /// </summary>
    private static JsonSerializerOptions GetDefaultJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}


/// <summary>
/// Builder for creating parameter sets
/// </summary>
public class ParameterSetBuilder
{
    private readonly List<ParameterDefinition> _definitions = new();

    /// <summary>
    /// Add a numeric parameter
    /// </summary>
    public ParameterSetBuilder AddNumeric<T>(
        string name,
        T? minValue = null,
        T? maxValue = null,
        T? defaultValue = null,
        string? description = null,
        bool isRequired = false) where T : struct, INumber<T>
    {
        var definition = ParameterDefinition.CreateNumeric(
            name, minValue, maxValue, defaultValue, description, isRequired);
        _definitions.Add(definition);
        return this;
    }

    /// <summary>
    /// Add a string parameter
    /// </summary>
    public ParameterSetBuilder AddString(
        string name,
        string? defaultValue = null,
        string? description = null,
        bool isRequired = false,
        string? validationPattern = null)
    {
        var definition = ParameterDefinition.CreateString(
            name, defaultValue, description, isRequired, validationPattern);
        _definitions.Add(definition);
        return this;
    }

    /// <summary>
    /// Add a boolean parameter
    /// </summary>
    public ParameterSetBuilder AddBoolean(
        string name,
        bool? defaultValue = null,
        string? description = null,
        bool isRequired = false)
    {
        var definition = ParameterDefinition.CreateBoolean(
            name, defaultValue, description, isRequired);
        _definitions.Add(definition);
        return this;
    }

    /// <summary>
    /// Add an enum parameter
    /// </summary>
    public ParameterSetBuilder AddEnum<T>(
        string name,
        T? defaultValue = null,
        string? description = null,
        bool isRequired = false) where T : struct, Enum
    {
        var definition = ParameterDefinition.CreateEnum(
            name, defaultValue, description, isRequired);
        _definitions.Add(definition);
        return this;
    }

    /// <summary>
    /// Build the parameter set
    /// </summary>
    public ParameterSet Build(IParameterValidator? validator = null)
    {
        return new ParameterSet(_definitions, validator);
    }
}
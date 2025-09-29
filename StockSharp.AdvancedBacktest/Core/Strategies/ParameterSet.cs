// Backward compatibility alias for existing code
// This file provides aliases to maintain compatibility while new code uses the enhanced implementation

using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Configuration.Validation;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

/// <summary>
/// Backward compatibility wrapper around the enhanced ParameterSet.
/// New code should use StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterSet.
/// </summary>
[Obsolete("Use StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterSet instead. This alias will be removed in v3.0.")]
public class ParameterSet : IParameterSet, IDisposable
{
    private readonly StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterSet _enhanced;

    public int Count => _enhanced.Count;

    public ImmutableArray<Models.ParameterDefinition> Definitions =>
        _enhanced.Definitions.Cast<Models.ParameterDefinition>().ToImmutableArray();

    ImmutableArray<Models.ParameterDefinition> IParameterSet.Definitions => Definitions;

    public ParameterSet(IEnumerable<Models.ParameterDefinition> definitions, IParameterValidator? validator = null)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        // Convert legacy definitions to new definitions
        var enhancedDefinitions = definitions.Cast<ParameterDefinitionBase>();
        var enhancedValidator = validator != null ? new StockSharp.AdvancedBacktest.Core.Configuration.Validation.ParameterValidator() : null;

        _enhanced = new StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterSet(enhancedDefinitions, enhancedValidator);
    }

    public T GetValue<T>(string name) where T : struct, IComparable<T>, INumber<T>
    {
        return _enhanced.GetValue<T>(name);
    }

    public void SetValue<T>(string name, T value) where T : struct, IComparable<T>, INumber<T>
    {
        _enhanced.SetValue<T>(name, value);
    }

    public object? GetValue(string name)
    {
        return _enhanced.GetValue(name);
    }

    public void SetValue(string name, object? value)
    {
        _enhanced.SetValue(name, value);
    }

    public bool HasParameter(string name)
    {
        return _enhanced.HasParameter(name);
    }

    public Models.ValidationResult Validate()
    {
        return Models.ValidationResult.FromEnhanced(_enhanced.Validate());
    }

    public ImmutableDictionary<string, object?> GetSnapshot()
    {
        return _enhanced.GetSnapshot();
    }

    public IParameterSet Clone()
    {
        return new ParameterSet(Definitions, null);
    }

    public IEnumerable<string> GetParameterNames()
    {
        return _enhanced.GetParameterNames();
    }

    public Models.ParameterDefinition? GetParameterDefinition(string name)
    {
        var definition = _enhanced.GetParameterDefinition(name);
        return definition as Models.ParameterDefinition;
    }

    public void SetValues(IReadOnlyDictionary<string, object?> values)
    {
        _enhanced.SetValues(values);
    }

    public T GetValue<T>(string name, T fallback) where T : struct, IComparable<T>, INumber<T>
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

    public bool TryGetValue<T>(string name, out T value) where T : struct, IComparable<T>, INumber<T>
    {
        return _enhanced.TryGetValue<T>(name, out value);
    }

    public bool TryGetValue(string name, out object? value)
    {
        return _enhanced.TryGetValue(name, out value);
    }

    public Interfaces.ParameterSetStatistics GetStatistics()
    {
        var enhancedStats = _enhanced.GetStatistics();
        return new Interfaces.ParameterSetStatistics(
            enhancedStats.TotalParameters,
            enhancedStats.SetParameters,
            enhancedStats.RequiredParameters,
            enhancedStats.RequiredParametersSet,
            enhancedStats.IsComplete
        );
    }

    public string ToJson(JsonSerializerOptions? options = null)
    {
        return _enhanced.ToJson(options);
    }

    public static ParameterSet FromJson(string json, IParameterValidator? validator = null, JsonSerializerOptions? options = null)
    {
        var enhanced = StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterSet.FromJson(json, null, options);
        // Convert back to legacy format - this is complex, simplified for now
        var definitions = enhanced.Definitions.ToArray();
        return new ParameterSet(definitions.Cast<Models.ParameterDefinition>(), validator);
    }

    public void Dispose()
    {
        _enhanced?.Dispose();
    }
}

/// <summary>
/// Backward compatibility builder.
/// </summary>
[Obsolete("Use StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterSetBuilder instead. This alias will be removed in v3.0.")]
public class ParameterSetBuilder
{
    private readonly StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterSetBuilder _enhanced = new();

    public ParameterSetBuilder AddNumeric<T>(
        string name,
        T? minValue = null,
        T? maxValue = null,
        T? defaultValue = null,
        string? description = null,
        bool isRequired = false) where T : struct, IComparable<T>, INumber<T>
    {
        _enhanced.AddNumeric(name, minValue, maxValue, defaultValue, null, description, isRequired);
        return this;
    }

    public ParameterSet Build(IParameterValidator? validator = null)
    {
        var enhanced = _enhanced.Build();
        // Convert back to legacy format
        return new ParameterSet(enhanced.Definitions.Cast<Models.ParameterDefinition>(), validator);
    }
}
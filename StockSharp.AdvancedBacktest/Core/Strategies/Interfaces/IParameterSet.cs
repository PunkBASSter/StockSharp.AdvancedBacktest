using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;
using System.Numerics;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

public interface IParameterSet
{
    int Count { get; }
    ImmutableArray<ParameterDefinition> Definitions { get; }

    T GetValue<T>(string name) where T : INumber<T>;
    void SetValue<T>(string name, T value) where T : INumber<T>;
    object? GetValue(string name);
    void SetValue(string name, object? value);
    bool HasParameter(string name);
    ValidationResult Validate();
    ImmutableDictionary<string, object?> GetSnapshot();
    IParameterSet Clone();
    bool TryGetValue(string name, out object? value);
    ParameterSetStatistics GetStatistics();
}

public readonly record struct ParameterSetStatistics(
    int TotalParameters,
    int SetParameters,
    int RequiredParameters,
    int RequiredParametersSet,
    bool IsComplete
);
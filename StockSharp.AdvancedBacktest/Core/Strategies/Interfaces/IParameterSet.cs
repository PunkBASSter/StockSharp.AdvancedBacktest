using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;
using System.Numerics;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

/// <summary>
/// Interface for strategy parameter management with generic math support
/// </summary>
public interface IParameterSet
{
    /// <summary>
    /// Number of parameters in the set
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Get all parameter definitions
    /// </summary>
    ImmutableArray<ParameterDefinition> Definitions { get; }

    /// <summary>
    /// Get parameter value by name
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="name">Parameter name</param>
    /// <returns>Parameter value</returns>
    T GetValue<T>(string name) where T : INumber<T>;

    /// <summary>
    /// Set parameter value by name
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="name">Parameter name</param>
    /// <param name="value">Parameter value</param>
    void SetValue<T>(string name, T value) where T : INumber<T>;

    /// <summary>
    /// Get parameter value as object
    /// </summary>
    /// <param name="name">Parameter name</param>
    /// <returns>Parameter value as object</returns>
    object? GetValue(string name);

    /// <summary>
    /// Set parameter value as object
    /// </summary>
    /// <param name="name">Parameter name</param>
    /// <param name="value">Parameter value</param>
    void SetValue(string name, object? value);

    /// <summary>
    /// Check if parameter exists
    /// </summary>
    /// <param name="name">Parameter name</param>
    /// <returns>True if parameter exists</returns>
    bool HasParameter(string name);

    /// <summary>
    /// Validate all parameters
    /// </summary>
    /// <returns>Validation result</returns>
    ValidationResult Validate();

    /// <summary>
    /// Create a snapshot of current parameter values
    /// </summary>
    /// <returns>Immutable snapshot</returns>
    ImmutableDictionary<string, object?> GetSnapshot();

    /// <summary>
    /// Clone the parameter set
    /// </summary>
    /// <returns>New parameter set with same values</returns>
    IParameterSet Clone();

    /// <summary>
    /// Try to get parameter value by name
    /// </summary>
    /// <param name="name">Parameter name</param>
    /// <param name="value">Parameter value</param>
    /// <returns>True if parameter exists</returns>
    bool TryGetValue(string name, out object? value);

    /// <summary>
    /// Get parameter statistics
    /// </summary>
    /// <returns>Parameter set statistics</returns>
    ParameterSetStatistics GetStatistics();
}

/// <summary>
/// Parameter set statistics
/// </summary>
/// <param name="TotalParameters">Total number of parameters defined</param>
/// <param name="SetParameters">Number of parameters with values</param>
/// <param name="RequiredParameters">Number of required parameters</param>
/// <param name="RequiredParametersSet">Number of required parameters that have values</param>
/// <param name="IsComplete">Whether all required parameters are set</param>
public readonly record struct ParameterSetStatistics(
    int TotalParameters,
    int SetParameters,
    int RequiredParameters,
    int RequiredParametersSet,
    bool IsComplete
);
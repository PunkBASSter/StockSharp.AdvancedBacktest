namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

/// <summary>
/// Represents a parameter range for optimization
/// </summary>
/// <param name="Name">Parameter name</param>
/// <param name="MinValue">Minimum value</param>
/// <param name="MaxValue">Maximum value</param>
/// <param name="CurrentValue">Current value</param>
public sealed record ParameterRange(
    string Name,
    object MinValue,
    object MaxValue,
    object? CurrentValue
);
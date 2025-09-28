using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

/// <summary>
/// Configuration for parameter optimization
/// </summary>
/// <param name="Name">Configuration name</param>
/// <param name="Ranges">Parameter ranges for optimization</param>
/// <param name="CreatedAt">Creation timestamp</param>
public sealed record ParameterOptimizationConfig(
    string Name,
    ImmutableDictionary<string, ParameterRange> Ranges,
    DateTimeOffset CreatedAt
);
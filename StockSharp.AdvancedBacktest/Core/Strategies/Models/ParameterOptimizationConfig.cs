using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public sealed record ParameterOptimizationConfig(
    string Name,
    ImmutableDictionary<string, ParameterRange> Ranges,
    DateTimeOffset CreatedAt
);
namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public sealed record ParameterRange(
    string Name,
    object MinValue,
    object MaxValue,
    object? CurrentValue
);
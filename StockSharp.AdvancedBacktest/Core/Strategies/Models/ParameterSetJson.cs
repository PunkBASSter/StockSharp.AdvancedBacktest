using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public sealed class ParameterSetJson
{
    public ParameterDefinition[] Definitions { get; set; } = [];

    public ImmutableDictionary<string, object?> Values { get; set; } = ImmutableDictionary<string, object?>.Empty;

    public ImmutableDictionary<string, object> Statistics { get; set; } = ImmutableDictionary<string, object>.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
using System.Collections.Immutable;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public sealed class ParameterSetJson
{
    public ParameterDefinitionBase[] Definitions { get; set; } = [];

    public ImmutableDictionary<string, object?> Values { get; set; } = ImmutableDictionary<string, object?>.Empty;

    public ImmutableDictionary<string, object> Statistics { get; set; } = ImmutableDictionary<string, object>.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public sealed class ParameterSetJson
{
    /// <summary>
    /// Parameter definitions
    /// </summary>
    public ParameterDefinition[] Definitions { get; set; } = [];

    /// <summary>
    /// Parameter values
    /// </summary>
    public ImmutableDictionary<string, object?> Values { get; set; } = ImmutableDictionary<string, object?>.Empty;

    /// <summary>
    /// Parameter statistics
    /// </summary>
    public ImmutableDictionary<string, object> Statistics { get; set; } = ImmutableDictionary<string, object>.Empty;

    /// <summary>
    /// Timestamp of serialization
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
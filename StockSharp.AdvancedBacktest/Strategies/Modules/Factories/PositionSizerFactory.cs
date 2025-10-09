using Microsoft.Extensions.Options;
using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.Factories;

/// <summary>
/// Factory for creating position sizer instances based on strategy parameters
/// </summary>
public class PositionSizerFactory
{
    private readonly StrategyOptions _options;

    public PositionSizerFactory(IOptions<StrategyOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Create a position sizer based on the specified method
    /// </summary>
    /// <param name="method">Position sizing method</param>
    /// <returns>Position sizer implementation</returns>
    public IPositionSizer Create(PositionSizingMethod method)
    {
        return method switch
        {
            PositionSizingMethod.Fixed => new FixedPositionSizer(_options.FixedPositionSize),
            PositionSizingMethod.PercentOfEquity => new PercentEquityPositionSizer(_options.EquityPercentage),
            PositionSizingMethod.ATRBased => new ATRBasedPositionSizer(_options.EquityPercentage, _options.StopLossATRMultiplier),
            _ => throw new InvalidOperationException($"Unknown position sizing method: {method}")
        };
    }
}

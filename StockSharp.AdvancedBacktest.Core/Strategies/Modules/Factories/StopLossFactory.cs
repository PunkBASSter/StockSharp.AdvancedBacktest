using Microsoft.Extensions.Options;
using StockSharp.AdvancedBacktest.Strategies.Modules.StopLoss;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.Factories;

/// <summary>
/// Factory for creating stop-loss calculator instances based on strategy parameters
/// </summary>
public class StopLossFactory
{
    private readonly StrategyOptions _options;

    public StopLossFactory(IOptions<StrategyOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Create a stop-loss calculator based on the specified method
    /// </summary>
    /// <param name="method">Stop-loss method</param>
    /// <returns>Stop-loss calculator implementation</returns>
    public IStopLossCalculator Create(StopLossMethod method)
    {
        return method switch
        {
            StopLossMethod.Percentage => new PercentageStopLoss(_options.StopLossPercentage),
            StopLossMethod.ATR => new ATRStopLoss(_options.StopLossATRMultiplier),
            _ => throw new InvalidOperationException($"Unknown stop-loss method: {method}")
        };
    }
}

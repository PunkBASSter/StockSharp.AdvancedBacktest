using Microsoft.Extensions.Options;
using StockSharp.AdvancedBacktest.Strategies.Modules.TakeProfit;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.Factories;

/// <summary>
/// Factory for creating take-profit calculator instances based on strategy parameters
/// </summary>
public class TakeProfitFactory
{
    private readonly StrategyOptions _options;

    public TakeProfitFactory(IOptions<StrategyOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Create a take-profit calculator based on the specified method
    /// </summary>
    /// <param name="method">Take-profit method</param>
    /// <returns>Take-profit calculator implementation</returns>
    public ITakeProfitCalculator Create(TakeProfitMethod method)
    {
        return method switch
        {
            TakeProfitMethod.Percentage => new PercentageTakeProfit(_options.TakeProfitPercentage),
            TakeProfitMethod.ATR => new ATRTakeProfit(_options.TakeProfitATRMultiplier),
            TakeProfitMethod.RiskReward => new RiskRewardTakeProfit(_options.RiskRewardRatio),
            _ => throw new InvalidOperationException($"Unknown take-profit method: {method}")
        };
    }
}

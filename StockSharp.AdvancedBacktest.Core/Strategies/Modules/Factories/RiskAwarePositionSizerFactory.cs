using Microsoft.Extensions.Options;
using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.Factories;

public class RiskAwarePositionSizerFactory(IOptions<StrategyOptions> options)
{
    private readonly StrategyOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public IRiskAwarePositionSizer Create() => new FixedRiskPositionSizer(
        _options.RiskPercentPerTrade,
        _options.MinPositionSize,
        _options.MaxPositionSize);
}

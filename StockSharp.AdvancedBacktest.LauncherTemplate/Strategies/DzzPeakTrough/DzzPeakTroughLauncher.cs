using StockSharp.AdvancedBacktest.Launchers;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.DzzPeakTrough;

public class DzzPeakTroughLauncher : StrategyLauncherBase<DzzPeakTroughStrategy>
{
    public override string Name => "DzzPeakTrough";

    protected override DzzPeakTroughStrategy CreateStrategy(LauncherConfig config, Security security, Portfolio portfolio)
    {
        return new DzzPeakTroughStrategy
        {
            Security = security,
            Portfolio = portfolio
        };
    }

    protected override IList<ICustomParam> GetParameters()
    {
        return new List<ICustomParam>
        {
            new NumberParam<decimal>("DzzDepth", 5m)
        };
    }
}

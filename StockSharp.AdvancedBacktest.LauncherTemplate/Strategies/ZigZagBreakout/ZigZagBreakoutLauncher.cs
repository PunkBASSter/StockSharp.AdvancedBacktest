using StockSharp.AdvancedBacktest.Launchers;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;

public class ZigZagBreakoutLauncher : StrategyLauncherBase<ZigZagBreakout>
{
    public override string Name => "ZigZagBreakout";

    protected override ZigZagBreakout CreateStrategy(LauncherConfig config, Security security, Portfolio portfolio)
    {
        return new ZigZagBreakout
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

using Ecng.Collections;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Utilities;

namespace StockSharp.AdvancedBacktest.Strategies;

public abstract class CustomStrategyBase : Strategy
{
    public string Hash => $"{GetType().Name}V{Version}_{SecuritiesHash}_{ParamsHash}";
    public PerformanceMetrics? PerformanceMetrics { get; protected set; }
    public DateTimeOffset MetricWindowStart { get; set; }
    public DateTimeOffset MetricWindowEnd { get; set; }

    public virtual string Version { get; set; } = "1.0.0";

    public virtual string ParamsHash => ParamsContainer.GenerateHash();

    public virtual string SecuritiesHash
    {
        get
        {
            return string.Join(";", Securities
                .OrderBy(s => s.Key.Id)  // Deterministic ordering
                .Select(s => $"{s.Key.Id}={string.Join(",", s.Value)}"));
        }
    }

    public virtual Dictionary<Security, IEnumerable<TimeSpan>> Securities { get; set; } = new(new SecurityIdComparer());

    public CustomParamsContainer ParamsContainer { get; set; } = new(Enumerable.Empty<ICustomParam>());

    protected T GetParam<T>(string id) => ParamsContainer.Get<T>(id);

    public static T Create<T>(List<ICustomParam> paramSet) where T : CustomStrategyBase, new()
    {
        var strategy = new T();

        var secparams = paramSet.Where(p => p is SecurityParam)
            .Cast<SecurityParam>()
            .ToDictionary(sp => sp.Value.Key, sp => sp.Value.AsEnumerable());
        strategy.Securities.AddRange(secparams);

        var nonsecparams = paramSet.Where(p => p is not SecurityParam).ToList();
        strategy.ParamsContainer = new CustomParamsContainer(nonsecparams);

        return strategy;
    }

    //TODO handle more elegantly, now it serves as a temp param storage
    public List<ICustomParam> ParamsBackup { get; set; } = [];

    protected override void OnStopping()
    {
        PerformanceMetrics = MetricsCalculator.CalculateMetrics(this, MetricWindowStart, MetricWindowEnd);
        base.OnStopping();
    }
}

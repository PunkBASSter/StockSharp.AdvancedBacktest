using Ecng.Collections;
using Ecng.Logging;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.AdvancedBacktest.OrderManagement;
using System.Security.Cryptography;
using System.Text;

namespace StockSharp.AdvancedBacktest.Strategies;

public abstract class CustomStrategyBase : Strategy, IStrategyOrderOperations
{
    public string Hash => $"{GetType().Name}V{Version}_{SecuritiesHash}_{ParamsHash}";
    public PerformanceMetrics? PerformanceMetrics { get; protected set; }
    public DateTimeOffset MetricWindowStart { get; set; }
    public DateTimeOffset MetricWindowEnd { get; set; }

    public IDebugEventSink DebugEventSink { get; set; } = NullDebugEventSink.Instance;

    public TimeSpan? AuxiliaryTimeframe { get; set; }

    public virtual string Version { get; set; } = "1.0.0";

    public virtual string ParamsHash
    {
        get
        {
            var paramsString = ParamsContainer.GenerateHash();

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(paramsString));
            return Convert.ToHexString(hashBytes)[..8].ToLower();
        }
    }

    public virtual string SecuritiesHash
    {
        get
        {
            return string.Join(";", Securities
                .OrderBy(s => s.Key.Id)
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

    public List<ICustomParam> ParamsBackup { get; set; } = [];

    public new void LogInfo(string format, params object[] args) => this.AddInfoLog(format, args);

    public Order PlaceOrder(Order order)
    {
        if (order.Type == OrderTypes.Market)
            return order.Side == Sides.Buy ? BuyMarket(order.Volume) : SellMarket(order.Volume);

        return order.Side == Sides.Buy
            ? BuyLimit(order.Price, order.Volume)
            : SellLimit(order.Price, order.Volume);
    }
}

using StockSharp.Algo.Strategies;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Threading.Channels;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

public interface IEnhancedStrategy : IDisposable
{
    IParameterSet Parameters { get; }
    IPerformanceTracker? Performance { get; }
    IRiskManager? RiskManager { get; }
    ChannelReader<TradeExecutionData> TradeEvents { get; }
    ChannelReader<PerformanceSnapshot> PerformanceEvents { get; }
    StrategyState CurrentState { get; }

    Task InitializeAsync(IServiceProvider serviceProvider);
    ValidationResult ValidateParameters();
}
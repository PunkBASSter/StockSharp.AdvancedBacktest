using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Threading.Channels;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

public interface IStrategyEventHandler : IAsyncDisposable
{
    ChannelReader<TradeExecutionData> TradeEvents { get; }
    ChannelReader<PerformanceSnapshot> PerformanceEvents { get; }
    ChannelReader<RiskViolation> RiskEvents { get; }
    ChannelReader<StrategyStateChange> StateEvents { get; }

    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    Task StopProcessingAsync();
    bool PublishTradeEvent(TradeExecutionData tradeData);
    bool PublishPerformanceEvent(PerformanceSnapshot snapshot);
    bool PublishRiskEvent(RiskViolation violation);
    bool PublishStateEvent(StrategyStateChange stateChange);
}
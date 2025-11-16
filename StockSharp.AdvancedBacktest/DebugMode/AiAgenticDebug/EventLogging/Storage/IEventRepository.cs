using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public interface IEventRepository
{
	Task CreateBacktestRunAsync(BacktestRunEntity run);
	Task<BacktestRunEntity?> GetBacktestRunAsync(string runId);
	Task WriteEventAsync(EventEntity eventEntity);
	Task<EventEntity?> GetEventByIdAsync(string eventId);
	Task<EventQueryResult> QueryEventsAsync(EventQueryParameters parameters);
}

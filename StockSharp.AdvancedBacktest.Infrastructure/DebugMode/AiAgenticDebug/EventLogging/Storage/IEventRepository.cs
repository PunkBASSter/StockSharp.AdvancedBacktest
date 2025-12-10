using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public interface IEventRepository
{
	Task CreateBacktestRunAsync(BacktestRunEntity run);
	Task<BacktestRunEntity?> GetBacktestRunAsync(string runId);
	Task<IReadOnlyList<BacktestRunEntity>> GetAllBacktestRunsAsync();
	Task WriteEventAsync(EventEntity eventEntity);
	Task<EventEntity?> GetEventByIdAsync(string eventId);
	Task<EventQueryResult> QueryEventsAsync(EventQueryParameters parameters);
	Task<EventQueryResult> QueryEventsByEntityAsync(EntityReferenceQueryParameters parameters);
	Task<EventSequenceQueryResult> QueryEventSequenceAsync(EventSequenceQueryParameters parameters);
	Task<EventQueryResult> QueryEventsWithValidationErrorsAsync(ValidationErrorQueryParameters parameters);
	Task<AggregationResult> AggregateMetricsAsync(AggregationParameters parameters);
	Task<StateSnapshotResult> GetStateSnapshotAsync(StateSnapshotQueryParameters parameters);
	Task<StateDeltaResult> GetStateDeltaAsync(StateDeltaQueryParameters parameters);
}

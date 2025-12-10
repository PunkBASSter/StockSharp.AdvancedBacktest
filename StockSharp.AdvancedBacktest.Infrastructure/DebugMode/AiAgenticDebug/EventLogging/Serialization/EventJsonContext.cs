using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	WriteIndented = false,
	Converters = [typeof(DecimalConverter)]
)]
[JsonSerializable(typeof(EventEntity))]
[JsonSerializable(typeof(BacktestRunEntity))]
[JsonSerializable(typeof(ValidationError))]
[JsonSerializable(typeof(ValidationMetadata))]
[JsonSerializable(typeof(List<ValidationError>))]
[JsonSerializable(typeof(EventQueryResult))]
[JsonSerializable(typeof(QueryResultMetadata))]
[JsonSerializable(typeof(GetEventsByTypeRequest))]
[JsonSerializable(typeof(GetEventsByTypeResponse))]
[JsonSerializable(typeof(EventDto))]
[JsonSerializable(typeof(MetadataDto))]
[JsonSerializable(typeof(EventType))]
[JsonSerializable(typeof(EventSeverity))]
[JsonSerializable(typeof(EventCategory))]
[JsonSerializable(typeof(GetEventsByEntityResponse))]
[JsonSerializable(typeof(QueryEventSequenceResponse))]
[JsonSerializable(typeof(EventSequenceDto))]
[JsonSerializable(typeof(SequenceMetadataDto))]
[JsonSerializable(typeof(AggregateMetricsResponse))]
[JsonSerializable(typeof(AggregationsDto))]
[JsonSerializable(typeof(AggregationMetadataDto))]
[JsonSerializable(typeof(GetStateSnapshotResponse))]
[JsonSerializable(typeof(StateDto))]
[JsonSerializable(typeof(PositionDto))]
[JsonSerializable(typeof(IndicatorDto))]
[JsonSerializable(typeof(ActiveOrderDto))]
[JsonSerializable(typeof(PnlDto))]
[JsonSerializable(typeof(StateSnapshotMetadataDto))]
[JsonSerializable(typeof(ListBacktestRunsResponse))]
[JsonSerializable(typeof(BacktestRunDto))]
public partial class EventJsonContext : JsonSerializerContext
{
}

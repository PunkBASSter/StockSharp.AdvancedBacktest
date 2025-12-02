using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Server;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;

[McpServerToolType]
public sealed class GetEventsByEntityTool
{
	private static readonly string[] ValidEntityTypes = ["OrderId", "SecuritySymbol", "PositionId", "IndicatorName"];

	private readonly IEventRepository _repository;

	public GetEventsByEntityTool(IEventRepository repository)
	{
		_repository = repository;
	}

	[McpServerTool]
	[Description("Retrieve backtest events filtered by entity reference (OrderId, SecuritySymbol, PositionId, or IndicatorName). Enables cross-referencing events related to specific trading entities.")]
	public async Task<string> GetEventsByEntityAsync(
		[Description("Unique identifier of the backtest run (GUID format)")] string runId,
		[Description("Type of entity to query: OrderId, SecuritySymbol, PositionId, or IndicatorName")] string entityType,
		[Description("Value of the entity to search for")] string entityValue,
		[Description("Comma-separated list of event types to filter (optional, e.g., 'TradeExecution,OrderRejection')")] string? eventTypeFilter = null,
		[Description("Number of events per page (default: 100, max: 1000)")] int pageSize = 100,
		[Description("Zero-based page index (default: 0)")] int pageIndex = 0)
	{
		if (!ValidEntityTypes.Contains(entityType))
			throw new ArgumentException($"Invalid entity type: {entityType}. Must be one of: {string.Join(", ", ValidEntityTypes)}");

		EventType[]? eventTypeFilterArray = null;
		if (!string.IsNullOrEmpty(eventTypeFilter))
		{
			var eventTypes = eventTypeFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			eventTypeFilterArray = new EventType[eventTypes.Length];

			for (int i = 0; i < eventTypes.Length; i++)
			{
				if (!Enum.TryParse<EventType>(eventTypes[i], out var parsedType))
					throw new ArgumentException($"Invalid event type in filter: {eventTypes[i]}. Must be one of: TradeExecution, OrderRejection, IndicatorCalculation, PositionUpdate, StateChange, MarketDataEvent, RiskEvent");
				eventTypeFilterArray[i] = parsedType;
			}
		}

		pageSize = Math.Clamp(pageSize, 1, 1000);
		pageIndex = Math.Max(pageIndex, 0);

		var stopwatch = Stopwatch.StartNew();

		var parameters = new EntityReferenceQueryParameters
		{
			RunId = runId,
			EntityType = entityType,
			EntityValue = entityValue,
			EventTypeFilter = eventTypeFilterArray,
			PageSize = pageSize,
			PageIndex = pageIndex
		};

		var result = await _repository.QueryEventsByEntityAsync(parameters);
		stopwatch.Stop();

		result.Metadata.QueryTimeMs = (int)stopwatch.ElapsedMilliseconds;

		var response = new GetEventsByEntityResponse
		{
			Events = result.Events.Select(e => new EventDto
			{
				EventId = e.EventId,
				RunId = e.RunId,
				Timestamp = e.Timestamp.ToString("o"),
				EventType = e.EventType.ToString(),
				Severity = e.Severity.ToString(),
				Category = e.Category.ToString(),
				Properties = JsonSerializer.Deserialize<JsonElement>(e.Properties),
				ParentEventId = e.ParentEventId,
				ValidationErrors = e.ValidationErrors != null
					? JsonSerializer.Deserialize<JsonElement>(e.ValidationErrors)
					: null
			}).ToArray(),
			Metadata = new MetadataDto
			{
				TotalCount = result.Metadata.TotalCount,
				ReturnedCount = result.Metadata.ReturnedCount,
				PageIndex = result.Metadata.PageIndex,
				PageSize = result.Metadata.PageSize,
				HasMore = result.Metadata.HasMore,
				QueryTimeMs = result.Metadata.QueryTimeMs,
				Truncated = result.Metadata.Truncated
			}
		};

		return JsonSerializer.Serialize(response, EventJsonContext.Default.GetEventsByEntityResponse);
	}
}

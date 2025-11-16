using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;

public sealed class GetEventsByTypeTool
{
	private readonly IEventRepository _repository;

	public GetEventsByTypeTool(IEventRepository repository)
	{
		_repository = repository;
	}

	public static string GetToolName() => "get_events_by_type";

	public static string GetToolDescription() =>
		"Query backtest events filtered by event type (TradeExecution, OrderRejection, etc.)";

	public async Task<string> ExecuteAsync(string arguments)
	{
		var request = JsonSerializer.Deserialize<GetEventsByTypeRequest>(arguments, JsonSerializerOptionsProvider.Options);
		if (request == null)
			throw new ArgumentException("Invalid request format");

		if (!Enum.TryParse<EventType>(request.EventType, out var eventType))
			throw new ArgumentException($"Invalid event type: {request.EventType}");

		var parameters = new EventQueryParameters
		{
			RunId = request.RunId,
			EventType = eventType,
			PageSize = Math.Clamp(request.PageSize ?? 100, 1, 1000),
			PageIndex = Math.Max(request.PageIndex ?? 0, 0)
		};

		var result = await _repository.QueryEventsAsync(parameters);

		var response = new
		{
			events = result.Events.Select(e => new
			{
				eventId = e.EventId,
				timestamp = e.Timestamp.ToString("o"),
				eventType = e.EventType.ToString(),
				severity = e.Severity.ToString(),
				category = e.Category.ToString(),
				properties = JsonSerializer.Deserialize<JsonElement>(e.Properties),
				parentEventId = e.ParentEventId
			}).ToArray(),
			metadata = new
			{
				totalCount = result.Metadata.TotalCount,
				returnedCount = result.Metadata.ReturnedCount,
				pageIndex = result.Metadata.PageIndex,
				pageSize = result.Metadata.PageSize,
				hasMore = result.Metadata.HasMore,
				queryTimeMs = result.Metadata.QueryTimeMs
			}
		};

		return JsonSerializer.Serialize(response, JsonSerializerOptionsProvider.Options);
	}
}

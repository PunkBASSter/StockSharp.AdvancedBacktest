using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;

[McpServerToolType]
public sealed class GetEventsByTypeTool
{
	private readonly IEventRepository _repository;

	public GetEventsByTypeTool(IEventRepository repository)
	{
		_repository = repository;
	}

	[McpServerTool]
	[Description("Retrieve backtest events filtered by event type and optional time range. Supports pagination for large result sets.")]
	public async Task<string> GetEventsByTypeAsync(
		[Description("Unique identifier of the backtest run (GUID format)")] string runId,
		[Description("Type of events to retrieve: TradeExecution, OrderRejection, IndicatorCalculation, PositionUpdate, StateChange, MarketDataEvent, or RiskEvent")] string eventType,
		[Description("Start of time range in ISO 8601 format (optional)")] string? startTime = null,
		[Description("End of time range in ISO 8601 format (optional)")] string? endTime = null,
		[Description("Filter by severity level: Error, Warning, Info, or Debug (optional)")] string? severity = null,
		[Description("Number of events per page (default: 100, max: 1000)")] int pageSize = 100,
		[Description("Zero-based page index (default: 0)")] int pageIndex = 0)
	{
		if (!Enum.TryParse<EventType>(eventType, out var eventTypeEnum))
			throw new ArgumentException($"Invalid event type: {eventType}. Must be one of: TradeExecution, OrderRejection, IndicatorCalculation, PositionUpdate, StateChange, MarketDataEvent, RiskEvent");

		DateTime? startTimeValue = null;
		if (!string.IsNullOrEmpty(startTime))
		{
			if (!DateTime.TryParse(startTime, out var parsedStartTime))
				throw new ArgumentException($"Invalid start time format: {startTime}. Must be ISO 8601 format.");
			startTimeValue = parsedStartTime;
		}

		DateTime? endTimeValue = null;
		if (!string.IsNullOrEmpty(endTime))
		{
			if (!DateTime.TryParse(endTime, out var parsedEndTime))
				throw new ArgumentException($"Invalid end time format: {endTime}. Must be ISO 8601 format.");
			endTimeValue = parsedEndTime;
		}

		EventSeverity? severityValue = null;
		if (!string.IsNullOrEmpty(severity))
		{
			if (!Enum.TryParse<EventSeverity>(severity, out var parsedSeverity))
				throw new ArgumentException($"Invalid severity: {severity}. Must be one of: Error, Warning, Info, Debug");
			severityValue = parsedSeverity;
		}

		pageSize = Math.Clamp(pageSize, 1, 1000);
		pageIndex = Math.Max(pageIndex, 0);

		var stopwatch = Stopwatch.StartNew();

		var parameters = new EventQueryParameters
		{
			RunId = runId,
			EventType = eventTypeEnum,
			StartTime = startTimeValue,
			EndTime = endTimeValue,
			Severity = severityValue,
			PageSize = pageSize,
			PageIndex = pageIndex
		};

		var result = await _repository.QueryEventsAsync(parameters);
		stopwatch.Stop();

		result.Metadata.QueryTimeMs = (int)stopwatch.ElapsedMilliseconds;

		var response = new GetEventsByTypeResponse
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

		return JsonSerializer.Serialize(response, EventJsonContext.Default.GetEventsByTypeResponse);
	}
}

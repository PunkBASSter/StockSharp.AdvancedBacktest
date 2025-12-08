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
public sealed class QueryEventSequenceTool
{
	private readonly IEventRepository _repository;

	public QueryEventSequenceTool(IEventRepository repository)
	{
		_repository = repository;
	}

	[McpServerTool]
	[Description("Query event sequences by traversing parent-child relationships. Enables pattern detection and identification of incomplete event chains.")]
	public async Task<string> QueryEventSequenceAsync(
		[Description("Unique identifier of the backtest run (GUID format)")] string runId,
		[Description("Root event ID to start chain traversal from (optional, if not provided will find all root events)")] string? rootEventId = null,
		[Description("Comma-separated list of expected event types in sequence (e.g., 'TradeExecution,PositionUpdate')")] string? sequencePattern = null,
		[Description("When true, includes incomplete sequences that don't match the full pattern")] bool findIncomplete = false,
		[Description("Maximum depth of chain traversal (default: 10, max: 100)")] int maxDepth = 10,
		[Description("Number of sequences per page (default: 50, max: 100)")] int pageSize = 50,
		[Description("Zero-based page index (default: 0)")] int pageIndex = 0)
	{
		EventType[]? sequencePatternArray = null;
		if (!string.IsNullOrEmpty(sequencePattern))
		{
			var eventTypes = sequencePattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			sequencePatternArray = new EventType[eventTypes.Length];

			for (int i = 0; i < eventTypes.Length; i++)
			{
				if (!Enum.TryParse<EventType>(eventTypes[i], out var parsedType))
					throw new ArgumentException($"Invalid event type in sequence pattern: {eventTypes[i]}. Must be one of: TradeExecution, OrderRejection, IndicatorCalculation, PositionUpdate, StateChange, MarketDataEvent, RiskEvent");
				sequencePatternArray[i] = parsedType;
			}
		}

		maxDepth = Math.Clamp(maxDepth, 1, 100);
		pageSize = Math.Clamp(pageSize, 1, 100);
		pageIndex = Math.Max(pageIndex, 0);

		var stopwatch = Stopwatch.StartNew();

		var parameters = new EventSequenceQueryParameters
		{
			RunId = runId,
			RootEventId = rootEventId,
			SequencePattern = sequencePatternArray,
			FindIncomplete = findIncomplete,
			MaxDepth = maxDepth,
			PageSize = pageSize,
			PageIndex = pageIndex
		};

		var result = await _repository.QueryEventSequenceAsync(parameters);
		stopwatch.Stop();

		result.Metadata.QueryTimeMs = (int)stopwatch.ElapsedMilliseconds;

		var response = new QueryEventSequenceResponse
		{
			Sequences = result.Sequences.Select(s => new EventSequenceDto
			{
				RootEventId = s.RootEventId,
				Events = s.Events.Select(e => new EventDto
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
				Complete = s.Complete,
				MissingEventTypes = s.MissingEventTypes?.Select(t => t.ToString()).ToArray()
			}).ToArray(),
			Metadata = new SequenceMetadataDto
			{
				TotalSequences = result.Metadata.TotalSequences,
				ReturnedCount = result.Metadata.ReturnedCount,
				PageIndex = result.Metadata.PageIndex,
				PageSize = result.Metadata.PageSize,
				HasMore = result.Metadata.HasMore,
				QueryTimeMs = result.Metadata.QueryTimeMs
			}
		};

		return JsonSerializer.Serialize(response, EventJsonContext.Default.QueryEventSequenceResponse);
	}
}

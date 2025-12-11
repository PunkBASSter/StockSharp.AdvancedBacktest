using System.ComponentModel;
using ModelContextProtocol.Server;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;

[McpServerToolType]
public sealed class AggregateMetricsTool
{
	private readonly IEventRepository _repository;

	public AggregateMetricsTool(IEventRepository repository)
	{
		_repository = repository;
	}

	[McpServerTool]
	[Description("Calculate aggregations (count, sum, avg, min, max, stddev) on event properties without retrieving individual events")]
	public async Task<AggregateMetricsResponse> AggregateMetricsAsync(
		[Description("Unique identifier of the backtest run")] string runId,
		[Description("Type of events to aggregate (TradeExecution, OrderRejection, IndicatorCalculation, PositionUpdate, StateChange, MarketDataEvent, RiskEvent)")] string eventType,
		[Description("JSON path to the property to aggregate (e.g., '$.Price', '$.Quantity')")] string propertyPath,
		[Description("Aggregation functions to compute (count, sum, avg, min, max, stddev)")] string[] aggregations,
		[Description("Start of time range (ISO 8601, optional)")] string? startTime = null,
		[Description("End of time range (ISO 8601, optional)")] string? endTime = null)
	{
		if (string.IsNullOrEmpty(runId))
			throw new ArgumentException("RunId is required", nameof(runId));

		if (!Enum.TryParse<EventType>(eventType, true, out var parsedEventType))
			throw new ArgumentException($"Invalid event type: {eventType}", nameof(eventType));

		DateTime? parsedStartTime = null;
		DateTime? parsedEndTime = null;

		if (!string.IsNullOrEmpty(startTime))
		{
			if (!DateTime.TryParse(startTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var start))
				throw new ArgumentException($"Invalid start time format: {startTime}", nameof(startTime));
			parsedStartTime = start;
		}

		if (!string.IsNullOrEmpty(endTime))
		{
			if (!DateTime.TryParse(endTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var end))
				throw new ArgumentException($"Invalid end time format: {endTime}", nameof(endTime));
			parsedEndTime = end;
		}

		var effectiveAggregations = aggregations.Length > 0 ? aggregations : ["count"];

		var parameters = new AggregationParameters
		{
			RunId = runId,
			EventType = parsedEventType,
			PropertyPath = propertyPath,
			Aggregations = effectiveAggregations,
			StartTime = parsedStartTime,
			EndTime = parsedEndTime
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		return new AggregateMetricsResponse
		{
			Aggregations = new AggregationsDto
			{
				Count = result.Aggregations.Count,
				Sum = result.Aggregations.Sum,
				Avg = result.Aggregations.Avg,
				Min = result.Aggregations.Min,
				Max = result.Aggregations.Max,
				StdDev = result.Aggregations.StdDev
			},
			Metadata = new AggregationMetadataDto
			{
				TotalEvents = result.Metadata.TotalEvents,
				QueryTimeMs = result.Metadata.QueryTimeMs,
				EventType = result.Metadata.EventType,
				PropertyPath = result.Metadata.PropertyPath
			}
		};
	}
}

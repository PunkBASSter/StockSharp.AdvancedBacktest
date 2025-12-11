using ModelContextProtocol.Server;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;

[McpServerToolType]
public sealed class GetStateSnapshotTool
{
	private readonly IEventRepository _repository;

	public GetStateSnapshotTool(IEventRepository repository)
	{
		_repository = repository;
	}

	[McpServerTool]
	[Description("Retrieve strategy state (positions, PnL, indicators, active orders) at a specific timestamp by replaying events")]
	public async Task<GetStateSnapshotResponse> GetStateSnapshotAsync(
		[Description("Unique identifier of the backtest run (GUID format)")] string runId,
		[Description("Timestamp to query state for (ISO 8601 format)")] string timestamp,
		[Description("Filter state to specific security (optional, empty = all securities)")] string? securitySymbol = null,
		[Description("Include indicator values in state")] bool includeIndicators = true,
		[Description("Include active orders in state")] bool includeActiveOrders = true)
	{
		if (string.IsNullOrWhiteSpace(runId))
			throw new ArgumentException("runId is required", nameof(runId));

		if (!DateTime.TryParse(timestamp, null, DateTimeStyles.RoundtripKind, out var parsedTimestamp))
			throw new ArgumentException("Invalid timestamp format. Use ISO 8601 format.", nameof(timestamp));

		var parameters = new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = parsedTimestamp,
			SecuritySymbol = string.IsNullOrWhiteSpace(securitySymbol) ? null : securitySymbol,
			IncludeIndicators = includeIndicators,
			IncludeActiveOrders = includeActiveOrders
		};

		var result = await _repository.GetStateSnapshotAsync(parameters);

		var response = new GetStateSnapshotResponse
		{
			Timestamp = result.Timestamp,
			RunId = result.RunId,
			State = new StateDto
			{
				Positions = result.State.Positions.Select(p => new PositionDto
				{
					SecuritySymbol = p.SecuritySymbol,
					Quantity = p.Quantity,
					AveragePrice = p.AveragePrice,
					UnrealizedPnL = p.UnrealizedPnL,
					RealizedPnL = p.RealizedPnL
				}).ToList(),
				Indicators = result.State.Indicators.Select(i => new IndicatorDto
				{
					Name = i.Name,
					SecuritySymbol = i.SecuritySymbol,
					Value = i.Value,
					Parameters = i.Parameters
				}).ToList(),
				ActiveOrders = result.State.ActiveOrders.Select(o => new ActiveOrderDto
				{
					OrderId = o.OrderId,
					SecuritySymbol = o.SecuritySymbol,
					Direction = o.Direction,
					Quantity = o.Quantity,
					Price = o.Price
				}).ToList(),
				Pnl = new PnlDto
				{
					Total = result.State.Pnl.Total,
					Realized = result.State.Pnl.Realized,
					Unrealized = result.State.Pnl.Unrealized
				}
			},
			Metadata = new StateSnapshotMetadataDto
			{
				QueryTimeMs = result.Metadata.QueryTimeMs,
				Reconstructed = result.Metadata.Reconstructed
			}
		};

		return response;
	}
}

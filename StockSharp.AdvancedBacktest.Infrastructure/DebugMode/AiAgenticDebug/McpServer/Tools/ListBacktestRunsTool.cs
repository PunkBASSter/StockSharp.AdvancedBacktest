using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;

[McpServerToolType]
public sealed class ListBacktestRunsTool(IEventRepository repository)
{
	private readonly IEventRepository _repository = repository;

    [McpServerTool]
	[Description("List all available backtest runs. Use this to get the runId needed for other tools.")]
	public async Task<string> ListBacktestRunsAsync()
	{
		var runs = await _repository.GetAllBacktestRunsAsync();

		var response = new ListBacktestRunsResponse
		{
			Runs = runs.Select(r => new BacktestRunDto
			{
				Id = r.Id,
				StartTime = r.StartTime.ToString("o"),
				EndTime = r.EndTime.ToString("o"),
				StrategyConfigHash = r.StrategyConfigHash,
				CreatedAt = r.CreatedAt.ToString("o")
			}).ToArray(),
			TotalCount = runs.Count
		};

		return JsonSerializer.Serialize(response, EventJsonContext.Default.ListBacktestRunsResponse);
	}
}

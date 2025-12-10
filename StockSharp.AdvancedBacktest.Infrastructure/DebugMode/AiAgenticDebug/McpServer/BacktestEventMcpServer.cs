using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public static class BacktestEventMcpServer
{
	public static async Task RunAsync(string[] args, string? databasePath = null, CancellationToken ct = default)
	{
		var builder = Host.CreateApplicationBuilder(args);

		builder.Services.AddMcpServer(options =>
		{
			options.ServerInfo = new()
			{
				Name = "StockSharp.AdvancedBacktest.EventLog",
				Version = "1.0.0"
			};
		})
		.WithStdioServerTransport()
		.WithToolsFromAssembly(typeof(GetEventsByTypeTool).Assembly);

		builder.Services.AddSingleton<IEventRepository>(sp =>
		{
			var dbPath = databasePath ?? McpDatabasePaths.GetDefaultPath();

			Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

			var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
			connection.Open();
			DatabaseSchema.InitializeAsync(connection).Wait();
			return new SqliteEventRepository(connection);
		});

		builder.Services.AddSingleton<ListBacktestRunsTool>();
		builder.Services.AddSingleton<GetEventsByTypeTool>();
		builder.Services.AddSingleton<GetEventsByEntityTool>();
		builder.Services.AddSingleton<GetStateSnapshotTool>();
		builder.Services.AddSingleton<AggregateMetricsTool>();
		builder.Services.AddSingleton<QueryEventSequenceTool>();

		var host = builder.Build();
		await host.RunAsync(ct);
	}
}

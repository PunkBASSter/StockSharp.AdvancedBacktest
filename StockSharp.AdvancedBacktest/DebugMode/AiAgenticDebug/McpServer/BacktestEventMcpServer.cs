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
	public static async Task RunAsync(string[] args, string? databasePath = null)
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
		.WithToolsFromAssembly();

		builder.Services.AddSingleton<IEventRepository>(sp =>
		{
			var dbPath = databasePath ?? Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"StockSharp",
				"AdvancedBacktest",
				"event_logs.db");

			Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

			var connection = new SqliteConnection($"Data Source={dbPath}");
			connection.Open();
			DatabaseSchema.InitializeAsync(connection).Wait();
			return new SqliteEventRepository(connection);
		});

		builder.Services.AddSingleton<GetEventsByTypeTool>();

		var host = builder.Build();
		await host.RunAsync();
	}
}

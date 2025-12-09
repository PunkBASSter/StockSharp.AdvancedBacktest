using StockSharp.AdvancedBacktest.Backtest;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public static class McpDatabasePaths
{
    private const string EnvVarName = "STOCKSHARP_MCP_DATABASE";

    public static string GetDefaultPath() =>
        Path.Combine(AppContext.BaseDirectory, "debug", "events.db");

    public static string GetPath(AgenticLoggingSettings? settings)
    {
        if (!string.IsNullOrEmpty(settings?.DatabasePath))
            return Path.GetFullPath(settings.DatabasePath);

        var envPath = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrEmpty(envPath))
            return Path.GetFullPath(envPath);

        return GetDefaultPath();
    }
}

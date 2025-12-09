using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer;

public static class ServerStartup
{
    public static async Task RunAsync(string[] args, string? databasePath, CancellationToken ct)
    {
        using var shutdownSignal = McpShutdownSignal.CreateForServer();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var shutdownMonitor = Task.Run(() =>
        {
            shutdownSignal.WaitForShutdown(ct);
            linkedCts.Cancel();
        }, ct);

        await BacktestEventMcpServer.RunAsync(args, databasePath, linkedCts.Token);
    }
}

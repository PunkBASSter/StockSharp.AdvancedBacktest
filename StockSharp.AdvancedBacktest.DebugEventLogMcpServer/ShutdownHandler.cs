using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer;

public static class ShutdownHandler
{
    public static bool TrySignalShutdown()
    {
        using var signal = McpShutdownSignal.OpenExisting();
        if (signal is null)
            return false;

        signal.Signal();
        return true;
    }
}

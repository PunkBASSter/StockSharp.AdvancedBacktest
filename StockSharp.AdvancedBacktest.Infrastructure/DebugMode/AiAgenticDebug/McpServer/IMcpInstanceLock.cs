namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public interface IMcpInstanceLock : IDisposable
{
    bool TryAcquire();
    bool IsAnotherInstanceRunning();
}

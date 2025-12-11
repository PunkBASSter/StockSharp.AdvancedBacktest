namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public interface IDatabaseWatcher : IDisposable
{
    event EventHandler<DatabaseChangedEventArgs>? DatabaseChanged;
    void Start();
    void Stop();
}

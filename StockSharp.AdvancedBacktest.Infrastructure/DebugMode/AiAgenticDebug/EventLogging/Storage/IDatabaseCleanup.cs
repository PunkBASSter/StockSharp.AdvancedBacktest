namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public interface IDatabaseCleanup
{
    Task<DatabaseCleanupResult> CleanupAsync(string databasePath, CancellationToken ct = default);
}

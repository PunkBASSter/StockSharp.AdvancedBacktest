namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class DatabaseCleanupResult
{
    public required bool Success { get; init; }
    public required string[] FilesDeleted { get; init; }
    public required long ElapsedMs { get; init; }
    public string? Error { get; init; }

    public static DatabaseCleanupResult Successful(string[] filesDeleted, long elapsedMs) =>
        new() { Success = true, FilesDeleted = filesDeleted, ElapsedMs = elapsedMs };

    public static DatabaseCleanupResult Failed(string error, long elapsedMs) =>
        new() { Success = false, FilesDeleted = [], ElapsedMs = elapsedMs, Error = error };
}

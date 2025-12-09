using System.Diagnostics;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class DatabaseCleanup : IDatabaseCleanup
{
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;

    public DatabaseCleanup(int maxRetries = 5, int retryDelayMs = 200)
    {
        _maxRetries = maxRetries;
        _retryDelayMs = retryDelayMs;
    }

    public async Task<DatabaseCleanupResult> CleanupAsync(string databasePath, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var filesToDelete = GetDatabaseFiles(databasePath);
        var deletedFiles = new List<string>();

        for (var retry = 0; retry < _maxRetries; retry++)
        {
            deletedFiles.Clear();
            string? lastError = null;

            foreach (var file in filesToDelete)
            {
                if (!File.Exists(file))
                    continue;

                try
                {
                    File.Delete(file);
                    deletedFiles.Add(file);
                }
                catch (IOException ex) when (retry < _maxRetries - 1)
                {
                    lastError = ex.Message;
                }
                catch (UnauthorizedAccessException ex) when (retry < _maxRetries - 1)
                {
                    lastError = ex.Message;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    return DatabaseCleanupResult.Failed(ex.Message, stopwatch.ElapsedMilliseconds);
                }
            }

            var remainingFiles = filesToDelete.Where(File.Exists).ToArray();
            if (remainingFiles.Length == 0)
            {
                stopwatch.Stop();
                return DatabaseCleanupResult.Successful(deletedFiles.ToArray(), stopwatch.ElapsedMilliseconds);
            }

            if (retry < _maxRetries - 1)
            {
                await Task.Delay(_retryDelayMs, ct);
            }
            else if (lastError is not null)
            {
                stopwatch.Stop();
                return DatabaseCleanupResult.Failed($"Failed after {_maxRetries} retries: {lastError}", stopwatch.ElapsedMilliseconds);
            }
        }

        stopwatch.Stop();
        return DatabaseCleanupResult.Successful(deletedFiles.ToArray(), stopwatch.ElapsedMilliseconds);
    }

    private static string[] GetDatabaseFiles(string databasePath) =>
    [
        databasePath,
        $"{databasePath}-wal",
        $"{databasePath}-shm"
    ];
}

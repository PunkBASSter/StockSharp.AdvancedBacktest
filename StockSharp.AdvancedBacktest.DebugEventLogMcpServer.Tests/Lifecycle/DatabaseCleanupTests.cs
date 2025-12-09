using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Lifecycle;

public sealed class DatabaseCleanupTests : IDisposable
{
    private readonly string _testDir;

    public DatabaseCleanupTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"db_cleanup_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public async Task CleanupAsync_DeletesAllDatabaseFiles()
    {
        var dbPath = Path.Combine(_testDir, "test.db");
        var walPath = Path.Combine(_testDir, "test.db-wal");
        var shmPath = Path.Combine(_testDir, "test.db-shm");

        File.WriteAllText(dbPath, "test db");
        File.WriteAllText(walPath, "test wal");
        File.WriteAllText(shmPath, "test shm");

        var cleanup = new DatabaseCleanup();
        var result = await cleanup.CleanupAsync(dbPath);

        Assert.True(result.Success);
        Assert.False(File.Exists(dbPath));
        Assert.False(File.Exists(walPath));
        Assert.False(File.Exists(shmPath));
    }

    [Fact]
    public async Task CleanupAsync_SucceedsWhenNoFilesExist()
    {
        var dbPath = Path.Combine(_testDir, "nonexistent.db");

        var cleanup = new DatabaseCleanup();
        var result = await cleanup.CleanupAsync(dbPath);

        Assert.True(result.Success);
        Assert.Empty(result.FilesDeleted);
    }

    [Fact]
    public async Task CleanupAsync_ReturnsFilesDeletedCount()
    {
        var dbPath = Path.Combine(_testDir, "test.db");
        var walPath = Path.Combine(_testDir, "test.db-wal");

        File.WriteAllText(dbPath, "test db");
        File.WriteAllText(walPath, "test wal");

        var cleanup = new DatabaseCleanup();
        var result = await cleanup.CleanupAsync(dbPath);

        Assert.True(result.Success);
        Assert.Equal(2, result.FilesDeleted.Length);
    }

    [Fact]
    public async Task CleanupAsync_FailsOnLockedFile()
    {
        var dbPath = Path.Combine(_testDir, "locked.db");
        File.WriteAllText(dbPath, "test");

        using var lockHandle = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var cleanup = new DatabaseCleanup(maxRetries: 2, retryDelayMs: 50);
        var result = await cleanup.CleanupAsync(dbPath);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}

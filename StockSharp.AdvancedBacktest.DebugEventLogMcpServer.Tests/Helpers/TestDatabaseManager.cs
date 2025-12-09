using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Helpers;

public sealed class TestDatabaseManager : IAsyncDisposable
{
    private readonly string _testDir;
    private readonly string _databasePath;
    private SqliteConnection? _connection;
    private SqliteEventRepository? _repository;
    private bool _disposed;

    public string DatabasePath => _databasePath;
    public SqliteEventRepository Repository => _repository ?? throw new InvalidOperationException("Database not initialized");

    private TestDatabaseManager(string testDir, string databasePath)
    {
        _testDir = testDir;
        _databasePath = databasePath;
    }

    public static async Task<TestDatabaseManager> CreateAsync()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"mcp_e2e_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        var databasePath = Path.Combine(testDir, "events.db");
        var connectionString = $"Data Source={databasePath}";

        var manager = new TestDatabaseManager(testDir, databasePath);
        manager._connection = new SqliteConnection(connectionString);
        await manager._connection.OpenAsync();
        await DatabaseSchema.InitializeAsync(manager._connection);
        manager._repository = new SqliteEventRepository(manager._connection);

        return manager;
    }

    public async Task<string> CreateBacktestRunAsync(DateTime? startTime = null, DateTime? endTime = null)
    {
        var runId = Guid.NewGuid().ToString();
        await Repository.CreateBacktestRunAsync(new BacktestRunEntity
        {
            Id = runId,
            StartTime = startTime ?? new DateTime(2025, 1, 15, 9, 0, 0, DateTimeKind.Utc),
            EndTime = endTime ?? new DateTime(2025, 1, 15, 16, 0, 0, DateTimeKind.Utc),
            StrategyConfigHash = new string('a', 64)
        });
        return runId;
    }

    public async Task PopulateWithMockDataAsync(string runId, MockDataProfile profile)
    {
        var generator = new MockDataGenerator(Repository);
        await generator.PopulateAsync(runId, profile);
    }

    public void CloseConnection()
    {
        _connection?.Close();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        // Wait a bit for file handles to be released
        await Task.Delay(100);

        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }
}

public sealed class MockDataProfile
{
    public int TradeCount { get; init; } = 5;
    public int PositionUpdateCount { get; init; } = 5;
    public int IndicatorCalculationCount { get; init; } = 10;
    public string[] Securities { get; init; } = ["AAPL", "GOOGL"];
    public string[] IndicatorNames { get; init; } = ["SMA_10", "RSI_14"];
    public DateTime BaseTime { get; init; } = new(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    public static MockDataProfile Default => new();

    public static MockDataProfile Minimal => new()
    {
        TradeCount = 2,
        PositionUpdateCount = 2,
        IndicatorCalculationCount = 3,
        Securities = ["AAPL"],
        IndicatorNames = ["SMA_10"]
    };
}

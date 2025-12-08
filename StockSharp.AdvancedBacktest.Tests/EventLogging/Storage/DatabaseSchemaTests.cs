using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Storage;

public sealed class DatabaseSchemaTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;

	public DatabaseSchemaTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
	}

	[Fact]
	public async Task InitializeAsync_ShouldCreateBacktestRunsTable()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		var tableExists = await TableExistsAsync("BacktestRuns");
		Assert.True(tableExists);
	}

	[Fact]
	public async Task InitializeAsync_ShouldCreateEventsTable()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		var tableExists = await TableExistsAsync("Events");
		Assert.True(tableExists);
	}

	[Fact]
	public async Task InitializeAsync_ShouldSetJournalMode()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		using var command = _connection.CreateCommand();
		command.CommandText = "PRAGMA journal_mode";
		var result = await command.ExecuteScalarAsync();

		Assert.Contains(result?.ToString()?.ToLowerInvariant(), new[] { "wal", "memory" });
	}

	[Fact]
	public async Task InitializeAsync_ShouldEnableForeignKeys()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		using var command = _connection.CreateCommand();
		command.CommandText = "PRAGMA foreign_keys";
		var result = await command.ExecuteScalarAsync();

		Assert.Equal(1L, result);
	}

	[Fact]
	public async Task InitializeAsync_ShouldCreateRequiredIndexes()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		var indexes = new[]
		{
			"idx_events_run_time",
			"idx_events_type",
			"idx_events_severity",
			"idx_events_category",
			"idx_events_parent",
			"idx_events_eventid"
		};

		foreach (var indexName in indexes)
		{
			var exists = await IndexExistsAsync(indexName);
			Assert.True(exists, $"Index {indexName} should exist");
		}
	}

	[Fact]
	public async Task InitializeAsync_ShouldBeIdempotent()
	{
		await DatabaseSchema.InitializeAsync(_connection);
		await DatabaseSchema.InitializeAsync(_connection);
		await DatabaseSchema.InitializeAsync(_connection);

		var backtestRunsExists = await TableExistsAsync("BacktestRuns");
		var eventsExists = await TableExistsAsync("Events");

		Assert.True(backtestRunsExists);
		Assert.True(eventsExists);
	}

	[Fact]
	public async Task EventsTable_ShouldEnforceForeignKeyConstraint()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		using var command = _connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO Events (EventId, RunId, Timestamp, EventType, Severity, Category, Properties)
			VALUES ('test-event', 'non-existent-run', datetime('now'), 'TradeExecution', 'Info', 'Execution', '{}')";

		await Assert.ThrowsAsync<SqliteException>(async () => await command.ExecuteNonQueryAsync());
	}

	[Fact]
	public async Task EventsTable_ShouldEnforcePropertiesJsonValidation()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		await CreateTestRunAsync("test-run");

		using var command = _connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO Events (EventId, RunId, Timestamp, EventType, Severity, Category, Properties)
			VALUES ('test-event', 'test-run', datetime('now'), 'TradeExecution', 'Info', 'Execution', 'invalid-json')";

		await Assert.ThrowsAsync<SqliteException>(async () => await command.ExecuteNonQueryAsync());
	}

	[Fact]
	public async Task EventsTable_ShouldEnforceValidationErrorsJsonValidation()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		await CreateTestRunAsync("test-run");

		using var command = _connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO Events (EventId, RunId, Timestamp, EventType, Severity, Category, Properties, ValidationErrors)
			VALUES ('test-event', 'test-run', datetime('now'), 'TradeExecution', 'Info', 'Execution', '{}', 'invalid-json')";

		await Assert.ThrowsAsync<SqliteException>(async () => await command.ExecuteNonQueryAsync());
	}

	[Fact]
	public async Task EventsTable_ShouldAllowNullValidationErrors()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		await CreateTestRunAsync("test-run");

		using var command = _connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO Events (EventId, RunId, Timestamp, EventType, Severity, Category, Properties, ValidationErrors)
			VALUES ('test-event', 'test-run', datetime('now'), 'TradeExecution', 'Info', 'Execution', '{}', NULL)";

		await command.ExecuteNonQueryAsync();

		var count = await GetTableRowCountAsync("Events");
		Assert.Equal(1, count);
	}

	[Fact]
	public async Task BacktestRunsTable_ShouldHaveCorrectSchema()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		var columns = await GetTableColumnsAsync("BacktestRuns");

		Assert.Contains("Id", columns);
		Assert.Contains("StartTime", columns);
		Assert.Contains("EndTime", columns);
		Assert.Contains("StrategyConfigHash", columns);
		Assert.Contains("CreatedAt", columns);
	}

	[Fact]
	public async Task EventsTable_ShouldHaveCorrectSchema()
	{
		await DatabaseSchema.InitializeAsync(_connection);

		var columns = await GetTableColumnsAsync("Events");

		Assert.Contains("Id", columns);
		Assert.Contains("EventId", columns);
		Assert.Contains("RunId", columns);
		Assert.Contains("Timestamp", columns);
		Assert.Contains("EventType", columns);
		Assert.Contains("Severity", columns);
		Assert.Contains("Category", columns);
		Assert.Contains("Properties", columns);
		Assert.Contains("ParentEventId", columns);
		Assert.Contains("ValidationErrors", columns);
	}

	private async Task<bool> TableExistsAsync(string tableName)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = @"
			SELECT COUNT(*) FROM sqlite_master
			WHERE type='table' AND name=@tableName";
		command.Parameters.AddWithValue("@tableName", tableName);

		var result = await command.ExecuteScalarAsync();
		return Convert.ToInt64(result) > 0;
	}

	private async Task<bool> IndexExistsAsync(string indexName)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = @"
			SELECT COUNT(*) FROM sqlite_master
			WHERE type='index' AND name=@indexName";
		command.Parameters.AddWithValue("@indexName", indexName);

		var result = await command.ExecuteScalarAsync();
		return Convert.ToInt64(result) > 0;
	}

	private async Task<List<string>> GetTableColumnsAsync(string tableName)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = $"PRAGMA table_info({tableName})";

		var columns = new List<string>();
		using var reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			columns.Add(reader.GetString(1));
		}

		return columns;
	}

	private async Task<int> GetTableRowCountAsync(string tableName)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = $"SELECT COUNT(*) FROM {tableName}";
		var result = await command.ExecuteScalarAsync();
		return Convert.ToInt32(result);
	}

	private async Task CreateTestRunAsync(string runId)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO BacktestRuns (Id, StartTime, EndTime, StrategyConfigHash)
			VALUES (@runId, datetime('now'), datetime('now', '+1 hour'), @hash)";
		command.Parameters.AddWithValue("@runId", runId);
		command.Parameters.AddWithValue("@hash", new string('a', 64));
		await command.ExecuteNonQueryAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

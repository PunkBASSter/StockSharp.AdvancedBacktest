using Microsoft.Data.Sqlite;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public static class DatabaseSchema
{
	public static async Task InitializeAsync(SqliteConnection connection)
	{
		using var walCommand = connection.CreateCommand();
		walCommand.CommandText = "PRAGMA journal_mode = 'wal'";
		await walCommand.ExecuteNonQueryAsync();

		using var foreignKeysCommand = connection.CreateCommand();
		foreignKeysCommand.CommandText = "PRAGMA foreign_keys = ON";
		await foreignKeysCommand.ExecuteNonQueryAsync();

		await CreateBacktestRunsTableAsync(connection);
		await CreateEventsTableAsync(connection);
		await CreateIndexesAsync(connection);
	}

	private static async Task CreateBacktestRunsTableAsync(SqliteConnection connection)
	{
		using var command = connection.CreateCommand();
		command.CommandText = @"
			CREATE TABLE IF NOT EXISTS BacktestRuns (
				Id TEXT PRIMARY KEY,
				StartTime TEXT NOT NULL,
				EndTime TEXT NOT NULL,
				StrategyConfigHash TEXT NOT NULL,
				CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
			)";
		await command.ExecuteNonQueryAsync();
	}

	private static async Task CreateEventsTableAsync(SqliteConnection connection)
	{
		using var command = connection.CreateCommand();
		command.CommandText = @"
			CREATE TABLE IF NOT EXISTS Events (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				EventId TEXT NOT NULL UNIQUE,
				RunId TEXT NOT NULL,
				Timestamp TEXT NOT NULL,
				EventType TEXT NOT NULL,
				Severity TEXT NOT NULL,
				Category TEXT NOT NULL,
				Properties TEXT NOT NULL,
				ParentEventId TEXT,
				ValidationErrors TEXT,
				FOREIGN KEY (RunId) REFERENCES BacktestRuns(Id) ON DELETE CASCADE,
				CHECK (json_valid(Properties)),
				CHECK (ValidationErrors IS NULL OR json_valid(ValidationErrors))
			)";
		await command.ExecuteNonQueryAsync();
	}

	private static async Task CreateIndexesAsync(SqliteConnection connection)
	{
		await CreateIndexAsync(connection, "idx_events_run_time", "Events", "RunId, Timestamp");
		await CreateIndexAsync(connection, "idx_events_type", "Events", "EventType");
		await CreateIndexAsync(connection, "idx_events_severity", "Events", "Severity");
		await CreateIndexAsync(connection, "idx_events_category", "Events", "Category");
		await CreateIndexAsync(connection, "idx_events_parent", "Events", "ParentEventId", whereClause: "WHERE ParentEventId IS NOT NULL");
		await CreateIndexAsync(connection, "idx_events_eventid", "Events", "EventId", unique: true);
	}

	private static async Task CreateIndexAsync(
		SqliteConnection connection,
		string indexName,
		string tableName,
		string columns,
		bool unique = false,
		string? whereClause = null)
	{
		using var command = connection.CreateCommand();
		command.CommandText = $@"
			CREATE {(unique ? "UNIQUE" : "")} INDEX IF NOT EXISTS {indexName}
			ON {tableName} ({columns})
			{whereClause ?? ""}";
		await command.ExecuteNonQueryAsync();
	}
}

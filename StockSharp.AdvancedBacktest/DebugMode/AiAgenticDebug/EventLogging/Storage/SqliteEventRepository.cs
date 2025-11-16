using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class SqliteEventRepository : IEventRepository
{
	private readonly SqliteConnection _connection;

	public SqliteEventRepository(SqliteConnection connection)
	{
		_connection = connection;
	}

	public async Task CreateBacktestRunAsync(BacktestRunEntity run)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO BacktestRuns (Id, StartTime, EndTime, StrategyConfigHash)
			VALUES (@id, @startTime, @endTime, @configHash)";

		command.Parameters.AddWithValue("@id", run.Id);
		command.Parameters.AddWithValue("@startTime", run.StartTime.ToString("o"));
		command.Parameters.AddWithValue("@endTime", run.EndTime.ToString("o"));
		command.Parameters.AddWithValue("@configHash", run.StrategyConfigHash);

		await command.ExecuteNonQueryAsync();
	}

	public async Task<BacktestRunEntity?> GetBacktestRunAsync(string runId)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = "SELECT * FROM BacktestRuns WHERE Id = @id";
		command.Parameters.AddWithValue("@id", runId);

		using var reader = await command.ExecuteReaderAsync();
		if (!await reader.ReadAsync()) return null;

		return new BacktestRunEntity
		{
			Id = reader.GetString(0),
			StartTime = DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
			EndTime = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
			StrategyConfigHash = reader.GetString(3),
			CreatedAt = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind)
		};
	}

	public async Task WriteEventAsync(EventEntity eventEntity)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO Events (EventId, RunId, Timestamp, EventType, Severity, Category, Properties, ParentEventId, ValidationErrors)
			VALUES (@eventId, @runId, @timestamp, @type, @severity, @category, json(@properties), @parent, json(@validation))";

		command.Parameters.AddWithValue("@eventId", eventEntity.EventId);
		command.Parameters.AddWithValue("@runId", eventEntity.RunId);
		command.Parameters.AddWithValue("@timestamp", eventEntity.Timestamp.ToString("o"));
		command.Parameters.AddWithValue("@type", eventEntity.EventType.ToString());
		command.Parameters.AddWithValue("@severity", eventEntity.Severity.ToString());
		command.Parameters.AddWithValue("@category", eventEntity.Category.ToString());
		command.Parameters.AddWithValue("@properties", eventEntity.Properties);
		command.Parameters.AddWithValue("@parent", (object?)eventEntity.ParentEventId ?? DBNull.Value);
		command.Parameters.AddWithValue("@validation", (object?)eventEntity.ValidationErrors ?? DBNull.Value);

		await command.ExecuteNonQueryAsync();
	}

	public async Task<EventEntity?> GetEventByIdAsync(string eventId)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = "SELECT * FROM Events WHERE EventId = @eventId";
		command.Parameters.AddWithValue("@eventId", eventId);

		using var reader = await command.ExecuteReaderAsync();
		if (!await reader.ReadAsync()) return null;

		return MapEventEntity(reader);
	}

	public async Task<EventQueryResult> QueryEventsAsync(EventQueryParameters parameters)
	{
		var stopwatch = Stopwatch.StartNew();

		var queryBuilder = new StringBuilder();
		queryBuilder.Append("SELECT * FROM Events WHERE RunId = @runId");

		if (parameters.EventType.HasValue)
			queryBuilder.Append(" AND EventType = @eventType");

		if (parameters.Severity.HasValue)
			queryBuilder.Append(" AND Severity = @severity");

		if (parameters.Category.HasValue)
			queryBuilder.Append(" AND Category = @category");

		if (parameters.StartTime.HasValue)
			queryBuilder.Append(" AND Timestamp >= @startTime");

		if (parameters.EndTime.HasValue)
			queryBuilder.Append(" AND Timestamp <= @endTime");

		queryBuilder.Append(" ORDER BY Timestamp");
		queryBuilder.Append($" LIMIT {parameters.PageSize} OFFSET {parameters.PageIndex * parameters.PageSize}");

		using var command = _connection.CreateCommand();
		command.CommandText = queryBuilder.ToString();

		command.Parameters.AddWithValue("@runId", parameters.RunId);

		if (parameters.EventType.HasValue)
			command.Parameters.AddWithValue("@eventType", parameters.EventType.Value.ToString());

		if (parameters.Severity.HasValue)
			command.Parameters.AddWithValue("@severity", parameters.Severity.Value.ToString());

		if (parameters.Category.HasValue)
			command.Parameters.AddWithValue("@category", parameters.Category.Value.ToString());

		if (parameters.StartTime.HasValue)
			command.Parameters.AddWithValue("@startTime", parameters.StartTime.Value.ToString("o"));

		if (parameters.EndTime.HasValue)
			command.Parameters.AddWithValue("@endTime", parameters.EndTime.Value.ToString("o"));

		var events = new List<EventEntity>();
		using (var reader = await command.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				events.Add(MapEventEntity(reader));
			}
		}

		stopwatch.Stop();

		return new EventQueryResult
		{
			Events = events,
			Metadata = new QueryResultMetadata
			{
				TotalCount = events.Count,
				ReturnedCount = events.Count,
				PageIndex = parameters.PageIndex,
				PageSize = parameters.PageSize,
				HasMore = events.Count == parameters.PageSize,
				QueryTimeMs = (int)stopwatch.ElapsedMilliseconds,
				Truncated = false
			}
		};
	}

	private static EventEntity MapEventEntity(SqliteDataReader reader)
	{
		return new EventEntity
		{
			Id = reader.GetInt64(0),
			EventId = reader.GetString(1),
			RunId = reader.GetString(2),
			Timestamp = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
			EventType = Enum.Parse<EventType>(reader.GetString(4)),
			Severity = Enum.Parse<EventSeverity>(reader.GetString(5)),
			Category = Enum.Parse<EventCategory>(reader.GetString(6)),
			Properties = reader.GetString(7),
			ParentEventId = reader.IsDBNull(8) ? null : reader.GetString(8),
			ValidationErrors = reader.IsDBNull(9) ? null : reader.GetString(9)
		};
	}
}

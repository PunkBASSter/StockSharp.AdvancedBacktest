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

		var whereClauseBuilder = new StringBuilder();
		whereClauseBuilder.Append("WHERE RunId = @runId");

		if (parameters.EventType.HasValue)
			whereClauseBuilder.Append(" AND EventType = @eventType");

		if (parameters.Severity.HasValue)
			whereClauseBuilder.Append(" AND Severity = @severity");

		if (parameters.Category.HasValue)
			whereClauseBuilder.Append(" AND Category = @category");

		if (parameters.StartTime.HasValue)
			whereClauseBuilder.Append(" AND Timestamp >= @startTime");

		if (parameters.EndTime.HasValue)
			whereClauseBuilder.Append(" AND Timestamp <= @endTime");

		var whereClause = whereClauseBuilder.ToString();

		int totalCount;
		using (var countCommand = _connection.CreateCommand())
		{
			countCommand.CommandText = $"SELECT COUNT(*) FROM Events {whereClause}";
			AddQueryParameters(countCommand, parameters);
			totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
		}

		var queryBuilder = new StringBuilder();
		queryBuilder.Append($"SELECT * FROM Events {whereClause}");
		queryBuilder.Append(" ORDER BY Timestamp");
		queryBuilder.Append($" LIMIT {parameters.PageSize} OFFSET {parameters.PageIndex * parameters.PageSize}");

		using var command = _connection.CreateCommand();
		command.CommandText = queryBuilder.ToString();
		AddQueryParameters(command, parameters);

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
				TotalCount = totalCount,
				ReturnedCount = events.Count,
				PageIndex = parameters.PageIndex,
				PageSize = parameters.PageSize,
				HasMore = (parameters.PageIndex + 1) * parameters.PageSize < totalCount,
				QueryTimeMs = (int)stopwatch.ElapsedMilliseconds,
				Truncated = false
			}
		};
	}

	public async Task<EventQueryResult> QueryEventsByEntityAsync(EntityReferenceQueryParameters parameters)
	{
		var stopwatch = Stopwatch.StartNew();

		var validEntityTypes = new[] { "OrderId", "SecuritySymbol", "PositionId", "IndicatorName" };
		if (!validEntityTypes.Contains(parameters.EntityType))
			throw new ArgumentException($"Invalid entity type: {parameters.EntityType}");

		var whereClauseBuilder = new StringBuilder();
		whereClauseBuilder.Append("WHERE RunId = @runId");
		whereClauseBuilder.Append($" AND json_extract(Properties, '$.{parameters.EntityType}') = @entityValue");

		if (parameters.EventTypeFilter?.Length > 0)
		{
			var eventTypeParams = string.Join(", ", parameters.EventTypeFilter.Select((_, i) => $"@eventType{i}"));
			whereClauseBuilder.Append($" AND EventType IN ({eventTypeParams})");
		}

		var whereClause = whereClauseBuilder.ToString();

		int totalCount;
		using (var countCommand = _connection.CreateCommand())
		{
			countCommand.CommandText = $"SELECT COUNT(*) FROM Events {whereClause}";
			AddEntityQueryParameters(countCommand, parameters);
			totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
		}

		var queryBuilder = new StringBuilder();
		queryBuilder.Append($"SELECT * FROM Events {whereClause}");
		queryBuilder.Append(" ORDER BY Timestamp");
		queryBuilder.Append($" LIMIT {parameters.PageSize} OFFSET {parameters.PageIndex * parameters.PageSize}");

		using var command = _connection.CreateCommand();
		command.CommandText = queryBuilder.ToString();
		AddEntityQueryParameters(command, parameters);

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
				TotalCount = totalCount,
				ReturnedCount = events.Count,
				PageIndex = parameters.PageIndex,
				PageSize = parameters.PageSize,
				HasMore = (parameters.PageIndex + 1) * parameters.PageSize < totalCount,
				QueryTimeMs = (int)stopwatch.ElapsedMilliseconds,
				Truncated = false
			}
		};
	}

	public async Task<EventSequenceQueryResult> QueryEventSequenceAsync(EventSequenceQueryParameters parameters)
	{
		var stopwatch = Stopwatch.StartNew();
		var sequences = new List<EventSequence>();

		if (!string.IsNullOrEmpty(parameters.RootEventId))
		{
			var sequence = await GetEventChainAsync(parameters.RunId, parameters.RootEventId, parameters.MaxDepth);
			if (sequence != null)
			{
				var complete = EvaluateSequenceCompleteness(sequence, parameters.SequencePattern);
				sequences.Add(new EventSequence
				{
					RootEventId = parameters.RootEventId,
					Events = sequence,
					Complete = complete.IsComplete,
					MissingEventTypes = complete.MissingTypes
				});
			}
		}
		else
		{
			var rootEvents = await GetRootEventsAsync(parameters.RunId, parameters.SequencePattern?.FirstOrDefault());

			foreach (var rootEvent in rootEvents)
			{
				var chain = await GetEventChainAsync(parameters.RunId, rootEvent.EventId, parameters.MaxDepth);
				if (chain == null) continue;

				var complete = EvaluateSequenceCompleteness(chain, parameters.SequencePattern);

				if (!parameters.FindIncomplete && !complete.IsComplete)
					continue;

				if (parameters.SequencePattern != null && !PatternMatches(chain, parameters.SequencePattern) && !parameters.FindIncomplete)
					continue;

				sequences.Add(new EventSequence
				{
					RootEventId = rootEvent.EventId,
					Events = chain,
					Complete = complete.IsComplete,
					MissingEventTypes = complete.MissingTypes
				});
			}
		}

		stopwatch.Stop();

		var totalSequences = sequences.Count;
		var pagedSequences = sequences
			.Skip(parameters.PageIndex * parameters.PageSize)
			.Take(parameters.PageSize)
			.ToList();

		return new EventSequenceQueryResult
		{
			Sequences = pagedSequences,
			Metadata = new SequenceQueryMetadata
			{
				TotalSequences = totalSequences,
				ReturnedCount = pagedSequences.Count,
				PageIndex = parameters.PageIndex,
				PageSize = parameters.PageSize,
				HasMore = (parameters.PageIndex + 1) * parameters.PageSize < totalSequences,
				QueryTimeMs = (int)stopwatch.ElapsedMilliseconds
			}
		};
	}

	private async Task<List<EventEntity>?> GetEventChainAsync(string runId, string rootEventId, int maxDepth)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = @"
			WITH RECURSIVE EventChain AS (
				SELECT *, 1 as depth FROM Events WHERE EventId = @rootEventId AND RunId = @runId
				UNION ALL
				SELECT e.*, ec.depth + 1 FROM Events e
				INNER JOIN EventChain ec ON e.ParentEventId = ec.EventId
				WHERE ec.depth < @maxDepth
			)
			SELECT Id, EventId, RunId, Timestamp, EventType, Severity, Category, Properties, ParentEventId, ValidationErrors
			FROM EventChain ORDER BY Timestamp";

		command.Parameters.AddWithValue("@rootEventId", rootEventId);
		command.Parameters.AddWithValue("@runId", runId);
		command.Parameters.AddWithValue("@maxDepth", maxDepth);

		var events = new List<EventEntity>();
		using (var reader = await command.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				events.Add(MapEventEntity(reader));
			}
		}

		return events.Count > 0 ? events : null;
	}

	private async Task<List<EventEntity>> GetRootEventsAsync(string runId, EventType? filterType)
	{
		using var command = _connection.CreateCommand();
		var whereClause = "WHERE RunId = @runId AND ParentEventId IS NULL";
		if (filterType.HasValue)
			whereClause += " AND EventType = @eventType";

		command.CommandText = $"SELECT * FROM Events {whereClause} ORDER BY Timestamp";
		command.Parameters.AddWithValue("@runId", runId);
		if (filterType.HasValue)
			command.Parameters.AddWithValue("@eventType", filterType.Value.ToString());

		var events = new List<EventEntity>();
		using (var reader = await command.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				events.Add(MapEventEntity(reader));
			}
		}
		return events;
	}

	private static (bool IsComplete, EventType[]? MissingTypes) EvaluateSequenceCompleteness(
		IReadOnlyList<EventEntity> chain, EventType[]? pattern)
	{
		if (pattern == null || pattern.Length == 0)
			return (true, null);

		var chainTypes = chain.Select(e => e.EventType).ToHashSet();
		var missing = pattern.Where(p => !chainTypes.Contains(p)).ToArray();

		return (missing.Length == 0, missing.Length > 0 ? missing : null);
	}

	private static bool PatternMatches(IReadOnlyList<EventEntity> chain, EventType[] pattern)
	{
		var chainTypes = chain.Select(e => e.EventType).ToList();
		return pattern.All(p => chainTypes.Contains(p));
	}

	private static void AddEntityQueryParameters(SqliteCommand command, EntityReferenceQueryParameters parameters)
	{
		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@entityValue", parameters.EntityValue);

		if (parameters.EventTypeFilter?.Length > 0)
		{
			for (int i = 0; i < parameters.EventTypeFilter.Length; i++)
			{
				command.Parameters.AddWithValue($"@eventType{i}", parameters.EventTypeFilter[i].ToString());
			}
		}
	}

	private static void AddQueryParameters(SqliteCommand command, EventQueryParameters parameters)
	{
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

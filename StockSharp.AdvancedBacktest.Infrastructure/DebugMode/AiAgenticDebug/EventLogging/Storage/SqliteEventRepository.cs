using System.Diagnostics;
using System.Text;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Validation;

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

	public async Task<IReadOnlyList<BacktestRunEntity>> GetAllBacktestRunsAsync()
	{
		using var command = _connection.CreateCommand();
		command.CommandText = "SELECT * FROM BacktestRuns ORDER BY CreatedAt DESC";

		var runs = new List<BacktestRunEntity>();
		using var reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			runs.Add(new BacktestRunEntity
			{
				Id = reader.GetString(0),
				StartTime = DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
				EndTime = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
				StrategyConfigHash = reader.GetString(3),
				CreatedAt = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind)
			});
		}
		return runs;
	}

	public async Task WriteEventAsync(EventEntity eventEntity)
	{
		CircularReferenceDetector.ThrowIfSelfReference(eventEntity);

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

	public async Task<EventQueryResult> QueryEventsWithValidationErrorsAsync(ValidationErrorQueryParameters parameters)
	{
		var stopwatch = Stopwatch.StartNew();

		var whereClauseBuilder = new StringBuilder();
		whereClauseBuilder.Append("WHERE RunId = @runId AND ValidationErrors IS NOT NULL");

		if (!string.IsNullOrEmpty(parameters.SeverityFilter))
		{
			whereClauseBuilder.Append($" AND ValidationErrors LIKE @severityPattern");
		}

		var whereClause = whereClauseBuilder.ToString();

		int totalCount;
		using (var countCommand = _connection.CreateCommand())
		{
			countCommand.CommandText = $"SELECT COUNT(*) FROM Events {whereClause}";
			countCommand.Parameters.AddWithValue("@runId", parameters.RunId);
			if (!string.IsNullOrEmpty(parameters.SeverityFilter))
			{
				countCommand.Parameters.AddWithValue("@severityPattern", $"%\"Severity\":\"{parameters.SeverityFilter}\"%");
			}
			totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
		}

		var queryBuilder = new StringBuilder();
		queryBuilder.Append($"SELECT * FROM Events {whereClause}");
		queryBuilder.Append(" ORDER BY Timestamp");
		queryBuilder.Append($" LIMIT {parameters.PageSize} OFFSET {parameters.PageIndex * parameters.PageSize}");

		using var command = _connection.CreateCommand();
		command.CommandText = queryBuilder.ToString();
		command.Parameters.AddWithValue("@runId", parameters.RunId);
		if (!string.IsNullOrEmpty(parameters.SeverityFilter))
		{
			command.Parameters.AddWithValue("@severityPattern", $"%\"Severity\":\"{parameters.SeverityFilter}\"%");
		}

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

	public async Task<AggregationResult> AggregateMetricsAsync(AggregationParameters parameters)
	{
		var stopwatch = Stopwatch.StartNew();

		ValidatePropertyPath(parameters.PropertyPath);

		var whereClauseBuilder = new StringBuilder();
		whereClauseBuilder.Append("WHERE RunId = @runId AND EventType = @eventType");

		if (parameters.StartTime.HasValue)
			whereClauseBuilder.Append(" AND Timestamp >= @startTime");

		if (parameters.EndTime.HasValue)
			whereClauseBuilder.Append(" AND Timestamp <= @endTime");

		var whereClause = whereClauseBuilder.ToString();

		// Build aggregation SQL
		var aggregateColumns = new List<string>();
		aggregateColumns.Add("COUNT(*) as cnt");

		var requestedAggregations = parameters.Aggregations.Select(a => a.ToLowerInvariant()).ToHashSet();

		if (requestedAggregations.Contains("sum"))
			aggregateColumns.Add($"SUM(CAST(json_extract(Properties, @propertyPath) AS REAL)) as sum_val");

		// Always calculate avg if stddev is requested (needed for stddev calculation)
		if (requestedAggregations.Contains("avg") || requestedAggregations.Contains("stddev"))
			aggregateColumns.Add($"AVG(CAST(json_extract(Properties, @propertyPath) AS REAL)) as avg_val");

		if (requestedAggregations.Contains("min"))
			aggregateColumns.Add($"MIN(CAST(json_extract(Properties, @propertyPath) AS REAL)) as min_val");

		if (requestedAggregations.Contains("max"))
			aggregateColumns.Add($"MAX(CAST(json_extract(Properties, @propertyPath) AS REAL)) as max_val");

		using var command = _connection.CreateCommand();
		command.CommandText = $"SELECT {string.Join(", ", aggregateColumns)} FROM Events {whereClause}";

		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@eventType", parameters.EventType.ToString());
		command.Parameters.AddWithValue("@propertyPath", parameters.PropertyPath);

		if (parameters.StartTime.HasValue)
			command.Parameters.AddWithValue("@startTime", parameters.StartTime.Value.ToString("o"));

		if (parameters.EndTime.HasValue)
			command.Parameters.AddWithValue("@endTime", parameters.EndTime.Value.ToString("o"));

		int count = 0;
		decimal? sum = null, avg = null, min = null, max = null;

		using (var reader = await command.ExecuteReaderAsync())
		{
			if (await reader.ReadAsync())
			{
				count = reader.GetInt32(reader.GetOrdinal("cnt"));

				if (requestedAggregations.Contains("sum") && !reader.IsDBNull(reader.GetOrdinal("sum_val")))
					sum = (decimal)reader.GetDouble(reader.GetOrdinal("sum_val"));

				// Read avg if requested, or if stddev is requested (needed for stddev calculation)
				if ((requestedAggregations.Contains("avg") || requestedAggregations.Contains("stddev")) && !reader.IsDBNull(reader.GetOrdinal("avg_val")))
					avg = (decimal)reader.GetDouble(reader.GetOrdinal("avg_val"));

				if (requestedAggregations.Contains("min") && !reader.IsDBNull(reader.GetOrdinal("min_val")))
					min = (decimal)reader.GetDouble(reader.GetOrdinal("min_val"));

				if (requestedAggregations.Contains("max") && !reader.IsDBNull(reader.GetOrdinal("max_val")))
					max = (decimal)reader.GetDouble(reader.GetOrdinal("max_val"));
			}
		}

		// Calculate standard deviation in application layer (SQLite doesn't have STDDEV)
		decimal? stddev = null;
		if (requestedAggregations.Contains("stddev") && count > 0 && avg.HasValue)
		{
			stddev = await CalculateStdDevAsync(parameters, avg.Value, count);
		}

		stopwatch.Stop();

		return new AggregationResult
		{
			Aggregations = new AggregationValues
			{
				Count = count,
				Sum = sum,
				Avg = avg,
				Min = min,
				Max = max,
				StdDev = stddev
			},
			Metadata = new AggregationMetadata
			{
				TotalEvents = count,
				QueryTimeMs = (int)stopwatch.ElapsedMilliseconds,
				EventType = parameters.EventType.ToString(),
				PropertyPath = parameters.PropertyPath
			}
		};
	}

	private async Task<decimal> CalculateStdDevAsync(AggregationParameters parameters, decimal mean, int count)
	{
		var whereClauseBuilder = new StringBuilder();
		whereClauseBuilder.Append("WHERE RunId = @runId AND EventType = @eventType");

		if (parameters.StartTime.HasValue)
			whereClauseBuilder.Append(" AND Timestamp >= @startTime");

		if (parameters.EndTime.HasValue)
			whereClauseBuilder.Append(" AND Timestamp <= @endTime");

		using var command = _connection.CreateCommand();
		command.CommandText = $@"
			SELECT SUM((CAST(json_extract(Properties, @propertyPath) AS REAL) - @mean) *
			           (CAST(json_extract(Properties, @propertyPath) AS REAL) - @mean)) as variance_sum
			FROM Events {whereClauseBuilder}";

		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@eventType", parameters.EventType.ToString());
		command.Parameters.AddWithValue("@propertyPath", parameters.PropertyPath);
		command.Parameters.AddWithValue("@mean", (double)mean);

		if (parameters.StartTime.HasValue)
			command.Parameters.AddWithValue("@startTime", parameters.StartTime.Value.ToString("o"));

		if (parameters.EndTime.HasValue)
			command.Parameters.AddWithValue("@endTime", parameters.EndTime.Value.ToString("o"));

		var varianceSum = await command.ExecuteScalarAsync();
		if (varianceSum == null || varianceSum == DBNull.Value)
			return 0m;

		var variance = (double)varianceSum / count;
		return (decimal)Math.Sqrt(variance);
	}

	private static void ValidatePropertyPath(string propertyPath)
	{
		// Validate property path to prevent SQL injection
		// Valid format: $.PropertyName or $.Parent.Child
		if (string.IsNullOrEmpty(propertyPath))
			throw new ArgumentException("Property path cannot be empty", nameof(propertyPath));

		if (!propertyPath.StartsWith("$."))
			throw new ArgumentException("Property path must start with '$.'", nameof(propertyPath));

		// Only allow alphanumeric characters, underscores, and dots after the $. prefix
		var pathWithoutPrefix = propertyPath[2..];
		if (!System.Text.RegularExpressions.Regex.IsMatch(pathWithoutPrefix, @"^[a-zA-Z0-9_\.]+$"))
			throw new ArgumentException("Property path contains invalid characters", nameof(propertyPath));
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

	public async Task<StateSnapshotResult> GetStateSnapshotAsync(StateSnapshotQueryParameters parameters)
	{
		var stopwatch = Stopwatch.StartNew();

		var positions = await ReconstructPositionsAsync(parameters);
		var indicators = parameters.IncludeIndicators
			? await ReconstructIndicatorsAsync(parameters)
			: [];
		var activeOrders = parameters.IncludeActiveOrders
			? await ReconstructActiveOrdersAsync(parameters)
			: [];
		var pnl = await ReconstructPnLAsync(parameters);

		stopwatch.Stop();

		return new StateSnapshotResult
		{
			Timestamp = parameters.Timestamp,
			RunId = parameters.RunId,
			State = new StrategyState
			{
				Positions = positions,
				Indicators = indicators,
				ActiveOrders = activeOrders,
				Pnl = pnl
			},
			Metadata = new StateSnapshotMetadata
			{
				QueryTimeMs = (int)stopwatch.ElapsedMilliseconds,
				Reconstructed = true
			}
		};
	}

	public async Task<StateDeltaResult> GetStateDeltaAsync(StateDeltaQueryParameters parameters)
	{
		var stopwatch = Stopwatch.StartNew();

		var positionChanges = await CalculatePositionChangesAsync(parameters);
		var indicatorChanges = await CalculateIndicatorChangesAsync(parameters);
		var pnlChange = await CalculatePnLChangeAsync(parameters);

		stopwatch.Stop();

		return new StateDeltaResult
		{
			StartTimestamp = parameters.StartTimestamp,
			EndTimestamp = parameters.EndTimestamp,
			RunId = parameters.RunId,
			PositionChanges = positionChanges,
			IndicatorChanges = indicatorChanges,
			PnlChange = pnlChange,
			Metadata = new StateDeltaMetadata
			{
				QueryTimeMs = (int)stopwatch.ElapsedMilliseconds
			}
		};
	}

	private async Task<IReadOnlyList<PositionState>> ReconstructPositionsAsync(StateSnapshotQueryParameters parameters)
	{
		using var command = _connection.CreateCommand();

		var whereClause = new StringBuilder();
		whereClause.Append("WHERE RunId = @runId AND EventType = 'PositionUpdate' AND Timestamp <= @timestamp");

		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			whereClause.Append(" AND UPPER(json_extract(Properties, '$.SecuritySymbol')) = UPPER(@securitySymbol)");
		}

		command.CommandText = $@"
			WITH LatestPositions AS (
				SELECT Properties,
					   ROW_NUMBER() OVER (PARTITION BY json_extract(Properties, '$.SecuritySymbol') ORDER BY Timestamp DESC) as rn
				FROM Events
				{whereClause}
			)
			SELECT Properties FROM LatestPositions WHERE rn = 1";

		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@timestamp", parameters.Timestamp.ToString("o"));
		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			command.Parameters.AddWithValue("@securitySymbol", parameters.SecuritySymbol);
		}

		var positions = new List<PositionState>();
		using (var reader = await command.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				var props = System.Text.Json.JsonSerializer.Deserialize<PositionProperties>(reader.GetString(0));
				if (props != null)
				{
					positions.Add(new PositionState
					{
						SecuritySymbol = props.SecuritySymbol,
						Quantity = props.Quantity,
						AveragePrice = props.AveragePrice,
						UnrealizedPnL = props.UnrealizedPnL,
						RealizedPnL = props.RealizedPnL
					});
				}
			}
		}

		return positions;
	}

	private async Task<IReadOnlyList<IndicatorState>> ReconstructIndicatorsAsync(StateSnapshotQueryParameters parameters)
	{
		using var command = _connection.CreateCommand();

		var whereClause = new StringBuilder();
		whereClause.Append("WHERE RunId = @runId AND EventType = 'IndicatorCalculation' AND Timestamp <= @timestamp");

		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			whereClause.Append(" AND UPPER(json_extract(Properties, '$.SecuritySymbol')) = UPPER(@securitySymbol)");
		}

		command.CommandText = $@"
			WITH LatestIndicators AS (
				SELECT Properties,
					   ROW_NUMBER() OVER (PARTITION BY json_extract(Properties, '$.IndicatorName'), json_extract(Properties, '$.SecuritySymbol') ORDER BY Timestamp DESC) as rn
				FROM Events
				{whereClause}
			)
			SELECT Properties FROM LatestIndicators WHERE rn = 1";

		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@timestamp", parameters.Timestamp.ToString("o"));
		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			command.Parameters.AddWithValue("@securitySymbol", parameters.SecuritySymbol);
		}

		var indicators = new List<IndicatorState>();
		using (var reader = await command.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				var props = System.Text.Json.JsonSerializer.Deserialize<IndicatorProperties>(reader.GetString(0));
				if (props != null)
				{
					indicators.Add(new IndicatorState
					{
						Name = props.IndicatorName,
						SecuritySymbol = props.SecuritySymbol,
						Value = props.Value,
						Parameters = props.Parameters
					});
				}
			}
		}

		return indicators;
	}

	private async Task<IReadOnlyList<ActiveOrderState>> ReconstructActiveOrdersAsync(StateSnapshotQueryParameters parameters)
	{
		using var command = _connection.CreateCommand();

		var whereClause = new StringBuilder();
		whereClause.Append("WHERE RunId = @runId AND Timestamp <= @timestamp");
		whereClause.Append(" AND EventType = 'StateChange'");
		whereClause.Append(" AND json_extract(Properties, '$.OrderStatus') = 'Placed'");

		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			whereClause.Append(" AND UPPER(json_extract(Properties, '$.SecuritySymbol')) = UPPER(@securitySymbol)");
		}

		command.CommandText = $@"
			SELECT Properties, json_extract(Properties, '$.OrderId') as OrderId
			FROM Events
			{whereClause}
			AND json_extract(Properties, '$.OrderId') NOT IN (
				SELECT json_extract(Properties, '$.OrderId')
				FROM Events
				WHERE RunId = @runId AND Timestamp <= @timestamp
				AND EventType = 'TradeExecution'
			)";

		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@timestamp", parameters.Timestamp.ToString("o"));
		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			command.Parameters.AddWithValue("@securitySymbol", parameters.SecuritySymbol);
		}

		var activeOrders = new List<ActiveOrderState>();
		using (var reader = await command.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				var props = System.Text.Json.JsonSerializer.Deserialize<OrderProperties>(reader.GetString(0));
				if (props != null)
				{
					activeOrders.Add(new ActiveOrderState
					{
						OrderId = props.OrderId,
						SecuritySymbol = props.SecuritySymbol,
						Direction = props.Direction,
						Quantity = props.Quantity,
						Price = props.Price
					});
				}
			}
		}

		return activeOrders;
	}

	private async Task<PnLState> ReconstructPnLAsync(StateSnapshotQueryParameters parameters)
	{
		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			return await ReconstructSecurityPnLAsync(parameters);
		}

		using var command = _connection.CreateCommand();
		command.CommandText = @"
			WITH LatestPnL AS (
				SELECT Properties,
					   ROW_NUMBER() OVER (ORDER BY Timestamp DESC) as rn
				FROM Events
				WHERE RunId = @runId AND EventType = 'StateChange' AND Timestamp <= @timestamp
				AND json_extract(Properties, '$.StateType') = 'PnL'
			)
			SELECT Properties FROM LatestPnL WHERE rn = 1";

		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@timestamp", parameters.Timestamp.ToString("o"));

		var result = await command.ExecuteScalarAsync();
		if (result == null || result == DBNull.Value)
		{
			return new PnLState { Total = 0, Realized = 0, Unrealized = 0 };
		}

		var props = System.Text.Json.JsonSerializer.Deserialize<StateChangeProperties>(result.ToString()!);
		if (props?.StateAfter != null)
		{
			return new PnLState
			{
				Realized = props.StateAfter.RealizedPnL,
				Unrealized = props.StateAfter.UnrealizedPnL,
				Total = props.StateAfter.RealizedPnL + props.StateAfter.UnrealizedPnL
			};
		}

		return new PnLState { Total = 0, Realized = 0, Unrealized = 0 };
	}

	private async Task<PnLState> ReconstructSecurityPnLAsync(StateSnapshotQueryParameters parameters)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = @"
			WITH LatestPosition AS (
				SELECT Properties,
					   ROW_NUMBER() OVER (ORDER BY Timestamp DESC) as rn
				FROM Events
				WHERE RunId = @runId AND EventType = 'PositionUpdate' AND Timestamp <= @timestamp
				AND UPPER(json_extract(Properties, '$.SecuritySymbol')) = UPPER(@securitySymbol)
			)
			SELECT Properties FROM LatestPosition WHERE rn = 1";

		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@timestamp", parameters.Timestamp.ToString("o"));
		command.Parameters.AddWithValue("@securitySymbol", parameters.SecuritySymbol!);

		var result = await command.ExecuteScalarAsync();
		if (result == null || result == DBNull.Value)
		{
			return new PnLState { Total = 0, Realized = 0, Unrealized = 0 };
		}

		var props = System.Text.Json.JsonSerializer.Deserialize<PositionProperties>(result.ToString()!);
		if (props != null)
		{
			return new PnLState
			{
				Realized = props.RealizedPnL,
				Unrealized = props.UnrealizedPnL,
				Total = props.RealizedPnL + props.UnrealizedPnL
			};
		}

		return new PnLState { Total = 0, Realized = 0, Unrealized = 0 };
	}

	private async Task<IReadOnlyList<PositionChange>> CalculatePositionChangesAsync(StateDeltaQueryParameters parameters)
	{
		var beforeParams = new StateSnapshotQueryParameters
		{
			RunId = parameters.RunId,
			Timestamp = parameters.StartTimestamp,
			SecuritySymbol = parameters.SecuritySymbol,
			IncludeIndicators = false,
			IncludeActiveOrders = false
		};

		var afterParams = new StateSnapshotQueryParameters
		{
			RunId = parameters.RunId,
			Timestamp = parameters.EndTimestamp,
			SecuritySymbol = parameters.SecuritySymbol,
			IncludeIndicators = false,
			IncludeActiveOrders = false
		};

		var beforePositions = await ReconstructPositionsAsync(beforeParams);
		var afterPositions = await ReconstructPositionsAsync(afterParams);

		var allSymbols = beforePositions.Select(p => p.SecuritySymbol)
			.Union(afterPositions.Select(p => p.SecuritySymbol))
			.Distinct()
			.ToList();

		using var command = _connection.CreateCommand();
		var whereClause = new StringBuilder();
		whereClause.Append("WHERE RunId = @runId AND EventType = 'PositionUpdate'");
		whereClause.Append(" AND Timestamp > @startTime AND Timestamp <= @endTime");

		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			whereClause.Append(" AND UPPER(json_extract(Properties, '$.SecuritySymbol')) = UPPER(@securitySymbol)");
		}

		command.CommandText = $"SELECT DISTINCT json_extract(Properties, '$.SecuritySymbol') FROM Events {whereClause}";
		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@startTime", parameters.StartTimestamp.ToString("o"));
		command.Parameters.AddWithValue("@endTime", parameters.EndTimestamp.ToString("o"));
		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			command.Parameters.AddWithValue("@securitySymbol", parameters.SecuritySymbol);
		}

		var changedSymbols = new List<string>();
		using (var reader = await command.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				if (!reader.IsDBNull(0))
					changedSymbols.Add(reader.GetString(0));
			}
		}

		var changes = new List<PositionChange>();
		foreach (var symbol in changedSymbols)
		{
			var before = beforePositions.FirstOrDefault(p => p.SecuritySymbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
			var after = afterPositions.FirstOrDefault(p => p.SecuritySymbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

			changes.Add(new PositionChange
			{
				SecuritySymbol = symbol,
				QuantityBefore = before?.Quantity ?? 0,
				QuantityAfter = after?.Quantity ?? 0,
				AveragePriceBefore = before?.AveragePrice ?? 0,
				AveragePriceAfter = after?.AveragePrice ?? 0
			});
		}

		return changes;
	}

	private async Task<IReadOnlyList<IndicatorChange>> CalculateIndicatorChangesAsync(StateDeltaQueryParameters parameters)
	{
		using var command = _connection.CreateCommand();
		var whereClause = new StringBuilder();
		whereClause.Append("WHERE RunId = @runId AND EventType = 'IndicatorCalculation'");
		whereClause.Append(" AND Timestamp > @startTime AND Timestamp <= @endTime");

		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			whereClause.Append(" AND UPPER(json_extract(Properties, '$.SecuritySymbol')) = UPPER(@securitySymbol)");
		}

		command.CommandText = $@"
			WITH LatestIndicators AS (
				SELECT Properties,
					   json_extract(Properties, '$.IndicatorName') as Name,
					   json_extract(Properties, '$.SecuritySymbol') as Symbol,
					   ROW_NUMBER() OVER (PARTITION BY json_extract(Properties, '$.IndicatorName'), json_extract(Properties, '$.SecuritySymbol') ORDER BY Timestamp DESC) as rn
				FROM Events
				{whereClause}
			)
			SELECT Properties, Name, Symbol FROM LatestIndicators WHERE rn = 1";

		command.Parameters.AddWithValue("@runId", parameters.RunId);
		command.Parameters.AddWithValue("@startTime", parameters.StartTimestamp.ToString("o"));
		command.Parameters.AddWithValue("@endTime", parameters.EndTimestamp.ToString("o"));
		if (!string.IsNullOrEmpty(parameters.SecuritySymbol))
		{
			command.Parameters.AddWithValue("@securitySymbol", parameters.SecuritySymbol);
		}

		var changes = new List<IndicatorChange>();
		using (var reader = await command.ExecuteReaderAsync())
		{
			while (await reader.ReadAsync())
			{
				var props = System.Text.Json.JsonSerializer.Deserialize<IndicatorProperties>(reader.GetString(0));
				if (props != null)
				{
					changes.Add(new IndicatorChange
					{
						Name = props.IndicatorName,
						SecuritySymbol = props.SecuritySymbol,
						ValueBefore = null,
						ValueAfter = props.Value
					});
				}
			}
		}

		return changes;
	}

	private async Task<PnLChange?> CalculatePnLChangeAsync(StateDeltaQueryParameters parameters)
	{
		var beforeParams = new StateSnapshotQueryParameters
		{
			RunId = parameters.RunId,
			Timestamp = parameters.StartTimestamp,
			SecuritySymbol = parameters.SecuritySymbol,
			IncludeIndicators = false,
			IncludeActiveOrders = false
		};

		var afterParams = new StateSnapshotQueryParameters
		{
			RunId = parameters.RunId,
			Timestamp = parameters.EndTimestamp,
			SecuritySymbol = parameters.SecuritySymbol,
			IncludeIndicators = false,
			IncludeActiveOrders = false
		};

		var beforePnL = await ReconstructPnLAsync(beforeParams);
		var afterPnL = await ReconstructPnLAsync(afterParams);

		return new PnLChange
		{
			RealizedBefore = beforePnL.Realized,
			RealizedAfter = afterPnL.Realized,
			UnrealizedBefore = beforePnL.Unrealized,
			UnrealizedAfter = afterPnL.Unrealized
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

	private sealed class PositionProperties
	{
		public required string SecuritySymbol { get; init; }
		public decimal Quantity { get; init; }
		public decimal AveragePrice { get; init; }
		public decimal UnrealizedPnL { get; init; }
		public decimal RealizedPnL { get; init; }
	}

	private sealed class IndicatorProperties
	{
		public required string IndicatorName { get; init; }
		public required string SecuritySymbol { get; init; }
		public decimal Value { get; init; }
		public Dictionary<string, object>? Parameters { get; init; }
	}

	private sealed class OrderProperties
	{
		public required string OrderId { get; init; }
		public required string SecuritySymbol { get; init; }
		public required string Direction { get; init; }
		public decimal Quantity { get; init; }
		public decimal Price { get; init; }
	}

	private sealed class StateChangeProperties
	{
		public required string StateType { get; init; }
		public PnLStateProperties? StateAfter { get; init; }
	}

	private sealed class PnLStateProperties
	{
		public decimal UnrealizedPnL { get; init; }
		public decimal RealizedPnL { get; init; }
	}
}

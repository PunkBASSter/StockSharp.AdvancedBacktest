using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Performance;

/// <summary>
/// Tests for token efficiency (SC-002: reduce context window usage, SC-006: targeted queries).
/// Compares query result size vs equivalent JSONL parsing to demonstrate token reduction.
/// </summary>
public sealed class TokenEfficiencyTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly string _runId = Guid.NewGuid().ToString();

	public TokenEfficiencyTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).GetAwaiter().GetResult();
		_repository = new SqliteEventRepository(_connection);
	}

	[Fact]
	public async Task QueryResult_ShouldBeSmallerThanFullJsonlExport()
	{
		await SetupTestData(1000);

		// Get filtered query result (only TradeExecution events)
		var result = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PageSize = 100,
			PageIndex = 0
		});

		// Serialize query result
		var queryResultJson = JsonSerializer.Serialize(result, EventJsonContext.Default.EventQueryResult);
		var queryResultBytes = Encoding.UTF8.GetByteCount(queryResultJson);

		// Get all events (simulating full JSONL export)
		var allEvents = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			PageSize = 1000,
			PageIndex = 0
		});

		// Simulate JSONL format (one event per line)
		var jsonlBuilder = new StringBuilder();
		foreach (var evt in allEvents.Events)
		{
			jsonlBuilder.AppendLine(JsonSerializer.Serialize(evt, EventJsonContext.Default.EventEntity));
		}
		var jsonlBytes = Encoding.UTF8.GetByteCount(jsonlBuilder.ToString());

		// Filtered query result should be significantly smaller than full export
		Assert.True(queryResultBytes < jsonlBytes,
			$"Query result ({queryResultBytes} bytes) should be smaller than full JSONL ({jsonlBytes} bytes)");
	}

	[Fact]
	public async Task AggregationResult_ShouldBeMuchSmallerThanRawEvents()
	{
		await SetupTestData(10_000);

		// Get aggregation result
		var aggregation = await _repository.AggregateMetricsAsync(new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count", "sum", "avg", "min", "max", "stddev"]
		});

		// Serialize aggregation result
		var aggregationJson = JsonSerializer.Serialize(aggregation);
		var aggregationBytes = Encoding.UTF8.GetByteCount(aggregationJson);

		// Get raw events count for comparison
		var allEvents = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PageSize = 10_000,
			PageIndex = 0
		});

		// Estimate raw events size
		var rawEventsBytes = 0;
		foreach (var evt in allEvents.Events)
		{
			rawEventsBytes += Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(evt, EventJsonContext.Default.EventEntity));
		}

		// Aggregation result should be orders of magnitude smaller
		var reductionFactor = (double)rawEventsBytes / aggregationBytes;
		Assert.True(reductionFactor > 100,
			$"Aggregation result ({aggregationBytes} bytes) should be >100x smaller than raw events ({rawEventsBytes} bytes). Reduction: {reductionFactor:F1}x");
	}

	[Fact]
	public async Task PaginatedQuery_ShouldReduceTokenUsageVsFullLoad()
	{
		await SetupTestData(5000);

		// Get first page only (typical LLM agent pattern)
		var firstPage = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PageSize = 100,
			PageIndex = 0
		});

		var firstPageJson = JsonSerializer.Serialize(firstPage, EventJsonContext.Default.EventQueryResult);
		var firstPageBytes = Encoding.UTF8.GetByteCount(firstPageJson);

		// Metadata tells agent about total count without loading all events
		Assert.True(firstPage.Metadata.TotalCount > firstPage.Events.Count);
		Assert.True(firstPage.Metadata.HasMore);

		// Calculate what full load would cost
		var estimatedFullLoadBytes = firstPageBytes * (firstPage.Metadata.TotalCount / 100);

		// First page is a small fraction of total
		var savings = 1.0 - ((double)firstPageBytes / estimatedFullLoadBytes);
		Assert.True(savings > 0.9, $"Pagination should save >90% tokens. Savings: {savings:P1}");
	}

	[Fact]
	public async Task TimeRangeFilter_ShouldReduceResultSize()
	{
		await SetupTestData(10_000);
		var baseTime = DateTime.UtcNow;

		// Query with time range filter (only 10% of events)
		var filteredResult = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			StartTime = baseTime.AddSeconds(1000),
			EndTime = baseTime.AddSeconds(2000),
			PageSize = 1000,
			PageIndex = 0
		});

		// Query without filter
		var fullResult = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PageSize = 1000,
			PageIndex = 0
		});

		// Filtered result should have fewer events
		Assert.True(filteredResult.Metadata.TotalCount < fullResult.Metadata.TotalCount,
			"Time range filter should reduce result count");
	}

	[Fact]
	public async Task SeverityFilter_ShouldEnableFocusedDebugging()
	{
		await SetupMixedSeverityData(1000);

		// Agent looking for errors only
		var errorEvents = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			Severity = EventSeverity.Error,
			PageSize = 100,
			PageIndex = 0
		});

		// All events
		var allEvents = await _repository.QueryEventsAsync(new EventQueryParameters
		{
			RunId = _runId,
			PageSize = 1000,
			PageIndex = 0
		});

		// Error-only query should return much fewer events
		var errorRatio = (double)errorEvents.Metadata.TotalCount / allEvents.Metadata.TotalCount;
		Assert.True(errorRatio < 0.2, $"Error-only query should return <20% of events. Ratio: {errorRatio:P1}");
	}

	private async Task SetupTestData(int count)
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(24),
			StrategyConfigHash = new string('a', 64)
		});

		var random = new Random(42);
		var baseTime = DateTime.UtcNow;
		var eventTypes = new[] { EventType.TradeExecution, EventType.OrderRejection, EventType.IndicatorCalculation };

		for (int i = 0; i < count; i++)
		{
			await _repository.WriteEventAsync(new EventEntity
			{
				EventId = Guid.NewGuid().ToString(),
				RunId = _runId,
				Timestamp = baseTime.AddSeconds(i),
				EventType = eventTypes[i % eventTypes.Length],
				Severity = EventSeverity.Info,
				Category = EventCategory.Execution,
				Properties = $$$"""{"Price": {{{100 + random.NextDouble() * 100:F2}}}, "Quantity": {{{random.Next(1, 100)}}}}"""
			});
		}
	}

	private async Task SetupMixedSeverityData(int count)
	{
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(24),
			StrategyConfigHash = new string('a', 64)
		});

		var baseTime = DateTime.UtcNow;

		for (int i = 0; i < count; i++)
		{
			// 80% Info, 10% Warning, 10% Error
			var remainder = i % 10;
			var severity = remainder switch
			{
				0 => EventSeverity.Error,
				1 => EventSeverity.Warning,
				_ => EventSeverity.Info
			};

			await _repository.WriteEventAsync(new EventEntity
			{
				EventId = Guid.NewGuid().ToString(),
				RunId = _runId,
				Timestamp = baseTime.AddSeconds(i),
				EventType = EventType.TradeExecution,
				Severity = severity,
				Category = EventCategory.Execution,
				Properties = "{}"
			});
		}
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

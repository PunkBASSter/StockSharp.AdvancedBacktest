using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Integration;

public sealed class QueryValidationErrorsTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly string _runId = Guid.NewGuid().ToString();

	public QueryValidationErrorsTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).GetAwaiter().GetResult();
		_repository = new SqliteEventRepository(_connection);
	}

	[Fact]
	public async Task QueryEventsWithValidationErrors_ShouldReturnOnlyEventsWithErrors()
	{
		await CreateBacktestRun();

		// Create event with validation errors
		var eventWithErrors = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "invalid"}""",
			ValidationErrors = """[{"Field": "Properties.Price", "Error": "Missing required field", "Severity": "Error"}]"""
		};
		await _repository.WriteEventAsync(eventWithErrors);

		// Create event without validation errors
		var eventWithoutErrors = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "test-123", "Price": 100.50}""",
			ValidationErrors = null
		};
		await _repository.WriteEventAsync(eventWithoutErrors);

		var parameters = new ValidationErrorQueryParameters
		{
			RunId = _runId,
			PageSize = 100,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsWithValidationErrorsAsync(parameters);

		Assert.Single(result.Events);
		Assert.Equal(eventWithErrors.EventId, result.Events[0].EventId);
		Assert.NotNull(result.Events[0].ValidationErrors);
	}

	[Fact]
	public async Task QueryEventsWithValidationErrors_FilterBySeverity_ShouldReturnMatchingEvents()
	{
		await CreateBacktestRun();

		// Create event with Error severity validation
		var eventWithError = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "invalid"}""",
			ValidationErrors = """[{"Field": "Properties.Price", "Error": "Missing required field", "Severity": "Error"}]"""
		};
		await _repository.WriteEventAsync(eventWithError);

		// Create event with Warning severity validation
		var eventWithWarning = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "test-123", "Price": 100.50}""",
			ValidationErrors = """[{"Field": "Properties.Quantity", "Error": "Missing optional field", "Severity": "Warning"}]"""
		};
		await _repository.WriteEventAsync(eventWithWarning);

		var parameters = new ValidationErrorQueryParameters
		{
			RunId = _runId,
			SeverityFilter = "Error",
			PageSize = 100,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsWithValidationErrorsAsync(parameters);

		Assert.Single(result.Events);
		Assert.Equal(eventWithError.EventId, result.Events[0].EventId);
	}

	[Fact]
	public async Task QueryEventsWithValidationErrors_WithPagination_ShouldReturnCorrectPage()
	{
		await CreateBacktestRun();

		// Create 5 events with validation errors
		for (int i = 0; i < 5; i++)
		{
			var eventEntity = new EventEntity
			{
				EventId = Guid.NewGuid().ToString(),
				RunId = _runId,
				Timestamp = DateTime.UtcNow.AddMinutes(i),
				EventType = EventType.TradeExecution,
				Severity = EventSeverity.Info,
				Category = EventCategory.Execution,
				Properties = $$$"""{"OrderId": "order-{{{i}}}"}""",
				ValidationErrors = """[{"Field": "Properties.Price", "Error": "Missing", "Severity": "Error"}]"""
			};
			await _repository.WriteEventAsync(eventEntity);
		}

		var parameters = new ValidationErrorQueryParameters
		{
			RunId = _runId,
			PageSize = 2,
			PageIndex = 1
		};

		var result = await _repository.QueryEventsWithValidationErrorsAsync(parameters);

		Assert.Equal(2, result.Events.Count);
		Assert.Equal(5, result.Metadata.TotalCount);
		Assert.True(result.Metadata.HasMore);
	}

	[Fact]
	public async Task QueryEventsWithValidationErrors_NoErrors_ShouldReturnEmptyResult()
	{
		await CreateBacktestRun();

		// Create event without validation errors
		var eventWithoutErrors = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "test-123", "Price": 100.50}""",
			ValidationErrors = null
		};
		await _repository.WriteEventAsync(eventWithoutErrors);

		var parameters = new ValidationErrorQueryParameters
		{
			RunId = _runId,
			PageSize = 100,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsWithValidationErrorsAsync(parameters);

		Assert.Empty(result.Events);
		Assert.Equal(0, result.Metadata.TotalCount);
	}

	[Fact]
	public async Task QueryEventsWithValidationErrors_ShouldIncludeMetadata()
	{
		await CreateBacktestRun();

		var eventWithErrors = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "invalid"}""",
			ValidationErrors = """[{"Field": "Properties.Price", "Error": "Missing", "Severity": "Error"}]"""
		};
		await _repository.WriteEventAsync(eventWithErrors);

		var parameters = new ValidationErrorQueryParameters
		{
			RunId = _runId,
			PageSize = 100,
			PageIndex = 0
		};

		var result = await _repository.QueryEventsWithValidationErrorsAsync(parameters);

		Assert.NotNull(result.Metadata);
		Assert.Equal(1, result.Metadata.TotalCount);
		Assert.Equal(1, result.Metadata.ReturnedCount);
		Assert.Equal(0, result.Metadata.PageIndex);
		Assert.Equal(100, result.Metadata.PageSize);
		Assert.False(result.Metadata.HasMore);
		Assert.True(result.Metadata.QueryTimeMs >= 0);
	}

	private async Task CreateBacktestRun()
	{
		var run = new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow.AddHours(-1),
			EndTime = DateTime.UtcNow,
			StrategyConfigHash = new string('a', 64)
		};
		await _repository.CreateBacktestRunAsync(run);
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

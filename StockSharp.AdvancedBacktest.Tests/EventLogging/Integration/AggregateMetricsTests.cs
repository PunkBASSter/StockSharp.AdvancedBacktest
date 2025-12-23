using System.Globalization;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Integration;

public sealed class AggregateMetricsTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly string _runId = Guid.NewGuid().ToString();

	public AggregateMetricsTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).GetAwaiter().GetResult();
		_repository = new SqliteEventRepository(_connection);
	}

	[Fact]
	public async Task AggregateMetrics_Count_ShouldReturnCorrectCount()
	{
		await CreateBacktestRun();
		await CreateTradeEvents(10);

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.NotNull(result);
		Assert.Equal(10, result.Aggregations.Count);
	}

	[Fact]
	public async Task AggregateMetrics_Sum_ShouldReturnCorrectSum()
	{
		await CreateBacktestRun();
		// Create trades with prices: 100, 110, 120, 130, 140 = 600 total
		for (int i = 0; i < 5; i++)
		{
			await CreateTradeEvent(100 + i * 10);
		}

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["sum"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.Equal(600m, result.Aggregations.Sum);
	}

	[Fact]
	public async Task AggregateMetrics_Average_ShouldReturnCorrectAverage()
	{
		await CreateBacktestRun();
		// Create trades with prices: 100, 110, 120, 130, 140 = avg 120
		for (int i = 0; i < 5; i++)
		{
			await CreateTradeEvent(100 + i * 10);
		}

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["avg"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.Equal(120m, result.Aggregations.Avg);
	}

	[Fact]
	public async Task AggregateMetrics_MinMax_ShouldReturnCorrectValues()
	{
		await CreateBacktestRun();
		for (int i = 0; i < 5; i++)
		{
			await CreateTradeEvent(100 + i * 10);
		}

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["min", "max"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.Equal(100m, result.Aggregations.Min);
		Assert.Equal(140m, result.Aggregations.Max);
	}

	[Fact]
	public async Task AggregateMetrics_StdDev_ShouldReturnCorrectValue()
	{
		await CreateBacktestRun();
		// Create trades with prices: 10, 20, 30, 40, 50
		// Mean = 30, Variance = ((10-30)^2 + (20-30)^2 + (30-30)^2 + (40-30)^2 + (50-30)^2) / 5 = 200
		// StdDev = sqrt(200) ≈ 14.142
		for (int i = 0; i < 5; i++)
		{
			await CreateTradeEvent(10 + i * 10);
		}

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["stddev"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.NotNull(result.Aggregations.StdDev);
		Assert.True(Math.Abs(result.Aggregations.StdDev.Value - 14.142m) < 0.01m);
	}

	[Fact]
	public async Task AggregateMetrics_AllAggregations_ShouldReturnAllValues()
	{
		await CreateBacktestRun();
		for (int i = 0; i < 5; i++)
		{
			await CreateTradeEvent(100 + i * 10);
		}

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count", "sum", "avg", "min", "max", "stddev"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.Equal(5, result.Aggregations.Count);
		Assert.Equal(600m, result.Aggregations.Sum);
		Assert.Equal(120m, result.Aggregations.Avg);
		Assert.Equal(100m, result.Aggregations.Min);
		Assert.Equal(140m, result.Aggregations.Max);
		Assert.NotNull(result.Aggregations.StdDev);
	}

	[Fact]
	public async Task AggregateMetrics_WithTimeRange_ShouldFilterByTime()
	{
		await CreateBacktestRun();
		var baseTime = DateTime.UtcNow.AddHours(-1);

		for (int i = 0; i < 5; i++)
		{
			await CreateTradeEvent(100, baseTime.AddMinutes(i * 10));
		}

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count"],
			StartTime = baseTime.AddMinutes(5),
			EndTime = baseTime.AddMinutes(35)
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.Equal(3, result.Aggregations.Count); // Events at 10, 20, 30 minutes
	}

	[Fact]
	public async Task AggregateMetrics_NoMatchingEvents_ShouldReturnZeroCount()
	{
		await CreateBacktestRun();

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.Equal(0, result.Aggregations.Count);
	}

	[Fact]
	public async Task AggregateMetrics_NoMatchingEvents_ShouldReturnNullForNumerics()
	{
		await CreateBacktestRun();

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["avg", "min", "max", "sum", "stddev"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.Null(result.Aggregations.Avg);
		Assert.Null(result.Aggregations.Min);
		Assert.Null(result.Aggregations.Max);
		Assert.Null(result.Aggregations.Sum);
		Assert.Null(result.Aggregations.StdDev);
	}

	[Fact]
	public async Task AggregateMetrics_ShouldIncludeMetadata()
	{
		await CreateBacktestRun();
		await CreateTradeEvents(5);

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "$.Price",
			Aggregations = ["count"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.NotNull(result.Metadata);
		Assert.Equal(5, result.Metadata.TotalEvents);
		Assert.Equal(EventType.TradeExecution.ToString(), result.Metadata.EventType);
		Assert.Equal("$.Price", result.Metadata.PropertyPath);
		Assert.True(result.Metadata.QueryTimeMs >= 0);
	}

	[Fact]
	public async Task AggregateMetrics_InvalidPropertyPath_ShouldThrowArgumentException()
	{
		await CreateBacktestRun();

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.TradeExecution,
			PropertyPath = "SELECT * FROM Users; DROP TABLE Events;--", // SQL injection attempt
			Aggregations = ["count"]
		};

		await Assert.ThrowsAsync<ArgumentException>(() => _repository.AggregateMetricsAsync(parameters));
	}

	[Fact]
	public async Task AggregateMetrics_NestedPropertyPath_ShouldWork()
	{
		await CreateBacktestRun();
		await CreateIndicatorEvent("SMA", 50.5m);
		await CreateIndicatorEvent("SMA", 51.0m);
		await CreateIndicatorEvent("SMA", 51.5m);

		var parameters = new AggregationParameters
		{
			RunId = _runId,
			EventType = EventType.IndicatorCalculation,
			PropertyPath = "$.Value",
			Aggregations = ["avg"]
		};

		var result = await _repository.AggregateMetricsAsync(parameters);

		Assert.Equal(51.0m, result.Aggregations.Avg);
	}

	private async Task CreateBacktestRun()
	{
		var run = new BacktestRunEntity
		{
			Id = _runId,
			StartTime = DateTime.UtcNow.AddHours(-2),
			EndTime = DateTime.UtcNow,
			StrategyConfigHash = new string('a', 64)
		};
		await _repository.CreateBacktestRunAsync(run);
	}

	private async Task CreateTradeEvents(int count)
	{
		for (int i = 0; i < count; i++)
		{
			await CreateTradeEvent(100 + i);
		}
	}

	private async Task CreateTradeEvent(decimal price, DateTime? timestamp = null)
	{
		var entity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = timestamp ?? DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = $$"""{"OrderId": "{{Guid.NewGuid()}}", "Price": {{price.ToString(CultureInfo.InvariantCulture)}}, "Quantity": 10}"""
		};
		await _repository.WriteEventAsync(entity);
	}

	private async Task CreateIndicatorEvent(string name, decimal value)
	{
		var entity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = _runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.IndicatorCalculation,
			Severity = EventSeverity.Debug,
			Category = EventCategory.Analysis,
			Properties = $$"""{"IndicatorName": "{{name}}", "Value": {{value.ToString(CultureInfo.InvariantCulture)}}}"""
		};
		await _repository.WriteEventAsync(entity);
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}

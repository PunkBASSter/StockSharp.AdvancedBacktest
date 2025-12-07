using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;
using System.Text.Json;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.McpServer.Tools;

public sealed class GetStateSnapshotToolTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly GetStateSnapshotTool _tool;
	private readonly DateTime _baseTime = new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

	public GetStateSnapshotToolTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
		_tool = new GetStateSnapshotTool(_repository);
	}

	[Fact]
	public async Task GetStateSnapshotAsync_ReturnsPositionsAndIndicators()
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime);
		await CreateIndicatorEventAsync(runId, "SMA_10", "AAPL", 176.00m, _baseTime);

		var result = await _tool.GetStateSnapshotAsync(runId, _baseTime.AddHours(1).ToString("o"), null, true, false);

		Assert.Single(result.State.Positions);
		Assert.Equal("AAPL", result.State.Positions.First().SecuritySymbol);
		Assert.Single(result.State.Indicators);
		Assert.Equal("SMA_10", result.State.Indicators.First().Name);
	}

	[Theory]
	[InlineData(null, 2)]
	[InlineData("AAPL", 1)]
	[InlineData("aapl", 1)]
	[InlineData("UNKNOWN", 0)]
	public async Task GetStateSnapshotAsync_FiltersPositionsBySecuritySymbol(string? securityFilter, int expectedCount)
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime);
		await CreatePositionUpdateEventAsync(runId, "GOOGL", 50m, 140.00m, _baseTime.AddMinutes(10));

		var result = await _tool.GetStateSnapshotAsync(runId, _baseTime.AddHours(1).ToString("o"), securityFilter, false, false);

		Assert.Equal(expectedCount, result.State.Positions.Count);
	}

	[Theory]
	[InlineData(true, false, 1, 0)]
	[InlineData(false, true, 0, 1)]
	[InlineData(true, true, 1, 1)]
	[InlineData(false, false, 0, 0)]
	public async Task GetStateSnapshotAsync_RespectsIncludeFlags(bool includeIndicators, bool includeOrders, int expectedIndicators, int expectedOrders)
	{
		var runId = await CreateTestRunAsync();
		await CreateIndicatorEventAsync(runId, "SMA_10", "AAPL", 176.00m, _baseTime);
		await CreateOrderPlacedEventAsync(runId, Guid.NewGuid().ToString(), "AAPL", "Buy", 100m, 175.00m, _baseTime);

		var result = await _tool.GetStateSnapshotAsync(runId, _baseTime.AddHours(1).ToString("o"), null, includeIndicators, includeOrders);

		Assert.Equal(expectedIndicators, result.State.Indicators.Count);
		Assert.Equal(expectedOrders, result.State.ActiveOrders.Count);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task GetStateSnapshotAsync_ValidatesRunId(string invalidRunId)
	{
		await Assert.ThrowsAsync<ArgumentException>(() =>
			_tool.GetStateSnapshotAsync(invalidRunId, DateTime.UtcNow.ToString("o"), null, false, false));
	}

	[Theory]
	[InlineData("invalid-timestamp")]
	[InlineData("not-a-date")]
	[InlineData("2025-13-45")]
	public async Task GetStateSnapshotAsync_ValidatesTimestampFormat(string invalidTimestamp)
	{
		var runId = await CreateTestRunAsync();
		await Assert.ThrowsAsync<ArgumentException>(() =>
			_tool.GetStateSnapshotAsync(runId, invalidTimestamp, null, false, false));
	}

	[Fact]
	public async Task GetStateSnapshotAsync_IncludesPnLData()
	{
		var runId = await CreateTestRunAsync();
		await CreateStateChangeEventAsync(runId, "PnL", 100.00m, 250.00m, _baseTime);

		var result = await _tool.GetStateSnapshotAsync(runId, _baseTime.AddHours(1).ToString("o"), null, false, false);

		Assert.True(result.State.Pnl.Total > 0);
		Assert.True(result.Metadata.Reconstructed);
		Assert.True(result.Metadata.QueryTimeMs >= 0);
	}

	[Fact]
	public async Task GetStateSnapshotAsync_ReturnsEmptyStateForNoEvents()
	{
		var runId = await CreateTestRunAsync();

		var result = await _tool.GetStateSnapshotAsync(runId, _baseTime.ToString("o"), null, true, true);

		Assert.Empty(result.State.Positions);
		Assert.Empty(result.State.Indicators);
		Assert.Empty(result.State.ActiveOrders);
	}

	#region Helper Methods

	private async Task<string> CreateTestRunAsync()
	{
		var runId = Guid.NewGuid().ToString();
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = runId,
			StartTime = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2025, 1, 16, 0, 0, 0, DateTimeKind.Utc),
			StrategyConfigHash = new string('a', 64)
		});
		return runId;
	}

	private async Task CreatePositionUpdateEventAsync(string runId, string symbol, decimal quantity, decimal avgPrice, DateTime timestamp)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.PositionUpdate,
			Severity = EventSeverity.Info,
			Category = EventCategory.Performance,
			Properties = JsonSerializer.Serialize(new { SecuritySymbol = symbol, Quantity = quantity, AveragePrice = avgPrice, UnrealizedPnL = 0m, RealizedPnL = 0m })
		});
	}

	private async Task CreateIndicatorEventAsync(string runId, string name, string symbol, decimal value, DateTime timestamp)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.IndicatorCalculation,
			Severity = EventSeverity.Debug,
			Category = EventCategory.Indicators,
			Properties = JsonSerializer.Serialize(new { IndicatorName = name, SecuritySymbol = symbol, Value = value, Parameters = new { Period = 10 } })
		});
	}

	private async Task CreateStateChangeEventAsync(string runId, string stateType, decimal unrealizedPnl, decimal realizedPnl, DateTime timestamp)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.StateChange,
			Severity = EventSeverity.Info,
			Category = EventCategory.Performance,
			Properties = JsonSerializer.Serialize(new { StateType = stateType, StateBefore = new { UnrealizedPnL = unrealizedPnl - 50m, RealizedPnL = realizedPnl - 50m }, StateAfter = new { UnrealizedPnL = unrealizedPnl, RealizedPnL = realizedPnl } })
		});
	}

	private async Task CreateOrderPlacedEventAsync(string runId, string orderId, string symbol, string direction, decimal quantity, decimal price, DateTime timestamp)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.StateChange,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = JsonSerializer.Serialize(new { OrderId = orderId, SecuritySymbol = symbol, Direction = direction, Quantity = quantity, Price = price, OrderStatus = "Placed" })
		});
	}

	#endregion

	public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using System.Text.Json;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Integration;

public sealed class StateTrackingTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly DateTime _baseTime = new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

	public StateTrackingTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
	}

	#region State Snapshot - Position Reconstruction

	[Theory]
	[InlineData(1.5, "AAPL", 150, 176.00)]
	[InlineData(0.5, "AAPL", 100, 175.50)]
	[InlineData(2.5, "AAPL", 200, 177.00)]
	public async Task GetStateSnapshot_ReconstructsPositionAtTimestamp(double hoursOffset, string symbol, decimal expectedQty, decimal expectedPrice)
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime);
		await CreatePositionUpdateEventAsync(runId, "AAPL", 150m, 176.00m, _baseTime.AddHours(1));
		await CreatePositionUpdateEventAsync(runId, "AAPL", 200m, 177.00m, _baseTime.AddHours(2));

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime.AddHours(hoursOffset),
			IncludeIndicators = false,
			IncludeActiveOrders = false
		});

		var position = result.State.Positions.FirstOrDefault(p => p.SecuritySymbol == symbol);
		Assert.NotNull(position);
		Assert.Equal(expectedQty, position.Quantity);
		Assert.Equal(expectedPrice, position.AveragePrice);
	}

	[Theory]
	[InlineData(null, 3)]
	[InlineData("AAPL", 1)]
	[InlineData("GOOGL", 1)]
	[InlineData("UNKNOWN", 0)]
	public async Task GetStateSnapshot_FiltersPositionsBySecuritySymbol(string? securityFilter, int expectedCount)
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime);
		await CreatePositionUpdateEventAsync(runId, "GOOGL", 50m, 140.00m, _baseTime.AddMinutes(10));
		await CreatePositionUpdateEventAsync(runId, "MSFT", 75m, 380.00m, _baseTime.AddMinutes(20));

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime.AddHours(1),
			SecuritySymbol = securityFilter,
			IncludeIndicators = false,
			IncludeActiveOrders = false
		});

		Assert.Equal(expectedCount, result.State.Positions.Count);
	}

	[Theory]
	[InlineData("aapl")]
	[InlineData("AAPL")]
	[InlineData("AaPl")]
	public async Task GetStateSnapshot_SecurityFilterIsCaseInsensitive(string securityFilter)
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime);

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime.AddHours(1),
			SecuritySymbol = securityFilter,
			IncludeIndicators = false,
			IncludeActiveOrders = false
		});

		Assert.Single(result.State.Positions);
		Assert.Equal("AAPL", result.State.Positions.First().SecuritySymbol);
	}

	#endregion

	#region State Snapshot - Indicators

	[Theory]
	[InlineData(true, 2)]
	[InlineData(false, 0)]
	public async Task GetStateSnapshot_IncludesIndicatorsBasedOnFlag(bool includeIndicators, int expectedCount)
	{
		var runId = await CreateTestRunAsync();
		await CreateIndicatorCalculationEventAsync(runId, "SMA_10", "AAPL", 176.20m, _baseTime);
		await CreateIndicatorCalculationEventAsync(runId, "SMA_20", "AAPL", 174.80m, _baseTime.AddMinutes(30));

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime.AddHours(1),
			IncludeIndicators = includeIndicators,
			IncludeActiveOrders = false
		});

		Assert.Equal(expectedCount, result.State.Indicators.Count);
	}

	[Fact]
	public async Task GetStateSnapshot_ReconstructsLatestIndicatorValue()
	{
		var runId = await CreateTestRunAsync();
		await CreateIndicatorCalculationEventAsync(runId, "SMA_10", "AAPL", 175.00m, _baseTime);
		await CreateIndicatorCalculationEventAsync(runId, "SMA_10", "AAPL", 176.20m, _baseTime.AddHours(1));
		await CreateIndicatorCalculationEventAsync(runId, "SMA_10", "AAPL", 177.50m, _baseTime.AddHours(2));

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime.AddHours(1.5),
			IncludeIndicators = true,
			IncludeActiveOrders = false
		});

		var sma10 = result.State.Indicators.First(i => i.Name == "SMA_10");
		Assert.Equal(176.20m, sma10.Value);
	}

	#endregion

	#region State Snapshot - PnL and Active Orders

	[Fact]
	public async Task GetStateSnapshot_ReconstructsPnLFromStateChanges()
	{
		var runId = await CreateTestRunAsync();
		await CreateStateChangeEventAsync(runId, "PnL", 100.00m, 250.00m, _baseTime);
		await CreateStateChangeEventAsync(runId, "PnL", 300.00m, 500.00m, _baseTime.AddHours(1));

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime.AddHours(1.5),
			IncludeIndicators = false,
			IncludeActiveOrders = false
		});

		Assert.Equal(500.00m, result.State.Pnl.Realized);
		Assert.Equal(300.00m, result.State.Pnl.Unrealized);
		Assert.Equal(800.00m, result.State.Pnl.Total);
	}

	[Fact]
	public async Task GetStateSnapshot_TracksActiveOrdersNotYetExecuted()
	{
		var runId = await CreateTestRunAsync();
		var order1Id = Guid.NewGuid().ToString();
		var order2Id = Guid.NewGuid().ToString();

		await CreateOrderPlacedEventAsync(runId, order1Id, "AAPL", "Buy", 100m, 175.00m, _baseTime);
		await CreateOrderPlacedEventAsync(runId, order2Id, "GOOGL", "Sell", 50m, 140.00m, _baseTime.AddMinutes(30));
		await CreateTradeExecutionEventAsync(runId, order1Id, "AAPL", "Buy", 100m, 175.50m, _baseTime.AddHours(1));

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime.AddHours(1.5),
			IncludeIndicators = false,
			IncludeActiveOrders = true
		});

		Assert.Single(result.State.ActiveOrders);
		Assert.Equal(order2Id, result.State.ActiveOrders.First().OrderId);
		Assert.Equal("GOOGL", result.State.ActiveOrders.First().SecuritySymbol);
	}

	[Fact]
	public async Task GetStateSnapshot_ReturnsEmptyStateBeforeAnyEvents()
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime.AddHours(1));

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime,
			IncludeIndicators = true,
			IncludeActiveOrders = true
		});

		Assert.Empty(result.State.Positions);
		Assert.Empty(result.State.Indicators);
		Assert.Empty(result.State.ActiveOrders);
		Assert.Equal(0m, result.State.Pnl.Total);
	}

	#endregion

	#region State Delta - Position Changes

	[Theory]
	[InlineData("AAPL", 0, 150, 150)]
	[InlineData("GOOGL", 0, 50, 50)]
	public async Task GetStateDelta_CalculatesPositionChanges(string symbol, decimal expectedBefore, decimal expectedAfter, decimal expectedChange)
	{
		var runId = await CreateTestRunAsync();
		var t1 = _baseTime;
		var t2 = _baseTime.AddHours(2);

		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, t1.AddMinutes(30));
		await CreatePositionUpdateEventAsync(runId, "AAPL", 150m, 176.00m, t1.AddHours(1));
		await CreatePositionUpdateEventAsync(runId, "GOOGL", 50m, 140.00m, t1.AddMinutes(45));

		var result = await _repository.GetStateDeltaAsync(new StateDeltaQueryParameters
		{
			RunId = runId,
			StartTimestamp = t1,
			EndTimestamp = t2
		});

		var change = result.PositionChanges.First(p => p.SecuritySymbol == symbol);
		Assert.Equal(expectedBefore, change.QuantityBefore);
		Assert.Equal(expectedAfter, change.QuantityAfter);
		Assert.Equal(expectedChange, change.QuantityChange);
	}

	[Theory]
	[InlineData(100, 0, -100)]
	[InlineData(0, 75, 75)]
	public async Task GetStateDelta_HandlesPositionOpenAndClose(decimal initialQty, decimal finalQty, decimal expectedChange)
	{
		var runId = await CreateTestRunAsync();
		var t1 = _baseTime;
		var t2 = _baseTime.AddHours(2);

		if (initialQty > 0)
			await CreatePositionUpdateEventAsync(runId, "AAPL", initialQty, 175.50m, t1.AddMinutes(-30));
		if (finalQty > 0)
			await CreatePositionUpdateEventAsync(runId, "AAPL", finalQty, 176.00m, t1.AddHours(1));
		else if (initialQty > 0)
			await CreatePositionUpdateEventAsync(runId, "AAPL", 0m, 0m, t1.AddHours(1));

		var result = await _repository.GetStateDeltaAsync(new StateDeltaQueryParameters
		{
			RunId = runId,
			StartTimestamp = t1,
			EndTimestamp = t2
		});

		if (expectedChange != 0)
		{
			var change = result.PositionChanges.First(p => p.SecuritySymbol == "AAPL");
			Assert.Equal(expectedChange, change.QuantityChange);
		}
	}

	[Fact]
	public async Task GetStateDelta_ReturnsEmptyWhenNoChanges()
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime.AddHours(-1));

		var result = await _repository.GetStateDeltaAsync(new StateDeltaQueryParameters
		{
			RunId = runId,
			StartTimestamp = _baseTime,
			EndTimestamp = _baseTime.AddHours(2)
		});

		Assert.Empty(result.PositionChanges);
		Assert.Empty(result.IndicatorChanges);
	}

	#endregion

	#region State Delta - PnL and Indicators

	[Fact]
	public async Task GetStateDelta_CalculatesPnLChanges()
	{
		var runId = await CreateTestRunAsync();
		var t1 = _baseTime;
		var t2 = _baseTime.AddHours(2);

		await CreateStateChangeEventAsync(runId, "PnL", 100.00m, 200.00m, t1.AddMinutes(30));
		await CreateStateChangeEventAsync(runId, "PnL", 300.00m, 500.00m, t1.AddHours(1));

		var result = await _repository.GetStateDeltaAsync(new StateDeltaQueryParameters
		{
			RunId = runId,
			StartTimestamp = t1,
			EndTimestamp = t2
		});

		Assert.Equal(500m, result.PnlChange!.RealizedAfter);
		Assert.Equal(300m, result.PnlChange.UnrealizedAfter);
		Assert.Equal(500m, result.PnlChange.RealizedChange);
	}

	[Fact]
	public async Task GetStateDelta_CalculatesIndicatorChanges()
	{
		var runId = await CreateTestRunAsync();
		var t1 = _baseTime;
		var t2 = _baseTime.AddHours(2);

		await CreateIndicatorCalculationEventAsync(runId, "SMA_10", "AAPL", 175.00m, t1.AddMinutes(30));
		await CreateIndicatorCalculationEventAsync(runId, "SMA_10", "AAPL", 177.50m, t2.AddMinutes(-30));

		var result = await _repository.GetStateDeltaAsync(new StateDeltaQueryParameters
		{
			RunId = runId,
			StartTimestamp = t1,
			EndTimestamp = t2
		});

		var smaChange = result.IndicatorChanges.First(i => i.Name == "SMA_10");
		Assert.Equal(177.50m, smaChange.ValueAfter);
	}

	[Theory]
	[InlineData(null, 2)]
	[InlineData("AAPL", 1)]
	public async Task GetStateDelta_FiltersChangesBySecuritySymbol(string? securityFilter, int expectedPositionChanges)
	{
		var runId = await CreateTestRunAsync();
		var t1 = _baseTime;
		var t2 = _baseTime.AddHours(2);

		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, t1.AddMinutes(30));
		await CreatePositionUpdateEventAsync(runId, "GOOGL", 50m, 140.00m, t1.AddHours(1));

		var result = await _repository.GetStateDeltaAsync(new StateDeltaQueryParameters
		{
			RunId = runId,
			StartTimestamp = t1,
			EndTimestamp = t2,
			SecuritySymbol = securityFilter
		});

		Assert.Equal(expectedPositionChanges, result.PositionChanges.Count);
	}

	#endregion

	#region Metadata

	[Fact]
	public async Task GetStateSnapshot_IncludesMetadata()
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime);

		var result = await _repository.GetStateSnapshotAsync(new StateSnapshotQueryParameters
		{
			RunId = runId,
			Timestamp = _baseTime.AddHours(1),
			IncludeIndicators = false,
			IncludeActiveOrders = false
		});

		Assert.True(result.Metadata.QueryTimeMs >= 0);
		Assert.True(result.Metadata.Reconstructed);
		Assert.Equal(runId, result.RunId);
	}

	[Fact]
	public async Task GetStateDelta_IncludesMetadata()
	{
		var runId = await CreateTestRunAsync();
		await CreatePositionUpdateEventAsync(runId, "AAPL", 100m, 175.50m, _baseTime.AddHours(1));

		var result = await _repository.GetStateDeltaAsync(new StateDeltaQueryParameters
		{
			RunId = runId,
			StartTimestamp = _baseTime,
			EndTimestamp = _baseTime.AddHours(2)
		});

		Assert.True(result.Metadata.QueryTimeMs >= 0);
		Assert.Equal(_baseTime, result.StartTimestamp);
		Assert.Equal(_baseTime.AddHours(2), result.EndTimestamp);
	}

	#endregion

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

	private async Task CreateIndicatorCalculationEventAsync(string runId, string indicatorName, string symbol, decimal value, DateTime timestamp)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.IndicatorCalculation,
			Severity = EventSeverity.Debug,
			Category = EventCategory.Indicators,
			Properties = JsonSerializer.Serialize(new { IndicatorName = indicatorName, SecuritySymbol = symbol, Value = value, Parameters = new { Period = 10 } })
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

	private async Task CreateTradeExecutionEventAsync(string runId, string orderId, string symbol, string direction, decimal quantity, decimal price, DateTime timestamp)
	{
		await _repository.WriteEventAsync(new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = timestamp,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = JsonSerializer.Serialize(new { OrderId = orderId, SecuritySymbol = symbol, Direction = direction, Quantity = quantity, Price = price, Commission = 1.00m, Slippage = 0.05m })
		});
	}

	#endregion

	public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

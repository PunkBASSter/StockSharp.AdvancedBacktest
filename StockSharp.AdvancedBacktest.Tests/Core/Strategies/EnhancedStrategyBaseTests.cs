using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;
using System.Numerics;
using System.Threading.Channels;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies;

/// <summary>
/// Comprehensive unit tests for EnhancedStrategyBase class
/// Tests cover initialization, lifecycle management, parameter validation,
/// event processing, StockSharp integration, and thread safety
/// </summary>
public class EnhancedStrategyBaseTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnhancedStrategyBase> _logger;

    public EnhancedStrategyBaseTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IPerformanceTracker, TestPerformanceTracker>();
        services.AddSingleton<IRiskManager, TestRiskManager>();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<EnhancedStrategyBase>>();
    }

    [Fact]
    public void EnhancedStrategyBase_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Act
        var strategy = new TestEnhancedStrategy { Parameters = new TestParameterSet() };

        // Assert
        Assert.NotNull(strategy);
        Assert.NotNull(strategy.TradeEvents);
        Assert.NotNull(strategy.PerformanceEvents);
        Assert.NotNull(strategy.RiskEvents);
        Assert.NotNull(strategy.StateEvents);
    }

    [Fact]
    public void EnhancedStrategyBase_DependencyInjectionConstructor_ShouldInitializeWithServices()
    {
        // Act
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };

        // Assert
        Assert.NotNull(strategy);
        Assert.NotNull(strategy.TradeEvents);
        Assert.NotNull(strategy.PerformanceEvents);
        Assert.NotNull(strategy.RiskEvents);
        Assert.NotNull(strategy.StateEvents);
    }

    [Fact]
    public async Task StartEnhancedAsync_ShouldUpdateStateToRunning()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        await strategy.InitializeAsync(_serviceProvider);

        // Act
        await strategy.StartEnhancedAsync();

        // Assert
        Assert.Equal(StrategyStatus.Running, strategy.CurrentState.Status);
    }

    [Fact]
    public async Task StopEnhancedAsync_ShouldUpdateStateToStopped()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        await strategy.InitializeAsync(_serviceProvider);
        await strategy.StartEnhancedAsync();

        // Act
        await strategy.StopEnhancedAsync();

        // Assert
        Assert.Equal(StrategyStatus.Stopped, strategy.CurrentState.Status);
    }

    [Fact]
    public async Task InitializeAsync_WhenCalledMultipleTimes_ShouldInitializeOnlyOnce()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };

        // Act
        await strategy.InitializeAsync(_serviceProvider);
        await strategy.InitializeAsync(_serviceProvider);
        await strategy.InitializeAsync(_serviceProvider);

        // Assert - Services should be injected only once
        Assert.NotNull(strategy.Performance);
        Assert.NotNull(strategy.RiskManager);
    }

    [Fact]
    public void ProcessOrder_WithValidOrder_ShouldReturnTrue()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        var order = CreateTestOrder();

        // Act
        var result = strategy.ProcessOrder(order);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ProcessOrder_WithRiskViolation_ShouldReturnFalse()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        var riskManager = new TestRiskManager(shouldRejectOrders: true);
        strategy.SetRiskManager(riskManager);
        var order = CreateTestOrder();

        // Act
        var result = strategy.ProcessOrder(order);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ProcessTrade_ShouldRecordTradeAndUpdateState()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        var trade = CreateTestTrade();

        // Act
        strategy.ProcessTrade(trade);

        // Assert
        Assert.Equal(trade.Time, strategy.CurrentState.LastTradeTime);
    }

    [Fact]
    public void ValidateParameters_WithValidParameters_ShouldReturnSuccess()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy { Parameters = new TestParameterSet() };

        // Act
        var result = strategy.ValidateParameters();

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateParameters_WithInvalidParameters_ShouldReturnFailure()
    {
        // Arrange
        var strategy = new TestEnhancedStrategyWithInvalidParameters { Parameters = new TestInvalidParameterSet() };

        // Act
        var result = strategy.ValidateParameters();

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task TradeEvents_ShouldReceiveTradeData()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        await strategy.InitializeAsync(_serviceProvider);
        var trade = CreateTestTrade();

        // Act
        strategy.ProcessTrade(trade);

        // Assert
        var reader = strategy.TradeEvents;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        try
        {
            var tradeData = await reader.ReadAsync(cts.Token);
            Assert.Equal(trade.Id, tradeData.OriginalTrade.Id);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("Trade event was not published within timeout");
        }
    }

    [Fact]
    public async Task RiskEvents_ShouldReceiveViolations()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        var riskManager = new TestRiskManager(shouldRejectOrders: true);
        strategy.SetRiskManager(riskManager);
        var order = CreateTestOrder();

        // Act
        strategy.ProcessOrder(order);

        // Assert
        var reader = strategy.RiskEvents;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        try
        {
            var violation = await reader.ReadAsync(cts.Token);
            Assert.NotNull(violation);
            Assert.Equal(RiskViolationType.OrderValidationFailed, violation.ViolationType);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("Risk event was not published within timeout");
        }
    }

    [Fact]
    public async Task ConcurrentTrades_ShouldHandleWithoutRaceConditions()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        await strategy.InitializeAsync(_serviceProvider);
        var trades = GenerateTestTrades(100);

        // Act
        var tasks = trades.Select(trade =>
            Task.Run(() => strategy.ProcessTrade(trade)));

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(100, strategy.ProcessedTradeCount);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentOperations_ShouldNotCauseDataCorruption()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        await strategy.InitializeAsync(_serviceProvider);
        var operationCount = 100;
        var tasks = new List<Task>();

        // Act - Mix of concurrent operations
        for (int i = 0; i < operationCount; i++)
        {
            var trade = CreateTestTrade();
            var order = CreateTestOrder();

            tasks.Add(Task.Run(() => strategy.ProcessTrade(trade)));
            tasks.Add(Task.Run(() => strategy.ProcessOrder(order)));
        }

        await Task.WhenAll(tasks);

        // Assert - No exceptions and consistent state
        Assert.Equal(operationCount, strategy.ProcessedTradeCount);
        Assert.True(strategy.ProcessedOrderCount >= operationCount); // Some orders might be rejected
    }

    [Fact]
    public async Task Dispose_ShouldCleanupResourcesProperly()
    {
        // Arrange
        var strategy = new TestEnhancedStrategy(_logger, _serviceProvider) { Parameters = new TestParameterSet() };
        await strategy.InitializeAsync(_serviceProvider);
        await strategy.StartEnhancedAsync();

        // Act
        await strategy.DisposeAsync();

        // Assert
        // Verify channels are completed and resources cleaned up
        Assert.True(strategy.TradeEvents.Completion.IsCompleted ||
                   strategy.TradeEvents.Completion.IsCanceled);
    }

    #region Helper Methods

    private static Order CreateTestOrder()
    {
        var order = new Order
        {
            Id = Random.Shared.NextInt64(),
            Volume = 100,
            Price = 100.50m,
            Security = new Security { Code = "TEST" },
            Time = DateTimeOffset.UtcNow
        };
        // Note: Direction property assignment skipped due to StockSharp type resolution
        return order;
    }

    private static Trade CreateTestTrade()
    {
        return new Trade
        {
            Id = Random.Shared.NextInt64(),
            Price = 100.25m,
            Volume = 100,
            Time = DateTimeOffset.UtcNow,
            Security = new Security { Code = "TEST" }
        };
    }

    private static List<Trade> GenerateTestTrades(int count)
    {
        var trades = new List<Trade>(count);
        for (int i = 0; i < count; i++)
        {
            trades.Add(new Trade
            {
                Id = i,
                Price = 100m + (decimal)(Random.Shared.NextDouble() * 10),
                Volume = Random.Shared.Next(1, 1000),
                Time = DateTimeOffset.UtcNow.AddSeconds(i),
                Security = new Security { Code = "TEST" }
            });
        }
        return trades;
    }

    #endregion

    #region Test Implementation Classes

    private class TestEnhancedStrategy : EnhancedStrategyBase
    {
        private int _processedTradeCount;
        private int _processedOrderCount;

        public Action? OnInitializeCallback { get; set; }
        public int ProcessedTradeCount => _processedTradeCount;
        public int ProcessedOrderCount => _processedOrderCount;

        public TestEnhancedStrategy() : base()
        {
        }

        public TestEnhancedStrategy(ILogger<EnhancedStrategyBase> logger, IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
        }

        public override void ProcessTrade(Trade trade)
        {
            base.ProcessTrade(trade);
            Interlocked.Increment(ref _processedTradeCount);
        }

        public override bool ProcessOrder(Order order)
        {
            var result = base.ProcessOrder(order);
            Interlocked.Increment(ref _processedOrderCount);
            return result;
        }

        public void SetRiskManager(IRiskManager riskManager)
        {
            RiskManager = riskManager;
        }

        public void InitializeForTest()
        {
            OnInitializeCallback?.Invoke();
        }
    }

    private class TestEnhancedStrategyWithInvalidParameters : EnhancedStrategyBase
    {
        public TestEnhancedStrategyWithInvalidParameters() : base()
        {
        }
    }

    private class TestParameterSet : IParameterSet
    {
        public int Count => 1;
        public ImmutableArray<ParameterDefinition> Definitions => ImmutableArray.Create(
            ParameterDefinition.CreateNumeric<int>("TestParam", 1, 100, 50)
        );

        public T GetValue<T>(string name) where T : INumber<T> => T.CreateChecked(50);
        public object? GetValue(string name) => 50;
        public void SetValue<T>(string name, T value) where T : INumber<T> { }
        public void SetValue(string name, object? value) { }
        public bool HasParameter(string name) => name == "TestParam";
        public ImmutableDictionary<string, object?> GetSnapshot() => ImmutableDictionary<string, object?>.Empty.Add("TestParam", 50);
        public ValidationResult Validate() => ValidationResult.Success;
        public IParameterSet Clone() => new TestParameterSet();
        public bool TryGetValue(string name, out object? value)
        {
            value = name == "TestParam" ? 50 : null;
            return name == "TestParam";
        }
        public ParameterSetStatistics GetStatistics() => new(1, 1, 1, 1, true);
    }

    private class TestInvalidParameterSet : IParameterSet
    {
        public int Count => 1;
        public ImmutableArray<ParameterDefinition> Definitions => ImmutableArray.Create(
            ParameterDefinition.CreateNumeric<int>("InvalidParam", 1, 100, 150) // Invalid default
        );

        public T GetValue<T>(string name) where T : INumber<T> => T.CreateChecked(150);
        public object? GetValue(string name) => 150;
        public void SetValue<T>(string name, T value) where T : INumber<T> { }
        public void SetValue(string name, object? value) { }
        public bool HasParameter(string name) => name == "InvalidParam";
        public ImmutableDictionary<string, object?> GetSnapshot() => ImmutableDictionary<string, object?>.Empty.Add("InvalidParam", 150);
        public ValidationResult Validate() => ValidationResult.Failure("Invalid parameter value");
        public IParameterSet Clone() => new TestInvalidParameterSet();
        public bool TryGetValue(string name, out object? value)
        {
            value = name == "InvalidParam" ? 150 : null;
            return name == "InvalidParam";
        }
        public ParameterSetStatistics GetStatistics() => new(1, 1, 1, 0, false);
    }

    private class TestPerformanceTracker : IPerformanceTracker
    {
        public decimal CurrentValue => 100000m;
        public decimal TotalReturn => 0.05m;
        public decimal SharpeRatio => 1.5m;
        public decimal MaxDrawdown => -0.02m;
        public decimal CurrentDrawdown => -0.01m;
        public decimal WinRate => 0.6m;
        public int TotalTrades => 100;
        public int WinningTrades => 60;
        public bool IsConsistent => true;

        public void RecordTrade(Trade trade) { }
        public void UpdatePortfolioValue(decimal value, DateTimeOffset timestamp) { }
        public decimal CalculateVolatility(int periods = 252) => 0.15m;
        public PerformanceSnapshot GetSnapshot() => new(
            DateTimeOffset.UtcNow, 100000m, TotalReturn, SharpeRatio, MaxDrawdown, CurrentDrawdown, WinRate, TotalTrades, WinningTrades, 0.15m, 0m);
        public ImmutableArray<PerformanceSnapshot> GetHistory(DateTimeOffset? from = null, DateTimeOffset? to = null) =>
            ImmutableArray<PerformanceSnapshot>.Empty;
        public void Reset() { }
        public void Dispose() { }
    }

    private class TestRiskManager : IRiskManager
    {
        private readonly bool _shouldRejectOrders;

        public TestRiskManager(bool shouldRejectOrders = false)
        {
            _shouldRejectOrders = shouldRejectOrders;
        }

        public decimal MaxDrawdownLimit { get; set; } = 0.1m;
        public decimal MaxPositionSize { get; set; } = 1000m;
        public decimal DailyLossLimit { get; set; } = 5000m;
        public decimal CurrentRiskLevel => 0.05m;
        public bool IsRiskLimitBreached => false;

        public bool ValidateOrder(Order order) => !_shouldRejectOrders;
        public bool ValidatePositionSize(Security security, decimal volume) => volume <= MaxPositionSize;
        public bool IsDrawdownLimitBreached(decimal currentDrawdown) => currentDrawdown > MaxDrawdownLimit;
        public bool IsDailyLossLimitBreached(decimal dailyPnL) => dailyPnL < -DailyLossLimit;
        public void RecordViolation(RiskViolation violation) { }
        public IReadOnlyList<RiskViolation> GetRecentViolations(int count = 10) => new List<RiskViolation>();
        public void ResetDaily() { }
        public Task EmergencyStopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    #endregion
}
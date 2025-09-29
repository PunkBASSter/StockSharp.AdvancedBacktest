using Microsoft.Extensions.DependencyInjection;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies.Interfaces;

/// <summary>
/// Tests for IEnhancedStrategy interface contract and implementations
/// </summary>
public class IEnhancedStrategyTests
{
    [Fact]
    public void IEnhancedStrategy_Contract_ShouldDefineRequiredProperties()
    {
        // Arrange & Act
        var interfaceType = typeof(IEnhancedStrategy);

        // Assert - Verify required properties exist
        Assert.NotNull(interfaceType.GetProperty("Parameters"));
        Assert.NotNull(interfaceType.GetProperty("Performance"));
        Assert.NotNull(interfaceType.GetProperty("RiskManager"));
        Assert.NotNull(interfaceType.GetProperty("TradeEvents"));
        Assert.NotNull(interfaceType.GetProperty("PerformanceEvents"));
        Assert.NotNull(interfaceType.GetProperty("CurrentState"));

        // Verify property types
        Assert.Equal(typeof(IParameterSet), interfaceType.GetProperty("Parameters")?.PropertyType);
        Assert.Equal(typeof(IPerformanceTracker), interfaceType.GetProperty("Performance")?.PropertyType);
        Assert.Equal(typeof(IRiskManager), interfaceType.GetProperty("RiskManager")?.PropertyType);
        Assert.Equal(typeof(ChannelReader<TradeExecutionData>), interfaceType.GetProperty("TradeEvents")?.PropertyType);
        Assert.Equal(typeof(ChannelReader<PerformanceSnapshot>), interfaceType.GetProperty("PerformanceEvents")?.PropertyType);
        Assert.Equal(typeof(StrategyState), interfaceType.GetProperty("CurrentState")?.PropertyType);
    }

    [Fact]
    public void IEnhancedStrategy_Contract_ShouldDefineRequiredMethods()
    {
        // Arrange & Act
        var interfaceType = typeof(IEnhancedStrategy);

        // Assert - Verify required methods exist
        var initializeMethod = interfaceType.GetMethod("InitializeAsync");
        var validateMethod = interfaceType.GetMethod("ValidateParameters");

        Assert.NotNull(initializeMethod);
        Assert.NotNull(validateMethod);

        // Verify method signatures
        Assert.Equal(typeof(Task), initializeMethod?.ReturnType);
        Assert.Equal(typeof(ValidationResult), validateMethod?.ReturnType);
        Assert.Single(initializeMethod?.GetParameters() ?? []);
        Assert.Equal(typeof(IServiceProvider), initializeMethod?.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void IEnhancedStrategy_ShouldInheritFromIDisposable()
    {
        // Arrange & Act
        var interfaceType = typeof(IEnhancedStrategy);

        // Assert
        Assert.True(typeof(IDisposable).IsAssignableFrom(interfaceType));
    }

    /// <summary>
    /// Mock implementation for testing interface contracts
    /// </summary>
    private class MockEnhancedStrategy : IEnhancedStrategy
    {
        public IParameterSet Parameters { get; }
        public IPerformanceTracker? Performance { get; }
        public IRiskManager? RiskManager { get; }
        public ChannelReader<TradeExecutionData> TradeEvents { get; }
        public ChannelReader<PerformanceSnapshot> PerformanceEvents { get; }
        public StrategyState CurrentState { get; }

        private readonly Channel<TradeExecutionData> _tradeChannel;
        private readonly Channel<PerformanceSnapshot> _performanceChannel;

        public MockEnhancedStrategy()
        {
            // Create minimal mock implementations
            Parameters = new MockParameterSet();
            CurrentState = StrategyState.Initial;

            _tradeChannel = Channel.CreateUnbounded<TradeExecutionData>();
            _performanceChannel = Channel.CreateUnbounded<PerformanceSnapshot>();
            TradeEvents = _tradeChannel.Reader;
            PerformanceEvents = _performanceChannel.Reader;
        }

        public Task InitializeAsync(IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
        }

        public ValidationResult ValidateParameters()
        {
            return ValidationResult.CreateSuccess();
        }

        public void Dispose()
        {
            _tradeChannel.Writer.Complete();
            _performanceChannel.Writer.Complete();
        }
    }

    /// <summary>
    /// Mock parameter set for testing
    /// </summary>
    private class MockParameterSet : IParameterSet
    {
        public int Count => 0;
        public ImmutableArray<ParameterDefinition> Definitions => ImmutableArray<ParameterDefinition>.Empty;

        public IParameterSet Clone() => new MockParameterSet();
        public ImmutableDictionary<string, object?> GetSnapshot() => ImmutableDictionary<string, object?>.Empty;
        public ParameterSetStatistics GetStatistics() => new(0, 0, 0, 0, true);
        public object? GetValue(string name) => null;
        public T GetValue<T>(string name) where T : struct, IComparable<T>, System.Numerics.INumber<T> => default(T);
        public bool HasParameter(string name) => false;
        public void SetValue(string name, object? value) { }
        public void SetValue<T>(string name, T value) where T : struct, IComparable<T>, System.Numerics.INumber<T> { }
        public bool TryGetValue(string name, out object? value) { value = null; return false; }
        public ValidationResult Validate() => ValidationResult.CreateSuccess();
    }

    [Fact]
    public void MockEnhancedStrategy_ShouldImplementAllInterfaceMembers()
    {
        // Arrange & Act
        using var strategy = new MockEnhancedStrategy();

        // Assert - Verify all interface members are implemented
        Assert.NotNull(strategy.Parameters);
        Assert.NotNull(strategy.TradeEvents);
        Assert.NotNull(strategy.PerformanceEvents);
        Assert.NotNull(strategy.CurrentState);
    }

    [Fact]
    public async Task MockEnhancedStrategy_InitializeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        using var strategy = new MockEnhancedStrategy();
        var serviceCollection = new ServiceCollection();
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        // Act & Assert
        await strategy.InitializeAsync(serviceProvider);
    }

    [Fact]
    public void MockEnhancedStrategy_ValidateParameters_ShouldReturnSuccess()
    {
        // Arrange
        using var strategy = new MockEnhancedStrategy();

        // Act
        var result = strategy.ValidateParameters();

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }
}
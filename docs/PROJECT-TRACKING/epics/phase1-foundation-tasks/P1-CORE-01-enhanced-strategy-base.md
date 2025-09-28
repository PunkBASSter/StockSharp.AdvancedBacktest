# Task: P1-CORE-01 - Create Enhanced Strategy Base Classes

**Epic**: Phase1-Foundation
**Priority**: HIGH-01
**Agent**: dotnet-csharp-expert
**Status**: READY
**Dependencies**: None

## Overview

Implement EnhancedStrategyBase class that extends StockSharp.Algo.Strategies.Strategy while maintaining full compatibility with the StockSharp ecosystem. This serves as the foundation for all advanced backtesting capabilities.

## Technical Requirements

### .NET 10 & C# 14 Implementation Strategy

1. **EnhancedStrategyBase Class - Modern .NET Patterns**
   - Inherit from `StockSharp.Algo.Strategies.Strategy`
   - Maintain 100% compatibility with StockSharp Strategy pattern
   - Leverage C# 14 features: required members, raw string literals, generic math
   - Implement nullable reference types throughout
   - Use record types for immutable data structures
   - Apply dependency injection patterns for testability

2. **Core Architecture - Performance-First Design**
   ```csharp
   // Primary enhanced strategy base using modern C# patterns
   public abstract class EnhancedStrategyBase : Strategy, IDisposable
   {
       // Required members pattern (C# 11+)
       public required ParameterSet Parameters { get; init; }

       // Nullable reference types enabled
       public IPerformanceTracker? Performance { get; private set; }
       public IRiskManager? RiskManager { get; protected set; }

       // Dependency injection support
       protected readonly ILogger<EnhancedStrategyBase> _logger;
       protected readonly IServiceProvider _serviceProvider;

       // High-performance event handling with Channels
       private readonly Channel<TradeExecutionData> _tradeChannel;
       private readonly Channel<PerformanceSnapshot> _performanceChannel;

       // Modern async patterns
       protected EnhancedStrategyBase(
           ILogger<EnhancedStrategyBase> logger,
           IServiceProvider serviceProvider)
       {
           _logger = logger ?? throw new ArgumentNullException(nameof(logger));
           _serviceProvider = serviceProvider;
           _tradeChannel = Channel.CreateUnbounded<TradeExecutionData>();
           _performanceChannel = Channel.CreateUnbounded<PerformanceSnapshot>();
       }
   }
   ```

3. **StockSharp Integration Patterns**
   ```csharp
   // Specific StockSharp lifecycle integration
   protected override void OnStarted(DateTimeOffset time)
   {
       base.OnStarted(time);

       // Initialize enhanced features after StockSharp initialization
       InitializePerformanceTracking();
       InitializeRiskManagement();

       _logger.LogInformation("Enhanced strategy {StrategyName} started at {Time}",
           Name, time);
   }

   // Override key StockSharp methods with enhanced functionality
   protected override void OnNewTrade(Trade trade)
   {
       base.OnNewTrade(trade);

       // Capture enhanced trade data
       var enhancedTradeData = new TradeExecutionData
       {
           OriginalTrade = trade,
           StrategyParameters = Parameters.GetSnapshot(),
           Timestamp = trade.Time,
           PortfolioSnapshot = GetCurrentPortfolioSnapshot()
       };

       // Non-blocking event publishing
       _ = _tradeChannel.Writer.TryWrite(enhancedTradeData);
   }
   ```

### File Structure - Organized by Responsibility

Create in `StockSharp.AdvancedBacktest/Core/Strategies/`:
- `EnhancedStrategyBase.cs` - Main strategy base class with modern patterns
- `IEnhancedStrategy.cs` - Interface for enhanced strategy contract
- `StrategyExtensions.cs` - Extension methods for StockSharp integration
- `ServiceCollectionExtensions.cs` - DI registration helpers

Create in `StockSharp.AdvancedBacktest/Core/Strategies/Models/`:
- `TradeExecutionData.cs` - Record type for trade execution events
- `PerformanceSnapshot.cs` - Record type for performance snapshots
- `RiskViolation.cs` - Record type for risk violation events
- `StrategyState.cs` - Record type for strategy state management

Create in `StockSharp.AdvancedBacktest/Core/Strategies/Interfaces/`:
- `IParameterValidator.cs` - Interface for parameter validation
- `IPerformanceTracker.cs` - Interface for performance tracking
- `IRiskManager.cs` - Interface for risk management
- `IStrategyEventHandler.cs` - Interface for event handling patterns

## Implementation Details - Modern .NET Architecture

### 1. Enhanced Parameter Management (C# 14 Patterns)

```csharp
// Record types for immutable parameter definitions
public record ParameterDefinition(
    string Name,
    Type Type,
    object? MinValue = null,
    object? MaxValue = null,
    object? DefaultValue = null
);

// Generic constraints with mathematical operations (C# 11+)
public class TypedParameter<T> where T : INumber<T>
{
    public required string Name { get; init; }
    public required T Value { get; init; }
    public T? MinValue { get; init; }
    public T? MaxValue { get; init; }

    // Generic math operations
    public bool IsValid() => MinValue is null || MaxValue is null ||
        (Value >= MinValue && Value <= MaxValue);
}
```

### 2. High-Performance Event System (System.Threading.Channels)

```csharp
// Modern async event handling avoiding traditional event memory leaks
public class StrategyEventSystem : IAsyncDisposable
{
    private readonly Channel<TradeExecutionData> _tradeEvents;
    private readonly Channel<PerformanceSnapshot> _performanceEvents;
    private readonly CancellationTokenSource _cancellationSource;

    public ChannelReader<TradeExecutionData> TradeEvents => _tradeEvents.Reader;
    public ChannelReader<PerformanceSnapshot> PerformanceEvents => _performanceEvents.Reader;

    // Background processing with proper cancellation
    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var tradeEvent in _tradeEvents.Reader.ReadAllAsync(cancellationToken))
        {
            await ProcessTradeEventAsync(tradeEvent, cancellationToken);
        }
    }
}
```

### 3. StockSharp Lifecycle Integration

```csharp
// Specific patterns for StockSharp compatibility
protected override void OnStarted(DateTimeOffset time)
{
    try
    {
        base.OnStarted(time); // CRITICAL: Always call base first

        // Initialize enhanced features only after StockSharp initialization
        InitializeEnhancedFeatures();

        _logger.LogInformation("Enhanced strategy {Name} initialized with {ParameterCount} parameters",
            Name, Parameters.Count);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to start enhanced strategy {Name}", Name);
        throw; // Re-throw to maintain StockSharp error handling
    }
}

// Proper StockSharp order handling with enhancements
protected override void RegisterOrder(Order order)
{
    // Pre-order risk validation
    if (RiskManager?.ValidateOrder(order) == false)
    {
        _logger.LogWarning("Order rejected by risk manager: {OrderId}", order.Id);
        return;
    }

    base.RegisterOrder(order); // CRITICAL: Call base to maintain StockSharp functionality

    // Post-order tracking
    TrackOrderSubmission(order);
}
```

### 4. Memory-Efficient Performance Tracking

```csharp
// Span<T> and Memory<T> for high-performance calculations
public class PerformanceTracker : IPerformanceTracker
{
    private readonly CircularBuffer<decimal> _returns;
    private readonly CircularBuffer<decimal> _portfolioValues;

    // Use Span<T> for zero-allocation calculations
    public decimal CalculateVolatility(int periods = 252)
    {
        Span<decimal> returns = stackalloc decimal[Math.Min(periods, _returns.Count)];
        _returns.CopyTo(returns);

        return CalculateStandardDeviation(returns);
    }

    // Vectorized operations where possible
    private static decimal CalculateStandardDeviation(ReadOnlySpan<decimal> values)
    {
        if (values.Length < 2) return 0m;

        decimal mean = 0m;
        foreach (var value in values)
        {
            mean += value;
        }
        mean /= values.Length;

        decimal sumSquaredDiffs = 0m;
        foreach (var value in values)
        {
            var diff = value - mean;
            sumSquaredDiffs += diff * diff;
        }

        return (decimal)Math.Sqrt((double)(sumSquaredDiffs / (values.Length - 1)));
    }
}
```

### StockSharp Integration Points - Critical Compatibility Patterns

1. **StockSharp Lifecycle Management**
   ```csharp
   // CRITICAL: Maintain exact StockSharp lifecycle order
   protected override void OnStarted(DateTimeOffset time)
   {
       base.OnStarted(time); // MUST be first
       // Enhanced initialization after StockSharp setup
   }

   protected override void OnStopping()
   {
       // Enhanced cleanup before StockSharp cleanup
       base.OnStopping(); // MUST be last
   }

   // StockSharp Security/Connector integration
   protected override void OnSecurityChanged(Security security)
   {
       base.OnSecurityChanged(security);

       // Update enhanced tracking with new security reference
       Parameters.UpdateSecurityContext(security);
   }
   ```

2. **Modern .NET Patterns with StockSharp**
   ```csharp
   // Dependency injection that works with StockSharp's constructor patterns
   public abstract class EnhancedStrategyBase : Strategy
   {
       // StockSharp requires parameterless constructor for some scenarios
       protected EnhancedStrategyBase() : base()
       {
           // Initialize with service locator pattern as fallback
           InitializeServices();
       }

       // Preferred constructor for DI scenarios
       protected EnhancedStrategyBase(
           ILogger<EnhancedStrategyBase> logger,
           IServiceProvider serviceProvider) : base()
       {
           _logger = logger;
           _serviceProvider = serviceProvider;
       }

       // Service location pattern for StockSharp compatibility
       private void InitializeServices()
       {
           var serviceProvider = ServiceLocator.Current; // Fallback pattern
           _logger = serviceProvider.GetService<ILogger<EnhancedStrategyBase>>();
       }
   }
   ```

3. **Performance Optimization Patterns**
   ```csharp
   // Thread-safe collections for concurrent StockSharp operations
   private readonly ConcurrentDictionary<long, Order> _enhancedOrders = new();
   private readonly ConcurrentQueue<TradeExecutionData> _tradeQueue = new();

   // Memory pooling for high-frequency operations
   private readonly ObjectPool<PerformanceSnapshot> _snapshotPool;

   // Avoid allocations in hot paths
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   protected void RecordTrade(Trade trade)
   {
       var snapshot = _snapshotPool.Get();
       try
       {
           snapshot.Initialize(trade, Portfolio);
           ProcessSnapshot(snapshot);
       }
       finally
       {
           _snapshotPool.Return(snapshot);
       }
   }
   ```

## Acceptance Criteria - Enhanced for .NET 10

### Functional Requirements

- [ ] EnhancedStrategyBase successfully extends StockSharp Strategy with zero breaking changes
- [ ] Parameter management system supports C# 14 generic math and record types
- [ ] Performance tracking uses System.Threading.Channels for high-performance events
- [ ] Risk management hooks integrate without affecting StockSharp order flow
- [ ] Full StockSharp compatibility maintained across all connector types
- [ ] Dependency injection works with both DI containers and StockSharp's instantiation patterns

### .NET 10 Technical Requirements

- [ ] Code leverages C# 14 features: required members, raw string literals, generic math
- [ ] Nullable reference types enabled with zero warnings
- [ ] XML documentation with `<inheritdoc/>` for StockSharp overrides
- [ ] Memory-efficient implementation using `Span<T>`, `Memory<T>`, and object pooling
- [ ] Async/await patterns with proper `ConfigureAwait(false)`
- [ ] Source generators used for performance-critical serialization scenarios
- [ ] Logging uses structured logging with `LoggerMessage.Define` for high-performance scenarios

### Performance Requirements

- [ ] Zero allocation in hot trading paths (validated with BenchmarkDotNet)
- [ ] Event processing latency under 100 microseconds
- [ ] Memory usage growth linear with position count, not trade count
- [ ] GC pressure minimized through object pooling and `Span<T>` usage
- [ ] Thread safety verified through concurrent testing

### Testing Requirements - Comprehensive .NET 10 Coverage

#### Unit Testing (xUnit v3)

- [ ] Unit tests for all public methods using xUnit v3 patterns
- [ ] Test data generation using `TheoryData<T>` and `InlineData`
- [ ] Async testing with `Task`-based test methods
- [ ] Memory leak testing using `dotMemUnit` integration
- [ ] Performance testing with `BenchmarkDotNet` integration

#### StockSharp Integration Testing

- [ ] Integration tests with real StockSharp Strategy lifecycle
- [ ] Testing with multiple StockSharp connectors (CSV, fake, etc.)
- [ ] Parameter validation against StockSharp optimization engine
- [ ] Event firing validation with StockSharp's threading model
- [ ] Performance tracking accuracy against StockSharp's built-in metrics

#### Concurrency and Performance Testing

```csharp
[Fact]
public async Task EnhancedStrategy_ConcurrentTrades_HandlesWithoutRaceConditions()
{
    // Test concurrent trade processing
    var strategy = new TestEnhancedStrategy();
    var trades = GenerateTestTrades(10000);

    var tasks = trades.Select(trade =>
        Task.Run(() => strategy.ProcessTrade(trade)));

    await Task.WhenAll(tasks);

    // Verify no race conditions or data corruption
    Assert.Equal(10000, strategy.ProcessedTradeCount);
    Assert.True(strategy.PerformanceMetrics.IsConsistent);
}

[Benchmark]
public void ProcessTrade_Performance()
{
    var trade = CreateTestTrade();
    _strategy.ProcessTrade(trade);
}
```

## Dependencies - .NET 10 Optimized Stack

### NuGet Packages Required

```xml
<!-- StockSharp Integration -->
<PackageReference Include="StockSharp.Algo" Version="[latest]" />
<PackageReference Include="StockSharp.Strategies" Version="[latest]" />

<!-- Modern .NET Patterns -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.ObjectPool" Version="8.0.0" />

<!-- High-Performance Libraries -->
<PackageReference Include="System.Threading.Channels" Version="8.0.0" />
<PackageReference Include="System.Collections.Immutable" Version="8.0.0" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />

<!-- Development/Testing Dependencies -->
<PackageReference Include="BenchmarkDotNet" Version="0.13.7" Condition="'$(Configuration)' == 'Release'" />
<PackageReference Include="Microsoft.Extensions.Logging.Testing" Version="8.0.0" />
```

### Framework Dependencies - .NET 10 Features

- **.NET 10**: Required for C# 14 features and performance improvements
- **System.Numerics**: For generic math constraints
- **System.Memory**: For high-performance memory operations
- **System.Text.Json**: For source-generated serialization
- **System.Threading.Channels**: For high-performance async messaging
- **System.ComponentModel.DataAnnotations**: For validation attributes

### Source Generators Configuration

```xml
<!-- Enable source generators for performance -->
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<ItemGroup>
  <Analyzer Include="System.Text.Json.SourceGeneration" />
</ItemGroup>
```

## Definition of Done

1. **Code Complete**
   - EnhancedStrategyBase class implemented
   - All supporting interfaces and models created
   - Parameter validation system working
   - Event system functional

2. **Testing Complete**
   - Unit tests written and passing
   - Integration tests with StockSharp validated
   - Performance impact measured and acceptable
   - Memory leaks verified as absent

3. **Documentation Complete**
   - XML documentation for all public APIs
   - Code examples for common usage patterns
   - Integration guide with StockSharp
   - Performance considerations documented

4. **Integration Verified**
   - Works with existing StockSharp strategies
   - Compatible with StockSharp Studio
   - Performance metrics integration ready
   - Pipeline integration hooks functional

## Implementation Notes - .NET 10 Best Practices

### Design Considerations - Performance First

1. **StockSharp Compatibility Strategy**
   ```csharp
   // CRITICAL: StockSharp lifecycle compatibility
   // - Always call base methods first in lifecycle events
   // - Never override sealed StockSharp methods
   // - Respect StockSharp's threading model
   // - Maintain parameter-less constructor for StockSharp Studio
   ```

2. **Memory Efficiency Patterns**
   ```csharp
   // Use record structs for small data
   public readonly record struct PricePoint(decimal Price, DateTime Time);

   // Object pooling for frequently allocated objects
   private readonly ObjectPool<StringBuilder> _stringBuilderPool;

   // Span<T> for zero-allocation processing
   public void ProcessPrices(ReadOnlySpan<decimal> prices) { }
   ```

3. **Thread Safety with StockSharp**
   ```csharp
   // StockSharp callbacks can come from different threads
   private readonly object _lockObject = new();
   private readonly ConcurrentDictionary<string, object> _threadSafeData = new();

   // Use lock-free patterns where possible
   private volatile bool _isInitialized;
   private long _tradeCounter;

   public void IncrementTradeCount() => Interlocked.Increment(ref _tradeCounter);
   ```

### Critical StockSharp Integration Patterns

1. **Lifecycle Integration Order**
   ```csharp
   // MUST follow this exact order:
   protected override void OnStarted(DateTimeOffset time)
   {
       base.OnStarted(time);           // 1. StockSharp initialization
       InitializeParameters();         // 2. Parameter setup
       InitializePerformanceTracking(); // 3. Enhanced features
       StartEventProcessing();         // 4. Background processing
   }
   ```

2. **Order Management Enhancement**
   ```csharp
   // Enhance without breaking StockSharp flow
   protected override void RegisterOrder(Order order)
   {
       // Pre-processing (risk checks, logging)
       EnhancedPreOrderProcessing(order);

       base.RegisterOrder(order); // CRITICAL: StockSharp processing

       // Post-processing (tracking, events)
       EnhancedPostOrderProcessing(order);
   }
   ```

### Performance Anti-Patterns to Avoid

1. **Memory Allocation in Hot Paths**
   ```csharp
   // AVOID: Allocations in trade processing
   public void ProcessTrade(Trade trade)
   {
       var data = new TradeData { Trade = trade }; // ❌ Allocation
   }

   // PREFER: Object pooling or stack allocation
   public void ProcessTrade(Trade trade)
   {
       var data = _tradeDataPool.Get(); // ✅ Pooled object
       try { /* process */ }
       finally { _tradeDataPool.Return(data); }
   }
   ```

2. **Async/Await Misuse with StockSharp**
   ```csharp
   // AVOID: Async void in StockSharp callbacks
   protected override async void OnNewTrade(Trade trade) // ❌

   // PREFER: Fire-and-forget with proper error handling
   protected override void OnNewTrade(Trade trade)
   {
       _ = Task.Run(async () => await ProcessTradeAsync(trade))
           .ContinueWith(t => _logger.LogError(t.Exception, "Trade processing failed"),
                       TaskContinuationOptions.OnlyOnFaulted);
   }
   ```

3. **Event Handler Memory Leaks**
   ```csharp
   // AVOID: Traditional events that leak memory
   public event Action<Trade> TradeProcessed; // ❌ Can leak

   // PREFER: Channels or weak references
   public ChannelReader<Trade> TradeEvents => _tradeChannel.Reader; // ✅
   ```

## Summary - Foundation for Advanced Backtesting

This task establishes the **foundation for all subsequent Phase 1 development** using modern .NET 10 patterns while maintaining **100% StockSharp compatibility**. The implementation must:

- **Leverage C# 14 features** for performance and developer experience
- **Integrate seamlessly** with StockSharp's architecture and lifecycle
- **Provide zero-allocation paths** for high-frequency trading scenarios
- **Support both dependency injection and StockSharp's instantiation patterns**
- **Enable comprehensive testing** with modern xUnit v3 patterns

**Success Criteria**: A strategy developer should be able to inherit from `EnhancedStrategyBase` and immediately gain advanced capabilities without any changes to their existing StockSharp integration code.

**Performance Target**: Zero measurable performance degradation compared to pure StockSharp Strategy base class when enhanced features are unused.
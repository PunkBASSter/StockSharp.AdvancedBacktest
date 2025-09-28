# Task: P1-OPT-01 - Create BruteForceOptimizerWrapper

**Epic**: Phase1-Foundation
**Priority**: HIGH-03
**Agent**: dotnet-csharp-expert
**Status**: READY
**Dependencies**: P1-CORE-01, P1-CORE-02

## Overview

Create a wrapper around StockSharp's BruteForceOptimizer that enhances result capture, provides real-time progress monitoring, and integrates with the advanced backtesting pipeline while maintaining full compatibility with StockSharp's optimization framework.

## Technical Requirements - Modern .NET 10 StockSharp Integration

### Core Implementation - High-Performance Wrapper Architecture

1. **BruteForceOptimizerWrapper Class - Composition over Inheritance**
   - Wrap StockSharp.Algo.Strategies.Optimization.BruteForceOptimizer using composition
   - Enhance result capture with zero-allocation patterns
   - Provide real-time progress monitoring using System.Threading.Channels
   - Handle optimization failures with structured error recovery
   - Support cancellation and resume with checkpoint-based persistence
   - Implement memory-bounded processing for large parameter spaces
   - Use source-generated serialization for performance metrics

2. **Modern Component Architecture - Channels & Source Generation**
   ```csharp
   // High-performance wrapper using modern .NET patterns
   public sealed class BruteForceOptimizerWrapper : IAsyncDisposable
   {
       private readonly BruteForceOptimizer _innerOptimizer;
       private readonly ILogger<BruteForceOptimizerWrapper> _logger;
       private readonly IMemoryUsageMonitor _memoryMonitor;

       // Channel-based event system (no memory leaks)
       private readonly Channel<OptimizationProgress> _progressChannel;
       private readonly Channel<ParameterCombinationResult> _resultChannel;

       // Memory-efficient state management
       private readonly OptimizationSettings _settings;
       private readonly CancellationTokenSource _cancellationSource = new();

       // Object pooling for frequent allocations
       private readonly ObjectPool<ParameterCombinationResult> _resultPool;

       public required OptimizationSettings Settings { get; init; }
       public required EnhancedStrategyBase StrategyTemplate { get; init; }

       // Modern async enumerable pattern
       public IAsyncEnumerable<OptimizationProgress> ProgressUpdates =>
           _progressChannel.Reader.ReadAllAsync();

       public IAsyncEnumerable<ParameterCombinationResult> Results =>
           _resultChannel.Reader.ReadAllAsync();

       // Primary optimization method with comprehensive error handling
       public async Task<OptimizationStageResult> OptimizeAsync(
           CancellationToken cancellationToken = default)
       {
           using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
               cancellationToken, _cancellationSource.Token);

           return await OptimizeCore(linkedCts.Token);
       }
   }
   ```

3. **Result Capture Enhancement**
   ```csharp
   public class OptimizationStageResult : PipelineStageResult
   {
       public List<ParameterCombinationResult> Results { get; set; }
       public ParameterCombinationResult BestResult { get; set; }
       public OptimizationStatistics Statistics { get; set; }
       public TimeSpan Duration { get; set; }
   }
   ```

### File Structure

Create in `StockSharp.AdvancedBacktest/Core/Optimization/`:
- `BruteForceOptimizerWrapper.cs` - Main wrapper class
- `OptimizationSettings.cs` - Configuration for optimization
- `OptimizationStageResult.cs` - Enhanced result model
- `ParameterCombinationResult.cs` - Individual parameter test result
- `OptimizationProgress.cs` - Progress reporting model
- `OptimizationStatistics.cs` - Statistical analysis of results

## Implementation Details

### StockSharp Integration Architecture - Composition Patterns

1. **Composition-Based Integration (Preferred)**
   ```csharp
   // CRITICAL: Use composition to avoid breaking StockSharp's internal state
   public sealed class BruteForceOptimizerWrapper
   {
       private readonly BruteForceOptimizer _innerOptimizer;

       public BruteForceOptimizerWrapper(BruteForceOptimizer innerOptimizer)
       {
           _innerOptimizer = innerOptimizer ?? throw new ArgumentNullException(nameof(innerOptimizer));

           // Hook into StockSharp events without modifying internal state
           _innerOptimizer.ProgressChanged += OnStockSharpProgressChanged;
           _innerOptimizer.StateChanged += OnStockSharpStateChanged;
       }

       // Delegate StockSharp methods while adding enhancements
       public async Task<OptimizationResult> StartAsync(Strategy strategy, ParameterSet parameters)
       {
           // Pre-optimization setup with enhanced tracking
           var enhancedContext = await PrepareEnhancedOptimizationAsync(strategy, parameters);

           // Delegate to StockSharp while capturing enhanced data
           var stockSharpTask = Task.Run(() => _innerOptimizer.Start(strategy));
           var enhancementTask = Task.Run(() => CaptureEnhancedDataAsync(enhancedContext));

           await Task.WhenAll(stockSharpTask, enhancementTask);

           return await BuildEnhancedResultAsync(_innerOptimizer.Results, enhancedContext);
       }
   }
   ```

2. **Memory-Efficient Event Interception**
   ```csharp
   // Intercept StockSharp events without affecting performance
   private void OnStockSharpProgressChanged(object sender, ProgressChangedEventArgs e)
   {
       var enhancedProgress = new OptimizationProgress
       {
           StockSharpProgress = e.Progress,
           EnhancedMetrics = _memoryMonitor.GetCurrentMetrics(),
           Timestamp = DateTimeOffset.UtcNow,
           EstimatedTimeRemaining = CalculateETA(e.Progress)
       };

       // Non-blocking progress reporting
       _ = _progressChannel.Writer.TryWrite(enhancedProgress);
   }
   ```

2. **Zero-Allocation Result Capture System**
   ```csharp
   // Memory-efficient result capture using object pooling
   public sealed class EnhancedResultCapture : IDisposable
   {
       private readonly ObjectPool<TradeExecutionSnapshot> _tradeSnapshotPool;
       private readonly ObjectPool<PortfolioSnapshot> _portfolioSnapshotPool;
       private readonly CircularBuffer<decimal> _portfolioValues;

       // Span-based processing to avoid allocations
       public void CaptureTradeExecution(ReadOnlySpan<Trade> trades, Strategy strategy)
       {
           foreach (var trade in trades)
           {
               var snapshot = _tradeSnapshotPool.Get();
               try
               {
                   snapshot.Initialize(trade, strategy.Portfolio, DateTimeOffset.UtcNow);
                   ProcessTradeSnapshot(snapshot);
               }
               finally
               {
                   _tradeSnapshotPool.Return(snapshot);
               }
           }
       }

       // Streaming portfolio capture with configurable intervals
       public async Task CapturePortfolioSnapshotsAsync(
           Strategy strategy,
           TimeSpan interval,
           CancellationToken cancellationToken)
       {
           using var timer = new PeriodicTimer(interval);

           while (await timer.WaitForNextTickAsync(cancellationToken))
           {
               var snapshot = _portfolioSnapshotPool.Get();
               try
               {
                   snapshot.Initialize(strategy.Portfolio, DateTimeOffset.UtcNow);
                   await ProcessPortfolioSnapshotAsync(snapshot, cancellationToken);
               }
               finally
               {
                   _portfolioSnapshotPool.Return(snapshot);
               }
           }
       }
   }
   ```

3. **Progress Monitoring**
   - Real-time progress reporting
   - ETA calculation based on completed combinations
   - Memory usage monitoring
   - Performance statistics tracking

4. **Error Handling**
   - Graceful handling of strategy execution failures
   - Partial result recovery and continuation
   - Detailed error logging with parameter context
   - Automatic retry mechanisms for transient failures

### Optimization Enhancements

1. **Result Processing**
   ```csharp
   public class ParameterCombinationResult
   {
       public Dictionary<string, object> Parameters { get; set; }
       public string ParameterHash { get; set; }
       public PerformanceMetrics Performance { get; set; }
       public List<Trade> Trades { get; set; }
       public List<PortfolioSnapshot> PortfolioHistory { get; set; }
       public TimeSpan ExecutionTime { get; set; }
       public bool IsSuccessful { get; set; }
       public string ErrorMessage { get; set; }
   }
   ```

2. **Progress Tracking**
   ```csharp
   public class OptimizationProgress
   {
       public int TotalCombinations { get; set; }
       public int CompletedCombinations { get; set; }
       public int SuccessfulCombinations { get; set; }
       public int FailedCombinations { get; set; }
       public TimeSpan ElapsedTime { get; set; }
       public TimeSpan EstimatedTimeRemaining { get; set; }
       public ParameterCombinationResult CurrentBest { get; set; }
       public double MemoryUsageMB { get; set; }
   }
   ```

3. **Statistical Analysis**
   ```csharp
   public class OptimizationStatistics
   {
       public int TotalParameterCombinations { get; set; }
       public int SuccessfulRuns { get; set; }
       public double SuccessRate { get; set; }
       public TimeSpan AverageExecutionTime { get; set; }
       public ParameterCombinationResult BestBySharpe { get; set; }
       public ParameterCombinationResult BestByReturn { get; set; }
       public ParameterCombinationResult BestByDrawdown { get; set; }
       public Dictionary<string, ParameterDistribution> ParameterDistributions { get; set; }
   }
   ```

### Advanced Performance Optimization - .NET 10 Patterns

1. **Memory Management - Zero-Allocation Hot Paths**
   ```csharp
   // Memory-bounded optimization with automatic cleanup
   public sealed class MemoryBoundedOptimizer
   {
       private readonly IMemoryUsageMonitor _memoryMonitor;
       private readonly long _maxMemoryBytes;
       private volatile bool _memoryPressure;

       public async Task OptimizeWithMemoryBoundsAsync(
           ParameterSet parameterSet,
           CancellationToken cancellationToken)
       {
           // Monitor memory usage continuously
           using var memoryTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
           var memoryTask = Task.Run(async () =>
           {
               while (await memoryTimer.WaitForNextTickAsync(cancellationToken))
               {
                   var currentMemory = _memoryMonitor.GetCurrentUsage();
                   _memoryPressure = currentMemory > _maxMemoryBytes * 0.8; // 80% threshold

                   if (_memoryPressure)
                   {
                       _logger.LogWarning("Memory pressure detected: {MemoryMB}MB", currentMemory / 1024 / 1024);
                       await TriggerMemoryCleanupAsync();
                   }
               }
           }, cancellationToken);

           // Process parameters in memory-bounded batches
           await foreach (var batch in parameterSet.GenerateBatchesAsync(GetDynamicBatchSize(), cancellationToken))
           {
               await ProcessParameterBatchAsync(batch, cancellationToken);

               // Yield control if under memory pressure
               if (_memoryPressure)
               {
                   await Task.Delay(100, cancellationToken); // Brief pause
                   GC.Collect(1, GCCollectionMode.Optimized); // Targeted GC
               }
           }
       }

       private int GetDynamicBatchSize() =>
           _memoryPressure ? 100 : 1000; // Adaptive batch sizing
   }
   ```

2. **Vectorized Performance Calculations**
   ```csharp
   // Use System.Numerics.Vector for SIMD acceleration
   public static class VectorizedMetrics
   {
       public static decimal CalculateReturns(ReadOnlySpan<decimal> prices)
       {
           if (prices.Length < 2) return 0m;

           // Convert to double for vectorization (precision acceptable for intermediate calcs)
           Span<double> pricesDouble = stackalloc double[prices.Length];
           for (int i = 0; i < prices.Length; i++)
               pricesDouble[i] = (double)prices[i];

           return CalculateReturnsVectorized(pricesDouble);
       }

       private static decimal CalculateReturnsVectorized(ReadOnlySpan<double> prices)
       {
           var vectors = MemoryMarshal.Cast<double, Vector<double>>(prices);
           var sum = Vector<double>.Zero;

           foreach (var vector in vectors)
           {
               sum += vector;
           }

           // Handle remaining elements + convert back to decimal
           return (decimal)(Vector.Sum(sum) / prices.Length);
       }
   }
   ```

2. **Parallel Processing**
   - Leverage StockSharp's existing parallelization
   - Add batched processing for better memory control
   - Support custom threading configurations
   - Load balancing across available CPU cores

3. **Checkpointing System**
   - Save optimization state at regular intervals
   - Resume interrupted optimizations
   - Incremental result saving
   - Recovery from system failures

## Acceptance Criteria

### Functional Requirements

- [ ] Successfully wraps StockSharp BruteForceOptimizer
- [ ] Maintains 100% compatibility with StockSharp optimization
- [ ] Captures enhanced result data including trades and portfolio snapshots
- [ ] Provides real-time progress monitoring
- [ ] Handles optimization failures gracefully

### Performance Requirements

- [ ] No significant performance degradation vs. native StockSharp optimizer
- [ ] Memory usage stays within configurable limits
- [ ] Progress reporting updates at least every 5 seconds
- [ ] Checkpointing doesn't impact optimization performance significantly

### Integration Requirements

- [ ] Works with EnhancedStrategyBase and ParameterSet
- [ ] Integrates with artifact management system
- [ ] Supports cancellation tokens for clean shutdown
- [ ] Compatible with pipeline orchestration

## Implementation Specifications

### Modern Configuration with Options Pattern

```csharp
// Configuration using modern .NET options pattern
public sealed record OptimizationSettings
{
    // Progress monitoring settings
    public bool EnableProgressReporting { get; init; } = true;
    public TimeSpan ProgressReportingInterval { get; init; } = TimeSpan.FromSeconds(5);

    // Checkpointing and persistence
    public bool EnableCheckpointing { get; init; } = true;
    public TimeSpan CheckpointInterval { get; init; } = TimeSpan.FromMinutes(10);
    public string CheckpointDirectory { get; init; } = "./checkpoints";

    // Memory management
    public long MaxMemoryUsageBytes { get; init; } = 4L * 1024 * 1024 * 1024; // 4GB
    public double MemoryPressureThreshold { get; init; } = 0.8; // 80%
    public int DynamicBatchSizeMin { get; init; } = 100;
    public int DynamicBatchSizeMax { get; init; } = 1000;

    // Parallel processing
    public bool EnableParallelProcessing { get; init; } = true;
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
    public ParallelismStrategy ParallelismStrategy { get; init; } = ParallelismStrategy.Adaptive;

    // Error handling and resilience
    public bool ContinueOnErrors { get; init; } = true;
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public ExponentialBackoffSettings BackoffSettings { get; init; } = new();

    // Performance optimization
    public bool EnableVectorization { get; init; } = true;
    public bool UseObjectPooling { get; init; } = true;
    public int ObjectPoolMaxRetained { get; init; } = 1000;

    // Validation
    public bool ValidateParameterCombinations { get; init; } = true;
    public bool EnableResultHashing { get; init; } = true;
    public HashAlgorithmName HashAlgorithm { get; init; } = HashAlgorithmName.SHA256;
}

public enum ParallelismStrategy
{
    Fixed,      // Use MaxDegreeOfParallelism exactly
    Adaptive,   // Adjust based on system load
    Memory      // Adjust based on memory pressure
}

public sealed record ExponentialBackoffSettings
{
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public double Multiplier { get; init; } = 2.0;
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(5);
    public double Jitter { get; init; } = 0.1; // 10% random jitter
}
```

### Event System

1. **Progress Events**
   - Overall optimization progress
   - Individual parameter combination completion
   - Memory usage warnings
   - Error notifications

2. **Result Events**
   - New best result discovered
   - Checkpoint saved
   - Statistics updated
   - Optimization completed

### Integration Points

1. **StockSharp Dependencies**
   - BruteForceOptimizer (core optimization engine)
   - Strategy (base strategy class)
   - Portfolio (portfolio management)
   - Security (securities and market data)

2. **Advanced Backtesting Integration**
   - EnhancedStrategyBase for strategy execution
   - ParameterSet for parameter management
   - ArtifactManager for result storage
   - PerformanceCalculator for metrics

## Dependencies - High-Performance .NET 10 Stack

### NuGet Packages Required

```xml
<!-- StockSharp Core -->
<PackageReference Include="StockSharp.Algo" Version="[latest]" />
<PackageReference Include="StockSharp.Strategies" Version="[latest]" />

<!-- Modern .NET Patterns -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.ObjectPool" Version="8.0.0" />

<!-- High-Performance Async -->
<PackageReference Include="System.Threading.Channels" Version="8.0.0" />
<PackageReference Include="System.Threading.Tasks.Extensions" Version="8.0.0" />

<!-- Memory Management -->
<PackageReference Include="System.Collections.Immutable" Version="8.0.0" />
<PackageReference Include="System.Memory" Version="8.0.0" />

<!-- Serialization -->
<PackageReference Include="System.Text.Json" Version="8.0.0" />

<!-- SIMD and Vectorization -->
<PackageReference Include="System.Numerics.Vectors" Version="8.0.0" />

<!-- Performance Monitoring -->
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.0" />
<PackageReference Include="System.Diagnostics.PerformanceCounter" Version="8.0.0" Condition="$([MSBuild]::IsOSPlatform('Windows'))" />

<!-- Development/Testing -->
<PackageReference Include="BenchmarkDotNet" Version="0.13.7" Condition="'$(Configuration)' == 'Release'" />
```

### Framework Dependencies - .NET 10 Advanced Features

- **.NET 10**: Required for latest performance improvements and C# 14 features
- **System.Threading.Channels**: Lock-free, high-performance async messaging
- **System.Threading.Tasks**: Advanced async/await patterns with cancellation
- **System.Collections.Concurrent**: Thread-safe collections for parallel processing
- **System.Diagnostics**: Performance counters and memory monitoring
- **System.Numerics**: SIMD vectorization for mathematical operations
- **System.Memory**: High-performance memory operations with Span<T>
- **System.Text.Json**: Source-generated serialization for checkpointing

### Performance Monitoring Integration

```xml
<!-- Enable ETW event source for production monitoring -->
<PropertyGroup>
  <EventSourceSupport>true</EventSourceSupport>
</PropertyGroup>
```

## Definition of Done

1. **Code Complete**
   - BruteForceOptimizerWrapper fully implemented
   - Enhanced result capture working
   - Progress monitoring functional
   - Error handling robust

2. **Testing Complete**
   - Unit tests for wrapper functionality
   - Integration tests with StockSharp optimizer
   - Performance benchmarking completed
   - Memory usage testing verified

3. **Documentation Complete**
   - XML documentation for all public APIs
   - Integration guide with StockSharp
   - Performance considerations documented
   - Error handling guide complete

4. **Integration Verified**
   - Works with existing StockSharp strategies
   - Integrates with EnhancedStrategyBase
   - Compatible with ParameterSet
   - Ready for pipeline integration

## Implementation Notes

### Design Considerations

1. **Compatibility**: Maintain full backward compatibility with StockSharp
2. **Performance**: Minimize overhead while adding enhanced capabilities
3. **Reliability**: Robust error handling and recovery mechanisms
4. **Monitoring**: Comprehensive progress tracking and diagnostics

### Critical StockSharp Integration Patterns

1. **StockSharp Compatibility Patterns**
   ```csharp
   // CRITICAL: Never inherit from BruteForceOptimizer
   // StockSharp's internal state management breaks with inheritance

   // AVOID: Inheritance approach
   public class MyOptimizer : BruteForceOptimizer { } // ❌ Breaks internal state

   // PREFER: Composition approach
   public class EnhancedOptimizer
   {
       private readonly BruteForceOptimizer _stockSharpOptimizer; // ✅ Safe composition
   }
   ```

2. **Memory Leak Prevention in Long Optimizations**
   ```csharp
   // AVOID: Event handler memory leaks
   _optimizer.ProgressChanged += (s, e) => { /* capture 'this' */ }; // ❌ Memory leak

   // PREFER: Weak event handlers or proper disposal
   public sealed class OptimizationEventHandler : IDisposable
   {
       private readonly BruteForceOptimizer _optimizer;

       public OptimizationEventHandler(BruteForceOptimizer optimizer)
       {
           _optimizer = optimizer;
           _optimizer.ProgressChanged += OnProgressChanged; // ✅ Proper lifecycle
       }

       public void Dispose()
       {
           _optimizer.ProgressChanged -= OnProgressChanged; // ✅ Cleanup
       }
   }
   ```

3. **Performance Anti-Patterns**
   ```csharp
   // AVOID: Allocations in optimization hot paths
   foreach (var result in optimizationResults)
   {
       var metrics = new PerformanceMetrics(); // ❌ Allocation per iteration
       CalculateMetrics(result, metrics);
   }

   // PREFER: Object pooling and reuse
   var metricsPool = new ObjectPool<PerformanceMetrics>();
   foreach (var result in optimizationResults)
   {
       var metrics = metricsPool.Get(); // ✅ Pooled object
       try
       {
           CalculateMetrics(result, metrics);
       }
       finally
       {
           metricsPool.Return(metrics); // ✅ Return to pool
       }
   }
   ```

4. **Thread Safety with StockSharp's Threading Model**
   ```csharp
   // CRITICAL: StockSharp uses specific threading patterns
   // Always respect StockSharp's synchronization context

   // AVOID: Cross-thread operations without synchronization
   Task.Run(() => strategy.Portfolio.TotalValue); // ❌ Thread unsafe

   // PREFER: Proper synchronization
   await Task.Run(() =>
   {
       lock (strategy.SyncRoot) // ✅ Use StockSharp's sync object
       {
           return strategy.Portfolio.TotalValue;
       }
   });
   ```

## Summary - High-Performance StockSharp Integration

This task creates **enterprise-grade optimization infrastructure** that seamlessly integrates with StockSharp while providing modern .NET 10 performance optimizations:

### Key Technical Achievements:
- **Composition-Based Architecture**: Maintains 100% StockSharp compatibility without inheritance risks
- **Memory-Bounded Processing**: Handles massive parameter spaces with O(1) memory usage
- **Channel-Based Events**: Eliminates traditional event memory leaks with high-performance messaging
- **SIMD Vectorization**: Accelerates mathematical calculations using System.Numerics
- **Object Pooling**: Zero-allocation hot paths for sustained performance
- **Adaptive Parallelism**: Dynamically adjusts based on system resources and memory pressure

### Performance Targets:
- **Zero allocations** in optimization hot paths
- **Linear scaling** with CPU cores up to memory bandwidth limits
- **Memory efficiency**: Constant memory usage regardless of parameter space size
- **Fault tolerance**: Automatic recovery from transient failures with exponential backoff
- **Checkpointing**: Resume optimization from any point with minimal overhead

### StockSharp Integration Points:
- **Event Interception**: Capture StockSharp events without modifying internal state
- **Result Enhancement**: Add comprehensive metrics while preserving original StockSharp results
- **Threading Compatibility**: Respect StockSharp's synchronization patterns
- **API Preservation**: All existing StockSharp workflows continue to work unchanged

**Success Criteria**: Existing StockSharp optimization code should work without modification while gaining enhanced capabilities and performance improvements.
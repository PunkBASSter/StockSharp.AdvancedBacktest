# Task: P1-PIPE-01 - Create PipelineOrchestrator

**Epic**: Phase1-Foundation
**Priority**: HIGH-07
**Agent**: solution-architect
**Status**: READY
**Dependencies**: P1-PERF-01, P1-DATA-02

## Overview

Implement PipelineOrchestrator that coordinates the complete optimization pipeline from strategy execution through result generation and reporting. This orchestrator manages the workflow between optimization, performance calculation, artifact storage, and report generation stages.

## Technical Requirements - Modern Async Pipeline Architecture

### Core Implementation - Producer-Consumer with Channels

1. **PipelineOrchestrator Class - Enterprise Workflow Engine**
   - Coordinate optimization workflow using System.Threading.Channels
   - Manage data flow with strongly-typed pipeline stages
   - Handle error recovery with circuit breaker patterns and exponential backoff
   - Provide real-time progress monitoring with structured logging
   - Support graceful cancellation and checkpoint-based resume capabilities
   - Implement adaptive parallelism based on system resources
   - Use source-generated serialization for pipeline state persistence

2. **Modern Component Architecture - Channels & State Machines**
   ```csharp
   // High-performance pipeline orchestrator using modern async patterns
   public sealed class PipelineOrchestrator : IAsyncDisposable
   {
       private readonly ILogger<PipelineOrchestrator> _logger;
       private readonly IPipelineStateManager _stateManager;
       private readonly ICircuitBreakerPolicy _circuitBreaker;

       // Channel-based progress and completion notifications
       private readonly Channel<PipelineStageProgress> _progressChannel;
       private readonly Channel<PipelineStageCompleted> _completionChannel;
       private readonly CancellationTokenSource _orchestratorCts = new();

       // Async enumerable for real-time monitoring
       public IAsyncEnumerable<PipelineStageProgress> ProgressUpdates =>
           _progressChannel.Reader.ReadAllAsync();

       public IAsyncEnumerable<PipelineStageCompleted> CompletionEvents =>
           _completionChannel.Reader.ReadAllAsync();

       // Primary execution method with comprehensive error handling
       public async Task<PipelineExecutionResult> ExecuteAsync(
           PipelineConfiguration config,
           CancellationToken cancellationToken = default)
       {
           using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
               cancellationToken, _orchestratorCts.Token);

           var executionContext = new PipelineExecutionContext
           {
               ExecutionId = Guid.NewGuid().ToString(),
               Configuration = config,
               StartTime = DateTimeOffset.UtcNow
           };

           return await ExecutePipelineAsync(executionContext, linkedCts.Token);
       }

       // Resume from checkpoint with state reconstruction
       public async Task<PipelineExecutionResult> ResumeAsync(
           string executionId,
           CancellationToken cancellationToken = default)
       {
           var state = await _stateManager.LoadStateAsync(executionId, cancellationToken);
           if (state == null)
               throw new InvalidOperationException($"No state found for execution {executionId}");

           var executionContext = await ReconstructExecutionContextAsync(state, cancellationToken);
           return await ExecutePipelineAsync(executionContext, cancellationToken);
       }
   }
   ```

3. **Pipeline Stage Management**
   ```csharp
   public abstract class PipelineStage
   {
       public abstract string StageName { get; }
       public abstract Task<PipelineStageResult> ExecuteAsync(PipelineStageInput input, CancellationToken cancellationToken);
       public virtual bool CanResume => false;
       public virtual Task<PipelineStageResult> ResumeAsync(PipelineStageInput input, string checkpointData, CancellationToken cancellationToken) => throw new NotSupportedException();
   }
   ```

### File Structure

Create in `StockSharp.AdvancedBacktest/Core/Pipeline/`:
- `PipelineOrchestrator.cs` - Main orchestrator class
- `PipelineConfiguration.cs` - Pipeline configuration model
- `PipelineStage.cs` - Abstract base class for pipeline stages
- `PipelineStageResult.cs` - Result model for pipeline stages
- `PipelineExecutionResult.cs` - Overall pipeline execution result
- `Stages/OptimizationStage.cs` - Optimization pipeline stage
- `Stages/PerformanceCalculationStage.cs` - Performance metrics stage
- `Stages/ArtifactStorageStage.cs` - Artifact storage stage
- `Stages/ReportGenerationStage.cs` - Report generation stage

## Implementation Details

### Modern Pipeline Architecture - Typed Stages & Channels

1. **Strongly-Typed Stage Definitions with Generic Constraints**
   ```csharp
   // Generic pipeline stage with input/output type safety
   public abstract class PipelineStage<TInput, TOutput> : IPipelineStage
       where TInput : IPipelineStageInput
       where TOutput : IPipelineStageResult
   {
       protected readonly ILogger Logger;
       protected readonly IServiceProvider ServiceProvider;

       public abstract string StageName { get; }
       public virtual bool SupportsCheckpointing => false;
       public virtual bool SupportsParallelExecution => false;

       // Core execution with comprehensive error handling
       public async Task<IPipelineStageResult> ExecuteAsync(
           IPipelineStageInput input,
           PipelineExecutionContext context,
           CancellationToken cancellationToken)
       {
           var typedInput = (TInput)input;
           var stopwatch = Stopwatch.StartNew();

           try
           {
               Logger.LogInformation("Starting pipeline stage {StageName} for execution {ExecutionId}",
                   StageName, context.ExecutionId);

               var result = await ExecuteCoreAsync(typedInput, context, cancellationToken);
               stopwatch.Stop();

               Logger.LogInformation("Completed pipeline stage {StageName} in {Elapsed}ms",
                   StageName, stopwatch.ElapsedMilliseconds);

               return result;
           }
           catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
           {
               Logger.LogWarning("Pipeline stage {StageName} was cancelled", StageName);
               throw;
           }
           catch (Exception ex)
           {
               Logger.LogError(ex, "Pipeline stage {StageName} failed after {Elapsed}ms",
                   StageName, stopwatch.ElapsedMilliseconds);
               throw;
           }
       }

       protected abstract Task<TOutput> ExecuteCoreAsync(
           TInput input,
           PipelineExecutionContext context,
           CancellationToken cancellationToken);

       // Optional checkpoint creation for resumable stages
       public virtual async Task<string?> CreateCheckpointAsync(
           IPipelineStageInput input,
           IPipelineStageResult result,
           CancellationToken cancellationToken)
       {
           return await Task.FromResult<string?>(null);
       }
   }

   // Concrete optimization stage implementation
   public sealed class OptimizationStage : PipelineStage<OptimizationStageInput, OptimizationStageResult>
   {
       private readonly BruteForceOptimizerWrapper _optimizer;
       private readonly IMemoryUsageMonitor _memoryMonitor;

       public override string StageName => "Optimization";
       public override bool SupportsCheckpointing => true;
       public override bool SupportsParallelExecution => true;

       protected override async Task<OptimizationStageResult> ExecuteCoreAsync(
           OptimizationStageInput input,
           PipelineExecutionContext context,
           CancellationToken cancellationToken)
       {
           // Monitor memory usage during optimization
           using var memoryMonitor = _memoryMonitor.StartMonitoring(TimeSpan.FromSeconds(5));

           // Execute optimization with progress reporting
           var optimizationTask = _optimizer.OptimizeAsync(cancellationToken);
           var progressTask = MonitorOptimizationProgressAsync(context, cancellationToken);

           await Task.WhenAll(optimizationTask, progressTask);

           var result = await optimizationTask;
           var memoryStats = await memoryMonitor.GetStatisticsAsync();

           return new OptimizationStageResult
           {
               OptimizationResults = result,
               MemoryStatistics = memoryStats,
               ExecutionTime = context.GetElapsedTime(),
               IsSuccessful = true
           };
       }
   }
   ```

2. **Data Flow Management**
   ```csharp
   public class PipelineDataFlow
   {
       public Dictionary<string, object> SharedData { get; set; } = new();
       public ArtifactPath BasePath { get; set; }
       public PipelineConfiguration Configuration { get; set; }

       public T GetData<T>(string key) where T : class;
       public void SetData<T>(string key, T value);
       public bool HasData(string key);
   }
   ```

### Advanced Orchestration Logic - Producer-Consumer Architecture

1. **Channel-Based Pipeline Execution with Backpressure**
   ```csharp
   // High-performance pipeline execution using channels
   private async Task<PipelineExecutionResult> ExecutePipelineAsync(
       PipelineExecutionContext context,
       CancellationToken cancellationToken)
   {
       var config = context.Configuration;
       var results = new ConcurrentDictionary<string, IPipelineStageResult>();
       var exceptions = new ConcurrentBag<Exception>();

       // Create pipeline stages with dependency injection
       var stages = new IPipelineStage[]
       {
           ServiceProvider.GetRequiredService<OptimizationStage>(),
           ServiceProvider.GetRequiredService<PerformanceCalculationStage>(),
           ServiceProvider.GetRequiredService<ArtifactStorageStage>(),
           ServiceProvider.GetRequiredService<ReportGenerationStage>()
       };

       // Execute stages with proper dependency ordering
       try
       {
           // Stage 1: Optimization (independent)
           var optimizationResult = await ExecuteStageWithCircuitBreakerAsync(
               stages[0],
               new OptimizationStageInput { Configuration = config },
               context,
               cancellationToken);
           results["optimization"] = optimizationResult;

           // Stage 2: Performance Calculation (depends on optimization)
           var performanceInput = new PerformanceCalculationStageInput
           {
               OptimizationResult = (OptimizationStageResult)optimizationResult,
               Configuration = config
           };
           var performanceResult = await ExecuteStageWithCircuitBreakerAsync(
               stages[1],
               performanceInput,
               context,
               cancellationToken);
           results["performance"] = performanceResult;

           // Stages 3 & 4: Parallel execution (both depend on performance)
           var storageInput = new ArtifactStorageStageInput
           {
               OptimizationResult = (OptimizationStageResult)optimizationResult,
               PerformanceResult = (PerformanceCalculationStageResult)performanceResult
           };
           var reportInput = new ReportGenerationStageInput
           {
               PerformanceResult = (PerformanceCalculationStageResult)performanceResult,
               Configuration = config
           };

           // Execute storage and reporting in parallel
           var parallelTasks = new[]
           {
               ExecuteStageWithCircuitBreakerAsync(stages[2], storageInput, context, cancellationToken),
               ExecuteStageWithCircuitBreakerAsync(stages[3], reportInput, context, cancellationToken)
           };

           var parallelResults = await Task.WhenAll(parallelTasks);
           results["storage"] = parallelResults[0];
           results["reporting"] = parallelResults[1];

           return new PipelineExecutionResult
           {
               ExecutionId = context.ExecutionId,
               IsSuccessful = true,
               StageResults = results.Values.ToList(),
               TotalDuration = context.GetElapsedTime(),
               MemoryPeakUsage = await GetPeakMemoryUsageAsync(),
               CompletedAt = DateTimeOffset.UtcNow
           };
       }
       catch (Exception ex)
       {
           Logger.LogError(ex, "Pipeline execution failed for {ExecutionId}", context.ExecutionId);
           return await HandlePipelineFailureAsync(context, results.Values, ex, cancellationToken);
       }
   }

   // Circuit breaker pattern for resilient stage execution
   private async Task<IPipelineStageResult> ExecuteStageWithCircuitBreakerAsync(
       IPipelineStage stage,
       IPipelineStageInput input,
       PipelineExecutionContext context,
       CancellationToken cancellationToken)
   {
       var retryPolicy = Policy
           .Handle<Exception>(ex => !(ex is OperationCanceledException))
           .WaitAndRetryAsync(
               retryCount: 3,
               sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
               onRetry: (outcome, timespan, retryCount, context) =>
               {
                   Logger.LogWarning("Retrying stage {StageName}, attempt {RetryCount} after {Delay}ms",
                       stage.StageName, retryCount, timespan.TotalMilliseconds);
               });

       return await retryPolicy.ExecuteAsync(async () =>
       {
           // Create checkpoint before execution if supported
           if (stage.SupportsCheckpointing)
           {
               await _stateManager.SaveStageStateAsync(context.ExecutionId, stage.StageName, input, cancellationToken);
           }

           // Execute stage with progress monitoring
           var progressTask = MonitorStageProgressAsync(stage, context, cancellationToken);
           var executionTask = stage.ExecuteAsync(input, context, cancellationToken);

           var result = await executionTask;

           // Create post-execution checkpoint if supported
           if (stage.SupportsCheckpointing)
           {
               var checkpoint = await stage.CreateCheckpointAsync(input, result, cancellationToken);
               if (checkpoint != null)
               {
                   await _stateManager.SaveCheckpointAsync(context.ExecutionId, stage.StageName, checkpoint, cancellationToken);
               }
           }

           return result;
       });
   }
   ```

2. **Error Handling and Recovery**
   ```csharp
   private async Task<PipelineStageResult> ExecuteStageAsync(PipelineStage stage, PipelineDataFlow dataFlow, CancellationToken cancellationToken)
   {
       try
       {
           var input = CreateStageInput(stage.StageName, dataFlow);
           var result = await stage.ExecuteAsync(input, cancellationToken);

           // Update shared data with stage results
           UpdateDataFlow(dataFlow, stage.StageName, result);

           // Create checkpoint for resumable stages
           if (stage.CanResume)
           {
               await CreateCheckpointAsync(dataFlow.BasePath, stage.StageName, result);
           }

           StageCompleted?.Invoke(new PipelineStageCompleted
           {
               StageName = stage.StageName,
               IsSuccessful = result.IsSuccessful,
               Duration = result.ExecutionTime
           });

           return result;
       }
       catch (OperationCanceledException)
       {
           // Handle cancellation gracefully
           throw;
       }
       catch (Exception ex)
       {
           // Handle stage-specific errors
           return CreateErrorResult(stage.StageName, ex);
       }
   }
   ```

## Acceptance Criteria

### Functional Requirements

- [ ] Successfully orchestrates complete optimization pipeline
- [ ] Handles data flow between all pipeline stages
- [ ] Provides comprehensive progress monitoring
- [ ] Supports pipeline cancellation and graceful shutdown
- [ ] Implements error recovery and partial result handling

### Performance Requirements

- [ ] Pipeline overhead less than 5% of total execution time
- [ ] Memory usage scales linearly with pipeline complexity
- [ ] Progress reporting updates at least every 10 seconds
- [ ] Checkpoint creation doesn't significantly impact performance

### Reliability Requirements

- [ ] Graceful handling of individual stage failures
- [ ] Partial pipeline recovery and resume capabilities
- [ ] Proper resource cleanup on cancellation or failure
- [ ] Comprehensive logging and error reporting

## Implementation Specifications

### Configuration Model

```csharp
public class PipelineConfiguration
{
    public string StrategyName { get; set; }
    public ParameterSet Parameters { get; set; }
    public MarketDataConfiguration MarketData { get; set; }
    public OptimizationSettings OptimizationSettings { get; set; }
    public ArtifactConfiguration ArtifactSettings { get; set; }
    public ReportConfiguration ReportSettings { get; set; }
    public bool EnableCheckpointing { get; set; } = true;
    public TimeSpan CheckpointInterval { get; set; } = TimeSpan.FromMinutes(5);
}
```

### Progress Monitoring

```csharp
public class PipelineStageProgress
{
    public string StageName { get; set; }
    public double ProgressPercentage { get; set; }
    public string CurrentOperation { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    public Dictionary<string, object> StageSpecificData { get; set; }
}
```

## Dependencies - Enterprise Pipeline Stack

### NuGet Packages Required

```xml
<!-- Core Pipeline Infrastructure -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />

<!-- High-Performance Async -->
<PackageReference Include="System.Threading.Channels" Version="8.0.0" />
<PackageReference Include="System.Threading.Tasks.Extensions" Version="8.0.0" />
<PackageReference Include="System.Linq.Async" Version="6.0.1" />

<!-- Resilience Patterns -->
<PackageReference Include="Polly" Version="7.2.4" /> <!-- Circuit breaker, retry policies -->
<PackageReference Include="Polly.Extensions.Http" Version="3.0.0" />

<!-- State Management -->
<PackageReference Include="System.Text.Json" Version="8.0.0" />
<PackageReference Include="System.IO.MemoryMappedFiles" Version="8.0.0" />

<!-- Memory Management -->
<PackageReference Include="System.Memory" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.ObjectPool" Version="8.0.0" />

<!-- Performance Monitoring -->
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.0" />
<PackageReference Include="System.Diagnostics.PerformanceCounter" Version="8.0.0" Condition="$([MSBuild]::IsOSPlatform('Windows'))" />

<!-- Development/Testing -->
<PackageReference Include="BenchmarkDotNet" Version="0.13.7" Condition="'$(Configuration)' == 'Release'" />
```

### Framework Dependencies - Advanced Async Patterns

- **.NET 10**: Required for latest async improvements and performance optimizations
- **System.Threading.Channels**: Lock-free, high-performance async messaging for stage coordination
- **System.Threading.Tasks**: Advanced async/await patterns with proper cancellation
- **System.Collections.Concurrent**: Thread-safe collections for pipeline state management
- **System.Linq.Async**: Async enumerable operations for streaming pipeline data
- **Polly**: Resilience patterns (circuit breaker, retry, timeout) for robust pipeline execution
- **System.Text.Json**: Source-generated serialization for pipeline state persistence
- **System.Diagnostics**: Performance counters and memory monitoring for pipeline health

### Pipeline Configuration

```xml
<!-- Enable advanced async features -->
<PropertyGroup>
  <LangVersion>preview</LangVersion>
  <EnablePreviewFeatures>true</EnablePreviewFeatures>
</PropertyGroup>

<!-- Structured logging configuration -->
<ItemGroup>
  <Analyzer Include="Microsoft.Extensions.Logging.Generators" />
</ItemGroup>
```

## Definition of Done

1. **Code Complete**
   - PipelineOrchestrator fully implemented
   - All pipeline stages created and integrated
   - Error handling and recovery functional
   - Progress monitoring working

2. **Testing Complete**
   - Unit tests for orchestrator logic
   - Integration tests with all pipeline stages
   - Error scenario testing
   - Performance impact validation

3. **Documentation Complete**
   - XML documentation for all public APIs
   - Pipeline architecture documentation
   - Configuration guide
   - Troubleshooting documentation

4. **Integration Verified**
   - Works with all Phase 1 components
   - End-to-end pipeline execution successful
   - Error recovery validated
   - Performance acceptable

## Implementation Notes

### Design Considerations

1. **Modularity**: Each stage should be independently testable and replaceable
2. **Extensibility**: Easy to add new stages for future phases
3. **Resilience**: Robust error handling and recovery mechanisms
4. **Monitoring**: Comprehensive observability for debugging and optimization

### Common Pitfalls to Avoid

1. Tight coupling between pipeline stages
2. Memory leaks in long-running pipelines
3. Poor error isolation between stages
4. Inadequate progress reporting granularity

## Summary - Enterprise Pipeline Orchestration

This task delivers **production-grade pipeline orchestration** using modern .NET 10 async patterns for enterprise-scale trading system automation:

### Advanced Architecture Features:
- **Channel-Based Coordination**: Lock-free async messaging between pipeline stages
- **Circuit Breaker Resilience**: Automatic failure detection and recovery with exponential backoff
- **Checkpoint-Based Resume**: Granular state persistence for long-running optimizations
- **Adaptive Parallelism**: Dynamic resource allocation based on system capacity
- **Memory-Bounded Execution**: Automatic memory pressure detection and throttling
- **Structured Logging**: Comprehensive observability with correlation IDs

### Performance & Reliability:
- **Zero-Allocation Hot Paths**: Memory-efficient execution for sustained performance
- **Graceful Degradation**: Intelligent fallback strategies for partial system failures
- **Real-Time Monitoring**: Live progress tracking with sub-second granularity
- **Linear Scalability**: Pipeline throughput scales with available CPU cores
- **Memory Efficiency**: Constant memory usage regardless of optimization complexity
- **Fault Isolation**: Stage failures don't cascade to other pipeline components

### Enterprise Integration:
- **Dependency Injection**: Full DI container integration for testability and modularity
- **Configuration Management**: Strongly-typed configuration with validation
- **Health Monitoring**: Built-in health checks and performance metrics
- **Audit Trail**: Complete execution history with timing and resource usage
- **Multi-Tenancy Ready**: Isolated execution contexts for concurrent optimizations

### Phase 1 Component Integration:
- **EnhancedStrategyBase**: Seamless strategy lifecycle management
- **ParameterSet**: Efficient parameter space exploration coordination
- **BruteForceOptimizerWrapper**: High-performance optimization execution
- **PerformanceCalculator**: Real-time metrics calculation and aggregation
- **JsonSerializationService**: Persistent state management and result export
- **ArtifactManager**: Automated artifact lifecycle and cleanup

**Success Criteria**: Execute complex multi-stage optimization workflows with enterprise-grade reliability, observability, and performance while maintaining simple APIs for common use cases.
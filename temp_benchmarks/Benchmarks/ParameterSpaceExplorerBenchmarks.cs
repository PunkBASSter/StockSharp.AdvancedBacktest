#if RELEASE

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using System.Collections.Immutable;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;

namespace StockSharp.AdvancedBacktest.Core.Optimization.Benchmarks;

/// <summary>
/// Performance benchmarks for ParameterSpaceExplorer targeting 100,000+ combinations/second.
/// </summary>
[Config(typeof(Config))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ParameterSpaceExplorerBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithWarmupCount(3)
                .WithIterationCount(10));
        }
    }

    private ParameterSpaceExplorer _smallSpace = null!;
    private ParameterSpaceExplorer _mediumSpace = null!;
    private ParameterSpaceExplorer _largeSpace = null!;
    private ParameterSpaceExplorer _extremeSpace = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small space: ~1,000 combinations (10 x 10 x 10)
        _smallSpace = new ParameterSpaceExplorer([
            ParameterDefinition.CreateInteger("param1", 1, 10, 5),
            ParameterDefinition.CreateInteger("param2", 1, 10, 5),
            ParameterDefinition.CreateInteger("param3", 1, 10, 5)
        ]);

        // Medium space: ~100,000 combinations (20 x 50 x 100)
        _mediumSpace = new ParameterSpaceExplorer([
            ParameterDefinition.CreateInteger("param1", 1, 20, 10),
            ParameterDefinition.CreateInteger("param2", 1, 50, 25),
            ParameterDefinition.CreateInteger("param3", 1, 100, 50)
        ]);

        // Large space: ~10,000,000 combinations (100 x 100 x 1000)
        _largeSpace = new ParameterSpaceExplorer([
            ParameterDefinition.CreateInteger("param1", 1, 100, 50),
            ParameterDefinition.CreateInteger("param2", 1, 100, 50),
            ParameterDefinition.CreateInteger("param3", 1, 1000, 500)
        ]);

        // Extreme space: ~1,000,000,000 combinations (1000 x 1000 x 1000)
        _extremeSpace = new ParameterSpaceExplorer([
            ParameterDefinition.CreateInteger("param1", 1, 1000, 500),
            ParameterDefinition.CreateInteger("param2", 1, 1000, 500),
            ParameterDefinition.CreateInteger("param3", 1, 1000, 500)
        ]);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _smallSpace?.Dispose();
        _mediumSpace?.Dispose();
        _largeSpace?.Dispose();
        _extremeSpace?.Dispose();
    }

    [Benchmark]
    [OperationsPerInvoke(1000)]
    public async Task<int> SmallSpace_StreamingEnumeration()
    {
        var count = 0;
        await foreach (var combination in _smallSpace.EnumerateAsync())
        {
            count++;
        }
        return count;
    }

    [Benchmark]
    [OperationsPerInvoke(100000)]
    public async Task<int> MediumSpace_StreamingEnumeration()
    {
        var count = 0;
        await foreach (var combination in _mediumSpace.EnumerateAsync())
        {
            count++;
        }
        return count;
    }

    [Benchmark]
    [OperationsPerInvoke(100000)]
    public async Task<int> MediumSpace_BatchedEnumeration()
    {
        var count = 0;
        await foreach (var batch in _mediumSpace.EnumerateBatchesAsync(batchSize: 1000))
        {
            count += batch.Length;
        }
        return count;
    }

    [Benchmark]
    [OperationsPerInvoke(100000)]
    public int MediumSpace_ParallelQuery()
    {
        return _mediumSpace.AsParallelQuery().Count();
    }

    [Benchmark]
    [OperationsPerInvoke(10000)]
    public async Task<int> LargeSpace_StreamingEnumeration_Limited()
    {
        var count = 0;
        await foreach (var combination in _largeSpace.EnumerateAsync())
        {
            count++;
            if (count >= 10000) // Limit to avoid excessive test time
                break;
        }
        return count;
    }

    [Benchmark]
    [OperationsPerInvoke(10000)]
    public async Task<int> LargeSpace_BatchedEnumeration_Limited()
    {
        var count = 0;
        await foreach (var batch in _largeSpace.EnumerateBatchesAsync(batchSize: 1000))
        {
            count += batch.Length;
            if (count >= 10000) // Limit to avoid excessive test time
                break;
        }
        return count;
    }

    [Benchmark]
    [OperationsPerInvoke(1000)]
    public ImmutableDictionary<string, object?> DirectIndexAccess_Small()
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var index = random.Next(0, (int)_smallSpace.TotalCombinations!.Value);
        return _smallSpace.GetCombinationByIndex(index);
    }

    [Benchmark]
    [OperationsPerInvoke(1000)]
    public ImmutableDictionary<string, object?> DirectIndexAccess_Medium()
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var index = random.Next(0, (int)_mediumSpace.TotalCombinations!.Value);
        return _mediumSpace.GetCombinationByIndex(index);
    }

    [Benchmark]
    [OperationsPerInvoke(1000)]
    public ImmutableDictionary<string, object?> DirectIndexAccess_Large()
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var index = random.NextInt64(0, _largeSpace.TotalCombinations!.Value);
        return _largeSpace.GetCombinationByIndex(index);
    }

    [Benchmark]
    public ParameterSpaceMemoryStats MemoryEstimation_Small()
    {
        return _smallSpace.GetMemoryEstimate();
    }

    [Benchmark]
    public ParameterSpaceMemoryStats MemoryEstimation_Medium()
    {
        return _mediumSpace.GetMemoryEstimate();
    }

    [Benchmark]
    public ParameterSpaceMemoryStats MemoryEstimation_Large()
    {
        return _largeSpace.GetMemoryEstimate();
    }

    [Benchmark]
    public ParameterSpaceMemoryStats MemoryEstimation_Extreme()
    {
        return _extremeSpace.GetMemoryEstimate();
    }

    /// <summary>
    /// Throughput test specifically designed to measure combinations per second.
    /// Target: 100,000+ combinations/second.
    /// </summary>
    [Benchmark]
    [OperationsPerInvoke(100000)]
    public async Task<long> ThroughputTest_100k_Combinations()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var count = 0L;

        await foreach (var combination in _mediumSpace.EnumerateAsync())
        {
            count++;
            // Minimal processing to measure pure enumeration speed
            _ = combination.Count; // Access the combination to ensure it's not optimized away
        }

        stopwatch.Stop();
        var combinationsPerSecond = (long)(count / stopwatch.Elapsed.TotalSeconds);

        // Verify we achieved the performance target
        if (combinationsPerSecond < 100_000)
        {
            throw new InvalidOperationException(
                $"Performance target not met: {combinationsPerSecond:N0} combinations/sec < 100,000 target");
        }

        return combinationsPerSecond;
    }

    /// <summary>
    /// Memory efficiency test to ensure O(1) memory usage for streaming enumeration.
    /// </summary>
    [Benchmark]
    [OperationsPerInvoke(10000)]
    public async Task<bool> MemoryEfficiencyTest_Streaming()
    {
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);
        var count = 0;

        await foreach (var combination in _largeSpace.EnumerateAsync())
        {
            count++;
            // Process some combinations to ensure memory usage is realistic
            _ = combination.Values.Count;

            if (count >= 10000) break;

            // Check memory growth periodically
            if (count % 1000 == 0)
            {
                var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
                var memoryGrowth = currentMemory - initialMemory;

                // Memory should remain relatively stable (allow for some JIT and GC overhead)
                if (memoryGrowth > 10_000_000) // 10MB threshold
                {
                    throw new InvalidOperationException(
                        $"Memory growth exceeded threshold: {memoryGrowth:N0} bytes");
                }
            }
        }

        return true; // Success
    }
}

#endif
using System.Collections.Immutable;
using System.Diagnostics;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;

namespace StockSharp.AdvancedBacktest.Core.Optimization.Demo;

/// <summary>
/// Performance demonstration for ParameterSpaceExplorer.
/// Validates that we achieve 100,000+ parameter combinations per second target.
/// </summary>
public static class PerformanceDemo
{
    /// <summary>
    /// Runs comprehensive performance tests for ParameterSpaceExplorer.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== ParameterSpaceExplorer Performance Demo ===");
        Console.WriteLine();

        await TestSmallParameterSpace();
        await TestMediumParameterSpace();
        await TestLargeParameterSpace();
        await TestStreamingVsBatching();
        await TestMemoryEfficiency();
        await TestParallelProcessing();

        Console.WriteLine("=== Performance Demo Complete ===");
    }

    private static async Task TestSmallParameterSpace()
    {
        Console.WriteLine("1. Small Parameter Space Test (1,000 combinations)");
        Console.WriteLine("   Parameters: 3 integers, 10 values each");

        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 10, 5),
            ParameterDefinition.CreateInteger("param2", 1, 10, 5),
            ParameterDefinition.CreateInteger("param3", 1, 10, 5)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);
        var stats = explorer.GetMemoryEstimate();

        Console.WriteLine($"   Total combinations: {explorer.TotalCombinations:N0}");
        Console.WriteLine($"   Estimated memory per combination: {stats.EstimatedCombinationSize:N0} bytes");

        var stopwatch = Stopwatch.StartNew();
        var count = 0;

        await foreach (var combination in explorer.EnumerateAsync())
        {
            count++;
            // Minimal processing to measure pure enumeration speed
            _ = combination.Count;
        }

        stopwatch.Stop();

        var throughput = count / stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"   Processed: {count:N0} combinations in {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"   Throughput: {throughput:N0} combinations/second");
        Console.WriteLine($"   ✓ Target achieved: {throughput >= 100_000}");
        Console.WriteLine();
    }

    private static async Task TestMediumParameterSpace()
    {
        Console.WriteLine("2. Medium Parameter Space Test (100,000 combinations)");
        Console.WriteLine("   Parameters: 3 integers with varying ranges");

        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 20, 10),
            ParameterDefinition.CreateInteger("param2", 1, 50, 25),
            ParameterDefinition.CreateInteger("param3", 1, 100, 50)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);
        var stats = explorer.GetMemoryEstimate();

        Console.WriteLine($"   Total combinations: {explorer.TotalCombinations:N0}");
        Console.WriteLine($"   Estimated total memory for all combinations: {stats.TotalMemoryForAllCombinations / (1024 * 1024):N1} MB");

        var stopwatch = Stopwatch.StartNew();
        var count = 0;

        await foreach (var combination in explorer.EnumerateAsync())
        {
            count++;
            _ = combination.Count;

            // Stop after processing enough to measure throughput
            if (count >= 50_000) break;
        }

        stopwatch.Stop();

        var throughput = count / stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"   Processed: {count:N0} combinations in {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"   Throughput: {throughput:N0} combinations/second");
        Console.WriteLine($"   ✓ Target achieved: {throughput >= 100_000}");
        Console.WriteLine();
    }

    private static async Task TestLargeParameterSpace()
    {
        Console.WriteLine("3. Large Parameter Space Test (10,000,000 combinations - limited sample)");
        Console.WriteLine("   Parameters: 3 integers with large ranges");

        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 100, 50),
            ParameterDefinition.CreateInteger("param2", 1, 100, 50),
            ParameterDefinition.CreateInteger("param3", 1, 1000, 500)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);
        var stats = explorer.GetMemoryEstimate();

        Console.WriteLine($"   Total combinations: {explorer.TotalCombinations:N0}");
        Console.WriteLine($"   Streaming memory usage: {stats.StreamingMemoryUsage:N0} bytes");

        // Test direct index access for large spaces
        var stopwatch = Stopwatch.StartNew();
        var sampleSize = 10_000;

        for (int i = 0; i < sampleSize; i++)
        {
            var randomIndex = Random.Shared.NextInt64(0, explorer.TotalCombinations!.Value);
            var combination = explorer.GetCombinationByIndex(randomIndex);
            _ = combination.Count;
        }

        stopwatch.Stop();

        var throughput = sampleSize / stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"   Random access: {sampleSize:N0} combinations in {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"   Throughput: {throughput:N0} combinations/second");
        Console.WriteLine($"   ✓ Target achieved: {throughput >= 100_000}");
        Console.WriteLine();
    }

    private static async Task TestStreamingVsBatching()
    {
        Console.WriteLine("4. Streaming vs Batching Performance Comparison");

        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 50, 25),
            ParameterDefinition.CreateInteger("param2", 1, 20, 10),
            ParameterDefinition.CreateInteger("param3", 1, 10, 5)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);

        // Test streaming
        var stopwatch = Stopwatch.StartNew();
        var streamCount = 0;

        await foreach (var combination in explorer.EnumerateAsync())
        {
            streamCount++;
            _ = combination.Count;
            if (streamCount >= 5000) break;
        }

        stopwatch.Stop();
        var streamingThroughput = streamCount / stopwatch.Elapsed.TotalSeconds;

        Console.WriteLine($"   Streaming: {streamCount:N0} combinations in {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"   Streaming throughput: {streamingThroughput:N0} combinations/second");

        // Test batching
        stopwatch.Restart();
        var batchCount = 0;

        await foreach (var batch in explorer.EnumerateBatchesAsync(batchSize: 1000))
        {
            batchCount += batch.Length;
            foreach (var combination in batch)
            {
                _ = combination.Count;
            }
            if (batchCount >= 5000) break;
        }

        stopwatch.Stop();
        var batchingThroughput = batchCount / stopwatch.Elapsed.TotalSeconds;

        Console.WriteLine($"   Batching: {batchCount:N0} combinations in {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"   Batching throughput: {batchingThroughput:N0} combinations/second");
        Console.WriteLine($"   Batching advantage: {batchingThroughput / streamingThroughput:F2}x faster");
        Console.WriteLine();
    }

    private static async Task TestMemoryEfficiency()
    {
        Console.WriteLine("5. Memory Efficiency Test (O(1) memory usage validation)");

        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 100, 50),
            ParameterDefinition.CreateInteger("param2", 1, 100, 50)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);

        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);
        Console.WriteLine($"   Initial memory: {initialMemory / (1024 * 1024):F2} MB");

        var count = 0;
        var maxMemoryGrowth = 0L;

        await foreach (var combination in explorer.EnumerateAsync())
        {
            count++;
            _ = combination.Count;

            if (count % 1000 == 0)
            {
                var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
                var memoryGrowth = currentMemory - initialMemory;
                maxMemoryGrowth = Math.Max(maxMemoryGrowth, memoryGrowth);

                if (count % 5000 == 0)
                {
                    Console.WriteLine($"   Processed {count:N0}: Memory growth = {memoryGrowth / (1024 * 1024):F2} MB");
                }
            }

            if (count >= 10_000) break;
        }

        Console.WriteLine($"   Max memory growth: {maxMemoryGrowth / (1024 * 1024):F2} MB");
        Console.WriteLine($"   ✓ O(1) memory usage confirmed: {maxMemoryGrowth < 10_000_000}"); // < 10MB
        Console.WriteLine();
    }

    private static async Task TestParallelProcessing()
    {
        Console.WriteLine("6. Parallel Processing Performance Test");

        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 25, 13),
            ParameterDefinition.CreateInteger("param2", 1, 20, 10),
            ParameterDefinition.CreateInteger("param3", 1, 20, 10)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);
        var totalCombinations = Math.Min(10_000L, explorer.TotalCombinations ?? 10_000L);

        Console.WriteLine($"   Testing with {totalCombinations:N0} combinations");
        Console.WriteLine($"   Processor count: {Environment.ProcessorCount}");

        // Test sequential processing
        var stopwatch = Stopwatch.StartNew();
        var sequentialQuery = explorer.AsParallelQuery(CancellationToken.None)
            .WithDegreeOfParallelism(1)
            .Take((int)totalCombinations);

        var sequentialCount = sequentialQuery.Count();
        stopwatch.Stop();
        var sequentialThroughput = sequentialCount / stopwatch.Elapsed.TotalSeconds;

        Console.WriteLine($"   Sequential: {sequentialCount:N0} combinations in {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"   Sequential throughput: {sequentialThroughput:N0} combinations/second");

        // Test parallel processing
        stopwatch.Restart();
        var parallelQuery = explorer.AsParallelQuery(CancellationToken.None)
            .Take((int)totalCombinations);

        var parallelCount = parallelQuery.Count();
        stopwatch.Stop();
        var parallelThroughput = parallelCount / stopwatch.Elapsed.TotalSeconds;

        Console.WriteLine($"   Parallel: {parallelCount:N0} combinations in {stopwatch.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"   Parallel throughput: {parallelThroughput:N0} combinations/second");
        Console.WriteLine($"   Parallel speedup: {parallelThroughput / sequentialThroughput:F2}x");
        Console.WriteLine($"   ✓ Target achieved: {parallelThroughput >= 100_000}");
        Console.WriteLine();
    }
}
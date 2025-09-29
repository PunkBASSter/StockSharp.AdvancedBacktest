using System.Collections.Immutable;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Optimization;

namespace StockSharp.AdvancedBacktest.Tests.Core.Optimization;

public class ParameterSpaceExplorerTests
{
    [Fact]
    public void Constructor_WithEmptyParameters_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new ParameterSpaceExplorer(ImmutableArray<ParameterDefinitionBase>.Empty));
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 10, 5)
        );

        // Act
        using var explorer = new ParameterSpaceExplorer(parameters);

        // Assert
        Assert.Single(explorer.Parameters);
        Assert.Equal(10L, explorer.TotalCombinations);
    }

    [Fact]
    public void TotalCombinations_WithMultipleParameters_CalculatesCorrectly()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 5, 3),     // 5 values
            ParameterDefinition.CreateInteger("param2", 10, 12, 11),  // 3 values
            ParameterDefinition.CreateInteger("param3", 0, 1, 0)      // 2 values
        );

        // Act
        using var explorer = new ParameterSpaceExplorer(parameters);

        // Assert
        Assert.Equal(30L, explorer.TotalCombinations); // 5 * 3 * 2 = 30
    }

    [Fact]
    public void TotalCombinations_WithUnboundedParameter_ReturnsNull()
    {
        // Arrange - Parameter without max value cannot be enumerated
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, null, 5)
        );

        // Act
        using var explorer = new ParameterSpaceExplorer(parameters);

        // Assert
        Assert.Null(explorer.TotalCombinations);
    }

    [Fact]
    public async Task EnumerateAsync_WithSimpleParameters_GeneratesCorrectCombinations()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("x", 1, 2, 1),  // Values: 1, 2
            ParameterDefinition.CreateInteger("y", 10, 11, 10) // Values: 10, 11
        );

        using var explorer = new ParameterSpaceExplorer(parameters);
        var combinations = new List<ImmutableDictionary<string, object?>>();

        // Act
        await foreach (var combination in explorer.EnumerateAsync())
        {
            combinations.Add(combination);
        }

        // Assert
        Assert.Equal(4, combinations.Count); // 2 * 2 = 4 combinations

        // Verify all expected combinations exist
        var expectedCombinations = new[]
        {
            new { x = 1, y = 10 },
            new { x = 1, y = 11 },
            new { x = 2, y = 10 },
            new { x = 2, y = 11 }
        };

        foreach (var expected in expectedCombinations)
        {
            Assert.Contains(combinations, c =>
                c["x"]!.Equals(expected.x) && c["y"]!.Equals(expected.y));
        }
    }

    [Fact]
    public async Task EnumerateBatchesAsync_WithValidBatchSize_GeneratesBatches()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param", 1, 10, 5) // 10 combinations
        );

        using var explorer = new ParameterSpaceExplorer(parameters);
        var batches = new List<ImmutableArray<ImmutableDictionary<string, object?>>>();

        // Act
        await foreach (var batch in explorer.EnumerateBatchesAsync(batchSize: 3))
        {
            batches.Add(batch);
        }

        // Assert
        Assert.Equal(4, batches.Count); // 10 combinations in batches of 3: 3, 3, 3, 1
        Assert.Equal(3, batches[0].Length);
        Assert.Equal(3, batches[1].Length);
        Assert.Equal(3, batches[2].Length);
        Assert.Single(batches[3]);

        // Verify total combinations
        var totalCombinations = batches.SelectMany(b => b).Count();
        Assert.Equal(10, totalCombinations);
    }

    [Fact]
    public void EnumerateBatchesAsync_WithInvalidBatchSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param", 1, 5, 3)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            explorer.EnumerateBatchesAsync(batchSize: 0));
    }

    [Fact]
    public void AsParallelQuery_WithValidParameters_ReturnsParallelQuery()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param", 1, 5, 3)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);

        // Act
        var parallelQuery = explorer.AsParallelQuery();

        // Assert
        Assert.NotNull(parallelQuery);
        Assert.Equal(5, parallelQuery.Count());
    }

    [Fact]
    public void GetCombinationByIndex_WithValidIndex_ReturnsCorrectCombination()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("x", 1, 2, 1),  // Values: 1, 2
            ParameterDefinition.CreateInteger("y", 10, 11, 10) // Values: 10, 11
        );

        using var explorer = new ParameterSpaceExplorer(parameters);

        // Act & Assert
        var combination0 = explorer.GetCombinationByIndex(0);
        Assert.Equal(1, combination0["x"]);
        Assert.Equal(10, combination0["y"]);

        var combination1 = explorer.GetCombinationByIndex(1);
        Assert.Equal(1, combination1["x"]);
        Assert.Equal(11, combination1["y"]);

        var combination2 = explorer.GetCombinationByIndex(2);
        Assert.Equal(2, combination2["x"]);
        Assert.Equal(10, combination2["y"]);

        var combination3 = explorer.GetCombinationByIndex(3);
        Assert.Equal(2, combination3["x"]);
        Assert.Equal(11, combination3["y"]);
    }

    [Fact]
    public void GetCombinationByIndex_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param", 1, 3, 2) // 3 combinations
        );

        using var explorer = new ParameterSpaceExplorer(parameters);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => explorer.GetCombinationByIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => explorer.GetCombinationByIndex(3)); // Index 3 is out of range
    }

    [Fact]
    public void GetMemoryEstimate_WithValidParameters_ReturnsStatistics()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param1", 1, 10, 5),
            ParameterDefinition.CreateDecimal("param2", 0.1m, 1.0m, 0.5m, 0.1m)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);

        // Act
        var stats = explorer.GetMemoryEstimate();

        // Assert
        Assert.Equal(2, stats.ParameterCount);
        Assert.True(stats.TotalCombinations > 0);
        Assert.True(stats.EstimatedCombinationSize > 0);
        Assert.Equal(stats.EstimatedCombinationSize, stats.StreamingMemoryUsage);
        Assert.True(stats.TotalMemoryForAllCombinations > 0);
    }

    [Fact]
    public async Task EnumerateAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param", 1, 1000, 500) // Large space
        );

        using var explorer = new ParameterSpaceExplorer(parameters);
        using var cts = new CancellationTokenSource();

        var count = 0;

        try
        {
            // Act - Cancel after processing some combinations
            await foreach (var combination in explorer.EnumerateAsync(cts.Token))
            {
                count++;
                if (count == 10)
                {
                    cts.Cancel(); // Cancel enumeration
                }
            }

            Assert.Fail("Expected OperationCanceledException was not thrown");
        }
        catch (OperationCanceledException)
        {
            // Assert - Cancellation was respected
            Assert.Equal(10, count);
        }
    }

    [Theory]
    [InlineData(1, 10, 1, 10)]
    [InlineData(1, 5, 2, 3)]
    [InlineData(10, 20, 5, 3)]
    public void ParameterGeneration_WithDifferentSteps_GeneratesCorrectCount(
        int min, int max, int step, int expectedCount)
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("param", min, max, min, step)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);

        // Act
        var actualCount = explorer.TotalCombinations;

        // Assert
        Assert.Equal(expectedCount, actualCount);
    }

    [Fact]
    public async Task PerformanceTest_EnumerateThousandCombinations_CompletesQuickly()
    {
        // Arrange
        var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
            ParameterDefinition.CreateInteger("x", 1, 10, 5),   // 10 values
            ParameterDefinition.CreateInteger("y", 1, 10, 5),   // 10 values
            ParameterDefinition.CreateInteger("z", 1, 10, 5)    // 10 values (1000 total)
        );

        using var explorer = new ParameterSpaceExplorer(parameters);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var count = 0;
        await foreach (var combination in explorer.EnumerateAsync())
        {
            count++;
            // Simulate minimal processing
            _ = combination.Count;
        }

        stopwatch.Stop();

        // Assert
        Assert.Equal(1000, count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Enumeration took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");

        // Calculate throughput
        var combinationsPerSecond = count / stopwatch.Elapsed.TotalSeconds;
        Assert.True(combinationsPerSecond > 1000,
            $"Throughput was {combinationsPerSecond:F0} combinations/sec, expected > 1000");
    }
}
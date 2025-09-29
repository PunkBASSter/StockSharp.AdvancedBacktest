using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

namespace StockSharp.AdvancedBacktest.Core.Optimization;

/// <summary>
/// High-performance parameter space explorer supporting streaming enumeration of parameter combinations.
/// Targets 100,000+ parameter combinations per second with O(1) memory usage through streaming algorithms.
/// </summary>
public sealed class ParameterSpaceExplorer : IDisposable
{
    private readonly ImmutableArray<ParameterDefinitionBase> _parameters;
    private readonly ArrayPool<object?> _objectPool;
    private readonly ArrayPool<long> _indexPool;
    private bool _disposed;

    /// <summary>
    /// Gets the estimated total number of parameter combinations in the space.
    /// Returns null if the count cannot be determined or is infinite.
    /// </summary>
    public long? TotalCombinations { get; private set; }

    /// <summary>
    /// Gets the parameters that define this optimization space.
    /// </summary>
    public ImmutableArray<ParameterDefinitionBase> Parameters => _parameters;

    /// <summary>
    /// Initializes a new parameter space explorer with the specified parameter definitions.
    /// </summary>
    /// <param name="parameters">Parameter definitions to explore</param>
    /// <exception cref="ArgumentException">Thrown when no parameters are provided</exception>
    public ParameterSpaceExplorer(ImmutableArray<ParameterDefinitionBase> parameters)
    {
        if (parameters.IsEmpty)
            throw new ArgumentException("At least one parameter must be provided", nameof(parameters));

        _parameters = parameters;
        _objectPool = ArrayPool<object?>.Shared;
        _indexPool = ArrayPool<long>.Shared;

        CalculateTotalCombinations();
    }

    /// <summary>
    /// Convenience constructor for parameter definitions collections.
    /// </summary>
    /// <param name="parameters">Parameter definitions to explore</param>
    public ParameterSpaceExplorer(IEnumerable<ParameterDefinitionBase> parameters)
        : this(parameters.ToImmutableArray()) { }

    /// <summary>
    /// Enumerates all parameter combinations using streaming algorithm with O(1) memory usage.
    /// Generates parameter combinations at high speed without storing intermediate results.
    /// </summary>
    /// <param name="cancellationToken">Token for cancellation support</param>
    /// <returns>Stream of parameter combinations as dictionaries</returns>
    public IAsyncEnumerable<ImmutableDictionary<string, object?>> EnumerateAsync(
        CancellationToken cancellationToken = default)
    {
        return EnumerateInternalAsync(cancellationToken);
    }

    /// <summary>
    /// Enumerates parameter combinations in parallel batches for high-performance scenarios.
    /// Uses work-stealing queues and parallel processing to maximize throughput.
    /// </summary>
    /// <param name="batchSize">Size of each batch (default: 1000)</param>
    /// <param name="maxDegreeOfParallelism">Maximum parallel threads (default: Environment.ProcessorCount)</param>
    /// <param name="cancellationToken">Token for cancellation support</param>
    /// <returns>Stream of parameter combination batches</returns>
    public IAsyncEnumerable<ImmutableArray<ImmutableDictionary<string, object?>>> EnumerateBatchesAsync(
        int batchSize = 1000,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive");

        if (maxDegreeOfParallelism == -1)
            maxDegreeOfParallelism = Environment.ProcessorCount;

        return EnumerateBatchesInternalAsync(batchSize, maxDegreeOfParallelism, cancellationToken);
    }

    /// <summary>
    /// Creates a parallel query for high-performance LINQ-style operations over parameter combinations.
    /// Suitable for filtering, transformation, and aggregation operations.
    /// </summary>
    /// <param name="cancellationToken">Token for cancellation support</param>
    /// <returns>Parallel query over parameter combinations</returns>
    public ParallelQuery<ImmutableDictionary<string, object?>> AsParallelQuery(
        CancellationToken cancellationToken = default)
    {
        return EnumerateAsync(cancellationToken)
            .ToBlockingEnumerable(cancellationToken)
            .AsParallel()
            .WithCancellation(cancellationToken);
    }

    /// <summary>
    /// Gets a specific parameter combination by its index in the Cartesian product space.
    /// Useful for distributed processing where each worker handles a range of indices.
    /// </summary>
    /// <param name="index">Zero-based index of the combination</param>
    /// <returns>Parameter combination at the specified index</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is invalid</exception>
    public ImmutableDictionary<string, object?> GetCombinationByIndex(long index)
    {
        if (index < 0 || (TotalCombinations.HasValue && index >= TotalCombinations.Value))
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range");

        var indices = _indexPool.Rent(_parameters.Length);
        try
        {
            CalculateIndicesFromCombinationIndex(index, indices);
            return CreateCombinationFromIndices(indices);
        }
        finally
        {
            _indexPool.Return(indices);
        }
    }

    /// <summary>
    /// Estimates the memory usage for enumerating the entire parameter space.
    /// </summary>
    /// <returns>Estimated memory usage statistics</returns>
    public ParameterSpaceMemoryStats GetMemoryEstimate()
    {
        var combinationSize = EstimateCombinationMemorySize();
        var totalMemoryForAll = TotalCombinations.HasValue
            ? TotalCombinations.Value * combinationSize
            : (long?)null;

        return new ParameterSpaceMemoryStats(
            ParameterCount: _parameters.Length,
            TotalCombinations: TotalCombinations,
            EstimatedCombinationSize: combinationSize,
            StreamingMemoryUsage: combinationSize, // O(1) for streaming
            TotalMemoryForAllCombinations: totalMemoryForAll
        );
    }

    private async IAsyncEnumerable<ImmutableDictionary<string, object?>> EnumerateInternalAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!TotalCombinations.HasValue)
        {
            // Cannot enumerate infinite or unknown parameter spaces
            yield break;
        }

        // Pre-compute parameter value arrays for performance
        var parameterValues = new List<object?[]>(_parameters.Length);
        foreach (var parameter in _parameters)
        {
            var values = parameter.GenerateValidValues().ToArray();
            if (values.Length == 0)
                yield break; // Empty parameter space

            parameterValues.Add(values);
        }

        var indices = _indexPool.Rent(_parameters.Length);
        try
        {
            Array.Clear(indices, 0, _parameters.Length);

            long combinationCount = 0;
            var maxCombinations = TotalCombinations.Value;

            while (combinationCount < maxCombinations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return CreateCombinationFromParameterValues(parameterValues, indices);

                combinationCount++;

                // Increment indices in Cartesian product manner
                if (!IncrementIndices(indices, _parameters.Length, parameterValues))
                    break;

                // Yield control periodically for responsiveness
                if (combinationCount % 1000 == 0)
                    await Task.Yield();
            }
        }
        finally
        {
            _indexPool.Return(indices);
        }
    }

    private async IAsyncEnumerable<ImmutableArray<ImmutableDictionary<string, object?>>> EnumerateBatchesInternalAsync(
        int batchSize,
        int maxDegreeOfParallelism,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!TotalCombinations.HasValue)
            yield break;

        var totalCombinations = TotalCombinations.Value;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        for (long startIndex = 0; startIndex < totalCombinations; startIndex += batchSize)
        {
            var endIndex = Math.Min(startIndex + batchSize, totalCombinations);
            var currentBatchSize = (int)(endIndex - startIndex);

            var batch = new ImmutableDictionary<string, object?>[currentBatchSize];

            await Task.Run(() =>
            {
                Parallel.For(0, currentBatchSize, parallelOptions, i =>
                {
                    var globalIndex = startIndex + i;
                    batch[i] = GetCombinationByIndex(globalIndex);
                });
            }, cancellationToken);

            yield return batch.ToImmutableArray();
        }
    }

    private void CalculateTotalCombinations()
    {
        try
        {
            long total = 1;
            foreach (var parameter in _parameters)
            {
                var count = parameter.GetValidValueCount();
                if (!count.HasValue)
                {
                    TotalCombinations = null;
                    return;
                }

                checked
                {
                    total *= count.Value;
                }
            }
            TotalCombinations = total;
        }
        catch (OverflowException)
        {
            // Parameter space is too large to enumerate
            TotalCombinations = null;
        }
    }

    private void CalculateIndicesFromCombinationIndex(long combinationIndex, long[] indices)
    {
        var remaining = combinationIndex;

        for (int i = _parameters.Length - 1; i >= 0; i--)
        {
            var parameterCount = _parameters[i].GetValidValueCount() ?? 1;
            indices[i] = remaining % parameterCount;
            remaining /= parameterCount;
        }
    }

    private ImmutableDictionary<string, object?> CreateCombinationFromIndices(long[] indices)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, object?>();

        for (int i = 0; i < _parameters.Length; i++)
        {
            var parameter = _parameters[i];
            var values = parameter.GenerateValidValues().ToArray();
            var valueIndex = (int)indices[i];

            if (valueIndex < values.Length)
            {
                builder[parameter.Name] = values[valueIndex];
            }
        }

        return builder.ToImmutable();
    }

    private ImmutableDictionary<string, object?> CreateCombinationFromParameterValues(
        List<object?[]> parameterValues, long[] indices)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, object?>();

        for (int i = 0; i < _parameters.Length; i++)
        {
            var parameter = _parameters[i];
            var values = parameterValues[i];
            var valueIndex = (int)indices[i];

            if (valueIndex >= 0 && valueIndex < values.Length)
            {
                builder[parameter.Name] = values[valueIndex];
            }
        }

        return builder.ToImmutable();
    }

    private static bool IncrementIndices(long[] indices, int actualLength, List<object?[]> parameterValues)
    {
        if (actualLength != parameterValues.Count)
            throw new InvalidOperationException($"Actual length ({actualLength}) doesn't match parameter values count ({parameterValues.Count})");

        for (int i = actualLength - 1; i >= 0; i--)
        {
            indices[i]++;
            if (indices[i] < parameterValues[i].Length)
                return true;

            indices[i] = 0;
        }
        return false; // All combinations exhausted
    }

    private long EstimateCombinationMemorySize()
    {
        // Rough estimate based on dictionary overhead and parameter values
        const long dictionaryOverhead = 64; // Dictionary structure overhead
        const long keyValuePairOverhead = 24; // Per key-value pair overhead
        const long stringOverhead = 20; // String overhead

        long totalSize = dictionaryOverhead;

        foreach (var parameter in _parameters)
        {
            totalSize += keyValuePairOverhead;
            totalSize += stringOverhead + parameter.Name.Length * 2; // String key

            // Estimate value size based on type
            totalSize += parameter.Type.Name switch
            {
                "Int32" => 4,
                "Int64" => 8,
                "Double" => 8,
                "Decimal" => 16,
                "Single" => 4,
                _ => 8 // Default estimate
            };
        }

        return totalSize;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // ArrayPools are shared and don't need explicit disposal
            _disposed = true;
        }
    }
}

/// <summary>
/// Memory usage statistics for parameter space exploration.
/// </summary>
/// <param name="ParameterCount">Number of parameters in the space</param>
/// <param name="TotalCombinations">Total number of parameter combinations (null if infinite/unknown)</param>
/// <param name="EstimatedCombinationSize">Estimated memory size per combination in bytes</param>
/// <param name="StreamingMemoryUsage">Memory usage for streaming enumeration (O(1))</param>
/// <param name="TotalMemoryForAllCombinations">Total memory required to store all combinations (null if infinite/unknown)</param>
public readonly record struct ParameterSpaceMemoryStats(
    int ParameterCount,
    long? TotalCombinations,
    long EstimatedCombinationSize,
    long StreamingMemoryUsage,
    long? TotalMemoryForAllCombinations
);
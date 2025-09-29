using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Configuration.Serialization;
using StockSharp.AdvancedBacktest.Core.Configuration.Validation;

namespace StockSharp.AdvancedBacktest.Core.Configuration.Performance;

/// <summary>
/// Performance benchmarks for parameter hashing and JSON serialization.
/// Validates Phase 2D acceptance criteria: 10,000+ hashes/second, 50MB/second JSON serialization.
/// </summary>
public sealed class ParameterPerformanceBenchmark : IDisposable
{
    private readonly ParameterHashGenerator _hashGenerator;
    private readonly ParameterValidator _validator;
    private readonly ParameterSet[] _testParameterSets;
    private readonly Random _random;
    private bool _disposed;

    public ParameterPerformanceBenchmark(int testSetSize = 1000)
    {
        _hashGenerator = new ParameterHashGenerator();
        _validator = new ParameterValidator();
        _random = new Random(42); // Fixed seed for reproducible results
        _testParameterSets = GenerateTestParameterSets(testSetSize);
    }

    /// <summary>
    /// Runs comprehensive hashing performance benchmark.
    /// Target: 10,000+ hashes/second.
    /// </summary>
    public HashingBenchmarkResult BenchmarkHashing(int iterations = 10000, TimeSpan? maxDuration = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var maxTime = maxDuration ?? TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();
        var hashCount = 0;
        var collisions = 0;
        var hashSet = new HashSet<string>();
        var errors = new List<string>();

        try
        {
            for (int i = 0; i < iterations && stopwatch.Elapsed < maxTime; i++)
            {
                var parameterSet = _testParameterSets[i % _testParameterSets.Length];

                try
                {
                    var hash = parameterSet.GenerateHash();
                    hashCount++;

                    if (!hashSet.Add(hash))
                    {
                        collisions++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Hash {i}: {ex.Message}");
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        var hashesPerSecond = hashCount / stopwatch.Elapsed.TotalSeconds;
        var avgHashTimeMs = stopwatch.Elapsed.TotalMilliseconds / hashCount;

        return new HashingBenchmarkResult(
            HashCount: hashCount,
            ElapsedTime: stopwatch.Elapsed,
            HashesPerSecond: hashesPerSecond,
            AverageHashTimeMs: avgHashTimeMs,
            CollisionCount: collisions,
            CollisionRate: (double)collisions / hashCount,
            ErrorCount: errors.Count,
            Errors: errors.ToImmutableArray(),
            MeetsTarget: hashesPerSecond >= 10000
        );
    }

    /// <summary>
    /// Runs comprehensive JSON serialization performance benchmark.
    /// Target: 50MB/second serialization throughput.
    /// </summary>
    public SerializationBenchmarkResult BenchmarkSerialization(int iterations = 1000, TimeSpan? maxDuration = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var maxTime = maxDuration ?? TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();
        var serializationCount = 0;
        var totalBytes = 0L;
        var errors = new List<string>();

        // Test different serialization modes
        var optimizedStopwatch = Stopwatch.StartNew();
        var optimizedBytes = 0L;
        var optimizedCount = 0;

        var cachingStopwatch = Stopwatch.StartNew();
        var cachingBytes = 0L;
        var cachingCount = 0;

        try
        {
            for (int i = 0; i < iterations && stopwatch.Elapsed < maxTime; i++)
            {
                var parameterSet = _testParameterSets[i % _testParameterSets.Length];

                try
                {
                    // Test optimized serialization
                    optimizedStopwatch.Restart();
                    var optimizedJson = parameterSet.ToJsonOptimized();
                    optimizedStopwatch.Stop();

                    var optimizedByteCount = Encoding.UTF8.GetByteCount(optimizedJson);
                    optimizedBytes += optimizedByteCount;
                    optimizedCount++;

                    // Test caching serialization
                    cachingStopwatch.Restart();
                    var cachingJson = parameterSet.ToJsonForCaching();
                    cachingStopwatch.Stop();

                    var cachingByteCount = Encoding.UTF8.GetByteCount(cachingJson);
                    cachingBytes += cachingByteCount;
                    cachingCount++;

                    serializationCount += 2;
                    totalBytes += optimizedByteCount + cachingByteCount;
                }
                catch (Exception ex)
                {
                    errors.Add($"Serialization {i}: {ex.Message}");
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        var totalMB = totalBytes / (1024.0 * 1024.0);
        var mbPerSecond = totalMB / stopwatch.Elapsed.TotalSeconds;

        var optimizedMB = optimizedBytes / (1024.0 * 1024.0);
        var optimizedMbPerSecond = optimizedMB / optimizedStopwatch.Elapsed.TotalSeconds;

        var cachingMB = cachingBytes / (1024.0 * 1024.0);
        var cachingMbPerSecond = cachingMB / cachingStopwatch.Elapsed.TotalSeconds;

        return new SerializationBenchmarkResult(
            SerializationCount: serializationCount,
            ElapsedTime: stopwatch.Elapsed,
            TotalBytes: totalBytes,
            MegabytesPerSecond: mbPerSecond,
            OptimizedMegabytesPerSecond: optimizedMbPerSecond,
            CachingMegabytesPerSecond: cachingMbPerSecond,
            ErrorCount: errors.Count,
            Errors: errors.ToImmutableArray(),
            MeetsTarget: mbPerSecond >= 50
        );
    }

    /// <summary>
    /// Runs hash validation performance benchmark.
    /// </summary>
    public HashValidationBenchmarkResult BenchmarkHashValidation(int iterations = 5000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var stopwatch = Stopwatch.StartNew();
        var validationCount = 0;
        var validHashes = 0;
        var invalidHashes = 0;
        var errors = new List<string>();

        try
        {
            for (int i = 0; i < iterations; i++)
            {
                var parameterSet = _testParameterSets[i % _testParameterSets.Length];

                try
                {
                    var hash = parameterSet.GenerateHash();
                    var validationResult = parameterSet.ValidateHash(hash);
                    validationCount++;

                    if (validationResult.IsValid)
                        validHashes++;
                    else
                        invalidHashes++;

                    // Test with invalid hash
                    var invalidHash = "invalid_hash_" + i;
                    var invalidValidationResult = parameterSet.ValidateHash(invalidHash);
                    validationCount++;

                    if (!invalidValidationResult.IsValid)
                        invalidHashes++;
                    else
                        errors.Add($"Invalid hash {invalidHash} was incorrectly validated as valid");
                }
                catch (Exception ex)
                {
                    errors.Add($"Validation {i}: {ex.Message}");
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        var validationsPerSecond = validationCount / stopwatch.Elapsed.TotalSeconds;

        return new HashValidationBenchmarkResult(
            ValidationCount: validationCount,
            ElapsedTime: stopwatch.Elapsed,
            ValidationsPerSecond: validationsPerSecond,
            ValidHashes: validHashes,
            InvalidHashes: invalidHashes,
            ErrorCount: errors.Count,
            Errors: errors.ToImmutableArray()
        );
    }

    /// <summary>
    /// Runs full benchmark suite covering all Phase 2D performance requirements.
    /// </summary>
    public ComprehensiveBenchmarkResult RunComprehensiveBenchmark()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var hashingResult = BenchmarkHashing();
        var serializationResult = BenchmarkSerialization();
        var validationResult = BenchmarkHashValidation();

        stopwatch.Stop();

        var allTargetsMet = hashingResult.MeetsTarget &&
                           serializationResult.MeetsTarget;

        return new ComprehensiveBenchmarkResult(
            StartTime: startTime,
            ElapsedTime: stopwatch.Elapsed,
            HashingResult: hashingResult,
            SerializationResult: serializationResult,
            ValidationResult: validationResult,
            AllTargetsMet: allTargetsMet
        );
    }

    /// <summary>
    /// Generates test parameter sets for benchmarking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ParameterSet[] GenerateTestParameterSets(int count)
    {
        var builder = new ParameterSetBuilder();

        // Add various parameter types for comprehensive testing (no step validation for simplicity)
        builder
            .AddNumeric<int>("IntParam", 1, 100, 50, null, "Integer parameter")
            .AddNumeric<double>("DoubleParam", 0.0, 10.0, 5.0, null, "Double parameter")
            .AddNumeric<decimal>("DecimalParam", 0.00m, 1000.00m, 100.00m, null, "Decimal parameter")
            .AddNumeric<long>("LongParam", 0L, 1000000L, 50000L, null, "Long parameter")
            .AddNumeric<float>("FloatParam", 0.0f, 100.0f, 50.0f, null, "Float parameter");

        var basePameterSet = builder.Build(_validator, _hashGenerator);
        var parameterSets = new ParameterSet[count];

        for (int i = 0; i < count; i++)
        {
            var clone = basePameterSet.Clone();

            // Randomize values for variety
            clone.SetValue("IntParam", _random.Next(1, 101));
            clone.SetValue("DoubleParam", _random.NextDouble() * 10.0);
            clone.SetValue("DecimalParam", (decimal)(_random.NextDouble() * 1000.00));
            clone.SetValue("LongParam", _random.NextInt64(0L, 1000000L));
            clone.SetValue("FloatParam", (float)(_random.NextDouble() * 100.0));

            parameterSets[i] = clone;
        }

        basePameterSet.Dispose();
        return parameterSets;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _hashGenerator?.Dispose();
            foreach (var parameterSet in _testParameterSets)
            {
                parameterSet?.Dispose();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Result of hashing performance benchmark.
/// </summary>
public readonly record struct HashingBenchmarkResult(
    int HashCount,
    TimeSpan ElapsedTime,
    double HashesPerSecond,
    double AverageHashTimeMs,
    int CollisionCount,
    double CollisionRate,
    int ErrorCount,
    ImmutableArray<string> Errors,
    bool MeetsTarget
);

/// <summary>
/// Result of JSON serialization performance benchmark.
/// </summary>
public readonly record struct SerializationBenchmarkResult(
    int SerializationCount,
    TimeSpan ElapsedTime,
    long TotalBytes,
    double MegabytesPerSecond,
    double OptimizedMegabytesPerSecond,
    double CachingMegabytesPerSecond,
    int ErrorCount,
    ImmutableArray<string> Errors,
    bool MeetsTarget
);

/// <summary>
/// Result of hash validation performance benchmark.
/// </summary>
public readonly record struct HashValidationBenchmarkResult(
    int ValidationCount,
    TimeSpan ElapsedTime,
    double ValidationsPerSecond,
    int ValidHashes,
    int InvalidHashes,
    int ErrorCount,
    ImmutableArray<string> Errors
);

/// <summary>
/// Comprehensive benchmark result covering all Phase 2D requirements.
/// </summary>
public readonly record struct ComprehensiveBenchmarkResult(
    DateTimeOffset StartTime,
    TimeSpan ElapsedTime,
    HashingBenchmarkResult HashingResult,
    SerializationBenchmarkResult SerializationResult,
    HashValidationBenchmarkResult ValidationResult,
    bool AllTargetsMet
);
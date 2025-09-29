using System.Text;

namespace StockSharp.AdvancedBacktest.Core.Configuration.Performance;

/// <summary>
/// Console runner for Phase 2D performance benchmarks.
/// Validates acceptance criteria: 10,000+ hashes/second, 50MB/second JSON serialization.
/// </summary>
public static class BenchmarkRunner
{
    /// <summary>
    /// Runs all Phase 2D benchmarks and reports results.
    /// </summary>
    public static void RunAllBenchmarks()
    {
        Console.WriteLine("=== Phase 2D Performance Benchmarks ===");
        Console.WriteLine($"Start Time: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        using var benchmark = new ParameterPerformanceBenchmark(testSetSize: 2000);

        try
        {
            var results = benchmark.RunComprehensiveBenchmark();
            PrintComprehensiveResults(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Benchmark failed: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Runs only the hashing benchmark.
    /// </summary>
    public static void RunHashingBenchmark()
    {
        Console.WriteLine("=== Parameter Hashing Benchmark ===");
        Console.WriteLine($"Target: 10,000+ hashes/second");
        Console.WriteLine();

        using var benchmark = new ParameterPerformanceBenchmark();

        try
        {
            var result = benchmark.BenchmarkHashing(iterations: 15000);
            PrintHashingResults(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Hashing benchmark failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs only the JSON serialization benchmark.
    /// </summary>
    public static void RunSerializationBenchmark()
    {
        Console.WriteLine("=== JSON Serialization Benchmark ===");
        Console.WriteLine($"Target: 50MB/second serialization throughput");
        Console.WriteLine();

        using var benchmark = new ParameterPerformanceBenchmark();

        try
        {
            var result = benchmark.BenchmarkSerialization(iterations: 2000);
            PrintSerializationResults(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Serialization benchmark failed: {ex.Message}");
        }
    }

    private static void PrintComprehensiveResults(ComprehensiveBenchmarkResult results)
    {
        Console.WriteLine("=== COMPREHENSIVE BENCHMARK RESULTS ===");
        Console.WriteLine($"Total Execution Time: {results.ElapsedTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"All Targets Met: {(results.AllTargetsMet ? "✓ PASS" : "✗ FAIL")}");
        Console.WriteLine();

        // Hashing Results
        Console.WriteLine("--- HASHING PERFORMANCE ---");
        PrintHashingResults(results.HashingResult);
        Console.WriteLine();

        // Serialization Results
        Console.WriteLine("--- SERIALIZATION PERFORMANCE ---");
        PrintSerializationResults(results.SerializationResult);
        Console.WriteLine();

        // Validation Results
        Console.WriteLine("--- HASH VALIDATION PERFORMANCE ---");
        PrintValidationResults(results.ValidationResult);
        Console.WriteLine();

        // Summary
        Console.WriteLine("=== PHASE 2D ACCEPTANCE CRITERIA ===");
        Console.WriteLine($"1. Hashing Performance: {(results.HashingResult.MeetsTarget ? "✓ PASS" : "✗ FAIL")} " +
                         $"({results.HashingResult.HashesPerSecond:F0} hashes/sec, target: 10,000+)");
        Console.WriteLine($"2. Serialization Performance: {(results.SerializationResult.MeetsTarget ? "✓ PASS" : "✗ FAIL")} " +
                         $"({results.SerializationResult.MegabytesPerSecond:F1} MB/sec, target: 50+)");
        Console.WriteLine();

        if (results.AllTargetsMet)
        {
            Console.WriteLine("🎉 Phase 2D implementation SUCCESSFULLY meets all performance criteria!");
        }
        else
        {
            Console.WriteLine("⚠️  Phase 2D implementation does NOT meet all performance criteria.");
        }
    }

    private static void PrintHashingResults(HashingBenchmarkResult result)
    {
        Console.WriteLine($"Hash Count: {result.HashCount:N0}");
        Console.WriteLine($"Execution Time: {result.ElapsedTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"Hashes/Second: {result.HashesPerSecond:F0} ({(result.MeetsTarget ? "✓ PASS" : "✗ FAIL")})");
        Console.WriteLine($"Average Hash Time: {result.AverageHashTimeMs:F3} ms");
        Console.WriteLine($"Collision Count: {result.CollisionCount}");
        Console.WriteLine($"Collision Rate: {result.CollisionRate:P2}");

        if (result.ErrorCount > 0)
        {
            Console.WriteLine($"Errors: {result.ErrorCount}");
            foreach (var error in result.Errors.Take(5))
            {
                Console.WriteLine($"  - {error}");
            }
            if (result.Errors.Length > 5)
            {
                Console.WriteLine($"  ... and {result.Errors.Length - 5} more errors");
            }
        }
    }

    private static void PrintSerializationResults(SerializationBenchmarkResult result)
    {
        Console.WriteLine($"Serialization Count: {result.SerializationCount:N0}");
        Console.WriteLine($"Execution Time: {result.ElapsedTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"Total Bytes: {result.TotalBytes:N0} ({result.TotalBytes / (1024.0 * 1024.0):F1} MB)");
        Console.WriteLine($"Overall MB/Second: {result.MegabytesPerSecond:F1} ({(result.MeetsTarget ? "✓ PASS" : "✗ FAIL")})");
        Console.WriteLine($"Optimized Mode: {result.OptimizedMegabytesPerSecond:F1} MB/sec");
        Console.WriteLine($"Caching Mode: {result.CachingMegabytesPerSecond:F1} MB/sec");

        if (result.ErrorCount > 0)
        {
            Console.WriteLine($"Errors: {result.ErrorCount}");
            foreach (var error in result.Errors.Take(3))
            {
                Console.WriteLine($"  - {error}");
            }
            if (result.Errors.Length > 3)
            {
                Console.WriteLine($"  ... and {result.Errors.Length - 3} more errors");
            }
        }
    }

    private static void PrintValidationResults(HashValidationBenchmarkResult result)
    {
        Console.WriteLine($"Validation Count: {result.ValidationCount:N0}");
        Console.WriteLine($"Execution Time: {result.ElapsedTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"Validations/Second: {result.ValidationsPerSecond:F0}");
        Console.WriteLine($"Valid Hashes: {result.ValidHashes}");
        Console.WriteLine($"Invalid Hashes: {result.InvalidHashes}");

        if (result.ErrorCount > 0)
        {
            Console.WriteLine($"Errors: {result.ErrorCount}");
            foreach (var error in result.Errors.Take(3))
            {
                Console.WriteLine($"  - {error}");
            }
            if (result.Errors.Length > 3)
            {
                Console.WriteLine($"  ... and {result.Errors.Length - 3} more errors");
            }
        }
    }

    /// <summary>
    /// Creates a detailed benchmark report as a string.
    /// </summary>
    public static string GenerateBenchmarkReport()
    {
        var report = new StringBuilder();
        report.AppendLine("# Phase 2D Performance Benchmark Report");
        report.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        report.AppendLine();

        using var benchmark = new ParameterPerformanceBenchmark(testSetSize: 1500);

        try
        {
            var results = benchmark.RunComprehensiveBenchmark();

            report.AppendLine("## Performance Targets");
            report.AppendLine("- **Hashing Performance**: 10,000+ hashes/second");
            report.AppendLine("- **JSON Serialization**: 50MB/second throughput");
            report.AppendLine();

            report.AppendLine("## Results Summary");
            report.AppendLine($"- **Total Execution Time**: {results.ElapsedTime.TotalSeconds:F2} seconds");
            report.AppendLine($"- **All Targets Met**: {(results.AllTargetsMet ? "✅ YES" : "❌ NO")}");
            report.AppendLine();

            report.AppendLine("### Hashing Performance");
            report.AppendLine($"- **Hash Count**: {results.HashingResult.HashCount:N0}");
            report.AppendLine($"- **Hashes/Second**: {results.HashingResult.HashesPerSecond:F0} " +
                             $"{(results.HashingResult.MeetsTarget ? "✅" : "❌")}");
            report.AppendLine($"- **Average Hash Time**: {results.HashingResult.AverageHashTimeMs:F3} ms");
            report.AppendLine($"- **Collision Rate**: {results.HashingResult.CollisionRate:P2}");
            report.AppendLine();

            report.AppendLine("### JSON Serialization Performance");
            report.AppendLine($"- **Serialization Count**: {results.SerializationResult.SerializationCount:N0}");
            report.AppendLine($"- **Overall Throughput**: {results.SerializationResult.MegabytesPerSecond:F1} MB/sec " +
                             $"{(results.SerializationResult.MeetsTarget ? "✅" : "❌")}");
            report.AppendLine($"- **Optimized Mode**: {results.SerializationResult.OptimizedMegabytesPerSecond:F1} MB/sec");
            report.AppendLine($"- **Caching Mode**: {results.SerializationResult.CachingMegabytesPerSecond:F1} MB/sec");
            report.AppendLine();

            report.AppendLine("### Hash Validation Performance");
            report.AppendLine($"- **Validation Count**: {results.ValidationResult.ValidationCount:N0}");
            report.AppendLine($"- **Validations/Second**: {results.ValidationResult.ValidationsPerSecond:F0}");
            report.AppendLine();

            if (results.AllTargetsMet)
            {
                report.AppendLine("## Conclusion");
                report.AppendLine("🎉 **Phase 2D implementation successfully meets all performance criteria!**");
            }
            else
            {
                report.AppendLine("## Conclusion");
                report.AppendLine("⚠️ **Phase 2D implementation does not meet all performance criteria.**");
                report.AppendLine("Further optimization may be required.");
            }
        }
        catch (Exception ex)
        {
            report.AppendLine("## Error");
            report.AppendLine($"Benchmark execution failed: {ex.Message}");
        }

        return report.ToString();
    }
}
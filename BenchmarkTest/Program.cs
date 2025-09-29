using StockSharp.AdvancedBacktest.Core.Configuration.Performance;

namespace BenchmarkTest;

/// <summary>
/// Simple test console application to verify Phase 2D benchmarks.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Phase 2D Performance Benchmark Test");
        Console.WriteLine("====================================");

        try
        {
            // Run a quick hashing benchmark
            Console.WriteLine("Running hashing benchmark...");
            BenchmarkRunner.RunHashingBenchmark();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run a quick serialization benchmark
            Console.WriteLine("Running serialization benchmark...");
            BenchmarkRunner.RunSerializationBenchmark();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Generate a comprehensive report
            Console.WriteLine("Generating comprehensive report...");
            var report = BenchmarkRunner.GenerateBenchmarkReport();
            Console.WriteLine(report);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nBenchmark completed successfully!");
    }
}
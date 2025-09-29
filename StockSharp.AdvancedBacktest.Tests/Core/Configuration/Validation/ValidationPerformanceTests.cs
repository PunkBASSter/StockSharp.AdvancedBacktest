using System.Diagnostics;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Configuration.Validation;

namespace StockSharp.AdvancedBacktest.Tests.Core.Configuration.Validation;

/// <summary>
/// Performance tests for the advanced validation framework.
/// Verifies that the system can achieve 1M+ parameter validations per second.
/// </summary>
public class ValidationPerformanceTests
{
    private const int PERFORMANCE_TARGET_PER_SECOND = 1_000_000; // 1M+ validations/second target
    private const int WARMUP_ITERATIONS = 10_000;
    private const int BENCHMARK_ITERATIONS = 100_000;

    [Fact]
    public void RangeValidationRule_ShouldAchieve1MillionValidationsPerSecond()
    {
        // Arrange
        var rule = new RangeValidationRule<int>(1, 1000);
        var testValues = GenerateTestValues<int>(BENCHMARK_ITERATIONS, i => i % 1001); // Mix of valid and invalid

        // Warmup
        for (int i = 0; i < WARMUP_ITERATIONS; i++)
        {
            rule.IsValid(testValues[i % testValues.Length]);
        }

        // Benchmark
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
        {
            rule.IsValid(testValues[i % testValues.Length]);
        }

        stopwatch.Stop();

        // Assert
        var validationsPerSecond = BENCHMARK_ITERATIONS / stopwatch.Elapsed.TotalSeconds;
        Assert.True(validationsPerSecond >= PERFORMANCE_TARGET_PER_SECOND,
            $"RangeValidationRule achieved {validationsPerSecond:N0} validations/second, expected at least {PERFORMANCE_TARGET_PER_SECOND:N0}");
    }

    [Fact]
    public void StepValidationRule_ShouldAchieve1MillionValidationsPerSecond()
    {
        // Arrange
        var rule = new StepValidationRule<decimal>(0.01m); // Tick increments
        var testValues = GenerateTestValues<decimal>(BENCHMARK_ITERATIONS, i => i * 0.01m); // All valid step values

        // Warmup
        for (int i = 0; i < WARMUP_ITERATIONS; i++)
        {
            rule.IsValid(testValues[i % testValues.Length]);
        }

        // Benchmark
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
        {
            rule.IsValid(testValues[i % testValues.Length]);
        }

        stopwatch.Stop();

        // Assert
        var validationsPerSecond = BENCHMARK_ITERATIONS / stopwatch.Elapsed.TotalSeconds;
        Assert.True(validationsPerSecond >= PERFORMANCE_TARGET_PER_SECOND,
            $"StepValidationRule achieved {validationsPerSecond:N0} validations/second, expected at least {PERFORMANCE_TARGET_PER_SECOND:N0}");
    }

    [Fact]
    public void CustomValidationRule_ShouldAchieve1MillionValidationsPerSecond()
    {
        // Arrange
        var rule = new CustomValidationRule<int>(
            value => value >= 0 && value <= 1000,
            "Value must be between 0 and 1000"
        );
        var testValues = GenerateTestValues<int>(BENCHMARK_ITERATIONS, i => i % 1001);

        // Warmup
        for (int i = 0; i < WARMUP_ITERATIONS; i++)
        {
            rule.IsValidValue(testValues[i % testValues.Length]);
        }

        // Benchmark
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
        {
            rule.IsValidValue(testValues[i % testValues.Length]);
        }

        stopwatch.Stop();

        // Assert
        var validationsPerSecond = BENCHMARK_ITERATIONS / stopwatch.Elapsed.TotalSeconds;
        Assert.True(validationsPerSecond >= PERFORMANCE_TARGET_PER_SECOND,
            $"CustomValidationRule achieved {validationsPerSecond:N0} validations/second, expected at least {PERFORMANCE_TARGET_PER_SECOND:N0}");
    }

    [Fact]
    public void CompositeValidationRule_ShouldAchieveHighPerformance()
    {
        // Arrange - More complex composite rule with multiple validators
        var rangeRule = new RangeValidationRule<int>(1, 1000);
        var stepRule = new StepValidationRule<int>(5); // Steps of 5
        var customRule = new CustomValidationRule<int>(value => value % 2 == 0, "Must be even");

        var compositeRule = new CompositeAndValidationRule<int>(rangeRule, stepRule, customRule);
        var testValues = GenerateTestValues<int>(BENCHMARK_ITERATIONS, i => (i % 200) * 5 + 10); // Valid composite values

        // Warmup
        for (int i = 0; i < WARMUP_ITERATIONS; i++)
        {
            compositeRule.IsValid(testValues[i % testValues.Length]);
        }

        // Benchmark
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
        {
            compositeRule.IsValid(testValues[i % testValues.Length]);
        }

        stopwatch.Stop();

        // Assert - Slightly lower target due to complexity
        var validationsPerSecond = BENCHMARK_ITERATIONS / stopwatch.Elapsed.TotalSeconds;
        var compositeTarget = PERFORMANCE_TARGET_PER_SECOND * 0.5; // 500K/second for composite rules
        Assert.True(validationsPerSecond >= compositeTarget,
            $"CompositeValidationRule achieved {validationsPerSecond:N0} validations/second, expected at least {compositeTarget:N0}");
    }

    [Fact]
    public void ParameterValidator_FastPath_ShouldAchieveHighPerformance()
    {
        // Arrange
        var validator = CreateComplexValidator();
        var parameterSets = GenerateParameterSets(BENCHMARK_ITERATIONS / 10); // Fewer parameter sets as they're more complex

        // Warmup
        for (int i = 0; i < Math.Min(WARMUP_ITERATIONS, parameterSets.Length); i++)
        {
            validator.IsValidFast(parameterSets[i % parameterSets.Length]);
        }

        // Benchmark
        var iterations = parameterSets.Length;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            validator.IsValidFast(parameterSets[i % parameterSets.Length]);
        }

        stopwatch.Stop();

        // Assert - Lower target for full parameter set validation
        var validationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        var parameterSetTarget = PERFORMANCE_TARGET_PER_SECOND * 0.1; // 100K/second for parameter sets
        Assert.True(validationsPerSecond >= parameterSetTarget,
            $"ParameterValidator.IsValidFast achieved {validationsPerSecond:N0} validations/second, expected at least {parameterSetTarget:N0}");
    }

    [Fact]
    public void ParameterValidator_BatchValidation_ShouldAchieveHighThroughput()
    {
        // Arrange
        var validator = CreateSimpleValidator();
        var parameterSets = GenerateParameterSets(10_000);

        // Warmup
        validator.ValidateBatch(parameterSets.AsSpan(0, 1000));

        // Benchmark
        var stopwatch = Stopwatch.StartNew();
        var results = validator.ValidateBatch(parameterSets);
        stopwatch.Stop();

        // Assert
        var validationsPerSecond = parameterSets.Length / stopwatch.Elapsed.TotalSeconds;
        var batchTarget = PERFORMANCE_TARGET_PER_SECOND * 0.2; // 200K/second for batch validation
        Assert.True(validationsPerSecond >= batchTarget,
            $"Batch validation achieved {validationsPerSecond:N0} validations/second, expected at least {batchTarget:N0}");

        // Verify results are not all null (actual validation happened)
        Assert.NotNull(results);
        Assert.Equal(parameterSets.Length, results.Length);
    }

    [Fact]
    public void DependencyValidationRule_ShouldAchieveHighPerformance()
    {
        // Arrange
        var dependencyRule = DependencyValidationRule.Builder.LessThan<int>("fastMA", "slowMA");
        var parameterSets = GenerateMovingAverageParameterSets(BENCHMARK_ITERATIONS / 10);

        // Warmup
        for (int i = 0; i < Math.Min(WARMUP_ITERATIONS / 10, parameterSets.Length); i++)
        {
            dependencyRule.IsValid(parameterSets[i % parameterSets.Length]);
        }

        // Benchmark
        var iterations = parameterSets.Length;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            dependencyRule.IsValid(parameterSets[i % parameterSets.Length]);
        }

        stopwatch.Stop();

        // Assert
        var validationsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        var dependencyTarget = PERFORMANCE_TARGET_PER_SECOND * 0.3; // 300K/second for dependency rules
        Assert.True(validationsPerSecond >= dependencyTarget,
            $"DependencyValidationRule achieved {validationsPerSecond:N0} validations/second, expected at least {dependencyTarget:N0}");
    }

    [Fact]
    public void ValidationRuleBuilder_CreatedRules_ShouldMaintainPerformance()
    {
        // Arrange
        var rule = ValidationRuleBuilder<decimal>.ForTrading<decimal>()
            .WithRange(0.01m, 10000m)
            .WithStep(0.01m)
            .WithCustom(value => value > 0, "Must be positive")
            .Build();

        Assert.NotNull(rule);

        var testValues = GenerateTestValues<decimal>(BENCHMARK_ITERATIONS, i => (i % 10000) * 0.01m + 0.01m);

        // Warmup
        for (int i = 0; i < WARMUP_ITERATIONS; i++)
        {
            rule.IsValid(testValues[i % testValues.Length]);
        }

        // Benchmark
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
        {
            rule.IsValid(testValues[i % testValues.Length]);
        }

        stopwatch.Stop();

        // Assert
        var validationsPerSecond = BENCHMARK_ITERATIONS / stopwatch.Elapsed.TotalSeconds;
        var builderTarget = PERFORMANCE_TARGET_PER_SECOND * 0.4; // 400K/second for builder-created rules
        Assert.True(validationsPerSecond >= builderTarget,
            $"Builder-created rule achieved {validationsPerSecond:N0} validations/second, expected at least {builderTarget:N0}");
    }

    [Fact]
    public void RangeValidationRule_BatchValidation_ShouldOptimizeForThroughput()
    {
        // Arrange
        var rule = new RangeValidationRule<int>(1, 1000);
        var testValues = GenerateTestValues<int>(100_000, i => i % 1001);

        // Warmup
        rule.ValidateBatch(testValues.AsSpan(0, 1000));

        // Benchmark
        var stopwatch = Stopwatch.StartNew();
        var results = rule.ValidateBatch(testValues);
        stopwatch.Stop();

        // Assert
        var validationsPerSecond = testValues.Length / stopwatch.Elapsed.TotalSeconds;
        Assert.True(validationsPerSecond >= PERFORMANCE_TARGET_PER_SECOND,
            $"Batch range validation achieved {validationsPerSecond:N0} validations/second, expected at least {PERFORMANCE_TARGET_PER_SECOND:N0}");

        Assert.Equal(testValues.Length, results.Length);
    }

    #region Test Data Generation

    private static T[] GenerateTestValues<T>(int count, Func<int, T> generator)
    {
        var values = new T[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = generator(i);
        }
        return values;
    }

    private static ParameterSet[] GenerateParameterSets(int count)
    {
        var parameterSets = new ParameterSet[count];
        var random = new Random(42); // Fixed seed for reproducible tests

        for (int i = 0; i < count; i++)
        {
            var definitions = new ParameterDefinitionBase[]
            {
                ParameterDefinition.CreateInteger("intParam", 1, 100, random.Next(1, 100)),
                ParameterDefinition.CreateDecimal("decimalParam", 0.01m, 1000m, (decimal)random.NextDouble() * 1000),
                // Note: String and bool parameters would need specialized non-numeric definitions
                // For now, using only numeric parameters to match the existing framework
            };

            parameterSets[i] = new ParameterSet(definitions);
        }

        return parameterSets;
    }

    private static ParameterSet[] GenerateMovingAverageParameterSets(int count)
    {
        var parameterSets = new ParameterSet[count];
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var fastMA = random.Next(5, 20);
            var slowMA = random.Next(fastMA + 1, 200); // Ensure slowMA > fastMA

            var definitions = new ParameterDefinitionBase[]
            {
                ParameterDefinition.CreateInteger("fastMA", 2, 50, fastMA),
                ParameterDefinition.CreateInteger("slowMA", 10, 200, slowMA)
            };

            parameterSets[i] = new ParameterSet(definitions);
        }

        return parameterSets;
    }

    private static ParameterValidator CreateSimpleValidator()
    {
        return ParameterValidator.Builder()
            .WithRange<int>("intParam", 1, 100)
            .WithRange<decimal>("decimalParam", 0.01m, 1000m)
            .Build();
    }

    private static ParameterValidator CreateComplexValidator()
    {
        return ParameterValidator.Builder()
            .WithRange<int>("intParam", 1, 100)
            .WithStep<int>("intParam", 5)
            .WithRange<decimal>("decimalParam", 0.01m, 1000m)
            .WithStep<decimal>("decimalParam", 0.01m)
            .WithTradingRules("decimalParam", riskPercentParam: "decimalParam") // Use existing param for risk
            .Build();
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public void RangeValidationRule_ShouldMinimizeAllocations()
    {
        // Arrange
        var rule = new RangeValidationRule<int>(1, 1000);
        var testValue = 500;

        // Measure initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        // Act - Perform many validations
        for (int i = 0; i < 10_000; i++)
        {
            rule.IsValid(testValue);
        }

        // Measure final memory
        var finalMemory = GC.GetTotalMemory(false);
        var allocatedBytes = finalMemory - initialMemory;

        // Assert - Should have minimal allocations
        var maxAllowedAllocation = 1024; // 1KB maximum
        Assert.True(allocatedBytes <= maxAllowedAllocation,
            $"Range validation allocated {allocatedBytes} bytes, expected <= {maxAllowedAllocation}");
    }

    [Fact]
    public void ValidationCache_ShouldImproveRepeatedValidations()
    {
        // Arrange
        var validator = ParameterValidator.Builder()
            .WithCaching(true)
            .WithRange<int>("param1", 1, 100)
            .Build();

        var parameterSet = new ParameterSet(new[] { ParameterDefinition.CreateNumeric<int>("param1", null, null, 50) });

        // Warmup and populate cache
        for (int i = 0; i < 1000; i++)
        {
            validator.IsValid(parameterSet);
        }

        // Benchmark cached validations
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
        {
            validator.IsValid(parameterSet);
        }
        stopwatch.Stop();

        // Assert - Cached validations should be very fast
        var cachedValidationsPerSecond = BENCHMARK_ITERATIONS / stopwatch.Elapsed.TotalSeconds;
        var cachedTarget = PERFORMANCE_TARGET_PER_SECOND * 2; // 2M/second for cached validations
        Assert.True(cachedValidationsPerSecond >= cachedTarget,
            $"Cached validations achieved {cachedValidationsPerSecond:N0} validations/second, expected at least {cachedTarget:N0}");
    }

    #endregion
}
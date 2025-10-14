using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Pipeline;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Tests.TestHelpers;

/// <summary>
/// Helper class for creating OptimizationResult instances in tests
/// </summary>
public static class OptimizationResultHelper
{
    /// <summary>
    /// Creates an OptimizationResult with all required fields populated with sensible defaults
    /// </summary>
    public static OptimizationResult<TStrategy> Create<TStrategy>(
        OptimizationConfig config,
        TStrategy trainedStrategy,
        PerformanceMetrics trainingMetrics,
        PerformanceMetrics? validationMetrics = null,
        LaunchMode? launchMode = null,
        string? paramsHash = null,
        DateTimeOffset? trainingPeriodStart = null,
        DateTimeOffset? trainingPeriodEnd = null,
        DateTimeOffset? validationPeriodStart = null,
        DateTimeOffset? validationPeriodEnd = null,
        DateTimeOffset? startTime = null)
        where TStrategy : CustomStrategyBase, new()
    {
        return new OptimizationResult<TStrategy>
        {
            Config = config,
            TrainedStrategy = trainedStrategy,
            TrainingMetrics = trainingMetrics,
            ValidationMetrics = validationMetrics,
            StartTime = startTime ?? DateTimeOffset.UtcNow,

            // New required fields with sensible defaults
            // Note: StrategyName and StrategyVersion are computed from TrainedStrategy
            LaunchMode = launchMode ?? LaunchMode.Optimization,
            ParamsHash = paramsHash ?? trainedStrategy.Hash,
            TrainingPeriodStart = trainingPeriodStart ?? config.TrainingPeriod.TrainingStartDate,
            TrainingPeriodEnd = trainingPeriodEnd ?? config.TrainingPeriod.TrainingEndDate,
            ValidationPeriodStart = validationPeriodStart ?? config.TrainingPeriod.TrainingEndDate,
            ValidationPeriodEnd = validationPeriodEnd ?? config.TrainingPeriod.TrainingEndDate
        };
    }
}

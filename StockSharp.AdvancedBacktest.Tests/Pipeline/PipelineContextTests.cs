using StockSharp.AdvancedBacktest.Pipeline;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Tests.Pipeline;

public class PipelineContextTests
{
    private sealed class TestStrategy : CustomStrategyBase
    {
    }

    private static PipelineContext<TestStrategy> CreateMinimalContext()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = ["BTCUSDT"],
            TimeFrames = [TimeSpan.FromMinutes(5)],
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero)
        };

        return new PipelineContext<TestStrategy>
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            PipelineId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Optimization,
            Configuration = config
        };
    }

    [Fact]
    public void CreateContext_WithAllRequiredFields_Succeeds()
    {
        var context = CreateMinimalContext();

        Assert.NotNull(context);
        Assert.Equal("TestStrategy", context.StrategyName);
        Assert.Equal("1.0.0", context.StrategyVersion);
        Assert.NotNull(context.PipelineId);
        Assert.Equal(LaunchMode.Optimization, context.LaunchMode);
        Assert.NotNull(context.Configuration);
    }

    [Fact]
    public void With_UpdateSingleProperty_PreservesOthers()
    {
        var original = CreateMinimalContext();
        var newStrategyName = "UpdatedStrategy";

        var updated = original.With(strategyName: newStrategyName);

        Assert.Equal(newStrategyName, updated.StrategyName);
        Assert.Equal(original.StrategyVersion, updated.StrategyVersion);
        Assert.Equal(original.PipelineId, updated.PipelineId);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.Equal(original.LaunchMode, updated.LaunchMode);
        Assert.Same(original.Configuration, updated.Configuration);
    }

    [Fact]
    public void With_UpdateMultipleProperties_PreservesUnchanged()
    {
        var original = CreateMinimalContext();
        var newStrategyName = "UpdatedStrategy";
        var newStrategyVersion = "2.0.0";

        var updated = original.With(
            strategyName: newStrategyName,
            strategyVersion: newStrategyVersion);

        Assert.Equal(newStrategyName, updated.StrategyName);
        Assert.Equal(newStrategyVersion, updated.StrategyVersion);
        Assert.Equal(original.PipelineId, updated.PipelineId);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public void With_NoArguments_ReturnsNewInstanceWithSameValues()
    {
        var original = CreateMinimalContext();

        var updated = original.With();

        Assert.NotSame(original, updated);
        Assert.Equal(original.StrategyName, updated.StrategyName);
        Assert.Equal(original.StrategyVersion, updated.StrategyVersion);
        Assert.Equal(original.PipelineId, updated.PipelineId);
    }

    [Fact]
    public void With_ChainedCalls_WorksCorrectly()
    {
        var original = CreateMinimalContext();

        var updated = original
            .With(strategyName: "First")
            .With(strategyVersion: "2.0")
            .With(launchMode: LaunchMode.Single);

        Assert.Equal("First", updated.StrategyName);
        Assert.Equal("2.0", updated.StrategyVersion);
        Assert.Equal(LaunchMode.Single, updated.LaunchMode);
    }

    [Fact]
    public void WithDiagnostics_AddsNewDiagnostics_PreservesExisting()
    {
        var original = CreateMinimalContext();
        var withInitialDiagnostics = original.WithDiagnostics(new Dictionary<string, object>
        {
            ["Key1"] = "Value1"
        });

        var updated = withInitialDiagnostics.WithDiagnostics(new Dictionary<string, object>
        {
            ["Key2"] = "Value2"
        });

        Assert.Equal(2, updated.Diagnostics.Count);
        Assert.Equal("Value1", updated.Diagnostics["Key1"]);
        Assert.Equal("Value2", updated.Diagnostics["Key2"]);
    }

    [Fact]
    public void WithDiagnostics_OverwritesExistingKey()
    {
        var original = CreateMinimalContext();
        var withInitialDiagnostics = original.WithDiagnostics(new Dictionary<string, object>
        {
            ["Key1"] = "OriginalValue"
        });

        var updated = withInitialDiagnostics.WithDiagnostics(new Dictionary<string, object>
        {
            ["Key1"] = "UpdatedValue"
        });

        Assert.Single(updated.Diagnostics);
        Assert.Equal("UpdatedValue", updated.Diagnostics["Key1"]);
    }

    [Fact]
    public void Diagnostics_DefaultsToEmptyReadOnlyDictionary()
    {
        var context = CreateMinimalContext();

        Assert.NotNull(context.Diagnostics);
        Assert.Empty(context.Diagnostics);
    }

    [Fact]
    public void Context_IsImmutable_OriginalUnchangedAfterWith()
    {
        var original = CreateMinimalContext();
        var originalStrategyName = original.StrategyName;

        var _ = original.With(strategyName: "NewName");

        Assert.Equal(originalStrategyName, original.StrategyName);
    }
}

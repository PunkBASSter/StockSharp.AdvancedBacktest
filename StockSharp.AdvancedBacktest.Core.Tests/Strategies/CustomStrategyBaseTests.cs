using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Xunit;

namespace StockSharp.AdvancedBacktest.Core.Tests.Strategies;

public class CustomStrategyBaseTests
{
    [Fact]
    public void AuxiliaryTimeframe_DefaultValue_IsNull()
    {
        var strategy = new TestableStrategy();

        Assert.Null(strategy.AuxiliaryTimeframe);
    }

    [Fact]
    public void AuxiliaryTimeframe_WhenSet_ReturnsConfiguredValue()
    {
        var strategy = new TestableStrategy
        {
            AuxiliaryTimeframe = TimeSpan.FromMinutes(5)
        };

        Assert.Equal(TimeSpan.FromMinutes(5), strategy.AuxiliaryTimeframe);
    }

    [Fact]
    public void AuxiliaryTimeframe_WhenSetToZero_ReturnsZero()
    {
        var strategy = new TestableStrategy
        {
            AuxiliaryTimeframe = TimeSpan.Zero
        };

        Assert.Equal(TimeSpan.Zero, strategy.AuxiliaryTimeframe);
    }

    [Fact]
    public void AuxiliaryTimeframe_CanBeChangedAfterInitialization()
    {
        var strategy = new TestableStrategy
        {
            AuxiliaryTimeframe = TimeSpan.FromMinutes(5)
        };

        strategy.AuxiliaryTimeframe = TimeSpan.FromMinutes(15);

        Assert.Equal(TimeSpan.FromMinutes(15), strategy.AuxiliaryTimeframe);
    }

    [Fact]
    public void AuxiliaryTimeframe_CanBeSetToNullAfterConfiguration()
    {
        var strategy = new TestableStrategy
        {
            AuxiliaryTimeframe = TimeSpan.FromMinutes(5)
        };

        strategy.AuxiliaryTimeframe = null;

        Assert.Null(strategy.AuxiliaryTimeframe);
    }

    private class TestableStrategy : CustomStrategyBase
    {
        public override IEnumerable<(Security sec, DataType dt)> GetWorkingSecurities()
        {
            return [];
        }
    }
}

using StockSharp.AdvancedBacktest.OrderManagement;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class OrderGroupLimitsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var limits = new OrderGroupLimits();

        Assert.Equal(10, limits.MaxGroupsPerSecurity);
        Assert.Equal(2.0m, limits.MaxRiskPercentPerGroup);
        Assert.True(limits.ThrowIfNotMatchingVolume);
    }

    [Fact]
    public void Constructor_AcceptsCustomValues()
    {
        var limits = new OrderGroupLimits(
            maxGroupsPerSecurity: 5,
            maxRiskPercentPerGroup: 1.5m,
            throwIfNotMatchingVolume: false);

        Assert.Equal(5, limits.MaxGroupsPerSecurity);
        Assert.Equal(1.5m, limits.MaxRiskPercentPerGroup);
        Assert.False(limits.ThrowIfNotMatchingVolume);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsOnInvalidMaxGroupsPerSecurity(int maxGroups)
    {
        Assert.Throws<ArgumentException>(() => new OrderGroupLimits(
            maxGroupsPerSecurity: maxGroups,
            maxRiskPercentPerGroup: 2.0m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void Constructor_ThrowsOnInvalidMaxRiskPercentPerGroup_TooLow(decimal maxRisk)
    {
        Assert.Throws<ArgumentException>(() => new OrderGroupLimits(
            maxGroupsPerSecurity: 10,
            maxRiskPercentPerGroup: maxRisk));
    }

    [Theory]
    [InlineData(100.1)]
    [InlineData(150)]
    [InlineData(1000)]
    public void Constructor_ThrowsOnInvalidMaxRiskPercentPerGroup_TooHigh(decimal maxRisk)
    {
        Assert.Throws<ArgumentException>(() => new OrderGroupLimits(
            maxGroupsPerSecurity: 10,
            maxRiskPercentPerGroup: maxRisk));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Constructor_AcceptsValidMaxRiskPercentPerGroup(decimal maxRisk)
    {
        var limits = new OrderGroupLimits(
            maxGroupsPerSecurity: 10,
            maxRiskPercentPerGroup: maxRisk);

        Assert.Equal(maxRisk, limits.MaxRiskPercentPerGroup);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Constructor_AcceptsValidMaxGroupsPerSecurity(int maxGroups)
    {
        var limits = new OrderGroupLimits(
            maxGroupsPerSecurity: maxGroups,
            maxRiskPercentPerGroup: 2.0m);

        Assert.Equal(maxGroups, limits.MaxGroupsPerSecurity);
    }

    [Fact]
    public void Validate_ThrowsOnInvalidMaxGroupsPerSecurity()
    {
        var limits = new OrderGroupLimits();
        // Use reflection to set invalid value to simulate deserialization scenario
        var type = typeof(OrderGroupLimits);
        var field = type.GetField("<MaxGroupsPerSecurity>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(limits, 0);
            Assert.Throws<ArgumentException>(() => limits.Validate());
        }
    }

    [Fact]
    public void Validate_ThrowsOnInvalidMaxRiskPercentPerGroup()
    {
        var limits = new OrderGroupLimits();
        // Use reflection to set invalid value to simulate deserialization scenario
        var type = typeof(OrderGroupLimits);
        var field = type.GetField("<MaxRiskPercentPerGroup>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(limits, 0m);
            Assert.Throws<ArgumentException>(() => limits.Validate());
        }
    }

    [Fact]
    public void Validate_SucceedsOnValidLimits()
    {
        var limits = new OrderGroupLimits(
            maxGroupsPerSecurity: 5,
            maxRiskPercentPerGroup: 2.5m,
            throwIfNotMatchingVolume: true);

        limits.Validate(); // Should not throw
    }
}

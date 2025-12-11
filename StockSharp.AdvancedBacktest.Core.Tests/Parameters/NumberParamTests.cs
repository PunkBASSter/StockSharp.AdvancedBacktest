using StockSharp.AdvancedBacktest.Parameters;

namespace StockSharp.AdvancedBacktest.Tests.Parameters;

public class NumberParamTests
{
    [Fact]
    public void Constructor_SetsDefaultValue()
    {
        var param = new NumberParam<int>("test", 42);

        Assert.Equal(42, param.Value);
        Assert.Equal("test", param.Id);
    }

    [Fact]
    public void Constructor_SetsOptimizationRange()
    {
        var param = new NumberParam<int>("test", 10, 10, 30, 10);

        Assert.Equal(10, param.OptimizeFrom);
        Assert.Equal(30, param.OptimizeTo);
        Assert.Equal(10, param.OptimizeStep);
    }

    [Fact]
    public void OptimizationRange_GeneratesCorrectValues()
    {
        var param = new NumberParam<int>("test", 10, 10, 30, 10);

        var range = param.OptimizationRange.ToList();

        Assert.Equal(3, range.Count);
        Assert.Equal(10, range[0]);
        Assert.Equal(20, range[1]);
        Assert.Equal(30, range[2]);
    }

    [Fact]
    public void OptimizationRangeParams_GeneratesCorrectInstances()
    {
        var param = new NumberParam<int>("test", 10, 10, 30, 10);

        var range = param.OptimizationRangeParams.ToList();

        Assert.Equal(3, range.Count);
        Assert.Equal(10, ((NumberParam<int>)range[0]).Value);
        Assert.Equal(20, ((NumberParam<int>)range[1]).Value);
        Assert.Equal(30, ((NumberParam<int>)range[2]).Value);
    }

    [Fact]
    public void OptimizationRangeParams_WithoutOptimization_ReturnsSelf()
    {
        var param = new NumberParam<int>("test", 42);

        var range = param.OptimizationRangeParams.ToList();

        Assert.Single(range);
        Assert.Equal(42, ((NumberParam<int>)range[0]).Value);
    }

    [Fact]
    public void ParamType_ReturnsCorrectType()
    {
        var param = new NumberParam<int>("test", 42);

        Assert.Equal(typeof(int), param.ParamType);
    }

    [Fact]
    public void ImplementsICustomParam()
    {
        var param = new NumberParam<int>("test", 42);

        Assert.IsAssignableFrom<ICustomParam>(param);
    }
}

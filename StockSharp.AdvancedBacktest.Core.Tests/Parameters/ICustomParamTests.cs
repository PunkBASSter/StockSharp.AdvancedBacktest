using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Tests.Parameters;

public class ICustomParamTests
{
    [Fact]
    public void ICustomParam_ExtendsIStrategyParam()
    {
        Assert.True(typeof(IStrategyParam).IsAssignableFrom(typeof(ICustomParam)));
    }

    [Fact]
    public void ICustomParam_DefinesOptimizationRangeParams()
    {
        var property = typeof(ICustomParam).GetProperty("OptimizationRangeParams");

        Assert.NotNull(property);
        Assert.True(typeof(IEnumerable<ICustomParam>).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    public void ICustomParam_DefinesParamType()
    {
        var property = typeof(ICustomParam).GetProperty("ParamType");

        Assert.NotNull(property);
        Assert.Equal(typeof(Type), property.PropertyType);
    }
}

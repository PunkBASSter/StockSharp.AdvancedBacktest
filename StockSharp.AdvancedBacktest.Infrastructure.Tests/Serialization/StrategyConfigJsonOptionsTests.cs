using StockSharp.AdvancedBacktest.Serialization;

namespace StockSharp.AdvancedBacktest.Infrastructure.Tests.Serialization;

public class StrategyConfigJsonOptionsTests
{
    [Fact]
    public void Default_ReturnsValidOptions()
    {
        var options = StrategyConfigJsonOptions.Default;

        Assert.NotNull(options);
        Assert.True(options.WriteIndented);
        Assert.NotNull(options.PropertyNamingPolicy);
    }

    [Fact]
    public void Default_HasCustomParamConverter()
    {
        var options = StrategyConfigJsonOptions.Default;

        Assert.Contains(options.Converters, c => c is CustomParamJsonConverter);
    }
}

using StockSharp.AdvancedBacktest.Serialization;
using StockSharp.AdvancedBacktest.Parameters;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.Infrastructure.Tests.Serialization;

public class CustomParamJsonConverterTests
{
    [Fact]
    public void Class_InheritsFromJsonConverterOfICustomParam()
    {
        var type = typeof(CustomParamJsonConverter);
        var baseType = type.BaseType;

        Assert.NotNull(baseType);
        Assert.True(baseType.IsGenericType);
        Assert.Equal(typeof(JsonConverter<ICustomParam>), baseType);
    }
}

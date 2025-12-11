using StockSharp.AdvancedBacktest.Parameters;

namespace StockSharp.AdvancedBacktest.Tests.Parameters;

public class CustomParamsContainerTests
{
    [Fact]
    public void Constructor_CreatesContainerWithParameters()
    {
        var param1 = new NumberParam<int>("fast", 10);
        var param2 = new NumberParam<int>("slow", 50);

        var container = new CustomParamsContainer([param1, param2]);

        Assert.Equal(2, container.CustomParams.Count);
    }

    [Fact]
    public void Constructor_ThrowsOnNullParameters()
    {
        Assert.Throws<ArgumentNullException>(() => new CustomParamsContainer(null!));
    }

    [Fact]
    public void Get_ReturnsParameterValue()
    {
        var param = new NumberParam<int>("test", 42);
        var container = new CustomParamsContainer([param]);

        var value = container.Get<int>("test");

        Assert.Equal(42, value);
    }

    [Fact]
    public void Get_ThrowsOnMissingParameter()
    {
        var container = new CustomParamsContainer([]);

        Assert.Throws<InvalidOperationException>(() => container.Get<int>("missing"));
    }

    [Fact]
    public void TryGet_ReturnsTrueWhenExists()
    {
        var param = new NumberParam<int>("test", 42);
        var container = new CustomParamsContainer([param]);

        var result = container.TryGet<int>("test", out var value);

        Assert.True(result);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGet_ReturnsFalseWhenMissing()
    {
        var container = new CustomParamsContainer([]);

        var result = container.TryGet<int>("missing", out var value);

        Assert.False(result);
        Assert.Equal(default, value);
    }

    [Fact]
    public void GenerateHash_CreatesDeterministicHash()
    {
        var param1 = new NumberParam<int>("fast", 10);
        var param2 = new NumberParam<int>("slow", 50);
        var container1 = new CustomParamsContainer([param1, param2]);
        var container2 = new CustomParamsContainer([param2, param1]); // Reversed order

        var hash1 = container1.GenerateHash();
        var hash2 = container2.GenerateHash();

        Assert.Equal(hash1, hash2); // Should be the same due to ordering by Id
    }

    [Fact]
    public void ValidationRules_CanBeSet()
    {
        var param1 = new NumberParam<int>("fast", 10, 5, 15, 5);
        var param2 = new NumberParam<int>("slow", 50, 40, 60, 10);
        var container = new CustomParamsContainer([param1, param2])
        {
            ValidationRules =
            {
                dict =>
                {
                    var fast = (NumberParam<int>)dict["fast"];
                    var slow = (NumberParam<int>)dict["slow"];
                    return fast.Value < slow.Value;
                }
            }
        };

        Assert.Single(container.ValidationRules);
    }

    [Fact]
    public void ValidationRules_CanValidateParameters()
    {
        var container = new CustomParamsContainer([])
        {
            ValidationRules =
            {
                dict =>
                {
                    var fast = (NumberParam<int>)dict["fast"];
                    var slow = (NumberParam<int>)dict["slow"];
                    return fast.Value < slow.Value;
                }
            }
        };

        var validDict = new Dictionary<string, ICustomParam>
        {
            ["fast"] = new NumberParam<int>("fast", 10),
            ["slow"] = new NumberParam<int>("slow", 50)
        };

        var invalidDict = new Dictionary<string, ICustomParam>
        {
            ["fast"] = new NumberParam<int>("fast", 60),
            ["slow"] = new NumberParam<int>("slow", 50)
        };

        Assert.True(container.ValidationRules[0](validDict));
        Assert.False(container.ValidationRules[0](invalidDict));
    }
}

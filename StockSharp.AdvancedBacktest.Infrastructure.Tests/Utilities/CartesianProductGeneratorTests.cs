using StockSharp.AdvancedBacktest.Utilities;

namespace StockSharp.AdvancedBacktest.Infrastructure.Tests.Utilities;

public class CartesianProductGeneratorTests
{
    [Theory]
    [MemberData(nameof(EmptyInputCases))]
    public void Generate_WithInvalidInput_ReturnsEmpty(List<List<int>> input)
    {
        var result = CartesianProductGenerator.Generate(input);
        Assert.Empty(result);
    }

    public static IEnumerable<object[]> EmptyInputCases()
    {
        yield return new object[] { null! };
        yield return new object[] { new List<List<int>>() };
        yield return new object[] { new List<List<int>> { new() { 1, 2 }, new() } };
    }

    [Fact]
    public void Generate_WithSingleList_ReturnsSingleElementLists()
    {
        var input = new List<List<int>> { new() { 1, 2, 3 } };

        var result = CartesianProductGenerator.Generate(input);

        Assert.Equal(3, result.Count);
        Assert.Equal([1], result[0]);
        Assert.Equal([2], result[1]);
        Assert.Equal([3], result[2]);
    }

    [Fact]
    public void Generate_WithTwoLists_ReturnsCartesianProduct()
    {
        var input = new List<List<int>>
        {
            new() { 1, 2 },
            new() { 3, 4 }
        };

        var result = CartesianProductGenerator.Generate(input);

        Assert.Equal(4, result.Count);
        Assert.Contains(result, r => r.SequenceEqual([1, 3]));
        Assert.Contains(result, r => r.SequenceEqual([1, 4]));
        Assert.Contains(result, r => r.SequenceEqual([2, 3]));
        Assert.Contains(result, r => r.SequenceEqual([2, 4]));
    }

    [Fact]
    public void Generate_WithThreeLists_ReturnsCorrectProductCount()
    {
        var input = new List<List<int>>
        {
            new() { 1, 2 },
            new() { 3, 4 },
            new() { 5, 6 }
        };

        var result = CartesianProductGenerator.Generate(input);

        Assert.Equal(8, result.Count);
    }
}

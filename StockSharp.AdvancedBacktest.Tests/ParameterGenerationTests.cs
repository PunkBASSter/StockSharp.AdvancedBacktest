using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Utilities;

namespace StockSharp.AdvancedBacktest.Tests;

public class ParameterGenerationTests
{
	[Fact]
	public void NumberParam_GeneratesCorrectRange()
	{
		var param = new NumberParam<int>("test", 10, 10, 30, 10);

		var range = param.OptimizationRangeParams.ToList();

		Assert.Equal(3, range.Count);
		Assert.Equal(10, ((NumberParam<int>)range[0]).Value);
		Assert.Equal(20, ((NumberParam<int>)range[1]).Value);
		Assert.Equal(30, ((NumberParam<int>)range[2]).Value);
	}

	[Fact]
	public void NumberParam_WithoutOptimization_ReturnsSelf()
	{
		var param = new NumberParam<int>("test", 42);

		var range = param.OptimizationRangeParams.ToList();

		Assert.Single(range);
		Assert.Equal(42, ((NumberParam<int>)range[0]).Value);
	}

	[Fact]
	public void CartesianProduct_TwoLists_GeneratesAllCombinations()
	{
		var list1 = new List<int> { 1, 2 };
		var list2 = new List<int> { 10, 20, 30 };

		var result = CartesianProductGenerator.Generate(new List<List<int>> { list1, list2 });

		Assert.Equal(6, result.Count);
		Assert.Contains(result, c => c[0] == 1 && c[1] == 10);
		Assert.Contains(result, c => c[0] == 1 && c[1] == 20);
		Assert.Contains(result, c => c[0] == 1 && c[1] == 30);
		Assert.Contains(result, c => c[0] == 2 && c[1] == 10);
		Assert.Contains(result, c => c[0] == 2 && c[1] == 20);
		Assert.Contains(result, c => c[0] == 2 && c[1] == 30);
	}

	[Fact]
	public void CartesianProduct_ThreeLists_GeneratesAllCombinations()
	{
		var list1 = new List<int> { 1, 2 };
		var list2 = new List<int> { 10, 20 };
		var list3 = new List<int> { 100, 200 };

		var result = CartesianProductGenerator.Generate(new List<List<int>> { list1, list2, list3 });

		Assert.Equal(8, result.Count);
		Assert.Contains(result, c => c[0] == 1 && c[1] == 10 && c[2] == 100);
		Assert.Contains(result, c => c[0] == 2 && c[1] == 20 && c[2] == 200);
	}

	[Fact]
	public void CartesianProduct_EmptyList_ReturnsEmpty()
	{
		var result = CartesianProductGenerator.Generate(new List<List<int>>());

		Assert.Empty(result);
	}

	[Fact]
	public void CartesianProduct_NullList_ReturnsEmpty()
	{
		var result = CartesianProductGenerator.Generate<int>(null!);

		Assert.Empty(result);
	}

	[Fact]
	public void CartesianProduct_ListWithEmptyElement_ReturnsEmpty()
	{
		var list1 = new List<int> { 1, 2 };
		var list2 = new List<int>();

		var result = CartesianProductGenerator.Generate(new List<List<int>> { list1, list2 });

		Assert.Empty(result);
	}

	[Fact]
	public void ValidationRules_FilterParameters()
	{
		var params1 = new NumberParam<int>("fast", 10, 5, 15, 5);
		var params2 = new NumberParam<int>("slow", 50, 40, 60, 10);
		var container = new CustomParamsContainer(new[] { params1, params2 })
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

		var testDict = new Dictionary<string, ICustomParam>
		{
			["fast"] = new NumberParam<int>("fast", 10),
			["slow"] = new NumberParam<int>("slow", 50)
		};

		Assert.True(container.ValidationRules[0](testDict));

		testDict["fast"] = new NumberParam<int>("fast", 60);
		Assert.False(container.ValidationRules[0](testDict));
	}
}

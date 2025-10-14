using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Tests;

public class HashGenerationTests
{
    private class TestStrategy : CustomStrategyBase
    {
    }

    [Fact]
    public void GenerateHash_SameParameters_ProducesSameHash()
    {
        // Arrange
        var params1 = new List<ICustomParam>
        {
            new NumberParam<int>("fast", 10),
            new NumberParam<int>("slow", 30),
            new ClassParam<string>("symbol", new[] { "AAPL" })
        };

        var container1 = new CustomParamsContainer(params1);
        var container2 = new CustomParamsContainer(params1);

        // Act
        var hash1 = container1.GenerateHash();
        var hash2 = container2.GenerateHash();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateHash_DifferentOrder_ProducesSameHash()
    {
        // Arrange - Same parameters in different order
        var params1 = new List<ICustomParam>
        {
            new NumberParam<int>("fast", 10),
            new NumberParam<int>("slow", 30),
            new ClassParam<string>("symbol", new[] { "AAPL" })
        };

        var params2 = new List<ICustomParam>
        {
            new ClassParam<string>("symbol", new[] { "AAPL" }),
            new NumberParam<int>("slow", 30),
            new NumberParam<int>("fast", 10)
        };

        var container1 = new CustomParamsContainer(params1);
        var container2 = new CustomParamsContainer(params2);

        // Act
        var hash1 = container1.GenerateHash();
        var hash2 = container2.GenerateHash();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateHash_DifferentValues_ProducesDifferentHash()
    {
        // Arrange
        var params1 = new List<ICustomParam>
        {
            new NumberParam<int>("fast", 10),
            new NumberParam<int>("slow", 30)
        };

        var params2 = new List<ICustomParam>
        {
            new NumberParam<int>("fast", 10),
            new NumberParam<int>("slow", 40) // Different value
        };

        var container1 = new CustomParamsContainer(params1);
        var container2 = new CustomParamsContainer(params2);

        // Act
        var hash1 = container1.GenerateHash();
        var hash2 = container2.GenerateHash();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateHash_DifferentParameterIds_ProducesDifferentHash()
    {
        // Arrange
        var params1 = new List<ICustomParam>
        {
            new NumberParam<int>("param1", 10)
        };

        var params2 = new List<ICustomParam>
        {
            new NumberParam<int>("param2", 10) // Different ID, same value
        };

        var container1 = new CustomParamsContainer(params1);
        var container2 = new CustomParamsContainer(params2);

        // Act
        var hash1 = container1.GenerateHash();
        var hash2 = container2.GenerateHash();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateHash_EmptyContainer_ReturnsEmptyString()
    {
        // Arrange
        var container = new CustomParamsContainer(Enumerable.Empty<ICustomParam>());

        // Act
        var hash = container.GenerateHash();

        // Assert
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void GenerateHash_NoCollisions_WithThousandsOfCombinations()
    {
        // Arrange - Generate 10,000 unique parameter combinations
        var hashSet = new HashSet<string>();
        var collisions = 0;

        // Act - Create many combinations
        for (int fast = 1; fast <= 100; fast++)
        {
            for (int slow = 100; slow <= 199; slow++)
            {
                var parameters = new List<ICustomParam>
                {
                    new NumberParam<int>("fast", fast),
                    new NumberParam<int>("slow", slow)
                };

                var container = new CustomParamsContainer(parameters);
                var hash = container.GenerateHash();

                if (!hashSet.Add(hash))
                {
                    collisions++;
                }
            }
        }

        // Assert - Zero collisions guaranteed
        Assert.Equal(0, collisions);
        Assert.Equal(10000, hashSet.Count);
    }

    [Fact]
    public void GenerateHash_DecimalValues_MaintainsPrecision()
    {
        // Arrange
        var params1 = new List<ICustomParam>
        {
            new NumberParam<decimal>("threshold", 0.123456789m)
        };

        var params2 = new List<ICustomParam>
        {
            new NumberParam<decimal>("threshold", 0.123456788m) // Different by 0.000000001
        };

        var container1 = new CustomParamsContainer(params1);
        var container2 = new CustomParamsContainer(params2);

        // Act
        var hash1 = container1.GenerateHash();
        var hash2 = container2.GenerateHash();

        // Assert - Should be different due to precision difference
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateHash_SpecialCharacters_HandledCorrectly()
    {
        // Arrange
        var parameters = new List<ICustomParam>
        {
            new ClassParam<string>("desc", new[] { "Test;Param=Value" }),
            new ClassParam<string>("name", new[] { "Strategy_Name-v2.0" })
        };

        var container = new CustomParamsContainer(parameters);

        // Act
        var hash = container.GenerateHash();

        // Assert - Should not throw and should be deterministic
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);

        // Verify deterministic
        var hash2 = container.GenerateHash();
        Assert.Equal(hash, hash2);
    }

    [Fact]
    public void GenerateHash_MixedParameterTypes_ProducesConsistentHash()
    {
        // Arrange
        var parameters = new List<ICustomParam>
        {
            new NumberParam<int>("intParam", 42),
            new NumberParam<decimal>("decimalParam", 3.14m),
            new ClassParam<string>("stringParam", new[] { "test" }),
            new StructParam<bool>("boolParam", new[] { true })
        };

        var container = new CustomParamsContainer(parameters);

        // Act
        var hash1 = container.GenerateHash();
        var hash2 = container.GenerateHash();

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Contains("intParam=42", hash1);
        // Decimal formatting may vary by culture (3.14 or 3,14)
        Assert.True(hash1.Contains("decimalParam=3.14") || hash1.Contains("decimalParam=3,14"),
            $"Hash should contain decimal parameter: {hash1}");
        Assert.Contains("stringParam=test", hash1);
        Assert.Contains("boolParam=True", hash1);
    }

    [Fact]
    public void SecuritiesHash_SameSecurities_ProducesSameHash()
    {
        // Arrange
        var security = new Security { Id = "AAPL@NASDAQ" };
        var timeframes = new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5) };

        var strategy1 = new TestStrategy();
        strategy1.Securities[security] = timeframes;

        var strategy2 = new TestStrategy();
        strategy2.Securities[security] = timeframes;

        // Act
        var hash1 = strategy1.SecuritiesHash;
        var hash2 = strategy2.SecuritiesHash;

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void SecuritiesHash_DifferentOrder_ProducesSameHash()
    {
        // Arrange
        var security1 = new Security { Id = "AAPL@NASDAQ" };
        var security2 = new Security { Id = "MSFT@NASDAQ" };
        var timeframes = new[] { TimeSpan.FromMinutes(1) };

        var strategy1 = new TestStrategy();
        strategy1.Securities[security1] = timeframes;
        strategy1.Securities[security2] = timeframes;

        var strategy2 = new TestStrategy();
        strategy2.Securities[security2] = timeframes; // Different order
        strategy2.Securities[security1] = timeframes;

        // Act
        var hash1 = strategy1.SecuritiesHash;
        var hash2 = strategy2.SecuritiesHash;

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void SecuritiesHash_EmptySecurities_ReturnsEmptyString()
    {
        // Arrange
        var strategy = new TestStrategy();

        // Act
        var hash = strategy.SecuritiesHash;

        // Assert
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void StrategyHash_CombinesAllComponents_Correctly()
    {
        // Arrange
        var security = new Security { Id = "AAPL@NASDAQ" };
        var strategy = new TestStrategy
        {
            Version = "2.0.0",
            Securities = { [security] = new[] { TimeSpan.FromMinutes(1) } },
            ParamsContainer = new CustomParamsContainer(new List<ICustomParam>
            {
                new NumberParam<int>("fast", 10),
                new NumberParam<int>("slow", 30)
            })
        };

        // Act
        var fullHash = strategy.Hash;

        // Assert
        Assert.Contains("TestStrategy", fullHash);
        Assert.Contains("V2.0.0", fullHash);
        Assert.Contains("AAPL@NASDAQ", fullHash);
        Assert.Contains("fast=10", fullHash);
        Assert.Contains("slow=30", fullHash);
    }

    [Fact]
    public void StrategyHash_DifferentVersions_ProducesDifferentHash()
    {
        // Arrange
        var strategy1 = new TestStrategy { Version = "1.0.0" };
        var strategy2 = new TestStrategy { Version = "2.0.0" };

        // Act
        var hash1 = strategy1.Hash;
        var hash2 = strategy2.Hash;

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateHash_LargeOptimizationScenario_NoCollisions()
    {
        // Arrange - Simulate realistic optimization scenario
        var hashSet = new HashSet<string>();
        var testCases = new List<(int fast, int slow, decimal threshold, string symbol)>();

        // Generate 1000 realistic parameter combinations
        for (int fast = 5; fast <= 15; fast++)
        {
            for (int slow = 20; slow <= 50; slow += 10)
            {
                for (decimal threshold = 0.5m; threshold <= 2.0m; threshold += 0.5m)
                {
                    testCases.Add((fast, slow, threshold, "AAPL"));
                    testCases.Add((fast, slow, threshold, "MSFT"));
                }
            }
        }

        // Act - Generate hashes for all combinations
        var collisions = 0;
        foreach (var (fast, slow, threshold, symbol) in testCases)
        {
            var parameters = new List<ICustomParam>
            {
                new NumberParam<int>("fast", fast),
                new NumberParam<int>("slow", slow),
                new NumberParam<decimal>("threshold", threshold),
                new ClassParam<string>("symbol", new[] { symbol })
            };

            var container = new CustomParamsContainer(parameters);
            var hash = container.GenerateHash();

            if (!hashSet.Add(hash))
            {
                collisions++;
            }
        }

        // Assert
        Assert.Equal(0, collisions);
        Assert.Equal(testCases.Count, hashSet.Count);
    }
}

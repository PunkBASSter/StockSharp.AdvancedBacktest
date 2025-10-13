using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies;
using Xunit;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.BacktestMode;

public class MockStrategy : CustomStrategyBase
{
}

public class ParameterContainerBuilderTests
{
    private BacktestConfiguration CreateBasicConfiguration()
    {
        return new BacktestConfiguration
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            Securities = ["BTCUSDT@BNB"],
            TimeFrames = [TimeSpan.FromDays(1)],
            HistoryPath = Path.Combine(Path.GetTempPath(), "test-data"),
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };
    }

    [Fact]
    public void BuildParameterContainer_WithSecurityParameters_CreatesSecurityParam()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.Securities = ["BTCUSDT@BNB", "ETHUSDT@BNB"];
        config.TimeFrames = [TimeSpan.FromHours(1), TimeSpan.FromDays(1)];

        var runner = new BacktestRunner<MockStrategy>(config);

        // Access the private method through reflection for testing
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var container = (CustomParamsContainer)buildMethod!.Invoke(runner, null)!;

        // Assert
        Assert.NotNull(container);
        Assert.NotEmpty(container.CustomParams);

        var securityParam = container.CustomParams.FirstOrDefault(p => p.Id == "Security");
        Assert.NotNull(securityParam);
        Assert.IsType<SecurityParam>(securityParam);
        Assert.True(securityParam.CanOptimize);

        // Should have 2 securities × 2 timeframes = 4 combinations
        var rangeCount = securityParam.OptimizationRangeParams.Count();
        Assert.Equal(2, rangeCount); // 2 securities, each with 2 timeframes
    }

    [Fact]
    public void BuildParameterContainer_WithNumericParameters_CreatesNumberParams()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["IntParam"] = new ParameterDefinition
            {
                Name = "IntParam",
                Type = "int",
                MinValue = JsonSerializer.SerializeToElement(1),
                MaxValue = JsonSerializer.SerializeToElement(10),
                StepValue = JsonSerializer.SerializeToElement(1)
            },
            ["DecimalParam"] = new ParameterDefinition
            {
                Name = "DecimalParam",
                Type = "decimal",
                MinValue = JsonSerializer.SerializeToElement(0.1m),
                MaxValue = JsonSerializer.SerializeToElement(1.0m),
                StepValue = JsonSerializer.SerializeToElement(0.1m)
            },
            ["DoubleParam"] = new ParameterDefinition
            {
                Name = "DoubleParam",
                Type = "double",
                MinValue = JsonSerializer.SerializeToElement(5.0),
                MaxValue = JsonSerializer.SerializeToElement(15.0),
                StepValue = JsonSerializer.SerializeToElement(2.5)
            }
        };

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var container = (CustomParamsContainer)buildMethod!.Invoke(runner, null)!;

        // Assert
        Assert.Equal(4, container.CustomParams.Count); // 1 security + 3 numeric params

        var intParam = container.CustomParams.FirstOrDefault(p => p.Id == "IntParam");
        Assert.NotNull(intParam);
        Assert.IsType<NumberParam<int>>(intParam);
        Assert.True(intParam.CanOptimize);

        var decimalParam = container.CustomParams.FirstOrDefault(p => p.Id == "DecimalParam");
        Assert.NotNull(decimalParam);
        Assert.IsType<NumberParam<decimal>>(decimalParam);

        var doubleParam = container.CustomParams.FirstOrDefault(p => p.Id == "DoubleParam");
        Assert.NotNull(doubleParam);
        Assert.IsType<NumberParam<double>>(doubleParam);
    }

    [Fact]
    public void BuildParameterContainer_WithEnumParameters_CreatesClassParams()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["OrderType"] = new ParameterDefinition
            {
                Name = "OrderType",
                Type = "enum",
                Values = ["Market", "Limit", "StopLoss"]
            },
            ["TimeInForce"] = new ParameterDefinition
            {
                Name = "TimeInForce",
                Type = "string",
                Values = ["GTC", "IOC", "FOK"]
            }
        };

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var container = (CustomParamsContainer)buildMethod!.Invoke(runner, null)!;

        // Assert
        Assert.Equal(3, container.CustomParams.Count); // 1 security + 2 enum params

        var orderTypeParam = container.CustomParams.FirstOrDefault(p => p.Id == "OrderType");
        Assert.NotNull(orderTypeParam);
        Assert.IsType<ClassParam<string>>(orderTypeParam);
        Assert.Equal(3, orderTypeParam.OptimizationRangeParams.Count());

        var timeInForceParam = container.CustomParams.FirstOrDefault(p => p.Id == "TimeInForce");
        Assert.NotNull(timeInForceParam);
        Assert.IsType<ClassParam<string>>(timeInForceParam);
        Assert.Equal(3, timeInForceParam.OptimizationRangeParams.Count());
    }

    [Fact]
    public void BuildParameterContainer_WithMixedParameters_CreatesAllParamTypes()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.Securities = ["BTCUSDT@BNB"];
        config.TimeFrames = [TimeSpan.FromHours(1)];
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["Period"] = new ParameterDefinition
            {
                Name = "Period",
                Type = "int",
                MinValue = JsonSerializer.SerializeToElement(10),
                MaxValue = JsonSerializer.SerializeToElement(20),
                StepValue = JsonSerializer.SerializeToElement(5)
            },
            ["Strategy"] = new ParameterDefinition
            {
                Name = "Strategy",
                Type = "enum",
                Values = ["Aggressive", "Conservative"]
            }
        };

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var container = (CustomParamsContainer)buildMethod!.Invoke(runner, null)!;

        // Assert
        Assert.Equal(3, container.CustomParams.Count); // 1 security + 1 numeric + 1 enum
        Assert.Contains(container.CustomParams, p => p is SecurityParam);
        Assert.Contains(container.CustomParams, p => p is NumberParam<int>);
        Assert.Contains(container.CustomParams, p => p is ClassParam<string>);
    }


    [Fact]
    public void ValidateParameterDefinition_WithValidNumericParam_DoesNotThrow()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["ValidParam"] = new ParameterDefinition
            {
                Name = "ValidParam",
                Type = "int",
                MinValue = JsonSerializer.SerializeToElement(1),
                MaxValue = JsonSerializer.SerializeToElement(10),
                StepValue = JsonSerializer.SerializeToElement(1)
            }
        };

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert - should not throw
        var container = (CustomParamsContainer)buildMethod!.Invoke(runner, null)!;
        Assert.NotNull(container);
    }

    [Fact]
    public void ValidateParameterDefinition_WithNumericParamMissingMinValue_ThrowsException()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["InvalidParam"] = new ParameterDefinition
            {
                Name = "InvalidParam",
                Type = "int",
                MinValue = null,
                MaxValue = JsonSerializer.SerializeToElement(10),
                StepValue = JsonSerializer.SerializeToElement(1)
            }
        };

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.ThrowsAny<Exception>(() => buildMethod!.Invoke(runner, null));
        Assert.Contains("MinValue is required", ex.InnerException?.Message);
    }

    [Fact]
    public void ValidateParameterDefinition_WithEnumParamMissingValues_ThrowsException()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["InvalidEnum"] = new ParameterDefinition
            {
                Name = "InvalidEnum",
                Type = "enum",
                Values = null
            }
        };

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.ThrowsAny<Exception>(() => buildMethod!.Invoke(runner, null));
        Assert.Contains("Values list is required", ex.InnerException?.Message);
    }

    [Fact]
    public void ValidateParameterDefinition_WithUnsupportedType_ThrowsException()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["UnsupportedParam"] = new ParameterDefinition
            {
                Name = "UnsupportedParam",
                Type = "boolean",
                MinValue = JsonSerializer.SerializeToElement(true),
                MaxValue = JsonSerializer.SerializeToElement(false),
                StepValue = JsonSerializer.SerializeToElement(true)
            }
        };

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.ThrowsAny<Exception>(() => buildMethod!.Invoke(runner, null));
        Assert.Contains("not supported", ex.InnerException?.Message);
    }

    [Fact]
    public void CalculateTotalCombinations_WithMultipleParameters_ReturnsCorrectCount()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.Securities = ["BTCUSDT@BNB", "ETHUSDT@BNB"];
        config.TimeFrames = [TimeSpan.FromHours(1), TimeSpan.FromDays(1)];
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["Period"] = new ParameterDefinition
            {
                Name = "Period",
                Type = "int",
                MinValue = JsonSerializer.SerializeToElement(10),
                MaxValue = JsonSerializer.SerializeToElement(30),
                StepValue = JsonSerializer.SerializeToElement(10)
            },
            ["Strategy"] = new ParameterDefinition
            {
                Name = "Strategy",
                Type = "enum",
                Values = ["A", "B", "C"]
            }
        };

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var calcMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("CalculateTotalCombinations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var container = (CustomParamsContainer)buildMethod!.Invoke(runner, null)!;

        // Act
        var totalCombinations = (long)calcMethod!.Invoke(runner, new object[] { container })!;

        // Assert
        // 2 securities × 3 period values × 3 strategy values = 18 combinations
        Assert.Equal(18, totalCombinations);
    }

    [Fact]
    public void BuildParameterContainer_WithNoSecurities_ThrowsException()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.Securities = [];

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.ThrowsAny<Exception>(() => buildMethod!.Invoke(runner, null));
        Assert.Contains("At least one security", ex.InnerException?.Message);
    }

    [Fact]
    public void BuildParameterContainer_WithNoTimeFrames_ThrowsException()
    {
        // Arrange
        var config = CreateBasicConfiguration();
        config.TimeFrames = [];

        var runner = new BacktestRunner<MockStrategy>(config);
        var buildMethod = typeof(BacktestRunner<MockStrategy>)
            .GetMethod("BuildParameterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var ex = Assert.ThrowsAny<Exception>(() => buildMethod!.Invoke(runner, null));
        Assert.Contains("At least one timeframe", ex.InnerException?.Message);
    }
}

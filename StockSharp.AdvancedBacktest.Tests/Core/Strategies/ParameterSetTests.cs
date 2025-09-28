using StockSharp.AdvancedBacktest.Core.Strategies;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies;

/// <summary>
/// Unit tests for ParameterSet implementation
/// </summary>
public class ParameterSetTests
{
    [Fact]
    public void ParameterSet_WithValidDefinitions_ShouldInitializeCorrectly()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<int>("IntParam", 1, 100, 50),
            ParameterDefinition.CreateString("StringParam", "default", "Test parameter"),
            ParameterDefinition.CreateBoolean("BoolParam", true, "Boolean parameter")
        };

        // Act
        var parameterSet = new ParameterSet(definitions);

        // Assert
        Assert.Equal(3, parameterSet.Count);
        Assert.Equal(3, parameterSet.Definitions.Length);

        // Check default values
        Assert.Equal(50, parameterSet.GetValue<int>("IntParam"));
        Assert.Equal("default", parameterSet.GetValue("StringParam"));
        Assert.Equal(true, parameterSet.GetValue("BoolParam"));
    }

    [Fact]
    public void ParameterSet_SetValue_WithValidValue_ShouldSucceed()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<decimal>("Price", 0m, 1000m)
        };
        var parameterSet = new ParameterSet(definitions);

        // Act
        parameterSet.SetValue("Price", 123.45m);

        // Assert
        Assert.Equal(123.45m, parameterSet.GetValue<decimal>("Price"));
    }

    [Fact]
    public void ParameterSet_SetValue_WithInvalidValue_ShouldThrow()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<int>("Count", 1, 10)
        };
        var parameterSet = new ParameterSet(definitions);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => parameterSet.SetValue("Count", 15));
    }

    [Fact]
    public void ParameterSet_GetValue_WithNonExistentParameter_ShouldThrow()
    {
        // Arrange
        var parameterSet = new ParameterSet(Array.Empty<ParameterDefinition>());

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => parameterSet.GetValue<int>("NonExistent"));
    }

    [Fact]
    public void ParameterSet_Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<int>("Value", 0, 100, 50)
        };
        var original = new ParameterSet(definitions);
        original.SetValue("Value", 75);

        // Act
        var clone = original.Clone();
        clone.SetValue("Value", 25);

        // Assert
        Assert.Equal(75, original.GetValue<int>("Value"));
        Assert.Equal(25, clone.GetValue<int>("Value"));
    }

    [Fact]
    public void ParameterSet_GetSnapshot_ShouldReturnImmutableCopy()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateString("Name", "test"),
            ParameterDefinition.CreateNumeric<int>("Value", 0, 100, 42)
        };
        var parameterSet = new ParameterSet(definitions);

        // Act
        var snapshot = parameterSet.GetSnapshot();

        // Assert
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("test", snapshot["Name"]);
        Assert.Equal(42, snapshot["Value"]);

        // Verify it's truly immutable
        Assert.IsType<System.Collections.Immutable.ImmutableDictionary<string, object?>>(snapshot);
    }

    [Fact]
    public void ParameterSet_TryGetValue_WithExistingParameter_ShouldReturnTrue()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<decimal>("Amount", defaultValue: 100.5m)
        };
        var parameterSet = new ParameterSet(definitions);

        // Act
        var success = parameterSet.TryGetValue<decimal>("Amount", out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(100.5m, value);
    }

    [Fact]
    public void ParameterSet_TryGetValue_WithNonExistentParameter_ShouldReturnFalse()
    {
        // Arrange
        var parameterSet = new ParameterSet(Array.Empty<ParameterDefinition>());

        // Act
        var success = parameterSet.TryGetValue<int>("NonExistent", out var value);

        // Assert
        Assert.False(success);
        Assert.Equal(0, value); // Default value for int
    }

    [Theory]
    [InlineData(5, 10, 7, true)]    // Within range
    [InlineData(5, 10, 15, false)]  // Above range
    [InlineData(5, 10, 3, false)]   // Below range
    public void ParameterDefinition_ValidateValue_ShouldValidateRangeCorrectly(int min, int max, int value, bool expectedValid)
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", min, max);

        // Act
        var result = definition.ValidateValue(value);

        // Assert
        Assert.Equal(expectedValid, result.IsValid);
    }
}

/// <summary>
/// Integration tests for ParameterSetBuilder
/// </summary>
public class ParameterSetBuilderTests
{
    [Fact]
    public void ParameterSetBuilder_FluentAPI_ShouldBuildCorrectly()
    {
        // Arrange & Act
        var parameterSet = new ParameterSetBuilder()
            .AddNumeric<decimal>("Price", 0m, 1000m, 100m, "Asset price")
            .AddString("Symbol", "AAPL", "Trading symbol", true)
            .AddBoolean("Enabled", true, "Enable trading")
            .Build();

        // Assert
        Assert.Equal(3, parameterSet.Count);
        Assert.True(parameterSet.HasParameter("Price"));
        Assert.True(parameterSet.HasParameter("Symbol"));
        Assert.True(parameterSet.HasParameter("Enabled"));

        // Verify values
        Assert.Equal(100m, parameterSet.GetValue<decimal>("Price"));
        Assert.Equal("AAPL", parameterSet.GetValue("Symbol"));
        Assert.Equal(true, parameterSet.GetValue("Enabled"));
    }

    /*[Fact]
    public void ParameterSetBuilder_WithEnumParameter_ShouldWork()
    {
        // Arrange
        enum TestEnum { Value1, Value2, Value3 }

        // Act
        var parameterSet = new ParameterSetBuilder()
            .AddEnum<TestEnum>("EnumParam", TestEnum.Value2, "Test enum parameter")
            .Build();

        // Assert
        Assert.Equal(1, parameterSet.Count);
        Assert.Equal(TestEnum.Value2, parameterSet.GetValue("EnumParam"));
    }*/
}
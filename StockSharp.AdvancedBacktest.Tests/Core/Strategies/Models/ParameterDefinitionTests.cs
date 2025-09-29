using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using ParameterDefinition = StockSharp.AdvancedBacktest.Core.Configuration.Parameters.ParameterDefinition;
using System.Numerics;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies.Models;

/// <summary>
/// Tests for ParameterDefinition<T> record and related functionality
/// </summary>
public class ParameterDefinitionTests
{
    [Fact]
    public void ParameterDefinition_CreateNumeric_ShouldCreateValidDefinition()
    {
        // Act
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", 1, 100, 50, description: "Test description", isRequired: true);

        // Assert
        Assert.Equal("TestParam", definition.Name);
        Assert.Equal(typeof(int), definition.Type);
        Assert.Equal(1, definition.MinValue);
        Assert.Equal(100, definition.MaxValue);
        Assert.Equal(50, definition.DefaultValue);
        Assert.Equal("Test description", definition.Description);
        Assert.True(definition.IsRequired);
        Assert.True(definition.IsNumeric);
        Assert.True(definition.HasMinValue);
        Assert.True(definition.HasMaxValue);
        Assert.True(definition.HasDefaultValue);
    }

    [Fact]
    public void ParameterDefinition_CreateInteger_ShouldCreateValidDefinition()
    {
        // Act
        var definition = ParameterDefinition.CreateInteger("IntParam", 0, 1000, 500, 10, "Integer parameter", true);

        // Assert
        Assert.Equal("IntParam", definition.Name);
        Assert.Equal(typeof(int), definition.Type);
        Assert.Equal(0, definition.MinValue);
        Assert.Equal(1000, definition.MaxValue);
        Assert.Equal(500, definition.DefaultValue);
        Assert.Equal(10, definition.Step);
        Assert.Equal("Integer parameter", definition.Description);
        Assert.True(definition.IsRequired);
        Assert.True(definition.IsNumeric);
        Assert.True(definition.HasStep);
    }

    [Fact]
    public void ParameterDefinition_CreateDecimal_ShouldCreateValidDefinition()
    {
        // Act
        var definition = ParameterDefinition.CreateDecimal("DecimalParam", 0.1m, 99.9m, 50.0m, 0.1m, "Decimal parameter", false);

        // Assert
        Assert.Equal("DecimalParam", definition.Name);
        Assert.Equal(typeof(decimal), definition.Type);
        Assert.Equal(0.1m, definition.MinValue);
        Assert.Equal(99.9m, definition.MaxValue);
        Assert.Equal(50.0m, definition.DefaultValue);
        Assert.Equal(0.1m, definition.Step);
        Assert.Equal("Decimal parameter", definition.Description);
        Assert.False(definition.IsRequired);
        Assert.True(definition.IsNumeric);
        Assert.True(definition.HasStep);
    }

    [Fact]
    public void ParameterDefinition_CreateDouble_ShouldCreateValidDefinition()
    {
        // Act
        var definition = ParameterDefinition.CreateDouble("DoubleParam", 0.0, 100.0, 50.0, 1.0, "Double parameter", true);

        // Assert
        Assert.Equal("DoubleParam", definition.Name);
        Assert.Equal(typeof(double), definition.Type);
        Assert.Equal(0.0, definition.MinValue);
        Assert.Equal(100.0, definition.MaxValue);
        Assert.Equal(50.0, definition.DefaultValue);
        Assert.Equal(1.0, definition.Step);
        Assert.Equal("Double parameter", definition.Description);
        Assert.True(definition.IsRequired);
        Assert.True(definition.IsNumeric);
    }

    [Fact]
    public void ParameterDefinition_TypeName_ShouldReturnCorrectTypeName()
    {
        // Arrange
        var intDefinition = ParameterDefinition.CreateNumeric<int>("IntParam");
        var decimalDefinition = ParameterDefinition.CreateDecimal("DecimalParam");

        // Act & Assert
        Assert.Equal("Int32", intDefinition.TypeName);
        Assert.Equal("Decimal", decimalDefinition.TypeName);
    }

    [Fact]
    public void ParameterDefinition_FullTypeName_ShouldReturnCorrectFullTypeName()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<decimal>("DecimalParam");

        // Act & Assert
        Assert.Equal("System.Decimal", definition.FullTypeName);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_WithValidValue_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", 1, 100, 50);

        // Act
        var result = definition.ValidateValue(25);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_WithValueBelowMinimum_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", 10, 100, 50);

        // Act
        var result = definition.ValidateValue(5);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("below minimum", result.Errors[0]);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_WithValueAboveMaximum_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", 1, 50, 25);

        // Act
        var result = definition.ValidateValue(100);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("above maximum", result.Errors[0]);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_WithNullForRequired_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateInteger("RequiredParam", isRequired: true);

        // Act
        var result = definition.ValidateValue(null);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("required", result.Errors[0]);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_WithNullForOptional_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateInteger("OptionalParam", isRequired: false);

        // Act
        var result = definition.ValidateValue(null);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_WithWrongType_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("IntParam");

        // Act
        var result = definition.ValidateValue("not an int");

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("expects type", result.Errors[0]);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_WithStep_ValidValue_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateInteger("StepParam", 0, 100, 50, 10);

        // Act
        var result = definition.ValidateValue(20); // Valid: 0 + 2*10

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_WithStep_InvalidValue_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateInteger("StepParam", 0, 100, 50, 10);

        // Act
        var result = definition.ValidateValue(25); // Invalid: not aligned to step

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("not aligned to step", result.Errors[0]);
    }

    [Fact]
    public void ParameterDefinition_RecordEquality_ShouldWorkCorrectly()
    {
        // Arrange
        var definition1 = ParameterDefinition.CreateNumeric<int>("TestParam", 1, 100, 50);
        var definition2 = ParameterDefinition.CreateNumeric<int>("TestParam", 1, 100, 50);
        var definition3 = ParameterDefinition.CreateNumeric<int>("DifferentParam", 1, 100, 50);

        // Act & Assert
        Assert.Equal(definition1, definition2);
        Assert.NotEqual(definition1, definition3);
        Assert.Equal(definition1.GetHashCode(), definition2.GetHashCode());
    }

    [Fact]
    public void ParameterDefinition_WithValidValue_ShouldValidateCorrectly()
    {
        // Arrange
        var definition = ParameterDefinition.CreateDecimal("Price", 0m, 1000m, 100.50m);

        // Act
        var result = definition.ValidateValue(100.50m);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Price", definition.Name);
        Assert.Equal(typeof(decimal), definition.Type);
    }

    [Fact]
    public void ParameterDefinition_WithValueBelowMinimum_ShouldBeInvalid()
    {
        // Arrange
        var definition = ParameterDefinition.CreateDecimal("Price", 0m, 1000m, 100.50m);

        // Act
        var result = definition.ValidateValue(-10m);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("below minimum", result.Errors[0]);
    }

    [Fact]
    public void ParameterDefinition_WithValueAboveMaximum_ShouldBeInvalid()
    {
        // Arrange
        var definition = ParameterDefinition.CreateDecimal("Price", 0m, 1000m, 100.50m);

        // Act
        var result = definition.ValidateValue(1500m);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("above maximum", result.Errors[0]);
    }

    [Fact]
    public void ParameterDefinition_WithoutMinMax_ShouldAcceptAnyValue()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("Count");

        // Act & Assert
        var result1 = definition.ValidateValue(42);
        var result2 = definition.ValidateValue(-1000);
        var result3 = definition.ValidateValue(int.MaxValue);

        Assert.True(result1.IsValid);
        Assert.True(result2.IsValid);
        Assert.True(result3.IsValid);
    }

    [Fact]
    public void ParameterDefinition_GenerateValidValues_ShouldEnumerateCorrectly()
    {
        // Arrange
        var definition = ParameterDefinition.CreateInteger("TestParam", 0, 10, 5, 2);

        // Act
        var values = definition.GenerateValidValues().Cast<int>().ToList();

        // Assert
        Assert.Equal(new[] { 0, 2, 4, 6, 8, 10 }, values);
    }

    [Fact]
    public void ParameterDefinition_GetValidValueCount_ShouldCalculateCorrectly()
    {
        // Arrange
        var definition = ParameterDefinition.CreateInteger("TestParam", 0, 10, 5, 2);

        // Act
        var count = definition.GetValidValueCount();

        // Assert
        Assert.Equal(6, count); // 0, 2, 4, 6, 8, 10
    }

    [Fact]
    public void ParameterDefinition_WithRange_ShouldCreateNewInstance()
    {
        // Arrange
        var original = ParameterDefinition.CreateInteger("TestParam", 0, 100);

        // Act
        var updated = original.WithRange(10, 50);

        // Assert
        Assert.Equal(0, original.MinValue);
        Assert.Equal(100, original.MaxValue);
        Assert.Equal(10, updated.MinValue);
        Assert.Equal(50, updated.MaxValue);
    }

    [Fact]
    public void ParameterDefinition_WithStep_ShouldCreateNewInstance()
    {
        // Arrange
        var original = ParameterDefinition.CreateInteger("TestParam", 0, 100, step: 1);

        // Act
        var updated = original.WithStep(5);

        // Assert
        Assert.Equal(1, original.Step);
        Assert.Equal(5, updated.Step);
    }
}
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Numerics;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies.Models;

/// <summary>
/// Tests for ParameterDefinition record and related functionality
/// </summary>
public class ParameterDefinitionTests
{
    [Fact]
    public void ParameterDefinition_CreateNumeric_ShouldCreateValidDefinition()
    {
        // Act
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", 1, 100, 50, "Test description", true);

        // Assert
        Assert.Equal("TestParam", definition.Name);
        Assert.Equal(typeof(int), definition.Type);
        Assert.Equal(1, definition.MinValue);
        Assert.Equal(100, definition.MaxValue);
        Assert.Equal(50, definition.DefaultValue);
        Assert.Equal("Test description", definition.Description);
        Assert.True(definition.IsRequired);
        Assert.True(definition.IsNumeric);
        Assert.False(definition.IsString);
        Assert.False(definition.IsBoolean);
        Assert.False(definition.IsEnum);
    }

    [Fact]
    public void ParameterDefinition_CreateString_ShouldCreateValidDefinition()
    {
        // Act
        var definition = ParameterDefinition.CreateString("StringParam", "default", "String parameter", true, @"^[a-zA-Z]+$");

        // Assert
        Assert.Equal("StringParam", definition.Name);
        Assert.Equal(typeof(string), definition.Type);
        Assert.Equal("default", definition.DefaultValue);
        Assert.Equal("String parameter", definition.Description);
        Assert.True(definition.IsRequired);
        Assert.Equal(@"^[a-zA-Z]+$", definition.ValidationPattern);
        Assert.False(definition.IsNumeric);
        Assert.True(definition.IsString);
        Assert.False(definition.IsBoolean);
        Assert.False(definition.IsEnum);
    }

    [Fact]
    public void ParameterDefinition_CreateBoolean_ShouldCreateValidDefinition()
    {
        // Act
        var definition = ParameterDefinition.CreateBoolean("BoolParam", true, "Boolean parameter", false);

        // Assert
        Assert.Equal("BoolParam", definition.Name);
        Assert.Equal(typeof(bool), definition.Type);
        Assert.Equal(true, definition.DefaultValue);
        Assert.Equal("Boolean parameter", definition.Description);
        Assert.False(definition.IsRequired);
        Assert.False(definition.IsNumeric);
        Assert.False(definition.IsString);
        Assert.True(definition.IsBoolean);
        Assert.False(definition.IsEnum);
    }

    public enum TestEnum { Value1, Value2, Value3 }

    [Fact]
    public void ParameterDefinition_CreateEnum_ShouldCreateValidDefinition()
    {
        // Act
        var definition = ParameterDefinition.CreateEnum<TestEnum>("EnumParam", TestEnum.Value2, "Enum parameter", true);

        // Assert
        Assert.Equal("EnumParam", definition.Name);
        Assert.Equal(typeof(TestEnum), definition.Type);
        Assert.Equal(TestEnum.Value2, definition.DefaultValue);
        Assert.Equal("Enum parameter", definition.Description);
        Assert.True(definition.IsRequired);
        Assert.False(definition.IsNumeric);
        Assert.False(definition.IsString);
        Assert.False(definition.IsBoolean);
        Assert.True(definition.IsEnum);
    }

    [Fact]
    public void ParameterDefinition_TypeName_ShouldReturnCorrectTypeName()
    {
        // Arrange
        var intDefinition = ParameterDefinition.CreateNumeric<int>("IntParam");
        var stringDefinition = ParameterDefinition.CreateString("StringParam");

        // Act & Assert
        Assert.Equal("Int32", intDefinition.TypeName);
        Assert.Equal("String", stringDefinition.TypeName);
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
        var definition = ParameterDefinition.CreateString("RequiredParam", isRequired: true);

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
        var definition = ParameterDefinition.CreateString("OptionalParam", isRequired: false);

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
    public void ParameterDefinition_ValidateValue_StringWithPattern_ValidValue_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("EmailParam", validationPattern: @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");

        // Act
        var result = definition.ValidateValue("test@example.com");

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ParameterDefinition_ValidateValue_StringWithPattern_InvalidValue_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("EmailParam", validationPattern: @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");

        // Act
        var result = definition.ValidateValue("not-an-email");

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("does not match required pattern", result.Errors[0]);
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
    public void TypedParameter_WithValidValue_ShouldBeValid()
    {
        // Arrange
        var parameter = new TypedParameter<decimal>
        {
            Name = "Price",
            Value = 100.50m,
            MinValue = 0m,
            MaxValue = 1000m
        };

        // Act
        var isValid = parameter.IsValid();
        var error = parameter.GetValidationError();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void TypedParameter_WithValueBelowMinimum_ShouldBeInvalid()
    {
        // Arrange
        var parameter = new TypedParameter<decimal>
        {
            Name = "Price",
            Value = -10m,
            MinValue = 0m,
            MaxValue = 1000m
        };

        // Act
        var isValid = parameter.IsValid();
        var error = parameter.GetValidationError();

        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("below minimum", error);
    }

    [Fact]
    public void TypedParameter_WithValueAboveMaximum_ShouldBeInvalid()
    {
        // Arrange
        var parameter = new TypedParameter<decimal>
        {
            Name = "Price",
            Value = 1500m,
            MinValue = 0m,
            MaxValue = 1000m
        };

        // Act
        var isValid = parameter.IsValid();
        var error = parameter.GetValidationError();

        // Assert
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("above maximum", error);
    }

    [Fact]
    public void TypedParameter_WithoutMinMax_ShouldBeValid()
    {
        // Arrange
        var parameter = new TypedParameter<int>
        {
            Name = "Count",
            Value = 42
        };

        // Act
        var isValid = parameter.IsValid();
        var error = parameter.GetValidationError();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }
}
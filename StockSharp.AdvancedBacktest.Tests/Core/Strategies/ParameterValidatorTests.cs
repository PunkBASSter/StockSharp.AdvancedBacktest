using Microsoft.Extensions.Logging;
using StockSharp.AdvancedBacktest.Core.Strategies;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using StockSharp.BusinessEntities;
using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies;

/// <summary>
/// Tests for ParameterValidator implementation
/// </summary>
public class ParameterValidatorTests
{
    private readonly ILogger<ParameterValidator> _logger;
    private readonly ParameterValidator _validator;

    public ParameterValidatorTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ParameterValidator>();
        _validator = new ParameterValidator();
    }

    [Fact]
    public void ValidateParameter_WithValidNumericValue_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", 1, 100, 50);

        // Act
        var result = _validator.ValidateParameter("TestParam", 25, definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidateParameter_WithNullName_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("TestParam");

        // Act
        var result = _validator.ValidateParameter(null!, "value", definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("cannot be null or empty", result.Errors[0]);
    }

    [Fact]
    public void ValidateParameter_WithNullDefinition_ShouldReturnError()
    {
        // Act
        var result = _validator.ValidateParameter("TestParam", "value", null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("is null", result.Errors[0]);
    }

    [Fact]
    public void ValidateParameter_RequiredParameterWithNull_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("RequiredParam", isRequired: true);

        // Act
        var result = _validator.ValidateParameter("RequiredParam", null, definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("missing", result.Errors[0]);
    }

    [Fact]
    public void ValidateParameter_OptionalParameterWithNull_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("OptionalParam", isRequired: false);

        // Act
        var result = _validator.ValidateParameter("OptionalParam", null, definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidateParameter_NumericValueBelowMinimum_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", 10, 100, 50);

        // Act
        var result = _validator.ValidateParameter("TestParam", 5, definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("below minimum", result.Errors[0]);
    }

    [Fact]
    public void ValidateParameter_NumericValueAboveMaximum_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("TestParam", 1, 50, 25);

        // Act
        var result = _validator.ValidateParameter("TestParam", 100, definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("above maximum", result.Errors[0]);
    }

    [Fact]
    public void ValidateParameter_NumericValueNearMinimum_ShouldReturnWarning()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<decimal>("TestParam", 0m, 100m, 50m);

        // Act
        var result = _validator.ValidateParameter("TestParam", 2m, definition); // Very close to minimum

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.HasWarnings);
        Assert.Contains("very close to minimum", result.Warnings[0]);
    }

    [Fact]
    public void ValidateParameter_StringWithValidPattern_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("EmailParam", validationPattern: @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");

        // Act
        var result = _validator.ValidateParameter("EmailParam", "test@example.com", definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidateParameter_StringWithInvalidPattern_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("EmailParam", validationPattern: @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");

        // Act
        var result = _validator.ValidateParameter("EmailParam", "invalid-email", definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("does not match required pattern", result.Errors[0]);
    }

    [Fact]
    public void ValidateParameter_VeryLongString_ShouldReturnWarning()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("LongStringParam");
        var longString = new string('a', 1500); // Longer than 1000 characters

        // Act
        var result = _validator.ValidateParameter("LongStringParam", longString, definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.HasWarnings);
        Assert.Contains("very long", result.Warnings[0]);
    }

    [Fact]
    public void ValidateParameterSet_WithNullParameterSet_ShouldReturnError()
    {
        // Act
        var result = _validator.ValidateParameterSet(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("cannot be null", result.Errors[0]);
    }

    [Fact]
    public void ValidateParameterSet_WithValidParameters_ShouldReturnSuccess()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<int>("IntParam", 1, 100, 50),
            ParameterDefinition.CreateString("StringParam", "default")
        };
        var parameterSet = new ParameterSet(definitions, _validator);

        // Act
        var result = _validator.ValidateParameterSet(parameterSet);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateParameterSet_WithInvalidParameters_ShouldReturnErrors()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<int>("IntParam", 10, 100, 50),
            ParameterDefinition.CreateString("RequiredParam", isRequired: true)
        };
        var parameterSet = new ParameterSet(definitions, _validator);

        // Act & Assert - SetValue should throw for invalid value
        var exception = Assert.Throws<ArgumentException>(() => parameterSet.SetValue("IntParam", 5)); // Below minimum
        Assert.Contains("Parameter 'IntParam' validation failed", exception.Message);
    }

    [Fact]
    public void ValidateDependencies_WithNullParameterSet_ShouldReturnError()
    {
        // Act
        var result = _validator.ValidateDependencies(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("cannot be null", result.Errors[0]);
    }

    [Fact]
    public void ValidateDependencies_WithValidParameterSet_ShouldReturnSuccess()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<int>("IntParam", 1, 100, 50)
        };
        var parameterSet = new ParameterSet(definitions, _validator);

        // Act
        var result = _validator.ValidateDependencies(parameterSet);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(decimal), true)]
    [InlineData(typeof(double), true)]
    [InlineData(typeof(float), true)]
    [InlineData(typeof(long), true)]
    [InlineData(typeof(short), true)]
    [InlineData(typeof(byte), true)]
    [InlineData(typeof(uint), true)]
    [InlineData(typeof(ulong), true)]
    [InlineData(typeof(ushort), true)]
    [InlineData(typeof(sbyte), true)]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(decimal?), true)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(bool), false)]
    [InlineData(typeof(DateTime), false)]
    public void IsNumericType_ShouldIdentifyNumericTypesCorrectly(Type type, bool expectedIsNumeric)
    {
        // This test would require making the IsNumericType method public or internal
        // For now, we'll test it indirectly through parameter validation
        var definition = new ParameterDefinition("TestParam", type);

        // Act & Assert
        Assert.Equal(expectedIsNumeric, definition.IsNumeric);
    }

    [Fact]
    public void ValidateParameter_WrongTypeIncompatibility_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateNumeric<int>("IntParam");

        // Act
        var result = _validator.ValidateParameter("IntParam", "not a number", definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("expects type", result.Errors[0]);
    }

    public enum TestEnum { Value1, Value2, Value3 }

    [Fact]
    public void ValidateParameter_ValidEnumValue_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateEnum<TestEnum>("EnumParam");

        // Act
        var result = _validator.ValidateParameter("EnumParam", TestEnum.Value2, definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidateParameter_BooleanValue_ShouldReturnSuccess()
    {
        // Arrange
        var definition = ParameterDefinition.CreateBoolean("BoolParam");

        // Act
        var result = _validator.ValidateParameter("BoolParam", true, definition);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidateParameter_ExceptionDuringValidation_ShouldReturnError()
    {
        // Arrange
        var definition = ParameterDefinition.CreateString("TestParam", validationPattern: "["); // Invalid regex

        // Act
        var result = _validator.ValidateParameter("TestParam", "test", definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains("validation failed", result.Errors[0]);
    }
}
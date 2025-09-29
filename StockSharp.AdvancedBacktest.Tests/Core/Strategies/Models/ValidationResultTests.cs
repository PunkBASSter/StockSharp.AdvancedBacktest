using StockSharp.AdvancedBacktest.Core.Configuration.Validation;
using ValidationResult = StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult;
using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies.Models;

/// <summary>
/// Tests for ValidationResult record and related functionality
/// </summary>
public class ValidationResultTests
{
    [Fact]
    public void ValidationResult_Success_ShouldBeValid()
    {
        // Arrange & Act
        var result = ValidationResult.Success;

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Equal(0, result.TotalIssues);
        Assert.True(result.Errors.IsDefaultOrEmpty);
        Assert.True(result.Warnings.IsDefaultOrEmpty);
    }

    [Fact]
    public void ValidationResult_CreateSuccess_ShouldReturnValidResult()
    {
        // Act
        var result = ValidationResult.CreateSuccess();

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Equal(0, result.TotalIssues);
    }

    [Fact]
    public void ValidationResult_SuccessWithWarnings_Array_ShouldReturnValidResultWithWarnings()
    {
        // Arrange
        var warnings = new[] { "Warning 1", "Warning 2" };

        // Act
        var result = ValidationResult.SuccessWithWarnings(warnings);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        Assert.True(result.HasWarnings);
        Assert.Equal(2, result.TotalIssues);
        Assert.Equal(2, result.Warnings.Length);
        Assert.Equal("Warning 1", result.Warnings[0]);
        Assert.Equal("Warning 2", result.Warnings[1]);
    }

    [Fact]
    public void ValidationResult_SuccessWithWarnings_Enumerable_ShouldReturnValidResultWithWarnings()
    {
        // Arrange
        var warnings = new List<string> { "Warning 1", "Warning 2" };

        // Act
        var result = ValidationResult.SuccessWithWarnings(warnings);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        Assert.True(result.HasWarnings);
        Assert.Equal(2, result.TotalIssues);
        Assert.Equal(2, result.Warnings.Length);
    }

    [Fact]
    public void ValidationResult_Failure_Array_ShouldReturnInvalidResult()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        var result = ValidationResult.Failure(errors);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.False(result.HasWarnings);
        Assert.Equal(2, result.TotalIssues);
        Assert.Equal(2, result.Errors.Length);
        Assert.Equal("Error 1", result.Errors[0]);
        Assert.Equal("Error 2", result.Errors[1]);
    }

    [Fact]
    public void ValidationResult_Failure_Enumerable_ShouldReturnInvalidResult()
    {
        // Arrange
        var errors = new List<string> { "Error 1", "Error 2" };

        // Act
        var result = ValidationResult.Failure(errors);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Equal(2, result.Errors.Length);
    }

    [Fact]
    public void ValidationResult_Failure_WithWarnings_ShouldReturnInvalidResultWithWarnings()
    {
        // Arrange
        var errors = new[] { "Error 1" };
        var warnings = new[] { "Warning 1" };

        // Act
        var result = ValidationResult.Failure(errors, warnings);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.True(result.HasWarnings);
        Assert.Equal(2, result.TotalIssues);
        Assert.Single(result.Errors);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void ValidationResult_Combine_MultipleResults_ShouldCombineCorrectly()
    {
        // Arrange
        var result1 = ValidationResult.SuccessWithWarnings("Warning 1");
        var result2 = ValidationResult.Failure("Error 1");
        var result3 = ValidationResult.Failure(new[] { "Error 2" }, new[] { "Warning 2" });

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.False(combined.IsValid);
        Assert.True(combined.HasErrors);
        Assert.True(combined.HasWarnings);
        Assert.Equal(4, combined.TotalIssues);
        Assert.Equal(2, combined.Errors.Length);
        Assert.Equal(2, combined.Warnings.Length);
        Assert.Contains("Error 1", combined.Errors);
        Assert.Contains("Error 2", combined.Errors);
        Assert.Contains("Warning 1", combined.Warnings);
        Assert.Contains("Warning 2", combined.Warnings);
    }

    [Fact]
    public void ValidationResult_Combine_EmptyArray_ShouldReturnSuccess()
    {
        // Act
        var result = ValidationResult.Combine();

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void ValidationResult_Combine_EnumerableOverload_ShouldWorkCorrectly()
    {
        // Arrange
        var results = new List<ValidationResult>
        {
            ValidationResult.SuccessWithWarnings("Warning 1"),
            ValidationResult.Failure("Error 1")
        };

        // Act
        var combined = ValidationResult.Combine(results);

        // Assert
        Assert.False(combined.IsValid);
        Assert.True(combined.HasErrors);
        Assert.True(combined.HasWarnings);
    }

    [Fact]
    public void ValidationResult_WithError_ShouldAddError()
    {
        // Arrange
        var result = ValidationResult.CreateSuccess();

        // Act
        var newResult = result.WithError("New error");

        // Assert
        Assert.False(newResult.IsValid);
        Assert.True(newResult.HasErrors);
        Assert.Single(newResult.Errors);
        Assert.Equal("New error", newResult.Errors[0]);
    }

    [Fact]
    public void ValidationResult_WithErrors_ShouldAddMultipleErrors()
    {
        // Arrange
        var result = ValidationResult.CreateSuccess();

        // Act
        var newResult = result.WithErrors("Error 1", "Error 2");

        // Assert
        Assert.False(newResult.IsValid);
        Assert.True(newResult.HasErrors);
        Assert.Equal(2, newResult.Errors.Length);
    }

    [Fact]
    public void ValidationResult_WithWarning_ShouldAddWarning()
    {
        // Arrange
        var result = ValidationResult.CreateSuccess();

        // Act
        var newResult = result.WithWarning("New warning");

        // Assert
        Assert.True(newResult.IsValid);
        Assert.True(newResult.HasWarnings);
        Assert.Single(newResult.Warnings);
        Assert.Equal("New warning", newResult.Warnings[0]);
    }

    [Fact]
    public void ValidationResult_WithWarnings_ShouldAddMultipleWarnings()
    {
        // Arrange
        var result = ValidationResult.CreateSuccess();

        // Act
        var newResult = result.WithWarnings("Warning 1", "Warning 2");

        // Assert
        Assert.True(newResult.IsValid);
        Assert.True(newResult.HasWarnings);
        Assert.Equal(2, newResult.Warnings.Length);
    }

    [Fact]
    public void ValidationResult_GetFormattedIssues_WithErrorsAndWarnings_ShouldFormatCorrectly()
    {
        // Arrange
        var result = ValidationResult.Failure(new[] { "Error 1", "Error 2" }, new[] { "Warning 1" });

        // Act
        var formatted = result.GetFormattedIssues();

        // Assert
        Assert.Contains("Errors:", formatted);
        Assert.Contains("- Error 1", formatted);
        Assert.Contains("- Error 2", formatted);
        Assert.Contains("Warnings:", formatted);
        Assert.Contains("- Warning 1", formatted);
    }

    [Fact]
    public void ValidationResult_GetFormattedIssues_WithOnlyErrors_ShouldFormatCorrectly()
    {
        // Arrange
        var result = ValidationResult.Failure("Error 1");

        // Act
        var formatted = result.GetFormattedIssues();

        // Assert
        Assert.Contains("Errors:", formatted);
        Assert.Contains("- Error 1", formatted);
        Assert.DoesNotContain("Warnings:", formatted);
    }

    [Fact]
    public void ValidationResult_GetFormattedIssues_WithOnlyWarnings_ShouldFormatCorrectly()
    {
        // Arrange
        var result = ValidationResult.SuccessWithWarnings("Warning 1");

        // Act
        var formatted = result.GetFormattedIssues();

        // Assert
        Assert.Contains("Warnings:", formatted);
        Assert.Contains("- Warning 1", formatted);
        Assert.DoesNotContain("Errors:", formatted);
    }

    [Fact]
    public void ValidationResult_ImplicitConversion_FromBool_ShouldWorkCorrectly()
    {
        // Act
        StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult validResult = true;
        StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult invalidResult = false;

        // Assert
        Assert.True(validResult.IsValid);
        Assert.False(invalidResult.IsValid);
        Assert.True(invalidResult.HasErrors);
    }

    [Fact]
    public void ValidationResult_ImplicitConversion_ToBool_ShouldWorkCorrectly()
    {
        // Arrange
        var validResult = StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult.CreateSuccess();
        var invalidResult = StockSharp.AdvancedBacktest.Core.Configuration.Validation.ValidationResult.Failure("Error");

        // Act & Assert
        Assert.True(validResult);
        Assert.False(invalidResult);
    }

    [Fact]
    public void ValidationResult_ToString_ShouldReturnCorrectDescription()
    {
        // Arrange
        var validResult = ValidationResult.CreateSuccess();
        var validWithWarnings = ValidationResult.SuccessWithWarnings("Warning");
        var invalidResult = ValidationResult.Failure("Error");
        var invalidWithWarnings = ValidationResult.Failure(new[] { "Error" }, new[] { "Warning" });

        // Act & Assert
        Assert.Equal("Valid", validResult.ToString());
        Assert.Contains("Valid with", validWithWarnings.ToString());
        Assert.Contains("warning", validWithWarnings.ToString());
        Assert.Contains("Invalid:", invalidResult.ToString());
        Assert.Contains("error", invalidResult.ToString());
        Assert.Contains("warning", invalidWithWarnings.ToString());
    }

    [Fact]
    public void ValidationResult_RecordEquality_ShouldWorkCorrectly()
    {
        // Arrange
        var result1 = ValidationResult.Failure("Error 1");
        var result2 = ValidationResult.Failure("Error 1");
        var result3 = ValidationResult.Failure("Error 2");

        // Act & Assert
        Assert.Equal(result1, result2);
        Assert.NotEqual(result1, result3);
        Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
    }

    [Fact]
    public void ValidationResult_ImmutableArrays_ShouldBeImmutable()
    {
        // Arrange
        var result = ValidationResult.Failure("Error 1");

        // Act - Try to modify the errors array (this should compile but not affect the original)
        var errors = result.Errors;
        // errors cannot be modified as it's ImmutableArray

        // Assert
        Assert.Single(result.Errors);
        Assert.Equal("Error 1", result.Errors[0]);
    }
}
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Models;

public sealed class ValidationMetadataTests
{
	[Fact]
	public void ValidationMetadata_WithErrors_ShouldHaveErrorsTrue()
	{
		var metadata = new ValidationMetadata
		{
			Errors = new List<ValidationError>
			{
				new("Field1", "Error message", "Error")
			}
		};

		Assert.True(metadata.HasErrors);
	}

	[Fact]
	public void ValidationMetadata_WithWarnings_ShouldHaveWarningsTrue()
	{
		var metadata = new ValidationMetadata
		{
			Errors = new List<ValidationError>
			{
				new("Field1", "Warning message", "Warning")
			}
		};

		Assert.True(metadata.HasWarnings);
	}

	[Fact]
	public void ValidationMetadata_WithNoErrors_ShouldHaveErrorsFalse()
	{
		var metadata = new ValidationMetadata
		{
			Errors = new List<ValidationError>
			{
				new("Field1", "Warning message", "Warning")
			}
		};

		Assert.False(metadata.HasErrors);
	}

	[Fact]
	public void ValidationMetadata_WithNoWarnings_ShouldHaveWarningsFalse()
	{
		var metadata = new ValidationMetadata
		{
			Errors = new List<ValidationError>
			{
				new("Field1", "Error message", "Error")
			}
		};

		Assert.False(metadata.HasWarnings);
	}

	[Fact]
	public void ValidationMetadata_ToJson_ShouldSerializeCorrectly()
	{
		var metadata = new ValidationMetadata
		{
			Errors = new List<ValidationError>
			{
				new("Field1", "Error message", "Error"),
				new("Field2", "Warning message", "Warning")
			}
		};

		var json = metadata.ToJson();

		Assert.NotNull(json);
		Assert.Contains("Field1", json);
		Assert.Contains("Error message", json);
		Assert.Contains("Field2", json);
		Assert.Contains("Warning message", json);
	}

	[Fact]
	public void ValidationMetadata_FromJson_ShouldDeserializeCorrectly()
	{
		var json = """[{"field":"Field1","error":"Error message","severity":"Error"}]""";

		var metadata = ValidationMetadata.FromJson(json);

		Assert.NotNull(metadata);
		Assert.Single(metadata.Errors);
		Assert.Equal("Field1", metadata.Errors[0].Field);
		Assert.Equal("Error message", metadata.Errors[0].Error);
		Assert.Equal("Error", metadata.Errors[0].Severity);
	}

	[Fact]
	public void ValidationMetadata_FromJson_WithNull_ShouldReturnNull()
	{
		var metadata = ValidationMetadata.FromJson(null);

		Assert.Null(metadata);
	}

	[Fact]
	public void ValidationMetadata_FromJson_WithEmptyString_ShouldReturnNull()
	{
		var metadata = ValidationMetadata.FromJson("");

		Assert.Null(metadata);
	}

	[Fact]
	public void ValidationMetadata_RoundTrip_ShouldPreserveData()
	{
		var original = new ValidationMetadata
		{
			Errors = new List<ValidationError>
			{
				new("Field1", "Error1", "Error"),
				new("Field2", "Warning1", "Warning"),
				new("Field3", "Info1", "Info")
			}
		};

		var json = original.ToJson();
		var deserialized = ValidationMetadata.FromJson(json);

		Assert.NotNull(deserialized);
		Assert.Equal(3, deserialized.Errors.Count);
		Assert.Equal("Field1", deserialized.Errors[0].Field);
		Assert.Equal("Error1", deserialized.Errors[0].Error);
		Assert.Equal("Error", deserialized.Errors[0].Severity);
	}

	[Fact]
	public void ValidationError_ShouldStoreAllProperties()
	{
		var error = new ValidationError("TestField", "Test error message", "Error");

		Assert.Equal("TestField", error.Field);
		Assert.Equal("Test error message", error.Error);
		Assert.Equal("Error", error.Severity);
	}

	[Fact]
	public void ValidationMetadata_WithMixedSeverities_ShouldDetectBoth()
	{
		var metadata = new ValidationMetadata
		{
			Errors = new List<ValidationError>
			{
				new("Field1", "Error message", "Error"),
				new("Field2", "Warning message", "Warning")
			}
		};

		Assert.True(metadata.HasErrors);
		Assert.True(metadata.HasWarnings);
	}

	[Fact]
	public void ValidationMetadata_WithEmptyList_ShouldHaveNoErrorsOrWarnings()
	{
		var metadata = new ValidationMetadata
		{
			Errors = new List<ValidationError>()
		};

		Assert.False(metadata.HasErrors);
		Assert.False(metadata.HasWarnings);
	}
}

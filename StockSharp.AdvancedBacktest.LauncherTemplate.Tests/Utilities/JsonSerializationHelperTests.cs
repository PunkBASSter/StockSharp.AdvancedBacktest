using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;
using StockSharp.AdvancedBacktest.PerformanceValidation;
using Xunit;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.Utilities;

public class JsonSerializationHelperTests
{
    private enum TestEnum
    {
        Value1,
        Value2,
        LongValueName
    }

    private class TestModel
    {
        public decimal Price { get; set; }
        public decimal? OptionalPrice { get; set; }
        public TestEnum Status { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void DecimalStringConverter_PreservesMaximumPrecision()
    {
        // Arrange - Use a decimal with maximum precision (28-29 significant digits)
        var value = 123456789012345678901234567.89m;
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var deserialized = JsonSerializer.Deserialize<decimal>(json, options);

        // Assert
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void DecimalStringConverter_PreservesFinancialPrecision()
    {
        // Arrange - Typical financial value with many decimal places
        var value = 123456.789012345678901234567m;
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var deserialized = JsonSerializer.Deserialize<decimal>(json, options);

        // Assert
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void DecimalStringConverter_HandlesZero()
    {
        // Arrange
        var value = 0m;
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var deserialized = JsonSerializer.Deserialize<decimal>(json, options);

        // Assert
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void DecimalStringConverter_HandlesNegativeValues()
    {
        // Arrange
        var value = -987654.321098765432109876543m;
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var deserialized = JsonSerializer.Deserialize<decimal>(json, options);

        // Assert
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void DecimalStringConverter_HandlesVerySmallValues()
    {
        // Arrange
        var value = 0.000000000000000000000000001m;
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var deserialized = JsonSerializer.Deserialize<decimal>(json, options);

        // Assert
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void DecimalStringConverter_HandlesMaxDecimalValue()
    {
        // Arrange
        var value = decimal.MaxValue;
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var deserialized = JsonSerializer.Deserialize<decimal>(json, options);

        // Assert
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void DecimalStringConverter_HandlesMinDecimalValue()
    {
        // Arrange
        var value = decimal.MinValue;
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(value, options);
        var deserialized = JsonSerializer.Deserialize<decimal>(json, options);

        // Assert
        Assert.Equal(value, deserialized);
    }

    [Fact]
    public void DecimalStringConverter_SerializesAsString()
    {
        // Arrange
        var value = 123.456m;
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(value, options);

        // Assert - Should be wrapped in quotes (string format)
        Assert.StartsWith("\"", json);
        Assert.EndsWith("\"", json);
    }

    [Fact]
    public void EnumConverter_SerializesAsString()
    {
        // Arrange
        var model = new TestModel { Status = TestEnum.LongValueName, Price = 100m, Name = "Test" };
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(model, options);

        // Assert - Enum should be serialized as string, not number
        Assert.Contains("\"LongValueName\"", json);
        Assert.DoesNotContain("\"status\": 2", json);
    }

    [Fact]
    public void EnumConverter_DeserializesFromString()
    {
        // Arrange
        var json = "{\"status\": \"Value2\", \"price\": \"100\", \"name\": \"Test\"}";
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var model = JsonSerializer.Deserialize<TestModel>(json, options);

        // Assert
        Assert.NotNull(model);
        Assert.Equal(TestEnum.Value2, model.Status);
    }

    [Fact]
    public void StandardOptions_UsesCamelCaseNaming()
    {
        // Arrange
        var model = new TestModel { Price = 100m, Name = "TestName", Status = TestEnum.Value1 };
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(model, options);

        // Assert - Properties should be camelCase
        Assert.Contains("\"price\"", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"status\"", json);
    }

    [Fact]
    public void StandardOptions_IsCaseInsensitive()
    {
        // Arrange - JSON with different casing
        var json = "{\"Price\": \"100\", \"Name\": \"Test\", \"Status\": \"Value1\"}";
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var model = JsonSerializer.Deserialize<TestModel>(json, options);

        // Assert - Should deserialize despite PascalCase
        Assert.NotNull(model);
        Assert.Equal(100m, model.Price);
        Assert.Equal("Test", model.Name);
    }

    [Fact]
    public void StandardOptions_OmitsNullValues()
    {
        // Arrange
        var model = new TestModel { Price = 100m, OptionalPrice = null, Name = "Test", Status = TestEnum.Value1 };
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(model, options);

        // Assert - Null properties should not be in JSON
        Assert.DoesNotContain("optionalPrice", json);
    }

    [Fact]
    public void StandardOptions_IncludesNonNullOptionalValues()
    {
        // Arrange
        var model = new TestModel { Price = 100m, OptionalPrice = 50m, Name = "Test", Status = TestEnum.Value1 };
        var options = JsonSerializationHelper.CreateStandardOptions();

        // Act
        var json = JsonSerializer.Serialize(model, options);

        // Assert - Non-null optional values should be included
        Assert.Contains("\"optionalPrice\"", json);
    }

    [Fact]
    public void Serialize_WithDefaultOptions_ReturnsFormattedJson()
    {
        // Arrange
        var model = new TestModel { Price = 100.50m, Name = "Test", Status = TestEnum.Value1 };

        // Act
        var json = JsonSerializationHelper.Serialize(model);

        // Assert
        Assert.Contains("\"price\"", json);
        Assert.Contains("\"100.5\"", json);
        Assert.Contains("\"Value1\"", json);
    }

    [Fact]
    public void Deserialize_WithValidJson_ReturnsObject()
    {
        // Arrange
        var json = "{\"price\": \"100.50\", \"name\": \"Test\", \"status\": \"Value1\"}";

        // Act
        var model = JsonSerializationHelper.Deserialize<TestModel>(json);

        // Assert
        Assert.NotNull(model);
        Assert.Equal(100.50m, model.Price);
        Assert.Equal("Test", model.Name);
        Assert.Equal(TestEnum.Value1, model.Status);
    }

    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new TestModel
        {
            Price = 123456.789012345678901234567m,
            OptionalPrice = 987.654321098765432109876m,
            Name = "Complex Test",
            Status = TestEnum.LongValueName
        };

        // Act
        var json = JsonSerializationHelper.Serialize(original);
        var deserialized = JsonSerializationHelper.Deserialize<TestModel>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Price, deserialized.Price);
        Assert.Equal(original.OptionalPrice, deserialized.OptionalPrice);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Status, deserialized.Status);
    }

    [Fact]
    public async Task SerializeToFileAsync_CreatesFileWithCorrectContent()
    {
        // Arrange
        var model = new TestModel { Price = 100m, Name = "FileTest", Status = TestEnum.Value1 };
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            // Act
            await JsonSerializationHelper.SerializeToFileAsync(model, tempFile);

            // Assert
            Assert.True(File.Exists(tempFile));
            var json = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("\"name\"", json); // camelCase
            Assert.Contains("\"100\"", json);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task DeserializeFromFileAsync_ReadsFileCorrectly()
    {
        // Arrange
        var model = new TestModel { Price = 200m, Name = "FileRead", Status = TestEnum.Value2 };
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            await JsonSerializationHelper.SerializeToFileAsync(model, tempFile);

            // Act
            var deserialized = await JsonSerializationHelper.DeserializeFromFileAsync<TestModel>(tempFile);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(model.Price, deserialized.Price);
            Assert.Equal(model.Name, deserialized.Name);
            Assert.Equal(model.Status, deserialized.Status);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task DeserializeFromFileAsync_ThrowsWhenFileNotFound()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await JsonSerializationHelper.DeserializeFromFileAsync<TestModel>(nonExistentFile));
    }

    [Fact]
    public void SerializeToFile_CreatesFileWithCorrectContent()
    {
        // Arrange
        var model = new TestModel { Price = 300m, Name = "SyncFileTest", Status = TestEnum.Value1 };
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            // Act
            JsonSerializationHelper.SerializeToFile(model, tempFile);

            // Assert
            Assert.True(File.Exists(tempFile));
            var json = File.ReadAllText(tempFile);
            Assert.Contains("\"name\"", json);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void DeserializeFromFile_ReadsFileCorrectly()
    {
        // Arrange
        var model = new TestModel { Price = 400m, Name = "SyncRead", Status = TestEnum.Value2 };
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");

        try
        {
            JsonSerializationHelper.SerializeToFile(model, tempFile);

            // Act
            var deserialized = JsonSerializationHelper.DeserializeFromFile<TestModel>(tempFile);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(model.Price, deserialized.Price);
            Assert.Equal(model.Name, deserialized.Name);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void SerializeToFile_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"testdir_{Guid.NewGuid()}");
        var tempFile = Path.Combine(tempDir, "test.json");
        var model = new TestModel { Price = 500m, Name = "DirTest", Status = TestEnum.Value1 };

        try
        {
            // Act
            JsonSerializationHelper.SerializeToFile(model, tempFile);

            // Assert
            Assert.True(Directory.Exists(tempDir));
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void BacktestConfiguration_RoundTrip_PreservesDecimalPrecision()
    {
        // Arrange
        var config = new BacktestConfiguration
        {
            StrategyName = "PrecisionTest",
            StrategyVersion = "1.0.0",
            TrainingStartDate = DateTimeOffset.UtcNow,
            TrainingEndDate = DateTimeOffset.UtcNow.AddDays(100),
            ValidationStartDate = DateTimeOffset.UtcNow.AddDays(100),
            ValidationEndDate = DateTimeOffset.UtcNow.AddDays(200),
            Securities = ["AAPL"],
            OptimizableParameters = new Dictionary<string, ParameterDefinition>
            {
                ["Period"] = new ParameterDefinition
                {
                    Name = "Period",
                    Type = "int",
                    MinValue = JsonSerializer.SerializeToElement(10),
                    MaxValue = JsonSerializer.SerializeToElement(50),
                    StepValue = JsonSerializer.SerializeToElement(5)
                }
            },
            HistoryPath = "C:\\Data",
            InitialCapital = 123456.789012345678901234567m,
            TradeVolume = 0.123456789012345678901234m
        };

        // Act
        var json = JsonSerializationHelper.Serialize(config);
        var deserialized = JsonSerializationHelper.Deserialize<BacktestConfiguration>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(config.InitialCapital, deserialized.InitialCapital);
        Assert.Equal(config.TradeVolume, deserialized.TradeVolume);
        Assert.Equal(config.StrategyName, deserialized.StrategyName);
    }

    [Fact]
    public void StrategyParametersConfig_RoundTrip_PreservesDecimalPrecision()
    {
        // Arrange
        var config = new StrategyParametersConfig
        {
            StrategyName = "FinancialPrecision",
            StrategyVersion = "2.0.0",
            StrategyHash = "abc123def456abc123def456abc12345",
            OptimizationDate = DateTimeOffset.UtcNow,
            Parameters = new Dictionary<string, JsonElement>
            {
                ["Threshold"] = JsonSerializer.SerializeToElement(0.123456789012345m)
            },
            InitialCapital = 987654.321098765432109876543m,
            TradeVolume = 1.234567890123456789012345m,
            Securities = ["MSFT", "GOOGL"]
        };

        // Act
        var json = JsonSerializationHelper.Serialize(config);
        var deserialized = JsonSerializationHelper.Deserialize<StrategyParametersConfig>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(config.InitialCapital, deserialized.InitialCapital);
        Assert.Equal(config.TradeVolume, deserialized.TradeVolume);
        Assert.Equal(2, deserialized.Securities.Count);
    }

    [Fact]
    public void RiskLimitsConfig_RoundTrip_PreservesAllDecimalValues()
    {
        // Arrange
        var config = new RiskLimitsConfig
        {
            MaxPositionSize = 12345.6789012345678901234567m,
            MaxDailyLoss = 1000.123456789012345678m,
            MaxDailyLossIsPercentage = true,
            MaxDrawdownPercentage = 15.987654321098765432m,
            MaxTradesPerDay = 50,
            CircuitBreakerEnabled = true,
            CircuitBreakerThresholdPercentage = 10.123456789m,
            CircuitBreakerCooldownMinutes = 60
        };

        // Act
        var json = JsonSerializationHelper.Serialize(config);
        var deserialized = JsonSerializationHelper.Deserialize<RiskLimitsConfig>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(config.MaxPositionSize, deserialized.MaxPositionSize);
        Assert.Equal(config.MaxDailyLoss, deserialized.MaxDailyLoss);
        Assert.Equal(config.MaxDrawdownPercentage, deserialized.MaxDrawdownPercentage);
        Assert.Equal(config.CircuitBreakerThresholdPercentage, deserialized.CircuitBreakerThresholdPercentage);
    }
}

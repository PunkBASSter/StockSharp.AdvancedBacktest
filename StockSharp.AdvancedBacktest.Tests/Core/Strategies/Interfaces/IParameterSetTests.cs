using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;
using System.Numerics;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies.Interfaces;

/// <summary>
/// Tests for IParameterSet interface contract and implementations
/// </summary>
public class IParameterSetTests
{
    [Fact]
    public void IParameterSet_Contract_ShouldDefineRequiredProperties()
    {
        // Arrange & Act
        var interfaceType = typeof(IParameterSet);

        // Assert - Verify required properties exist
        Assert.NotNull(interfaceType.GetProperty("Count"));
        Assert.NotNull(interfaceType.GetProperty("Definitions"));

        // Verify property types
        Assert.Equal(typeof(int), interfaceType.GetProperty("Count")?.PropertyType);
        Assert.Equal(typeof(ImmutableArray<ParameterDefinition>), interfaceType.GetProperty("Definitions")?.PropertyType);
    }

    [Fact]
    public void IParameterSet_Contract_ShouldDefineRequiredMethods()
    {
        // Arrange & Act
        var interfaceType = typeof(IParameterSet);

        // Assert - Verify required methods exist
        var genericGetMethod = interfaceType.GetMethods()
            .FirstOrDefault(m => m.IsGenericMethod && m.Name == "GetValue" && m.GetParameters().Length == 1);
        var genericSetMethod = interfaceType.GetMethods()
            .FirstOrDefault(m => m.IsGenericMethod && m.Name == "SetValue" && m.GetParameters().Length == 2);
        var objectGetMethod = interfaceType.GetMethods()
            .FirstOrDefault(m => !m.IsGenericMethod && m.Name == "GetValue" && m.GetParameters().Length == 1);
        var objectSetMethod = interfaceType.GetMethods()
            .FirstOrDefault(m => !m.IsGenericMethod && m.Name == "SetValue" && m.GetParameters().Length == 2);

        Assert.NotNull(genericGetMethod);
        Assert.NotNull(genericSetMethod);
        Assert.NotNull(objectGetMethod);
        Assert.NotNull(objectSetMethod);
        Assert.NotNull(interfaceType.GetMethod("HasParameter"));
        Assert.NotNull(interfaceType.GetMethod("Validate"));
        Assert.NotNull(interfaceType.GetMethod("GetSnapshot"));
        Assert.NotNull(interfaceType.GetMethod("Clone"));
        Assert.NotNull(interfaceType.GetMethod("TryGetValue"));
        Assert.NotNull(interfaceType.GetMethod("GetStatistics"));
    }

    /// <summary>
    /// Mock implementation for testing interface contracts
    /// </summary>
    private class MockParameterSet : IParameterSet
    {
        private readonly Dictionary<string, object?> _values = new();
        private readonly ImmutableArray<ParameterDefinition> _definitions;

        public int Count => _definitions.Length;
        public ImmutableArray<ParameterDefinition> Definitions => _definitions;

        public MockParameterSet(params ParameterDefinition[] definitions)
        {
            _definitions = definitions.ToImmutableArray();

            // Initialize with default values
            foreach (var def in definitions)
            {
                if (def.DefaultValue != null)
                {
                    _values[def.Name] = def.DefaultValue;
                }
            }
        }

        public T GetValue<T>(string name) where T : struct, IComparable<T>, INumber<T>
        {
            if (_values.TryGetValue(name, out var value) && value is T typedValue)
                return typedValue;
            throw new KeyNotFoundException($"Parameter '{name}' not found or wrong type");
        }

        public void SetValue<T>(string name, T value) where T : struct, IComparable<T>, INumber<T>
        {
            _values[name] = value;
        }

        public object? GetValue(string name) => _values.TryGetValue(name, out var value) ? value : null;

        public void SetValue(string name, object? value) => _values[name] = value;

        public bool HasParameter(string name) => _definitions.Any(d => d.Name == name);

        public ValidationResult Validate() => ValidationResult.CreateSuccess();

        public ImmutableDictionary<string, object?> GetSnapshot() => _values.ToImmutableDictionary();

        public IParameterSet Clone()
        {
            var clone = new MockParameterSet(_definitions.ToArray());
            foreach (var kvp in _values)
            {
                clone._values[kvp.Key] = kvp.Value;
            }
            return clone;
        }

        public bool TryGetValue(string name, out object? value) => _values.TryGetValue(name, out value);

        public ParameterSetStatistics GetStatistics()
        {
            var total = _definitions.Length;
            var set = _values.Count(kvp => kvp.Value != null);
            var required = _definitions.Count(d => d.IsRequired);
            var requiredSet = _definitions.Where(d => d.IsRequired).Count(d => _values.ContainsKey(d.Name) && _values[d.Name] != null);

            return new ParameterSetStatistics(total, set, required, requiredSet, requiredSet == required);
        }
    }

    [Fact]
    public void MockParameterSet_WithDefinitions_ShouldInitializeCorrectly()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateNumeric<int>("IntParam", 1, 100, 50),
            ParameterDefinition.CreateString("StringParam", "default"),
            ParameterDefinition.CreateBoolean("BoolParam", true)
        };

        // Act
        var parameterSet = new MockParameterSet(definitions);

        // Assert
        Assert.Equal(3, parameterSet.Count);
        Assert.Equal(3, parameterSet.Definitions.Length);
        Assert.Equal(50, parameterSet.GetValue("IntParam"));
        Assert.Equal("default", parameterSet.GetValue("StringParam"));
        Assert.Equal(true, parameterSet.GetValue("BoolParam"));
    }

    [Fact]
    public void MockParameterSet_SetValue_Generic_ShouldWork()
    {
        // Arrange
        var definitions = new[] { ParameterDefinition.CreateNumeric<decimal>("Price", 0m, 1000m) };
        var parameterSet = new MockParameterSet(definitions);

        // Act
        parameterSet.SetValue("Price", 123.45m);

        // Assert
        Assert.Equal(123.45m, parameterSet.GetValue<decimal>("Price"));
    }

    [Fact]
    public void MockParameterSet_SetValue_Object_ShouldWork()
    {
        // Arrange
        var definitions = new[] { ParameterDefinition.CreateString("Name") };
        var parameterSet = new MockParameterSet(definitions);

        // Act
        parameterSet.SetValue("Name", "TestName");

        // Assert
        Assert.Equal("TestName", parameterSet.GetValue("Name"));
    }

    [Fact]
    public void MockParameterSet_HasParameter_ShouldReturnCorrectResult()
    {
        // Arrange
        var definitions = new[] { ParameterDefinition.CreateString("ExistingParam") };
        var parameterSet = new MockParameterSet(definitions);

        // Act & Assert
        Assert.True(parameterSet.HasParameter("ExistingParam"));
        Assert.False(parameterSet.HasParameter("NonExistentParam"));
    }

    [Fact]
    public void MockParameterSet_TryGetValue_ShouldReturnCorrectResult()
    {
        // Arrange
        var definitions = new[] { ParameterDefinition.CreateString("TestParam", "TestValue") };
        var parameterSet = new MockParameterSet(definitions);

        // Act & Assert
        Assert.True(parameterSet.TryGetValue("TestParam", out var value));
        Assert.Equal("TestValue", value);
        Assert.False(parameterSet.TryGetValue("NonExistent", out _));
    }

    [Fact]
    public void MockParameterSet_Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var definitions = new[] { ParameterDefinition.CreateString("TestParam", "Original") };
        var parameterSet = new MockParameterSet(definitions);

        // Act
        var clone = parameterSet.Clone();
        clone.SetValue("TestParam", "Modified");

        // Assert
        Assert.Equal("Original", parameterSet.GetValue("TestParam"));
        Assert.Equal("Modified", clone.GetValue("TestParam"));
    }

    [Fact]
    public void MockParameterSet_GetStatistics_ShouldReturnCorrectStats()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateString("Required", isRequired: true),
            ParameterDefinition.CreateString("Optional", isRequired: false),
            ParameterDefinition.CreateString("RequiredWithValue", "Value", isRequired: true)
        };
        var parameterSet = new MockParameterSet(definitions);
        parameterSet.SetValue("Required", "SomeValue");

        // Act
        var stats = parameterSet.GetStatistics();

        // Assert
        Assert.Equal(3, stats.TotalParameters);
        Assert.Equal(2, stats.SetParameters); // RequiredWithValue (default) + Required (set)
        Assert.Equal(2, stats.RequiredParameters);
        Assert.Equal(2, stats.RequiredParametersSet);
        Assert.True(stats.IsComplete);
    }

    [Fact]
    public void MockParameterSet_Validate_ShouldReturnSuccess()
    {
        // Arrange
        var definitions = new[] { ParameterDefinition.CreateString("TestParam") };
        var parameterSet = new MockParameterSet(definitions);

        // Act
        var result = parameterSet.Validate();

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MockParameterSet_GetSnapshot_ShouldReturnCurrentValues()
    {
        // Arrange
        var definitions = new[]
        {
            ParameterDefinition.CreateString("Param1", "Value1"),
            ParameterDefinition.CreateString("Param2", "Value2")
        };
        var parameterSet = new MockParameterSet(definitions);

        // Act
        var snapshot = parameterSet.GetSnapshot();

        // Assert
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("Value1", snapshot["Param1"]);
        Assert.Equal("Value2", snapshot["Param2"]);
    }
}
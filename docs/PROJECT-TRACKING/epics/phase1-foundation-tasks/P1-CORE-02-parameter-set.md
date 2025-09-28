# Task: P1-CORE-02 - Implement ParameterSet with Validation

**Epic**: Phase1-Foundation
**Priority**: HIGH-02
**Agent**: dotnet-csharp-expert
**Status**: READY
**Dependencies**: P1-CORE-01

## Overview

Implement a comprehensive ParameterSet class that manages optimization parameters with validation, serialization, and parameter space exploration capabilities. This class will integrate with EnhancedStrategyBase to provide robust parameter management for optimization scenarios.

## Technical Requirements - Modern .NET 10 Parameter Management

### Core Implementation - C# 14 Generic Math & Records

1. **ParameterSet Class - High-Performance Design**
   - Define optimization parameter ranges using C# 14 generic math
   - Validate parameter combinations with compiled expressions
   - Serialize/deserialize using System.Text.Json source generators
   - Support massive parameter space exploration with streaming
   - Generate cryptographic parameter hashes for optimization caching
   - Use record types for immutable parameter definitions

2. **Modern ParameterDefinition - Generic Math Support**
   ```csharp
   // Generic parameter definition using C# 11+ features
   public sealed record ParameterDefinition<T>(
       string Name,
       Type Type,
       T? MinValue = default,
       T? MaxValue = default,
       T? Step = default,
       IReadOnlyList<T>? DiscreteValues = null,
       IReadOnlyList<IValidationRule<T>>? ValidationRules = null
   ) where T : struct, IComparable<T>, INumber<T>
   {
       // Generic math validation
       public bool IsValidValue(T value)
       {
           if (MinValue.HasValue && value < MinValue.Value) return false;
           if (MaxValue.HasValue && value > MaxValue.Value) return false;

           // Use generic math for step validation
           if (Step.HasValue && MinValue.HasValue)
           {
               var steps = (value - MinValue.Value) / Step.Value;
               if (steps != T.CreateChecked(Math.Floor(double.CreateChecked(steps))))
                   return false;
           }

           return ValidationRules?.All(rule => rule.IsValid(value)) ?? true;
       }
   }

   // Type-erased base for collections
   public abstract record ParameterDefinitionBase(
       string Name,
       Type Type,
       object? MinValue = null,
       object? MaxValue = null,
       object? Step = null
   );
   ```

3. **Parameter Space Management**
   ```csharp
   public class ParameterSet
   {
       public Dictionary<string, ParameterDefinition> Parameters { get; set; }
       public ValidationResult Validate(Dictionary<string, object> values);
       public IEnumerable<Dictionary<string, object>> GenerateCombinations();
       public string GetParameterHash(Dictionary<string, object> values);
   }
   ```

### File Structure

Create in `StockSharp.AdvancedBacktest/Core/Configuration/`:
- `ParameterSet.cs` - Main parameter set class
- `ParameterDefinition.cs` - Individual parameter definition
- `ValidationRule.cs` - Parameter validation rules
- `ValidationResult.cs` - Validation result model
- `ParameterSpaceExplorer.cs` - Parameter combination generation

## Implementation Details

### ParameterSet Features

1. **Parameter Type Support**
   - Numeric types (int, decimal, double)
   - Boolean parameters
   - Enum parameters
   - String parameters with constraints
   - Custom type support via converters

2. **Validation System**
   - Range validation (min/max values)
   - Step validation (increment constraints)
   - Cross-parameter validation rules
   - Custom validation functions
   - Dependency validation between parameters

3. **Parameter Space Exploration**
   - Cartesian product generation
   - Random sampling support
   - Grid search parameter generation
   - Optimization-guided sampling
   - Parameter space size estimation

4. **Serialization Support**
   - JSON serialization/deserialization
   - XML configuration support
   - Binary serialization for performance
   - Schema versioning support

### Validation Framework

1. **Built-in Validation Rules**
   ```csharp
   public abstract class ValidationRule
   {
       public abstract ValidationResult Validate(object value, Dictionary<string, object> allParameters);
   }

   public class RangeValidationRule : ValidationRule
   public class StepValidationRule : ValidationRule
   public class DependencyValidationRule : ValidationRule
   public class CustomValidationRule : ValidationRule
   ```

2. **Cross-Parameter Validation**
   - Conditional parameter constraints
   - Mathematical relationships between parameters
   - Business rule validation
   - Performance constraint validation

### Parameter Hash Generation

1. **Deterministic Hashing**
   - SHA256-based parameter hashing
   - Consistent hash generation across runs
   - Support for parameter ordering independence
   - Cache key generation for optimization results

2. **Hash Collision Handling**
   - Collision detection mechanisms
   - Hash uniqueness validation
   - Fallback strategies for collisions

## Acceptance Criteria

### Functional Requirements

- [ ] ParameterSet manages all supported parameter types
- [ ] Validation system prevents invalid parameter combinations
- [ ] Parameter space exploration generates correct combinations
- [ ] Serialization/deserialization works correctly
- [ ] Parameter hashing is deterministic and unique

### Performance Requirements - Quantified Targets

- [ ] **Parameter Generation**: 100,000+ combinations per second on single thread
- [ ] **Memory Efficiency**: O(1) memory usage regardless of parameter space size
- [ ] **Validation Speed**: 1M+ parameter validations per second
- [ ] **Serialization**: JSON serialization at 50MB/second for large parameter sets
- [ ] **Hash Generation**: 10,000+ parameter hashes per second
- [ ] **Thread Safety**: Linear scaling with CPU cores for parallel generation

### Integration Requirements

- [ ] Integrates with EnhancedStrategyBase
- [ ] Works with BruteForceOptimizerWrapper
- [ ] Supports StockSharp parameter patterns
- [ ] Compatible with existing configuration systems

## Implementation Specifications

### Parameter Definition Schema

```json
{
  "parameters": {
    "fastMA": {
      "type": "int",
      "minValue": 5,
      "maxValue": 50,
      "step": 5,
      "description": "Fast moving average period"
    },
    "slowMA": {
      "type": "int",
      "minValue": 20,
      "maxValue": 200,
      "step": 10,
      "description": "Slow moving average period",
      "validationRules": [
        {
          "type": "dependency",
          "condition": "slowMA > fastMA",
          "message": "Slow MA must be greater than Fast MA"
        }
      ]
    }
  }
}
```

### Performance Requirements

1. **Parameter Space Size Calculation**
   - Efficient size estimation without enumeration
   - Memory usage prediction
   - Optimization time estimation

2. **Batch Processing Support**
   - Chunked parameter generation
   - Streaming parameter combinations
   - Memory-bounded enumeration

### Error Handling

1. **Validation Error Reporting**
   - Detailed error messages
   - Parameter-specific error context
   - Suggested corrections
   - Error aggregation for multiple issues

2. **Configuration Error Recovery**
   - Graceful handling of invalid configurations
   - Default value fallbacks
   - Partial configuration support

## Dependencies

### NuGet Packages Required

```xml
<PackageReference Include="System.ComponentModel.DataAnnotations" Version="8.0.0" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
```

### Framework Dependencies

- .NET 10
- System.Collections.Immutable (for thread-safe collections)
- System.Security.Cryptography (for hashing)

## Definition of Done

1. **Code Complete**
   - ParameterSet class implemented with full functionality
   - Validation rule system working
   - Parameter space exploration functional
   - Serialization support complete

2. **Testing Complete**
   - Unit tests for all parameter types
   - Validation rule tests
   - Parameter space generation tests
   - Serialization round-trip tests
   - Performance benchmarking completed

3. **Documentation Complete**
   - XML documentation for all public APIs
   - JSON schema documentation
   - Usage examples and patterns
   - Performance characteristics documented

4. **Integration Verified**
   - Works with EnhancedStrategyBase
   - Compatible with optimization scenarios
   - Handles large parameter spaces efficiently
   - Memory usage within acceptable limits

## Implementation Notes

### Design Considerations

1. **Immutability**: Parameter definitions should be immutable once created
2. **Performance**: Optimize for large parameter spaces (100,000+ combinations)
3. **Extensibility**: Allow custom parameter types and validation rules
4. **Configuration**: Support multiple configuration sources and formats

### Common Pitfalls to Avoid

1. Exponential memory growth with large parameter spaces
2. Performance degradation with complex validation rules
3. Thread safety issues in concurrent scenarios
4. Inconsistent parameter hashing across platforms

This task provides the parameter management foundation required for robust optimization scenarios and integrates directly with the EnhancedStrategyBase implementation.
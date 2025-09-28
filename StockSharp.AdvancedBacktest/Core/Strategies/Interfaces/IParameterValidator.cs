using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

/// <summary>
/// Interface for parameter validation
/// </summary>
public interface IParameterValidator
{
    /// <summary>
    /// Validate a single parameter
    /// </summary>
    /// <param name="parameterName">Parameter name</param>
    /// <param name="value">Parameter value</param>
    /// <param name="definition">Parameter definition</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidateParameter(string parameterName, object? value, ParameterDefinition definition);

    /// <summary>
    /// Validate all parameters in a set
    /// </summary>
    /// <param name="parameters">Parameter set to validate</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidateParameterSet(IParameterSet parameters);

    /// <summary>
    /// Validate parameter dependencies and relationships
    /// </summary>
    /// <param name="parameters">Parameter set to validate</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidateDependencies(IParameterSet parameters);
}
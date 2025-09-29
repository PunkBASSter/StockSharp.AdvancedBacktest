using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

public interface IParameterValidator
{
    ValidationResult ValidateParameter(string parameterName, object? value, ParameterDefinition definition);
    ValidationResult ValidateParameterSet(IParameterSet parameters);
    ValidationResult ValidateDependencies(IParameterSet parameters);
}
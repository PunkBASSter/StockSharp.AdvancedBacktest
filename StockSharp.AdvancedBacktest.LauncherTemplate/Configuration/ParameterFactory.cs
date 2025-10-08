using System.Numerics;
using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.Parameters;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration;

/// <summary>
/// Factory for creating ICustomParam instances from ParameterDefinition configurations.
/// Centralizes all parameter creation logic in one place.
/// </summary>
public static class ParameterFactory
{
	/// <summary>
	/// Creates a single parameter from a definition.
	/// </summary>
	public static ICustomParam CreateFrom(string name, ParameterDefinition definition)
	{
		ArgumentNullException.ThrowIfNull(definition);
		ValidateDefinition(name, definition);

		return definition.Type.ToLowerInvariant() switch
		{
			"int" => CreateNumberParam<int>(name, definition),
			"decimal" => CreateNumberParam<decimal>(name, definition),
			"double" => CreateNumberParam<double>(name, definition),
			"string" or "enum" => CreateClassParam(name, definition),
			_ => throw new NotSupportedException(
				$"Parameter type '{definition.Type}' is not supported. " +
				$"Supported types: int, decimal, double, string, enum")
		};
	}

	/// <summary>
	/// Creates multiple parameters from a dictionary of definitions.
	/// </summary>
	public static IEnumerable<ICustomParam> CreateFromDictionary(
		Dictionary<string, ParameterDefinition> definitions)
	{
		ArgumentNullException.ThrowIfNull(definitions);
		return definitions.Select(kvp => CreateFrom(kvp.Key, kvp.Value));
	}

	private static NumberParam<T> CreateNumberParam<T>(string name, ParameterDefinition def)
		where T : struct, IAdditionOperators<T, T, T>, IComparisonOperators<T, T, bool>
	{
		return new NumberParam<T>(
			name,
			def.DefaultValue?.Deserialize<T>() ?? def.MinValue!.Value.Deserialize<T>(),
			def.MinValue!.Value.Deserialize<T>(),
			def.MaxValue!.Value.Deserialize<T>(),
			def.StepValue!.Value.Deserialize<T>())
		{
			CanOptimize = true
		};
	}

	private static ClassParam<string> CreateClassParam(string name, ParameterDefinition def)
	{
		return new ClassParam<string>(name, def.Values!)
		{
			CanOptimize = true
		};
	}

	private static void ValidateDefinition(string name, ParameterDefinition def)
	{
		var typeLower = def.Type.ToLowerInvariant();

		if (typeLower is "int" or "decimal" or "double")
		{
			if (!def.MinValue.HasValue)
			{
				throw new InvalidOperationException(
					$"Parameter '{name}': MinValue is required for numeric type '{def.Type}'");
			}
			if (!def.MaxValue.HasValue)
			{
				throw new InvalidOperationException(
					$"Parameter '{name}': MaxValue is required for numeric type '{def.Type}'");
			}
			if (!def.StepValue.HasValue)
			{
				throw new InvalidOperationException(
					$"Parameter '{name}': StepValue is required for numeric type '{def.Type}'");
			}
		}
		else if (typeLower is "string" or "enum")
		{
			if (def.Values == null || def.Values.Count == 0)
			{
				throw new InvalidOperationException(
					$"Parameter '{name}': Values list is required for type '{def.Type}'");
			}
		}
	}
}

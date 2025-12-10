using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;

public static class JsonSerializerOptionsProvider
{
	private static readonly Lazy<JsonSerializerOptions> _options = new(() => CreateOptions());
	private static readonly Lazy<JsonSerializerOptions> _dynamicOptions = new(() => CreateDynamicOptions());

	public static JsonSerializerOptions Options => _options.Value;

	public static JsonSerializerOptions DynamicOptions => _dynamicOptions.Value;

	private static JsonSerializerOptions CreateOptions()
	{
		return EventJsonContext.Default.Options;
	}

	private static JsonSerializerOptions CreateDynamicOptions()
	{
		return new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false,
			Converters = { new DecimalConverter() }
		};
	}
}

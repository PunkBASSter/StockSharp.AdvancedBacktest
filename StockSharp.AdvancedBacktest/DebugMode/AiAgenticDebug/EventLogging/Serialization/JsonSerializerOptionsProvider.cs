using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;

public static class JsonSerializerOptionsProvider
{
	private static readonly Lazy<JsonSerializerOptions> _options = new(() => CreateOptions());

	public static JsonSerializerOptions Options => _options.Value;

	private static JsonSerializerOptions CreateOptions()
	{
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false,
			Converters =
			{
				new DecimalConverter(),
				new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
			}
		};

		return options;
	}
}

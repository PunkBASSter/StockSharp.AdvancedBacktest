using System.Text.Json;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;

public static class JsonSerializerOptionsProvider
{
	private static readonly Lazy<JsonSerializerOptions> _options = new(() => CreateOptions());

	public static JsonSerializerOptions Options => _options.Value;

	private static JsonSerializerOptions CreateOptions()
	{
		return EventJsonContext.Default.Options;
	}
}

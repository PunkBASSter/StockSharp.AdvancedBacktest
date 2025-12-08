using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;

public sealed class DecimalConverter : JsonConverter<decimal>
{
	public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
		{
			var stringValue = reader.GetString();
			if (decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
				return result;
		}
		else if (reader.TokenType == JsonTokenType.Number)
		{
			return reader.GetDecimal();
		}

		throw new JsonException($"Unable to parse decimal from {reader.TokenType}");
	}

	public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString("G", CultureInfo.InvariantCulture));
	}
}

using System.Text.Json;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.Parameters;

namespace StockSharp.AdvancedBacktest.Serialization;

public class CustomParamJsonConverter : JsonConverter<ICustomParam>
{
    public override ICustomParam? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("paramType", out var paramTypeElement))
            throw new JsonException("Missing 'paramType' discriminator in ICustomParam JSON");

        var paramTypeName = paramTypeElement.GetString()
            ?? throw new JsonException("'paramType' cannot be null");

        var id = root.GetProperty("id").GetString()
            ?? throw new JsonException("'id' cannot be null");

        var paramType = Type.GetType(paramTypeName)
            ?? throw new JsonException($"Cannot resolve type: {paramTypeName}");

        var valueType = paramType.GetProperty("ParamType")?.GetValue(null) as Type
            ?? throw new JsonException($"Cannot determine value type for {paramTypeName}");

        throw new NotImplementedException("CustomParam deserialization not yet implemented");
    }

    public override void Write(Utf8JsonWriter writer, ICustomParam value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("paramType", value.GetType().AssemblyQualifiedName);

        writer.WriteString("id", value.Id);
        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value.Value, value.ParamType, options);

        var optimizationRange = value.OptimizationRangeParams;
        if (optimizationRange != null)
        {
            writer.WritePropertyName("optimizationRange");
            writer.WriteStartArray();
            foreach (var rangeParam in optimizationRange)
            {
                JsonSerializer.Serialize(writer, rangeParam.Value, rangeParam.ParamType, options);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}

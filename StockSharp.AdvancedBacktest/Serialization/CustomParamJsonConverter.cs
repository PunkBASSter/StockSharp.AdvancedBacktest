using System.Text.Json;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.Parameters;

namespace StockSharp.AdvancedBacktest.Serialization;

// Polymorphic JSON converter for ICustomParam types.
// Handles serialization/deserialization of NumberParam, StructParam, ClassParam, etc.
public class CustomParamJsonConverter : JsonConverter<ICustomParam>
{
    public override ICustomParam? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Read discriminator and common properties
        if (!root.TryGetProperty("paramType", out var paramTypeElement))
            throw new JsonException("Missing 'paramType' discriminator in ICustomParam JSON");

        var paramTypeName = paramTypeElement.GetString()
            ?? throw new JsonException("'paramType' cannot be null");

        var id = root.GetProperty("id").GetString()
            ?? throw new JsonException("'id' cannot be null");

        // Deserialize based on discriminator
        var paramType = Type.GetType(paramTypeName)
            ?? throw new JsonException($"Cannot resolve type: {paramTypeName}");

        // Get the concrete value type (T in CustomParam<T>)
        var valueType = paramType.GetProperty("ParamType")?.GetValue(null) as Type
            ?? throw new JsonException($"Cannot determine value type for {paramTypeName}");

        // TODO: Complete implementation - create appropriate param type based on discriminator
        // This is a placeholder that needs to be completed with full deserialization logic
        throw new NotImplementedException("CustomParam deserialization not yet implemented");
    }

    public override void Write(Utf8JsonWriter writer, ICustomParam value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write discriminator
        writer.WriteString("paramType", value.GetType().AssemblyQualifiedName);

        // Write common properties
        writer.WriteString("id", value.Id);
        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value.Value, value.ParamType, options);

        // Write optimization range if present
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

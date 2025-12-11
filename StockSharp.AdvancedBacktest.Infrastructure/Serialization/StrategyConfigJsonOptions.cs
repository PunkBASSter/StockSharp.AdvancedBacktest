using System.Text.Json;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.Parameters;

namespace StockSharp.AdvancedBacktest.Serialization;

public static class StrategyConfigJsonOptions
{
    public static JsonSerializerOptions Default { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new CustomParamJsonConverter(),
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        return options;
    }

    public static string SerializeParameters(List<ICustomParam> parameters)
    {
        return JsonSerializer.Serialize(parameters, Default);
    }

    public static List<ICustomParam> DeserializeParameters(string json)
    {
        return JsonSerializer.Deserialize<List<ICustomParam>>(json, Default)
            ?? throw new JsonException("Failed to deserialize parameters - result was null");
    }

    public static void SerializeToFile(List<ICustomParam> parameters, string filePath)
    {
        var json = SerializeParameters(parameters);
        File.WriteAllText(filePath, json);
    }

    public static List<ICustomParam> DeserializeFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return DeserializeParameters(json);
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;

public static class JsonSerializationHelper
{
    public static JsonSerializerOptions CreateStandardOptions(bool writeIndented = true)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new DecimalStringConverter());
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }

    public static string Serialize<T>(T value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(value, options ?? CreateStandardOptions());
    }

    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(json, options ?? CreateStandardOptions());
    }

    public static async Task SerializeToFileAsync<T>(
        T value,
        string filePath,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, value, options ?? CreateStandardOptions(), cancellationToken);
    }

    public static async Task<T?> DeserializeFromFileAsync<T>(
        string filePath,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<T>(stream, options ?? CreateStandardOptions(), cancellationToken);
    }

    public static void SerializeToFile<T>(T value, string filePath, JsonSerializerOptions? options = null)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, options ?? CreateStandardOptions());
        File.WriteAllText(filePath, json);
    }

    public static T? DeserializeFromFile<T>(string filePath, JsonSerializerOptions? options = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, options ?? CreateStandardOptions());
    }
}

public class DecimalStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (decimal.TryParse(stringValue, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            throw new JsonException($"Unable to parse '{stringValue}' as decimal.");
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        // G29 format preserves up to 29 significant digits for decimal type
        writer.WriteStringValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}

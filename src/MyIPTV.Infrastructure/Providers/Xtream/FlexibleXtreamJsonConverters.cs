using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyIPTV.Infrastructure.Providers.Xtream;

internal sealed class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => JsonDocument.ParseValue(ref reader).RootElement.GetRawText(),
            JsonTokenType.True => bool.TrueString,
            JsonTokenType.False => bool.FalseString,
            JsonTokenType.Null => null,
            _ => throw new JsonException("Expected a scalar value."),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

internal sealed class FlexibleStringArrayConverter : JsonConverter<string[]>
{
    public override string[] Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            return string.IsNullOrWhiteSpace(value) ? [] : [value];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected an array or string.");
        }

        List<string> values = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            string? value = JsonSerializer.Deserialize<string>(ref reader, options);
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.AsEnumerable(), options);
}

internal sealed class FlexibleListConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(List<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type itemType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(
            typeof(FlexibleListConverter<>).MakeGenericType(itemType))!;
    }

    private sealed class FlexibleListConverter<TItem> : JsonConverter<List<TItem>>
    {
        public override List<TItem> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null or JsonTokenType.False)
            {
                return [];
            }

            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray()
                    .Select(item => item.Deserialize<TItem>(options))
                    .Where(item => item is not null)
                    .Cast<TItem>()
                    .ToList();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                return root.EnumerateObject()
                    .Select(property => property.Value.Deserialize<TItem>(options))
                    .Where(item => item is not null)
                    .Cast<TItem>()
                    .ToList();
            }

            throw new JsonException("Expected an array or keyed object.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            List<TItem> value,
            JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.AsEnumerable(), options);
    }
}

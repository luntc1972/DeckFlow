using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Reads list fields that may arrive as either plain strings or lightweight object entries and normalizes them to display-ready strings.
/// </summary>
public sealed class JsonStringListCoercingConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <summary>
    /// Deserializes a JSON array of strings, objects, or nulls into a normalized read-only string list.
    /// </summary>
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Array.Empty<string>();
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected a JSON array.");
        }

        var items = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return items;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    items.Add(value);
                }

                continue;
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                continue;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var elementDocument = JsonDocument.ParseValue(ref reader);
                var value = CoerceObjectElement(elementDocument.RootElement);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    items.Add(value);
                }

                continue;
            }

            throw new JsonException("Expected string, object, or null inside the array.");
        }

        throw new JsonException("Expected the array to terminate.");
    }

    /// <summary>
    /// Serializes the normalized list back as a standard JSON string array.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }

    private static string CoerceObjectElement(JsonElement element)
    {
        var label = GetTrimmedString(element, "name")
            ?? GetTrimmedString(element, "need")
            ?? GetTrimmedString(element, "title");
        var description = GetTrimmedString(element, "description");

        if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(description))
        {
            return $"{label}: {description}";
        }

        return label ?? description ?? string.Empty;
    }

    private static string? GetTrimmedString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

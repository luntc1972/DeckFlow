using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models;

/// <summary>
/// Reads an optional string field that may arrive as a non-string token (number, object, array) and treats it as absent.
/// </summary>
public sealed class JsonLenientStringConverter : JsonConverter<string?>
{
    /// <summary>
    /// Returns the string value, or <see langword="null"/> after skipping any non-string token.
    /// </summary>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        reader.Skip();
        return null;
    }

    /// <summary>
    /// Writes the value as a JSON string, or JSON null when absent.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}

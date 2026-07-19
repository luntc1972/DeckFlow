using System.Text;
using System.Text.Json;
using RestSharp;

namespace DeckFlow.Web.Services.CreatorStyle;

internal static class OwnerClientJson
{
    public static bool TryGetResponseContent(
        RestResponse response,
        int maxResponseBytes,
        out string content,
        Action<long>? onTooLarge = null)
    {
        content = response.Content ?? string.Empty;
        var byteCount = response.RawBytes?.LongLength ?? Encoding.UTF8.GetByteCount(content);
        if (byteCount > maxResponseBytes)
        {
            onTooLarge?.Invoke(byteCount);
            return false;
        }

        return true;
    }

    public static string ReadString(JsonElement item, string propertyName)
    {
        return ReadNullableString(item, propertyName) ?? string.Empty;
    }

    public static string? ReadNullableString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.GetRawText();
    }

    public static int ReadInt32(JsonElement item, string propertyName)
    {
        return ReadNullableInt32(item, propertyName) ?? 0;
    }

    public static int? ReadNullableInt32(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value))
        {
            return value;
        }

        return int.TryParse(property.GetRawText(), out value) ? value : null;
    }
}

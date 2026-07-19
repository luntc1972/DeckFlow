using System.Text;
using System.Text.Json;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Serializes and deserializes the Cut Lab working-session state envelope.</summary>
public static class CutLabStateSerializer
{
    /// <summary>Maximum allowed UTF-8 payload size for the serialized working-session JSON.</summary>
    public const int MaxUploadBytes = 262_144;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>Serializes the working-session state as camelCase JSON under the size cap.</summary>
    /// <param name="state">State to serialize.</param>
    /// <returns>The serialized JSON payload.</returns>
    public static string Serialize(CutLabState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var json = JsonSerializer.Serialize(state, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaxUploadBytes)
        {
            throw new InvalidOperationException("The Cut Lab working session is too large to save.");
        }

        return json;
    }

    /// <summary>Deserializes a previously submitted working-session payload.</summary>
    /// <param name="json">Serialized payload.</param>
    /// <returns>The deserialized state, or an empty state when input is blank or malformed.</returns>
    public static CutLabState Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CutLabState();
        }

        try
        {
            var state = JsonSerializer.Deserialize<CutLabState>(json, Options) ?? new CutLabState();
            return CutLabLockRules.EnforceCommanderLock(state);
        }
        catch (JsonException)
        {
            return new CutLabState();
        }
    }
}

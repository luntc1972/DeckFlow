using System.Text.Json;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// JSON section serialization helpers for creator style-profile subsection columns.
/// </summary>
public static class CreatorStyleProfileSections
{
    /// <summary>
    /// Serializes a creator style-profile section to JSON, or returns <see langword="null"/> for an empty section.
    /// </summary>
    /// <typeparam name="T">Section element type.</typeparam>
    /// <param name="section">Section list to serialize.</param>
    /// <returns>JSON array text for populated sections, or <see langword="null"/> when the section is empty.</returns>
    public static string? SerializeSection<T>(IReadOnlyList<T> section)
    {
        ArgumentNullException.ThrowIfNull(section);

        if (section.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(section);
    }

    /// <summary>
    /// Deserializes a creator style-profile section from JSON, returning an empty list when the stored section is absent.
    /// </summary>
    /// <typeparam name="T">Section element type.</typeparam>
    /// <param name="serializedSection">Serialized JSON array text, or <see langword="null"/> when no section was stored.</param>
    /// <returns>A non-null section list.</returns>
    public static IReadOnlyList<T> DeserializeSection<T>(string? serializedSection)
    {
        if (string.IsNullOrWhiteSpace(serializedSection))
        {
            return Array.Empty<T>();
        }

        return JsonSerializer.Deserialize<T[]>(serializedSection) ?? Array.Empty<T>();
    }
}

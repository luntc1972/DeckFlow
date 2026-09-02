using System.Globalization;
using DeckFlow.Core.Integration;

namespace DeckFlow.Core.Knowledge;

internal sealed record ArchidektDeckMetadataParameters(
    int? EdhBracket,
    int? DeckFormat,
    int? Theorycrafted,
    string? CreatedUtc,
    string? UpdatedUtc,
    string? CapturedUtc)
{
    internal static ArchidektDeckMetadataParameters From(ArchidektDeckMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new(
            metadata.EdhBracket,
            metadata.DeckFormat,
            metadata.Theorycrafted is null ? null : metadata.Theorycrafted.Value ? 1 : 0,
            Format(metadata.CreatedUtc),
            Format(metadata.UpdatedUtc),
            metadata.CapturedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    private static string? Format(DateTimeOffset? value)
        => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}

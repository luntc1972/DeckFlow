namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Shared distillation output contracts and validators used by both the OpenAI and CLI-backed
/// distillation services, so the 3-8-clip / 200-word / vocabulary rules are stated once.
/// </summary>
internal static class DistillationValidation
{
    internal const int SummaryMaxWords = 200;
    internal const int MinClipCount = 3;
    internal const int MaxClipCount = 8;

    internal static void ValidateSummary(string summary)
    {
        if (CountWords(summary) > SummaryMaxWords)
        {
            throw new InvalidOperationException("Summary exceeded the 200-word limit.");
        }
    }

    internal static void ValidateClips(IReadOnlyList<ClipItem> clips)
    {
        if (clips.Count is < MinClipCount or > MaxClipCount)
        {
            throw new InvalidOperationException("Clip extraction must return 3 to 8 clips.");
        }

        if (clips.Any(clip => clip.TimestampSeconds < 0))
        {
            throw new InvalidOperationException("Clip timestamps cannot be negative.");
        }
    }

    internal static void ValidateTags(TagsPayload payload)
    {
        ValidateTagDimension("archetype", payload.Archetype, ContentTagVocabulary.Archetypes);
        ValidateTagDimension("bracket", payload.Bracket, ContentTagVocabulary.Brackets);
        ValidateTagDimension("card_category", payload.CardCategory, ContentTagVocabulary.CardCategories);
    }

    private static int CountWords(string text)
        => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static void ValidateTagDimension(
        string dimension,
        IReadOnlyList<string> values,
        IReadOnlySet<string> allowlist)
    {
        if (values is null)
        {
            throw new InvalidOperationException($"{dimension} tags cannot be null.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !allowlist.Contains(value))
            {
                throw new InvalidOperationException($"{dimension} tag '{value}' is not in the content tag vocabulary.");
            }

            if (!seen.Add(value))
            {
                throw new InvalidOperationException($"{dimension} tag '{value}' is duplicated.");
            }
        }
    }
}

/// <summary>JSON payload shape for the summary extraction call.</summary>
internal sealed record SummaryPayload(string Summary);

/// <summary>JSON payload shape for the clip extraction call.</summary>
internal sealed record ClipsPayload(IReadOnlyList<ClipItem> Clips);

/// <summary>JSON payload shape for the tag extraction call.</summary>
internal sealed record TagsPayload(
    IReadOnlyList<string> Archetype,
    IReadOnlyList<string> Bracket,
    IReadOnlyList<string> CardCategory);

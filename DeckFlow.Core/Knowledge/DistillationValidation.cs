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

    internal static string TruncateSummary(string summary)
    {
        summary ??= string.Empty;
        var words = GetWords(summary);
        return words.Length <= SummaryMaxWords
            ? summary
            : string.Join(" ", words.Take(SummaryMaxWords));
    }

    internal static IReadOnlyList<ClipItem> SanitizeClips(IReadOnlyList<ClipItem>? clips)
    {
        return (clips ?? [])
            .Where(clip => clip.TimestampSeconds is null or >= 0)
            .Take(MaxClipCount)
            .ToArray();
    }

    internal static TagsPayload SanitizeTags(TagsPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new TagsPayload(
            SanitizeTagDimension(payload.Archetype, ContentTagVocabulary.Archetypes),
            SanitizeTagDimension(payload.Bracket, ContentTagVocabulary.Brackets),
            SanitizeTagDimension(payload.CardCategory, ContentTagVocabulary.CardCategories));
    }

    internal static ClassificationPayload SanitizeClassification(ClassificationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var verdict = payload.Verdict?.Trim().ToLowerInvariant();
        if (verdict is not "keep" and not "drop")
        {
            throw new InvalidOperationException($"Classification verdict '{payload.Verdict}' is invalid.");
        }

        return new ClassificationPayload(verdict, payload.Reason);
    }

    internal static int CountWords(string text)
        => GetWords(text).Length;

    private static string[] GetWords(string text)
    {
        text ??= string.Empty;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyList<string> SanitizeTagDimension(
        IReadOnlyList<string>? values,
        IReadOnlySet<string> allowlist)
    {
        if (values is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sanitized = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var canonical = allowlist.FirstOrDefault(allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase));
            if (canonical is null || !seen.Add(canonical))
            {
                continue;
            }

            sanitized.Add(canonical);
        }

        return sanitized;
    }

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

/// <summary>JSON payload shape for the classification call.</summary>
internal sealed record ClassificationPayload(string Verdict, string? Reason);

/// <summary>JSON payload shape for the tag extraction call.</summary>
internal sealed record TagsPayload(
    IReadOnlyList<string> Archetype,
    IReadOnlyList<string> Bracket,
    IReadOnlyList<string> CardCategory);

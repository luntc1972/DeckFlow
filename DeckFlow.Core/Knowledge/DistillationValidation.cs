using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Shared distillation output contracts and validators used by both the OpenAI and CLI-backed
/// distillation services, so the 3-8-clip / 200-word / vocabulary rules are stated once.
/// </summary>
internal static class DistillationValidation
{
    // Why: 60s is the conservative YouTube Shorts cutoff - long enough to exclude Shorts,
    // short enough to keep legitimate brief MTG videos.
    internal static readonly TimeSpan ShortVideoMaxDuration = TimeSpan.FromSeconds(60);
    internal const int SummaryMaxOutputTokens = 400;
    internal const int ClipsMaxOutputTokens = 1200;
    internal const int TagsMaxOutputTokens = 200;
    internal const int SummaryMaxWords = 200;
    internal const int MinClipCount = 3;
    internal const int MaxClipCount = 8;
    internal const int MaxStatedRulesPerVideo = 40;
    internal const int MaxTranscriptInputTokens = 120_000;
    internal const int DistillationCallCount = 3;
    internal const string DistillStatusDistilled = "distilled";
    internal const string DistillStatusSkippedOverCap = "skipped_over_cap";
    internal const string DistillStatusFailed = "failed";
    internal const string DistillStatusFiltered = "filtered";

    internal static void ValidateTranscriptLength(string transcript)
    {
        if (EstimateTokenCount(transcript) > MaxTranscriptInputTokens)
        {
            throw new InvalidOperationException("Transcript too long for the distillation context window.");
        }
    }

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

        if (clips.All(clip => (clip.TimestampSeconds ?? 0) == 0))
        {
            throw new InvalidOperationException("Clip extraction cannot return every clip with timestamp 0.");
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

    internal static IReadOnlyList<StatedRuleCandidate> SanitizeStatedRules(
        RulesPayload? payload,
        DateTimeOffset videoDateUtc)
    {
        return (payload?.Rules ?? [])
            .Where(rule => rule is not null)
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Category))
            .Where(rule => StatedRulesMetricVocabulary.Metrics.Contains(rule.Metric))
            .Where(rule => StatedRulesMetricVocabulary.Comparators.Contains(rule.Comparator))
            .Where(rule => rule.ClipTimestampSeconds is null or >= 0)
            .Where(rule => !string.IsNullOrWhiteSpace(rule.SourceClip))
            .Where(rule => rule.Confidence is >= 0d and <= 1d)
            .Where(
                rule => rule.Comparator.Equals("range", StringComparison.OrdinalIgnoreCase)
                    ? rule.Value is null
                      && rule.ValueMin is not null
                      && rule.ValueMax is not null
                      && rule.ValueMin <= rule.ValueMax
                    : rule.Value is not null
                      && rule.ValueMin is null
                      && rule.ValueMax is null)
            .Select(
                rule => new StatedRuleCandidate
                {
                    Category = rule.Category,
                    Metric = rule.Metric,
                    Value = rule.Value,
                    ValueMin = rule.ValueMin,
                    ValueMax = rule.ValueMax,
                    Comparator = rule.Comparator,
                    Condition = rule.Condition,
                    ClipTimestampSeconds = rule.ClipTimestampSeconds,
                    SourceClip = rule.SourceClip,
                    Confidence = rule.Confidence,
                    CardReference = rule.CardReference,
                    CardGrounded = null,
                    VideoDateUtc = videoDateUtc,
                })
            .Take(MaxStatedRulesPerVideo)
            .ToArray();
    }

    internal static void ValidateStatedRules(IReadOnlyList<StatedRuleCandidate> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var rule in rules)
        {
            if (!StatedRulesMetricVocabulary.Metrics.Contains(rule.Metric))
            {
                throw new InvalidOperationException($"Stated rule metric '{rule.Metric}' is not in the stated rule vocabulary.");
            }

            if (!StatedRulesMetricVocabulary.Comparators.Contains(rule.Comparator))
            {
                throw new InvalidOperationException($"Stated rule comparator '{rule.Comparator}' is not in the stated rule vocabulary.");
            }

            // Why: a rule must be either a single-value claim or a bounded range claim, never both.
            if (rule.Comparator.Equals("range", StringComparison.OrdinalIgnoreCase))
            {
                if (rule.Value is not null || rule.ValueMin is null || rule.ValueMax is null || rule.ValueMin > rule.ValueMax)
                {
                    throw new InvalidOperationException("Range stated rules require null value plus ordered non-null value_min and value_max.");
                }
            }
            else if (rule.Value is null || rule.ValueMin is not null || rule.ValueMax is not null)
            {
                throw new InvalidOperationException("Non-range stated rules require a single value and null range bounds.");
            }

            if (rule.Confidence is < 0d or > 1d)
            {
                throw new InvalidOperationException("Stated rule confidence must be between 0.0 and 1.0.");
            }

            if (string.IsNullOrWhiteSpace(rule.SourceClip))
            {
                throw new InvalidOperationException("Stated rule source_clip cannot be empty.");
            }

            // Why: provenance is mandatory because later fusion resolves recency conflicts by source video date.
            if (rule.VideoDateUtc == default)
            {
                throw new InvalidOperationException("Stated rule video_date provenance cannot be default.");
            }
        }
    }

    internal static int CountWords(string text)
        => GetWords(text).Length;

    internal static decimal ComputeProjectedVideoCostUsd(string transcript)
        => LlmSpendLedger.ComputeCostUsd(
            EstimateTokenCount(transcript) * DistillationCallCount,
            SummaryMaxOutputTokens + ClipsMaxOutputTokens + TagsMaxOutputTokens);

    internal static decimal ComputeProjectedCallCostUsd(string transcript, int maxOutputTokens)
        => LlmSpendLedger.ComputeCostUsd(EstimateTokenCount(transcript), maxOutputTokens);

    internal static int EstimateTokenCount(string transcript)
        => Math.Max(1, (int)Math.Ceiling(transcript.Length / 4m));

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

/// <summary>JSON payload shape for the stated-rule selection call.</summary>
internal sealed record SelectPayload(IReadOnlyList<string> Claims);

/// <summary>JSON payload shape for the stated-rule disambiguation call.</summary>
internal sealed record DisambiguatePayload(IReadOnlyList<string> Claims);

/// <summary>JSON payload shape for one stated rule emitted by decompose/reduce.</summary>
internal sealed record StatedRulePayload(
    string Category,
    string Metric,
    double? Value,
    double? ValueMin,
    double? ValueMax,
    string Comparator,
    string? Condition,
    int? ClipTimestampSeconds,
    string SourceClip,
    double Confidence,
    string? CardReference = null);

/// <summary>JSON payload shape for the stated-rule decompose/reduce calls.</summary>
internal sealed record RulesPayload(IReadOnlyList<StatedRulePayload> Rules);

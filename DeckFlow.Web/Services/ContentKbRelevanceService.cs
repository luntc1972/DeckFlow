using System.Globalization;
using System.Text.RegularExpressions;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services;

/// <summary>
/// Scores and selects Content KB clips relevant to a deck's commander, bracket, and archetype profile.
/// </summary>
public interface IContentKbRelevanceService
{
    /// <summary>
    /// Returns a budget-trimmed list of relevant expert clips, or <see langword="null"/> when the
    /// feature is disabled or no clips qualify.
    /// </summary>
    /// <param name="commanderName">Commander name used for free-text relevance.</param>
    /// <param name="bracket">Deck bracket used as a score bonus when tags align.</param>
    /// <param name="deckArchetypes">Optional pre-derived deck archetypes; when null, the service derives them from category knowledge.</param>
    /// <param name="maxRenderedChars">Maximum rendered expert-context budget for the final clip set.</param>
    /// <param name="ct">Token used to cancel the request.</param>
    /// <returns>The selected clips, or <see langword="null"/> when no clips qualify.</returns>
    Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(
        string? commanderName,
        string? bracket,
        IReadOnlySet<string>? deckArchetypes = null,
        int maxRenderedChars = 4500,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a budget-trimmed list of clips merged across pinned, followed, auto, and evergreen
    /// selection tiers, or <see langword="null"/> when the feature is disabled or no clips qualify.
    /// </summary>
    /// <param name="selection">Pinned video ids and followed creators to merge with auto selection.</param>
    /// <param name="commanderName">Commander name used for free-text relevance.</param>
    /// <param name="bracket">Deck bracket used as a score bonus when tags align.</param>
    /// <param name="deckArchetypes">Optional pre-derived deck archetypes; when null, the service derives them from category knowledge.</param>
    /// <param name="maxRenderedChars">Maximum rendered expert-context budget for the final clip set.</param>
    /// <param name="ct">Token used to cancel the request.</param>
    /// <returns>The selected clips, or <see langword="null"/> when no clips qualify.</returns>
    Task<IReadOnlyList<ContentKbExcerpt>?> GetMergedClipsAsync(
        ExpertSelection selection,
        string? commanderName,
        string? bracket,
        IReadOnlySet<string>? deckArchetypes = null,
        int maxRenderedChars = 4500,
        CancellationToken ct = default);

    /// <summary>
    /// Scores every visible artifact for admin preview use.
    /// </summary>
    /// <param name="commanderName">Commander name used for free-text relevance.</param>
    /// <param name="bracket">Deck bracket used as a score bonus when tags align.</param>
    /// <param name="ct">Token used to cancel the request.</param>
    /// <returns>Every visible row paired with its raw relevance score.</returns>
    Task<IReadOnlyList<(ContentSiteIndexRow Row, double Score)>> ScoreAllAsync(
        string? commanderName,
        string? bracket,
        CancellationToken ct = default);
}

/// <summary>
/// User-selected expert-context inputs merged with automatic Content KB relevance.
/// </summary>
public sealed record ExpertSelection(
    IReadOnlyList<string> PinnedVideoIds,
    IReadOnlySet<string> FollowedCreators);

/// <summary>
/// Default implementation of <see cref="IContentKbRelevanceService"/>.
/// </summary>
public sealed class ContentKbRelevanceService : IContentKbRelevanceService
{
    // Calibrated from 30-TAG-AUDIT.md (2026-06-05): 45% of all rows and 50% of visible rows
    // have empty bracket tags, so bracket is a bonus instead of a hard gate.
    private const double BracketWeight = 0.75d;

    // Calibrated from 30-TAG-AUDIT.md (2026-06-05): archetype tags are dense across the corpus,
    // so archetype overlap is the primary structured signal.
    private const double ArchetypeWeight = 1.25d;

    // Calibrated from 30-TAG-AUDIT.md (2026-06-05): visible corpus is tiny, so commander free-text
    // needs enough weight to let specific clip mentions qualify alongside one other dimension.
    private const double CommanderWeight = 1.5d;

    // Calibrated from 30-TAG-AUDIT.md (2026-06-05): this allows any two-dimension match to survive
    // while keeping single-dimension rows below threshold after the AND gate zeros them out.
    private const double MinSelectionScore = 2.0d;

    private const int MaxClips = 5;
    private const int DefaultHeaderBudget = 96;
    private const int PerClipOverhead = 48;

    // Calibrated from 30-TAG-AUDIT.md (2026-06-05): visible corpus is 2 rows and total corpus is 20,
    // so a ~4.5 KB pre-trimmed context block is safe without adding cache complexity.
    internal const int DefaultMaxRenderedChars = 4500;

    private static readonly IReadOnlyDictionary<string, double> ArchetypeSpecificityWeights =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["value-engine"] = 0.55d,
            ["ramp"] = 0.60d,
            ["aristocrats"] = 0.90d,
            ["control"] = 0.90d,
            ["aggro"] = 1.00d,
            ["stax"] = 1.00d,
            ["voltron"] = 1.00d,
            ["tribal"] = 1.00d,
            ["combo"] = 1.00d,
            ["lands"] = 1.10d,
            ["tokens"] = 1.10d,
            ["midrange"] = 1.10d,
            ["reanimator"] = 1.25d,
            ["spellslinger"] = 1.35d,
            ["blink"] = 1.50d,
        };

    private readonly IContentSiteIndexStore _store;
    private readonly Func<string, string> _resolveArtifactPath;
    private readonly IFeatureFlagCache _flagCache;
    private readonly ContentKbArchetypeDeriver _archetypeDeriver;
    private readonly ILogger<ContentKbRelevanceService> _logger;
    private readonly Func<string, CancellationToken, Task<string>> _readArtifactAsync;

    /// <summary>
    /// Creates the production relevance service.
    /// </summary>
    /// <param name="store">Visible Content KB row store.</param>
    /// <param name="pathResolver">Artifact path resolver.</param>
    /// <param name="flagCache">Feature-flag cache.</param>
    /// <param name="archetypeDeriver">Commander archetype deriver.</param>
    /// <param name="logger">Optional logger.</param>
    public ContentKbRelevanceService(
        IContentSiteIndexStore store,
        ContentKbArtifactPathResolver pathResolver,
        IFeatureFlagCache flagCache,
        ContentKbArchetypeDeriver archetypeDeriver,
        ILogger<ContentKbRelevanceService>? logger = null)
        : this(
            store,
            artifactPath => pathResolver.ResolveArtifactFullPath(artifactPath),
            flagCache,
            archetypeDeriver,
            logger,
            static (artifactPath, cancellationToken) => File.ReadAllTextAsync(artifactPath, cancellationToken))
    {
        ArgumentNullException.ThrowIfNull(pathResolver);
    }

    internal ContentKbRelevanceService(
        IContentSiteIndexStore store,
        Func<string, string> resolveArtifactPath,
        IFeatureFlagCache flagCache,
        ContentKbArchetypeDeriver archetypeDeriver,
        ILogger<ContentKbRelevanceService>? logger = null,
        Func<string, CancellationToken, Task<string>>? readArtifactAsync = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolveArtifactPath);
        ArgumentNullException.ThrowIfNull(flagCache);
        ArgumentNullException.ThrowIfNull(archetypeDeriver);

        _store = store;
        _resolveArtifactPath = resolveArtifactPath;
        _flagCache = flagCache;
        _archetypeDeriver = archetypeDeriver;
        _logger = logger ?? NullLogger<ContentKbRelevanceService>.Instance;
        _readArtifactAsync = readArtifactAsync ?? ((artifactPath, cancellationToken) => File.ReadAllTextAsync(artifactPath, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(
        string? commanderName,
        string? bracket,
        IReadOnlySet<string>? deckArchetypes = null,
        int maxRenderedChars = DefaultMaxRenderedChars,
        CancellationToken ct = default)
    {
        if (!_flagCache.IsEnabled("content.kb.enabled")) return null;

        var normalizedCommander = NormalizeCommander(commanderName);
        var normalizedBracket = NormalizeBracket(bracket);
        var effectiveArchetypes = await ResolveDeckArchetypesAsync(deckArchetypes, commanderName, ct).ConfigureAwait(false);
        var rows = await _store.GetPublishedRowsAsync(ct).ConfigureAwait(false);

        // Perf: 30-TAG-AUDIT.md captured only 2 visible rows / 20 total rows on 2026-06-05,
        // so one artifact read per visible row is acceptable without IMemoryCache.
        var parsedRows = await ParseRowsAsync(rows, normalizedCommander, normalizedBracket, effectiveArchetypes, includeFailedRowsAsZeroScore: false, ct).ConfigureAwait(false);
        var selectedClips = SelectTopClips(parsedRows);
        if (selectedClips.Count == 0)
        {
            return null;
        }

        while (selectedClips.Count > 0 && EstimateRenderedChars(selectedClips) > maxRenderedChars)
        {
            selectedClips.RemoveAt(selectedClips.Count - 1);
        }

        return selectedClips.Count == 0 ? null : selectedClips;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContentKbExcerpt>?> GetMergedClipsAsync(
        ExpertSelection selection,
        string? commanderName,
        string? bracket,
        IReadOnlySet<string>? deckArchetypes = null,
        int maxRenderedChars = DefaultMaxRenderedChars,
        CancellationToken ct = default)
    {
        if (!_flagCache.IsEnabled("content.kb.enabled")) return null;

        ArgumentNullException.ThrowIfNull(selection);

        var normalizedCommander = NormalizeCommander(commanderName);
        var normalizedBracket = NormalizeBracket(bracket);
        var effectiveArchetypes = await ResolveDeckArchetypesAsync(deckArchetypes, commanderName, ct).ConfigureAwait(false);
        var rows = await _store.GetPublishedRowsAsync(ct).ConfigureAwait(false);
        var parsedRows = await ParseRowsAsync(rows, normalizedCommander, normalizedBracket, effectiveArchetypes, includeFailedRowsAsZeroScore: false, ct).ConfigureAwait(false);
        var consumedRowIds = new HashSet<long>();
        var followedCreators = new HashSet<string>(selection.FollowedCreators, StringComparer.OrdinalIgnoreCase);
        var pinIds = selection.PinnedVideoIds.Distinct(StringComparer.Ordinal).Take(3).ToList();
        var pinOrder = pinIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);

        var tier1Rows = parsedRows
            .Where(row =>
            {
                var pinId = GetPinId(row.Row);
                return pinId is not null && pinOrder.ContainsKey(pinId);
            })
            .OrderBy(row => pinOrder[GetPinId(row.Row)!])
            .ThenBy(row => row.OriginalOrder)
            .ToList();
        ConsumeRows(tier1Rows, consumedRowIds);
        var tier1 = CreateClipsForArtifacts(tier1Rows, "pinned");

        var tier2Rows = parsedRows
            .Where(row => !consumedRowIds.Contains(row.Row.Id))
            .Where(row => followedCreators.Contains(row.Row.Source))
            .Select(row => new
            {
                Row = row,
                Score = CalculateUngatedScore(row.ScoreInput, normalizedCommander, normalizedBracket, effectiveArchetypes),
                DimensionsHit = CountDimensionsHit(row.ScoreInput, normalizedCommander, normalizedBracket, effectiveArchetypes)
            })
            .Where(item => item.DimensionsHit >= 1)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Row.OriginalOrder)
            .Select(item => item.Row)
            .ToList();
        ConsumeRows(tier2Rows, consumedRowIds);
        var tier2 = CreateClipsForArtifacts(tier2Rows, "followed");

        var tier3Rows = parsedRows
            .Where(row => !consumedRowIds.Contains(row.Row.Id) && row.Score >= MinSelectionScore)
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.OriginalOrder)
            .ToList();
        ConsumeRows(tier3Rows, consumedRowIds);
        var tier3 = CreateClipsForArtifacts(tier3Rows, "auto");

        var tier4Rows = parsedRows
            .Where(row => !consumedRowIds.Contains(row.Row.Id) && row.Row.IsEvergreen)
            .OrderBy(row => row.OriginalOrder)
            .Take(1)
            .ToList();
        var tier4 = CreateClipsForArtifacts(tier4Rows, "evergreen", maxClips: 1);

        var merged = new List<(ContentKbExcerpt Clip, int Tier)>(MaxClips);
        AppendTier(merged, tier1, 1);
        AppendTier(merged, tier2, 2);
        AppendTier(merged, tier3, 3);
        AppendTier(merged, tier4, 4);

        TrimMergedClipsToBudget(merged, maxRenderedChars);
        if (merged.Count == 0)
        {
            return null;
        }

        return merged.Select(item => item.Clip).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(ContentSiteIndexRow Row, double Score)>> ScoreAllAsync(
        string? commanderName,
        string? bracket,
        CancellationToken ct = default)
    {
        var normalizedCommander = NormalizeCommander(commanderName);
        var normalizedBracket = NormalizeBracket(bracket);
        var effectiveArchetypes = await ResolveDeckArchetypesAsync(deckArchetypes: null, commanderName, ct).ConfigureAwait(false);
        var rows = await _store.GetPublishedRowsAsync(ct).ConfigureAwait(false);
        var parsedRows = await ParseRowsAsync(rows, normalizedCommander, normalizedBracket, effectiveArchetypes, includeFailedRowsAsZeroScore: true, ct).ConfigureAwait(false);

        return parsedRows
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.OriginalOrder)
            .Select(row => (row.Row, row.Score))
            .ToList();
    }

    internal static double ScoreArtifact(
        ScoreInput scoreInput,
        NormalizedCommander? normalizedCommander,
        string? deckBracket,
        IReadOnlySet<string> deckArchetypes)
    {
        ArgumentNullException.ThrowIfNull(scoreInput);
        ArgumentNullException.ThrowIfNull(deckArchetypes);

        var score = 0d;
        var dimensionsHit = 0;

        if (!string.IsNullOrWhiteSpace(deckBracket)
            && scoreInput.BracketTags.Any(tag =>
                ContentTagVocabulary.Brackets.Contains(tag)
                && string.Equals(tag, deckBracket, StringComparison.OrdinalIgnoreCase)))
        {
            score += BracketWeight;
            dimensionsHit++;
        }

        var archetypeScore = scoreInput.ArchetypeTags
            .Where(tag => ContentTagVocabulary.Archetypes.Contains(tag) && deckArchetypes.Contains(tag))
            .Select(GetArchetypeSpecificityWeight)
            .Sum();
        if (archetypeScore > 0d)
        {
            score += archetypeScore * ArchetypeWeight;
            dimensionsHit++;
        }

        if (normalizedCommander is not null && ContainsCommanderName(scoreInput.SearchText, normalizedCommander))
        {
            score += CommanderWeight;
            dimensionsHit++;
        }

        return dimensionsHit >= 2 ? score : 0d;
    }

    private async Task<IReadOnlySet<string>> ResolveDeckArchetypesAsync(
        IReadOnlySet<string>? deckArchetypes,
        string? commanderName,
        CancellationToken ct)
    {
        if (deckArchetypes is not null)
        {
            return deckArchetypes
                .Where(ContentTagVocabulary.Archetypes.Contains)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(commanderName))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return await _archetypeDeriver.DeriveAsync(commanderName, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ParsedArtifactRow>> ParseRowsAsync(
        IReadOnlyList<ContentSiteIndexRow> rows,
        NormalizedCommander? normalizedCommander,
        string? deckBracket,
        IReadOnlySet<string> deckArchetypes,
        bool includeFailedRowsAsZeroScore,
        CancellationToken ct)
    {
        var parsedRows = new List<ParsedArtifactRow>(rows.Count);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            try
            {
                var scoreInput = await BuildScoreInputAsync(row, ct).ConfigureAwait(false);
                var score = ScoreArtifact(scoreInput, normalizedCommander, deckBracket, deckArchetypes);
                parsedRows.Add(new ParsedArtifactRow(row, index, scoreInput, score));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping Content KB artifact {ArtifactPath} after parse/read failure.", row.ArtifactPath);

                if (includeFailedRowsAsZeroScore)
                {
                    parsedRows.Add(new ParsedArtifactRow(row, index, ScoreInput.Empty(row), 0d));
                }
            }
        }

        return parsedRows;
    }

    private async Task<ScoreInput> BuildScoreInputAsync(ContentSiteIndexRow row, CancellationToken ct)
    {
        var artifactPath = _resolveArtifactPath(row.ArtifactPath);
        var raw = await _readArtifactAsync(artifactPath, ct).ConfigureAwait(false);
        var (header, body) = ContentArtifactParser.SplitHeader(raw);
        var clips = ContentKbClipParser.ParseKeyClips(body);
        var summaryText = ExtractSectionBody(body, "## Summary");
        var sourceUrl = header.TryGetValue("url", out var url) ? url : row.VideoUrl;
        var harvestDate = ParseHarvestDate(header, row);

        return new ScoreInput(
            row,
            row.Title,
            summaryText,
            string.Join(" ", clips.Select(clip => clip.Excerpt)),
            sourceUrl,
            row.ArchetypeTags,
            row.BracketTags,
            clips,
            harvestDate,
            ComposeSearchText(row.Title, summaryText, clips));
    }

    private List<ContentKbExcerpt> SelectTopClips(IReadOnlyList<ParsedArtifactRow> parsedRows)
    {
        var selected = new List<ContentKbExcerpt>(MaxClips);

        foreach (var artifact in parsedRows
                     .Where(row => row.Score >= MinSelectionScore)
                     .OrderByDescending(row => row.Score)
                     .ThenBy(row => row.OriginalOrder))
        {
            foreach (var clip in artifact.ScoreInput.Clips)
            {
                if (selected.Count >= MaxClips)
                {
                    return selected;
                }

                selected.Add(new ContentKbExcerpt
                {
                    Source = artifact.Row.Source,
                    Title = artifact.Row.Title,
                    VideoUrl = ContentKbClipParser.BuildDeepLink(artifact.ScoreInput.SourceUrl, clip.TimestampLabel),
                    TimestampLabel = clip.TimestampLabel,
                    Excerpt = clip.Excerpt,
                    HarvestDate = artifact.ScoreInput.HarvestDate,
                    Score = artifact.Score,
                    ClipOrigin = "auto"
                });
            }
        }

        return selected;
    }

    private static int EstimateRenderedChars(IReadOnlyList<ContentKbExcerpt> clips)
    {
        var total = DefaultHeaderBudget;

        foreach (var clip in clips)
        {
            total += clip.Excerpt.Length
                + clip.Source.Length
                + clip.Title.Length
                + clip.TimestampLabel.Length
                + PerClipOverhead;
        }

        return total;
    }

    private static List<ContentKbExcerpt> CreateClipsForArtifacts(
        IEnumerable<ParsedArtifactRow> artifacts,
        string clipOrigin,
        int maxClips = int.MaxValue)
    {
        var selected = new List<ContentKbExcerpt>();

        foreach (var artifact in artifacts)
        {
            foreach (var clip in artifact.ScoreInput.Clips)
            {
                if (selected.Count >= maxClips)
                {
                    return selected;
                }

                selected.Add(new ContentKbExcerpt
                {
                    Source = artifact.Row.Source,
                    Title = artifact.Row.Title,
                    VideoUrl = ContentKbClipParser.BuildDeepLink(artifact.ScoreInput.SourceUrl, clip.TimestampLabel),
                    TimestampLabel = clip.TimestampLabel,
                    Excerpt = clip.Excerpt,
                    HarvestDate = artifact.ScoreInput.HarvestDate,
                    Score = artifact.Score,
                    ClipOrigin = clipOrigin
                });
            }
        }

        return selected;
    }

    private static void AppendTier(List<(ContentKbExcerpt Clip, int Tier)> merged, IReadOnlyList<ContentKbExcerpt> tierClips, int tier)
    {
        foreach (var clip in tierClips)
        {
            if (merged.Count >= MaxClips)
            {
                return;
            }

            merged.Add((clip, tier));
        }
    }

    private static void TrimMergedClipsToBudget(List<(ContentKbExcerpt Clip, int Tier)> merged, int maxRenderedChars)
    {
        while (merged.Count > 0 && EstimateRenderedChars(merged.Select(item => item.Clip).ToList()) > maxRenderedChars)
        {
            var removeIndex = -1;
            for (var tier = 4; tier >= 1; tier--)
            {
                removeIndex = merged.FindLastIndex(item => item.Tier == tier);
                if (removeIndex < 0)
                {
                    continue;
                }

                if (tier == 1 && merged.Count(item => item.Tier == 1) == 1)
                {
                    removeIndex = -1;
                    continue;
                }

                break;
            }

            if (removeIndex < 0)
            {
                break;
            }

            merged.RemoveAt(removeIndex);
        }
    }

    private static void ConsumeRows(IEnumerable<ParsedArtifactRow> rows, ISet<long> consumedRowIds)
    {
        foreach (var row in rows)
        {
            consumedRowIds.Add(row.Row.Id);
        }
    }

    private static string? GetPinId(ContentSiteIndexRow row)
        => row.YoutubeVideoId ?? row.RssGuid;

    private static int CountDimensionsHit(
        ScoreInput scoreInput,
        NormalizedCommander? normalizedCommander,
        string? deckBracket,
        IReadOnlySet<string> deckArchetypes)
        => CalculateScoreAndDimensions(scoreInput, normalizedCommander, deckBracket, deckArchetypes).DimensionsHit;

    private static double CalculateUngatedScore(
        ScoreInput scoreInput,
        NormalizedCommander? normalizedCommander,
        string? deckBracket,
        IReadOnlySet<string> deckArchetypes)
        => CalculateScoreAndDimensions(scoreInput, normalizedCommander, deckBracket, deckArchetypes).Score;

    private static (double Score, int DimensionsHit) CalculateScoreAndDimensions(
        ScoreInput scoreInput,
        NormalizedCommander? normalizedCommander,
        string? deckBracket,
        IReadOnlySet<string> deckArchetypes)
    {
        var score = 0d;
        var dimensionsHit = 0;

        if (!string.IsNullOrWhiteSpace(deckBracket)
            && scoreInput.BracketTags.Any(tag =>
                ContentTagVocabulary.Brackets.Contains(tag)
                && string.Equals(tag, deckBracket, StringComparison.OrdinalIgnoreCase)))
        {
            score += BracketWeight;
            dimensionsHit++;
        }

        var archetypeScore = scoreInput.ArchetypeTags
            .Where(tag => ContentTagVocabulary.Archetypes.Contains(tag) && deckArchetypes.Contains(tag))
            .Select(GetArchetypeSpecificityWeight)
            .Sum();
        if (archetypeScore > 0d)
        {
            score += archetypeScore * ArchetypeWeight;
            dimensionsHit++;
        }

        if (normalizedCommander is not null && ContainsCommanderName(scoreInput.SearchText, normalizedCommander))
        {
            score += CommanderWeight;
            dimensionsHit++;
        }

        return (score, dimensionsHit);
    }

    private static string ComposeSearchText(
        string title,
        string summaryText,
        IReadOnlyList<(string TimestampLabel, string Excerpt)> clips)
    {
        return NormalizeFreeText(string.Join(
            " ",
            new[]
            {
                title,
                summaryText,
                string.Join(" ", clips.Select(clip => clip.Excerpt))
            }));
    }

    private static string ExtractSectionBody(string body, string sectionHeading)
    {
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => string.Equals(line.Trim(), sectionHeading, StringComparison.Ordinal));
        if (start < 0)
        {
            return string.Empty;
        }

        var end = lines.Length;
        for (var index = start + 1; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                end = index;
                break;
            }
        }

        return NormalizeFreeText(string.Join(' ', lines[(start + 1)..end]));
    }

    private static DateTimeOffset ParseHarvestDate(IReadOnlyDictionary<string, string> header, ContentSiteIndexRow row)
    {
        if (header.TryGetValue("generated_utc", out var generatedUtc)
            && DateTimeOffset.TryParse(
                generatedUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return row.PublishedUtc ?? row.IndexedUtc;
    }

    private static string? NormalizeBracket(string? bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket))
        {
            return null;
        }

        return bracket.Trim();
    }

    private static NormalizedCommander? NormalizeCommander(string? commanderName)
    {
        if (string.IsNullOrWhiteSpace(commanderName))
        {
            return null;
        }

        var singleLine = Regex.Replace(commanderName, @"\s+", " ").Trim();
        if (singleLine.Length == 0)
        {
            return null;
        }

        var partnerSegments = Regex.Split(
                singleLine,
                @"\s+(?:\/\/|/|&|and|partnered with)\s+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .SelectMany(ExpandCommanderMatchCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (partnerSegments.Count == 0)
        {
            partnerSegments.Add(NormalizeFreeText(singleLine));
        }

        return new NormalizedCommander(NormalizeFreeText(singleLine), partnerSegments);
    }

    private static IEnumerable<string> ExpandCommanderMatchCandidates(string commanderSegment)
    {
        var normalizedSegment = NormalizeFreeText(commanderSegment);
        if (normalizedSegment.Length == 0)
        {
            yield break;
        }

        yield return normalizedSegment;

        var commaIndex = normalizedSegment.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex < 4)
        {
            yield break;
        }

        // Why: artifact text often uses the short commander name ("Kinnan") while category
        // knowledge is keyed by the full printed name ("Kinnan, Bonder Prodigy").
        var shortName = NormalizeFreeText(normalizedSegment[..commaIndex]);
        if (shortName.Length >= 4)
        {
            yield return shortName;
        }
    }

    private static bool ContainsCommanderName(string searchText, NormalizedCommander normalizedCommander)
    {
        if (searchText.Length == 0)
        {
            return false;
        }

        if (searchText.Contains(normalizedCommander.FullName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedCommander.PartnerNames.Any(partner => searchText.Contains(partner, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeFreeText(string value)
    {
        var normalized = Regex.Replace(value, @"\s+", " ");
        return normalized.Trim();
    }

    private static double GetArchetypeSpecificityWeight(string archetypeTag)
    {
        return ArchetypeSpecificityWeights.TryGetValue(archetypeTag, out var weight)
            ? weight
            : 1d;
    }

    internal sealed record ScoreInput(
        ContentSiteIndexRow Row,
        string Title,
        string SummaryText,
        string ClipText,
        string SourceUrl,
        IReadOnlyList<string> ArchetypeTags,
        IReadOnlyList<string> BracketTags,
        IReadOnlyList<(string TimestampLabel, string Excerpt)> Clips,
        DateTimeOffset HarvestDate,
        string SearchText)
    {
        public static ScoreInput Empty(ContentSiteIndexRow row) => new(
            row,
            row.Title,
            string.Empty,
            string.Empty,
            row.VideoUrl,
            row.ArchetypeTags,
            row.BracketTags,
            Array.Empty<(string TimestampLabel, string Excerpt)>(),
            row.PublishedUtc ?? row.IndexedUtc,
            NormalizeFreeText(row.Title));
    }

    internal sealed record ParsedArtifactRow(
        ContentSiteIndexRow Row,
        int OriginalOrder,
        ScoreInput ScoreInput,
        double Score);

    internal sealed record NormalizedCommander(
        string FullName,
        IReadOnlyList<string> PartnerNames);
}

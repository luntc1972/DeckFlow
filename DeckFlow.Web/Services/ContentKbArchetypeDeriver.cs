using System.Text.RegularExpressions;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services;

/// <summary>
/// Derives deck archetype tags from category-knowledge rows for a commander.
/// </summary>
public sealed class ContentKbArchetypeDeriver
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CategoryToArchetypes =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            // Calibrated from 30-TAG-AUDIT.md.
            ["tutor"] = ["combo"],
            ["counter"] = ["control"],
            ["removal"] = ["control"],
            ["board-wipe"] = ["control"],
            ["protection"] = ["control", "voltron"],
            ["ramp"] = ["ramp"],
            ["draw"] = ["value-engine"],
            ["utility"] = ["value-engine", "midrange"],
            ["recursion"] = ["reanimator", "value-engine"],
            ["sacrifice"] = ["aristocrats"],
            ["aristocrat"] = ["aristocrats"],
            ["aristocrats"] = ["aristocrats"],
            ["tokens"] = ["tokens"],
            ["token"] = ["tokens"],
            ["lands"] = ["lands"],
            ["land"] = ["lands"],
            ["blink"] = ["blink"],
            ["tribal"] = ["tribal"],
            ["spellslinger"] = ["spellslinger"],
            ["spells"] = ["spellslinger"],
            ["combo"] = ["combo"],
            ["control"] = ["control"],
            ["stax"] = ["stax"],
            ["aggro"] = ["aggro"],
            ["midrange"] = ["midrange"],
            ["voltron"] = ["voltron"],
            ["reanimator"] = ["reanimator"],
            ["win-cons"] = ["combo", "aggro"],
            ["finishers"] = ["aggro", "midrange"],
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CommanderKeywordFallback =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tokens"] = ["tokens"],
            ["sacrifice"] = ["aristocrats"],
            ["aristocrats"] = ["aristocrats"],
            ["blink"] = ["blink"],
            ["reanimator"] = ["reanimator"],
            ["lands"] = ["lands"],
            ["spellslinger"] = ["spellslinger"],
            ["voltron"] = ["voltron"],
            ["stax"] = ["stax"],
        };

    private readonly ICategoryKnowledgeStore _categoryKnowledgeStore;
    private readonly ILogger<ContentKbArchetypeDeriver> _logger;

    /// <summary>
    /// Creates a new deriver.
    /// </summary>
    /// <param name="categoryKnowledgeStore">Category-knowledge store used to load commander rows.</param>
    /// <param name="logger">Optional logger.</param>
    public ContentKbArchetypeDeriver(
        ICategoryKnowledgeStore categoryKnowledgeStore,
        ILogger<ContentKbArchetypeDeriver>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(categoryKnowledgeStore);

        _categoryKnowledgeStore = categoryKnowledgeStore;
        _logger = logger ?? NullLogger<ContentKbArchetypeDeriver>.Instance;
    }

    /// <summary>
    /// Derives allowlisted archetype tags for the supplied commander.
    /// </summary>
    /// <param name="commanderName">Commander name to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    /// <returns>The derived archetype tag set.</returns>
    public async Task<IReadOnlySet<string>> DeriveAsync(string? commanderName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commanderName))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        IReadOnlyList<CategoryKnowledgeRow> rows;
        try
        {
            rows = await _categoryKnowledgeStore
                .GetCategoryRowsForCommanderAsync(commanderName.Trim(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to derive Content KB archetypes for commander {CommanderName}.", commanderName);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (rows.Count == 0)
        {
            return DeriveFromCommanderKeywords(commanderName);
        }

        var archetypeSupport = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!CategoryToArchetypes.TryGetValue(row.Category.Trim(), out var archetypes))
            {
                continue;
            }

            var categoryWeight = Math.Max(row.Count, row.DeckCount);
            if (categoryWeight <= 0)
            {
                continue;
            }

            foreach (var archetype in archetypes)
            {
                if (!ContentTagVocabulary.Archetypes.Contains(archetype))
                {
                    continue;
                }

                archetypeSupport[archetype] = archetypeSupport.TryGetValue(archetype, out var current)
                    ? current + categoryWeight
                    : categoryWeight;
            }
        }

        if (archetypeSupport.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var maxSupport = archetypeSupport.Values.Max();
        var minimumSupport = Math.Max(1d, maxSupport * 0.35d);

        return archetypeSupport
            .Where(entry => entry.Value >= minimumSupport)
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<string> DeriveFromCommanderKeywords(string commanderName)
    {
        var normalized = NormalizeText(commanderName);
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in CommanderKeywordFallback)
        {
            if (!normalized.Contains(entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var archetype in entry.Value)
            {
                if (ContentTagVocabulary.Archetypes.Contains(archetype))
                {
                    matches.Add(archetype);
                }
            }
        }

        return matches;
    }

    private static string NormalizeText(string value)
    {
        var collapsed = Regex.Replace(value, @"\s+", " ");
        return collapsed.Trim();
    }
}

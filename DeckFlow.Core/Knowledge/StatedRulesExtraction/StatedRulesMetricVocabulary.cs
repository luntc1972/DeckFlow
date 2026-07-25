using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Knowledge.StatedRulesExtraction;

/// <summary>
/// Controlled vocabularies shared by stated-rule extraction and validation.
/// </summary>
public static class StatedRulesMetricVocabulary
{
    /// <summary>Closed metric allowlist aligned to Phase 95 measured keys plus stated-only keys.</summary>
    public static readonly IReadOnlySet<string> Metrics = new HashSet<string>(
        ContentTagVocabulary.CardCategories,
        StringComparer.OrdinalIgnoreCase)
    {
        "karsten:target_lands",
        "karsten:land_delta",
        "karsten:health_score",
        "combo_density:included_per_deck",
        "land_count",
        "interaction",
        "opener_probability",
        "pip_distribution",
        "power_level_philosophy",
        // Why: lift:* is deliberately excluded because creators usually state absolute counts, not statistical lift; the stated-only keys above intentionally have no Phase 95 counterpart yet.
    };

    /// <summary>Closed comparator allowlist accepted by stated-rule extraction.</summary>
    public static readonly IReadOnlySet<string> Comparators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gte",
        "lte",
        "eq",
        "range",
    };
}

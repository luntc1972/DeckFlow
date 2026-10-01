namespace DeckFlow.Web.Services.CutLab;

/// <summary>Confidence level for a composition-based archetype suggestion.</summary>
public enum CutLabArchetypeConfidence
{
    /// <summary>Incomplete classification data or no stronger pattern was found.</summary>
    Low,

    /// <summary>Full classification data supported a matching composition pattern.</summary>
    High,
}

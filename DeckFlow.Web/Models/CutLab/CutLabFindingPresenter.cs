using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Models;

/// <summary>Shared presenter for Cut Lab structural findings across Razor and JSON surfaces.</summary>
internal static class CutLabFindingPresenter
{
    /// <summary>Finding kinds whose findings collapse into a single displayed section.</summary>
    private static readonly HashSet<CutLabFindingKind> MergedKinds =
    [
        CutLabFindingKind.WeakFloorCase,
        CutLabFindingKind.ComboProtected,
        CutLabFindingKind.FunctionalTwins,
    ];

    /// <summary>Builds view-ready finding rows from structural findings.</summary>
    internal static IReadOnlyList<CutLabFindingView> BuildFindings(IReadOnlyList<CutLabFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings
            .Select(finding => new CutLabFindingView
            {
                Kind = finding.Kind,
                Heading = finding.Heading,
                Lead = finding.Lead,
                Evidence = finding.Evidence
                    .Select(evidence => evidence.ManaValue is double manaValue
                        ? $"{evidence.CardName} · MV {manaValue:0.##}"
                        : evidence.CardName)
                    .ToArray(),
                Roles = finding.Roles,
            })
            .ToArray();
    }

    /// <summary>
    /// Builds grouped finding blocks, merging WeakFloorCase, ComboProtected and FunctionalTwins
    /// items into one section per kind while every other kind keeps its own single-item section.
    /// </summary>
    internal static IReadOnlyList<CutLabFindingGroupView> BuildFindingGroups(IReadOnlyList<CutLabFindingView> findings)
    {
        // Why: D-21. FunctionalTwins iterates eight roles rather than evaluating one condition, so
        // it is the kind most likely to flood the findings panel; merging it the way WeakFloorCase
        // and ComboProtected already merge keeps the panel reviewable (Success Criterion 5) and
        // reuses the existing multi-item render loop, so only the twins help note, the
        // transport-specific combo-badge keying and the client patch note are required.
        // The single pass below appends each merged section on its first occurrence and appends
        // later findings of that kind to the same item list, so first-occurrence order holds
        // structurally instead of arithmetically. Items deliberately preserve arrival order — the
        // detector's descending-mana-value emission order, which is TWIN-03's intent — so nothing
        // may sort, reverse or de-duplicate between accumulation and projection here.
        List<(CutLabFindingKind Kind, string Heading, List<CutLabFindingView> Items)> groups = [];
        Dictionary<CutLabFindingKind, List<CutLabFindingView>> mergedItemsByKind = [];

        foreach (CutLabFindingView finding in findings)
        {
            if (MergedKinds.Contains(finding.Kind))
            {
                if (mergedItemsByKind.TryGetValue(finding.Kind, out List<CutLabFindingView>? mergedItems))
                {
                    mergedItems.Add(finding);
                    continue;
                }

                mergedItems = [finding];
                mergedItemsByKind[finding.Kind] = mergedItems;
                groups.Add((finding.Kind, finding.Heading, mergedItems));
                continue;
            }

            groups.Add((finding.Kind, finding.Heading, [finding]));
        }

        return groups
            .Select(group => new CutLabFindingGroupView
            {
                Kind = group.Kind,
                Heading = group.Heading,
                Items = group.Items.ToArray(),
            })
            .ToArray();
    }
}

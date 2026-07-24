using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Models;

/// <summary>Shared presenter for Cut Lab structural findings across Razor and JSON surfaces.</summary>
internal static class CutLabFindingPresenter
{
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
            })
            .ToArray();
    }

    /// <summary>Builds grouped finding blocks, merging WeakFloorCase items into one section.</summary>
    internal static IReadOnlyList<CutLabFindingGroupView> BuildFindingGroups(IReadOnlyList<CutLabFindingView> findings)
    {
        List<CutLabFindingGroupView> groups = [];
        List<CutLabFindingView>? weakFloorItems = null;
        List<CutLabFindingView>? comboProtectedItems = null;
        int weakFloorInsertIndex = -1;
        int comboProtectedInsertIndex = -1;

        foreach (CutLabFindingView finding in findings)
        {
            if (finding.Kind == CutLabFindingKind.WeakFloorCase)
            {
                weakFloorItems ??= [];
                if (weakFloorInsertIndex < 0)
                {
                    weakFloorInsertIndex = groups.Count;
                }

                weakFloorItems.Add(finding);
                continue;
            }

            if (finding.Kind == CutLabFindingKind.ComboProtected)
            {
                comboProtectedItems ??= [];
                if (comboProtectedInsertIndex < 0)
                {
                    comboProtectedInsertIndex = groups.Count;
                }

                comboProtectedItems.Add(finding);
                continue;
            }

            groups.Add(new CutLabFindingGroupView
            {
                Kind = finding.Kind,
                Heading = finding.Heading,
                Items = [finding],
            });
        }

        if (weakFloorItems is { Count: > 0 })
        {
            groups.Insert(weakFloorInsertIndex, new CutLabFindingGroupView
            {
                Kind = CutLabFindingKind.WeakFloorCase,
                Heading = weakFloorItems[0].Heading,
                Items = weakFloorItems.ToArray(),
            });
        }

        if (comboProtectedItems is { Count: > 0 })
        {
            groups.Insert(comboProtectedInsertIndex, new CutLabFindingGroupView
            {
                Kind = CutLabFindingKind.ComboProtected,
                Heading = comboProtectedItems[0].Heading,
                Items = comboProtectedItems.ToArray(),
            });
        }

        return groups;
    }
}

using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Projects persisted role overrides onto completed analysis contexts.</summary>
internal static class CutLabRoleOverrideApplier
{
    internal static CutLabAnalysisContext Apply(CutLabAnalysisContext context, IReadOnlyList<CutLabRoleOverride> overrides)
    {
        if (overrides.Count == 0)
        {
            return context;
        }

        // Why: stored enablers overrides remain deck-independent in state and are inert on inactive decks.
        IReadOnlyList<CutLabRoleOverride> applicable = context.ActiveTriggerFamilies.Count == 0
            && overrides.Any(item => item.Added.Contains(CutLabRoleKeys.Enablers))
            ? overrides.Select(item => item with { Added = item.Added.Where(role => CutLabRoleKeys.IsApplicable(role, context.ActiveTriggerFamilies)).ToArray() }).ToArray()
            : overrides;
        CutLabAnalyzedCard[] cards = context.AnalyzedCards
            .Select(card => !card.IsCommander
                && card.Roles.Count > 0
                && CutLabRoleOverrideRules.Find(applicable, card.Name) is { } item
                    ? card with { Roles = CutLabRoleOverrideRules.ApplyToRoles(card.Roles, item) }
                    : card)
            .ToArray();
        Dictionary<string, IReadOnlyList<string>> roles = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);

        foreach (CutLabAnalyzedCard card in cards)
        {
            roles[card.Name] = card.Roles;

            foreach (string role in card.Roles)
            {
                counts[role] = counts.TryGetValue(role, out int count)
                    ? count + card.Quantity
                    : card.Quantity;
            }
        }

        return context with { AnalyzedCards = cards, RolesByCardName = roles, RoleCounts = counts };
    }
}

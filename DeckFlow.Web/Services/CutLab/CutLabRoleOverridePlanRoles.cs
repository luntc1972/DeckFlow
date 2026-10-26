using DeckFlow.Core.Analysis;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Maps explicit role edits onto the simulator's plan flags.</summary>
internal static class CutLabRoleOverridePlanRoles
{
    /// <summary>Applies touched role families while preserving untouched classifier flags.</summary>
    public static SpellRequirement Apply(SpellRequirement spell, CardFact fact, ManabaseMode mode, CutLabRoleOverride roleOverride)
    {
        var simBase = CutLabRoleAssigner.AssignRoles(fact, [], false, mode);
        var effective = CutLabRoleOverrideRules.ApplyToRoles(simBase, roleOverride);
        PlanRole roles = spell.PlanRoles;
        bool interaction = spell.IsInteractionSpell;

        ApplyFlag(PlanRole.Payoff, ["payoffs", "wincons"]);
        ApplyFlag(PlanRole.Engine, ["engines", "draw"]);
        ApplyFlag(PlanRole.TutorCombo, ["tutor"]);
        ApplyFlag(PlanRole.Interaction, ["interaction-targeted", "interaction-mass", "protection"]);

        if (CardTypeLine.IsNonPermanentFront(fact.TypeLine))
        {
            roles &= ~(PlanRole.Payoff | PlanRole.Interaction);
        }

        return spell with { PlanRoles = roles, IsInteractionSpell = interaction };

        void ApplyFlag(PlanRole flag, string[] keys)
        {
            if (!keys.Any(key => roleOverride.Added.Contains(key) || roleOverride.Removed.Contains(key)))
            {
                return;
            }

            bool present = keys.Any(effective.Contains);
            roles = present ? roles | flag : roles & ~flag;
            if (flag == PlanRole.Interaction)
            {
                interaction = present;
            }
        }
    }
}

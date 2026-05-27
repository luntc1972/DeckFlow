using System.Text;

namespace DeckFlow.Web.Services.PromptBuilders.Analysis;

/// <summary>
/// Appends shared deck-analysis prompt guidance blocks used by all analysis prompt variants.
/// </summary>
internal static class AnalysisPromptShared
{
    /// <summary>
    /// Appends the shared Commander bracket weighting guidance.
    /// </summary>
    /// <param name="builder">The prompt builder receiving the guidance lines.</param>
    internal static void AppendBracketWeightingGuidance(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AppendLine("The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.");
        builder.AppendLine("Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.");
        builder.AppendLine("Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.");
        builder.AppendLine("Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.");
    }

    /// <summary>
    /// Appends the canonical modal double-faced card land guidance with the caller's line prefix.
    /// </summary>
    /// <param name="builder">The prompt builder receiving the guidance line.</param>
    /// <param name="linePrefix">The exact prefix to prepend to the guidance line.</param>
    internal static void AppendMdfcLandGuidance(StringBuilder builder, string linePrefix)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(linePrefix);

        builder.AppendLine($"{linePrefix}Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.");
    }

    /// <summary>
    /// Appends the shared deck profile field-detail requirements with the caller's leading indentation.
    /// </summary>
    /// <param name="builder">The prompt builder receiving the field-detail lines.</param>
    /// <param name="indent">The exact indentation to prepend to each field-detail line.</param>
    internal static void AppendDeckProfileFieldDetails(StringBuilder builder, string indent)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(indent);

        builder.AppendLine($"{indent}Field-level detail requirements for the deck_profile JSON:");
        builder.AppendLine($"{indent}- game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.");
        builder.AppendLine($"{indent}- speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.");
        builder.AppendLine($"{indent}- estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.");
        builder.AppendLine($"{indent}- can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.");
        builder.AppendLine($"{indent}- assessed_bracket: your bracket verdict for this deck (e.g. \"Bracket 3: Upgraded\"), driven primarily by estimated_win_turn and can_answer_win_turn.");
        builder.AppendLine($"{indent}- bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.");
        builder.AppendLine($"{indent}- strengths: each item should be 1-2 sentences with a specific card or interaction reference.");
        builder.AppendLine($"{indent}- weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.");
        builder.AppendLine($"{indent}- deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.");
        builder.AppendLine($"{indent}- weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.");
    }
}

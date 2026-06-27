using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Primer;

/// <summary>
/// Builds a deck-primer prompt body formatted for ChatGPT.
/// </summary>
internal sealed class ChatGptPrimerPromptVariant : IPrimerPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.ChatGpt;

    /// <inheritdoc/>
    public string Build(
        DeckPrimerRequest request,
        string decklistText,
        IReadOnlyList<PrimerSectionEntry> selectedSections,
        CommanderSpellbookResult? comboResult,
        IReadOnlyList<EdhTop16Entry>? top16Entries,
        CategoryDistributionSummary? categoryDistribution,
        int bracketNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(decklistText);
        ArgumentNullException.ThrowIfNull(selectedSections);

        var bracket = ResolveBracketLabel(bracketNumber);
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(request.DeckName))
        {
            builder.AppendLine($"Title this chat: {DeckPrimerPacketService.NormalizeSingleLine(request.DeckName, "Commander Deck")} | Deck Primer");
            builder.AppendLine();
        }

        builder.AppendLine("You are an expert Magic: The Gathering primer writer specializing in Commander.");
        builder.AppendLine("Build a pilot-facing deck primer that is grounded in the supplied decklist and reference blocks before offering any inference.");
        builder.AppendLine();
        builder.AppendLine("## DECK CONTEXT");
        builder.AppendLine($"format: {DeckPrimerPacketService.NormalizeSingleLine(request.Format, "Commander")}");
        builder.AppendLine($"target_bracket: {bracket}");
        builder.AppendLine($"selected_sections: {selectedSections.Count}");
        if (categoryDistribution is not null)
        {
            builder.AppendLine($"CATEGORY_DISTRIBUTION: ramp={categoryDistribution.RampCount}, draw={categoryDistribution.DrawCount}, tutor={categoryDistribution.TutorCount}, interaction={categoryDistribution.InteractionCount}");
        }

        builder.AppendLine();
        builder.AppendLine("## EVIDENCE RULES");
        builder.AppendLine("- Use the grounded combo, matchup, and category data below as authoritative where present.");
        builder.AppendLine("- Do not invent card text, combo lines, or metagame facts.");
        builder.AppendLine("- Keep verified combos separate from speculative ideas.");
        builder.AppendLine("- If a conclusion depends on inference from the decklist, label it as an inference.");
        builder.AppendLine();
        builder.AppendLine("## MATCHUP TARGETS");
        AppendMatchupBlock(builder, top16Entries, bracketNumber);
        builder.AppendLine();
        builder.AppendLine(DeckPrimerPacketService.BuildComboReferenceText(comboResult, "sufficient"));
        builder.AppendLine();
        builder.AppendLine("## SECTION DIRECTIVES");
        builder.AppendLine("Write the primer in the numbered order below. Each section should be concrete, deck-specific, and useful to a pilot in real games.");
        var sectionNumber = 1;
        foreach (var section in selectedSections)
        {
            builder.AppendLine($"### {sectionNumber}. {section.Title}");
            builder.AppendLine(section.HelpText);
            sectionNumber++;
        }

        builder.AppendLine();
        builder.AppendLine("## DECKLIST");
        builder.AppendLine(decklistText);
        builder.AppendLine();
        builder.AppendLine("## OUTPUT FORMAT");
        builder.AppendLine("Return the finished primer inside a single ```markdown fenced code block.");
        builder.AppendLine("Use the same numbered section order as the SECTION DIRECTIVES block.");
        builder.AppendLine("Cite verified combos only in the known-combos section, keep speculative ideas in their own section, and keep matchup guidance grounded in the supplied targets.");

        return builder.ToString().TrimEnd();
    }

    private static void AppendMatchupBlock(StringBuilder builder, IReadOnlyList<EdhTop16Entry>? top16Entries, int bracketNumber)
    {
        if (bracketNumber == 5 && top16Entries is { Count: > 0 })
        {
            builder.AppendLine("Use these named cEDH archetypes when discussing matchup posture and mulligan pressure:");
            foreach (var entry in top16Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.PlayerName))
                {
                    builder.AppendLine($"- {entry.PlayerName}");
                }
            }

            return;
        }

        builder.AppendLine("- Aggro: go-wide combat decks, commander-damage races, and fast pressure backed by efficient threats.");
        builder.AppendLine("- Control: permission-heavy shells, wraths, and value engines that try to dictate pace over multiple turns.");
        builder.AppendLine("- Midrange: creature-value and incremental-advantage decks that pivot between pressure and stabilization.");
        builder.AppendLine("- Combo: proactive decks trying to assemble infinite loops or deterministic wins before turn 8.");
        builder.AppendLine("- Stax/Hate: tax, denial, and lock-piece strategies that attack mana, card flow, or game actions.");
    }

    private static string ResolveBracketLabel(int bracketNumber)
    {
        if (bracketNumber < 1 || bracketNumber > CommanderBracketCatalog.Options.Count)
        {
            return "Unknown";
        }

        return CommanderBracketCatalog.Options[bracketNumber - 1].Label;
    }
}

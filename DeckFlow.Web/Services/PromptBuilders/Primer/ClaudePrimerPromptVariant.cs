using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Primer;

/// <summary>
/// Builds a deck-primer prompt body formatted for Claude.
/// </summary>
internal sealed class ClaudePrimerPromptVariant : IPrimerPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Claude;

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

        builder.AppendLine("<deck_primer>");
        builder.AppendLine("<role>");
        builder.AppendLine("You are an expert Magic: The Gathering primer writer specializing in Commander.");
        builder.AppendLine("</role>");
        builder.AppendLine();

        builder.AppendLine("<context>");
        builder.AppendLine($"format: {DeckPrimerPacketService.NormalizeSingleLine(request.Format, "Commander")}");
        builder.AppendLine($"target_bracket: {bracket}");
        builder.AppendLine($"selected_sections: {selectedSections.Count}");
        if (!string.IsNullOrWhiteSpace(request.DeckName))
        {
            builder.AppendLine($"deck_name: {DeckPrimerPacketService.NormalizeSingleLine(request.DeckName, string.Empty)}");
        }

        if (categoryDistribution is not null)
        {
            builder.AppendLine($"CATEGORY_DISTRIBUTION: ramp={categoryDistribution.RampCount}, draw={categoryDistribution.DrawCount}, tutor={categoryDistribution.TutorCount}, interaction={categoryDistribution.InteractionCount}");
        }

        builder.AppendLine("</context>");
        builder.AppendLine();
        builder.AppendLine("<evidence_rules>");
        builder.AppendLine("Use the grounded combo, matchup, and category data below as authoritative where present.");
        builder.AppendLine("Do not invent card text, combo lines, or metagame facts.");
        builder.AppendLine("Keep verified combos separate from speculative ideas.");
        builder.AppendLine("If a conclusion depends on inference from the decklist, label it as an inference.");
        builder.AppendLine("</evidence_rules>");
        builder.AppendLine();
        builder.AppendLine("<matchup_targets>");
        AppendMatchupBlock(builder, top16Entries, bracketNumber);
        builder.AppendLine("</matchup_targets>");
        builder.AppendLine();
        builder.AppendLine("<grounded_combos>");
        builder.AppendLine(DeckPrimerPacketService.BuildComboReferenceText(comboResult, "sufficient"));
        builder.AppendLine("</grounded_combos>");
        builder.AppendLine();
        builder.AppendLine("<section_directives>");
        builder.AppendLine("Write the primer in the numbered order below. Each section should be concrete, deck-specific, and useful to a pilot in real games.");
        var sectionNumber = 1;
        foreach (var section in selectedSections)
        {
            builder.AppendLine($"{sectionNumber}. {section.Title}");
            builder.AppendLine(section.HelpText);
            sectionNumber++;
        }

        builder.AppendLine("</section_directives>");
        builder.AppendLine();
        builder.AppendLine("<decklist>");
        builder.AppendLine(decklistText);
        builder.AppendLine("</decklist>");
        builder.AppendLine();
        builder.AppendLine("<primer_output>");
        if (request.PrimerStyle == PrimerOutputStyle.Standard)
        {
            builder.AppendLine("Return the finished primer as readable markdown.");
            builder.AppendLine("Use the same numbered section order as <section_directives>.");
            builder.AppendLine("Keep verified combos only in the known-combos section, keep speculative ideas separate, and keep matchup guidance grounded in the supplied targets.");
        }
        else
        {
            builder.AppendLine("Return the finished primer as readable markdown with consistent formatting throughout.");
            builder.AppendLine("Use the same numbered section order as <section_directives>.");
            builder.AppendLine("Start with a clickable table of contents that uses markdown anchor links to each major section.");
            builder.AppendLine("Use markdown blockquote callout boxes with emoji prefixes for recurring coaching notes: 💡 Tips, ⚠️ Common Mistakes, and 🎯 Tutor Priorities.");
            builder.AppendLine("Present combo lines in collapsible sections using <details><summary>...</summary>...</details> when that format improves readability.");
            builder.AppendLine("Include combo diagrams, tutor flowcharts, matchup tables, and mana curve plus game-plan graphics using ASCII or markdown only.");
            builder.AppendLine("Keep verified combos only in the known-combos section, keep speculative ideas separate, and keep matchup guidance grounded in the supplied targets.");
            if (request.PrimerStyle == PrimerOutputStyle.FullCedh)
            {
                builder.AppendLine("Add cEDH-depth guidance for fast mana and turn 1-turn 3 lines, including how Sol Ring, Mana Crypt, rituals, and early commander deployment accelerate the plan.");
                builder.AppendLine("Explain how the deck operates under stax, tax, and denial pieces, including how to advance or win through common lock pieces and resource pressure.");
                builder.AppendLine("Count and sequence free interaction when discussing contested wins, specifically free interaction such as Force of Will, Fierce Guardianship, and Pact effects used to defend or force through a line.");
                builder.AppendLine("Include explicit win-by-turn guidance covering when to attempt the combo line versus hold, plus realistic turn windows for proactive and patient lines.");
                builder.AppendLine("When discussing positioning and mulligan pressure, reference the named cEDH archetypes supplied in <matchup_targets> rather than generic pod labels.");
            }
        }
        builder.AppendLine("</primer_output>");
        builder.AppendLine("</deck_primer>");

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

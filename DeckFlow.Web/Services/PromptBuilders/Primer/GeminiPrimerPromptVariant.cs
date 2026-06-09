using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Primer;

/// <summary>
/// Builds a deck-primer prompt body formatted for Gemini.
/// </summary>
internal sealed class GeminiPrimerPromptVariant : IPrimerPromptVariant
{
    // Why: D-4 — 32000 char cap from 31-SPIKE.md (full-31 max primer ~30.9K chars, ~94% of Gemini 32768-byte paste warning).
    private const int DefensivePromptCharCap = 32000;

    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Gemini;

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
        var omittedSections = new List<string>();

        builder.AppendLine("You are an expert Magic: The Gathering analyst and primer writer specializing in Commander.");
        builder.AppendLine("You produce pilot-facing primers that stay grounded in supplied deck, combo, and matchup evidence.");
        builder.AppendLine();
        builder.AppendLine("Think carefully through the problem before responding.");
        builder.AppendLine();
        builder.AppendLine("## DECK CONTEXT");
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

        builder.AppendLine();
        builder.AppendLine("## EVIDENCE RULES");
        builder.AppendLine("- Use the grounded combo, matchup, and category data below as authoritative where present.");
        builder.AppendLine("- Do not invent card text, combo lines, or metagame facts.");
        builder.AppendLine("- Keep verified combos separate from speculative ideas.");
        builder.AppendLine("- If a conclusion depends on inference from the decklist, label it as an inference.");
        builder.AppendLine();
        builder.AppendLine(DeckPrimerPacketService.BuildComboReferenceText(comboResult, "sufficient"));
        builder.AppendLine();

        var identityBlock = BuildIdentityBlock(selectedSections);
        AppendIfFits(builder, identityBlock, "Identity", omittedSections);

        var gameplayBlock = BuildGameplayBlock(selectedSections, decklistText);
        AppendIfFits(builder, gameplayBlock, "Gameplay", omittedSections);

        var matchupBlock = BuildMatchupBlock(selectedSections, top16Entries, bracketNumber);
        AppendIfFits(builder, matchupBlock, "Matchups", omittedSections);

        var maintenanceBlock = BuildMaintenanceBlock(selectedSections);
        AppendIfFits(builder, maintenanceBlock, "Maintenance", omittedSections);

        var outputBlock = BuildOutputBlock();
        AppendIfFits(builder, outputBlock, "Output Format", omittedSections);

        if (omittedSections.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"[Sections omitted due to Gemini paste limit: {string.Join(", ", omittedSections)}. Re-run with fewer sections selected.]");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendIfFits(StringBuilder builder, string block, string label, List<string> omittedSections)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return;
        }

        if ((builder.Length + block.Length) <= DefensivePromptCharCap)
        {
            builder.Append(block);
            return;
        }

        omittedSections.Add(label);
    }

    private static string BuildIdentityBlock(IReadOnlyList<PrimerSectionEntry> selectedSections)
    {
        var identitySections = selectedSections.Where(section => string.Equals(section.Group, "Identity", StringComparison.OrdinalIgnoreCase)).ToList();
        if (identitySections.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## IDENTITY DIRECTIVES");
        builder.AppendLine("Write the following identity sections in numbered order. Keep each section concrete, deck-specific, and useful to a pilot in real games.");
        foreach (var section in identitySections)
        {
            builder.AppendLine($"{section.Number}. {section.Title} — {section.HelpText}");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string BuildGameplayBlock(IReadOnlyList<PrimerSectionEntry> selectedSections, string decklistText)
    {
        var gameplaySections = selectedSections.Where(section => string.Equals(section.Group, "Gameplay", StringComparison.OrdinalIgnoreCase)).ToList();
        if (gameplaySections.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## GAMEPLAY DIRECTIVES");
        builder.AppendLine("Write the following gameplay sections in numbered order. Keep the advice grounded in the actual deck composition.");
        foreach (var section in gameplaySections)
        {
            builder.AppendLine($"{section.Number}. {section.Title} — {section.HelpText}");
        }

        builder.AppendLine();
        builder.AppendLine("## DECKLIST");
        builder.AppendLine(decklistText);
        builder.AppendLine();
        return builder.ToString();
    }

    private static string BuildMatchupBlock(IReadOnlyList<PrimerSectionEntry> selectedSections, IReadOnlyList<EdhTop16Entry>? top16Entries, int bracketNumber)
    {
        var matchupSections = selectedSections.Where(section => string.Equals(section.Group, "Matchups", StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchupSections.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## MATCHUP DIRECTIVES");
        builder.AppendLine("Write the following matchup sections in numbered order.");
        foreach (var section in matchupSections)
        {
            builder.AppendLine($"{section.Number}. {section.Title} — {section.HelpText}");
        }

        builder.AppendLine();
        builder.AppendLine("## MATCHUP TARGETS");
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
        }
        else
        {
            builder.AppendLine("- Aggro: go-wide combat decks, commander-damage races, and fast pressure backed by efficient threats.");
            builder.AppendLine("- Control: permission-heavy shells, wraths, and value engines that try to dictate pace over multiple turns.");
            builder.AppendLine("- Midrange: creature-value and incremental-advantage decks that pivot between pressure and stabilization.");
            builder.AppendLine("- Combo: proactive decks trying to assemble infinite loops or deterministic wins before turn 8.");
            builder.AppendLine("- Stax/Hate: tax, denial, and lock-piece strategies that attack mana, card flow, or game actions.");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string BuildMaintenanceBlock(IReadOnlyList<PrimerSectionEntry> selectedSections)
    {
        var maintenanceSections = selectedSections.Where(section => string.Equals(section.Group, "Maintenance", StringComparison.OrdinalIgnoreCase)).ToList();
        if (maintenanceSections.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## MAINTENANCE DIRECTIVES");
        builder.AppendLine("Write the following maintenance sections in numbered order.");
        foreach (var section in maintenanceSections)
        {
            builder.AppendLine($"{section.Number}. {section.Title} — {section.HelpText}");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string BuildOutputBlock()
    {
        var builder = new StringBuilder();
        builder.AppendLine("## OUTPUT FORMAT");
        builder.AppendLine("Return the finished primer as readable markdown.");
        builder.AppendLine("Use the same numbered section order as the directive blocks that remain in the prompt.");
        builder.AppendLine("Keep verified combos only in the known-combos section, keep speculative ideas separate, and keep matchup guidance grounded in the supplied targets.");
        builder.AppendLine();
        return builder.ToString();
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

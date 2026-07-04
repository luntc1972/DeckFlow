using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.SetUpgrade;

// Helpers used: NormalizeSingleLine [promoted to internal on DeckAnalysisPacketService],
// FormatBannedCardsLine [promoted to internal on DeckAnalysisPacketService].
// CommanderBracketCatalog, JsonTextFormatterService are public statics.

/// <summary>
/// Builds a set-upgrade prompt body formatted for Claude (XML-tagged prompts with direct JSON fenced-block output).
/// </summary>
internal sealed class ClaudeSetUpgradePromptVariant : ISetUpgradePromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Claude;

    /// <summary>
    /// Builds the Claude-targeted set-upgrade prompt text for the given request.
    /// Body is a byte-for-byte copy of the pre-refactor BuildSetUpgradePromptClaude switch arm (Phase 15-02).
    /// </summary>
    public string Build(
        DeckAnalysisRequest request,
        string decklistText,
        string deckProfileJson,
        string? commanderName,
        string? generatedSetPacket,
        IReadOnlyList<string> bannedCards)
    {
        var builder = new StringBuilder();
        var upgradeFocus = request.SetUpgradeFocus.Trim();
        var isLateralOnly = string.Equals(upgradeFocus, "lateral-moves", StringComparison.OrdinalIgnoreCase);
        var isStrictOnly = string.Equals(upgradeFocus, "strict-upgrades", StringComparison.OrdinalIgnoreCase);
        var isBoth = string.Equals(upgradeFocus, "both", StringComparison.OrdinalIgnoreCase);
        var bracket = CommanderBracketCatalog.Find(request.TargetCommanderBracket);
        var setPacket = !string.IsNullOrWhiteSpace(request.SetPacketText)
            ? request.SetPacketText
            : generatedSetPacket;

        builder.AppendLine("<role>");
        builder.AppendLine("You are an expert Magic: The Gathering deck analyst specializing in Commander set reviews and upgrade evaluation.");
        builder.AppendLine("</role>");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(commanderName))
        {
            builder.AppendLine($"<commander>{commanderName}</commander>");
            builder.AppendLine();
        }

        builder.AppendLine("<deck_profile>");
        builder.AppendLine($"format: {DeckAnalysisPacketService.NormalizeSingleLine(request.Format, "Commander")}");
        if (bracket is not null)
        {
            builder.AppendLine($"target_bracket: {bracket.Label}");
            builder.AppendLine($"bracket_summary: {bracket.Summary}");
            builder.AppendLine($"bracket_turn_expectation: {bracket.TurnsExpectation}");
        }
        builder.AppendLine(deckProfileJson);
        builder.AppendLine("</deck_profile>");
        builder.AppendLine();

        builder.AppendLine("<set_packet>");
        if (string.IsNullOrWhiteSpace(setPacket))
        {
            builder.AppendLine("Paste the condensed set packet here.");
        }
        else
        {
            builder.AppendLine(setPacket.Trim());
        }
        builder.AppendLine("</set_packet>");
        builder.AppendLine();

        builder.AppendLine("<reference>");
        builder.AppendLine("  <decklist>");
        builder.AppendLine(decklistText);
        builder.AppendLine("  </decklist>");
        builder.AppendLine("  <banlist>");
        builder.AppendLine($"official_commander_banned_cards: {DeckAnalysisPacketService.FormatBannedCardsLine(bannedCards)}");
        builder.AppendLine("  </banlist>");
        builder.AppendLine("</reference>");
        builder.AppendLine();

        builder.AppendLine("<output_schema>");
        builder.AppendLine("{");
        builder.AppendLine("  \"set_upgrade_report\": {");
        builder.AppendLine("    \"sets\": [");
        builder.AppendLine("      {");
        builder.AppendLine("        \"set_code\": \"\",");
        builder.AppendLine("        \"set_name\": \"\",");
        builder.AppendLine("        \"top_adds\": [");
        builder.AppendLine("          {");
        builder.AppendLine("            \"card\": \"\",");
        builder.AppendLine("            \"card_text\": \"\",");
        builder.AppendLine("            \"reason\": \"\",");
        builder.AppendLine("            \"suggested_cut\": \"\",");
        builder.AppendLine("            \"cut_reason\": \"\"");
        builder.AppendLine("          }");
        builder.AppendLine("        ],");
        builder.AppendLine("        \"traps\": [");
        builder.AppendLine("          {");
        builder.AppendLine("            \"card\": \"\",");
        builder.AppendLine("            \"reason\": \"\"");
        builder.AppendLine("          }");
        builder.AppendLine("        ],");
        builder.AppendLine("        \"speculative_tests\": [");
        builder.AppendLine("          {");
        builder.AppendLine("            \"card\": \"\",");
        builder.AppendLine("            \"reason\": \"\"");
        builder.AppendLine("          }");
        builder.AppendLine("        ]");
        builder.AppendLine("      }");
        builder.AppendLine("    ],");
        builder.AppendLine("    \"final_shortlist\": {");
        builder.AppendLine("      \"must_test\": [");
        builder.AppendLine("        {");
        builder.AppendLine("          \"card\": \"\",");
        builder.AppendLine("          \"card_text\": \"\",");
        builder.AppendLine("          \"reason\": \"\",");
        builder.AppendLine("          \"suggested_cut\": \"\",");
        builder.AppendLine("          \"cut_reason\": \"\"");
        builder.AppendLine("        }");
        builder.AppendLine("      ],");
        builder.AppendLine("      \"optional\": [");
        builder.AppendLine("        {");
        builder.AppendLine("          \"card\": \"\",");
        builder.AppendLine("          \"card_text\": \"\",");
        builder.AppendLine("          \"reason\": \"\",");
        builder.AppendLine("          \"suggested_cut\": \"\",");
        builder.AppendLine("          \"cut_reason\": \"\"");
        builder.AppendLine("        }");
        builder.AppendLine("      ],");
        builder.AppendLine("      \"skip\": [\"\"]");
        builder.AppendLine("    }");
        builder.AppendLine("  }");
        builder.AppendLine("}");
        builder.AppendLine("</output_schema>");
        builder.AppendLine();

        builder.AppendLine("<" + "task>");
        builder.AppendLine("Read all supplied deck profile, decklist, and set packet data before beginning.");
        builder.AppendLine("Use the deck profile as authoritative for the deck's plan, strengths, weaknesses, and replaceable slots.");
        builder.AppendLine("Use the set mechanics and card reference as authoritative for set cards.");
        builder.AppendLine("Do not invent card text or rules.");
        builder.AppendLine("When a conclusion is based on the deck profile or set card text, say so briefly.");
        builder.AppendLine("When a conclusion is based on inference from deck construction or play patterns, label it as an inference.");
        builder.AppendLine("If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.");
        builder.AppendLine("If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.");
        builder.AppendLine("Cards listed under Possible Includes are not part of the current deck. Treat them only as candidate additions.");
        builder.AppendLine("Do not recommend cards listed in <reference><banlist>.");
        builder.AppendLine();
        builder.AppendLine("Analyze each selected set for possible additions to this deck, suggested removals for those additions, and any traps.");
        if (bracket is not null)
        {
            builder.AppendLine($"Target the Commander experience of {bracket.Label}.");
            builder.AppendLine($"Bracket summary: {bracket.Summary}");
            builder.AppendLine($"Turn expectation: {bracket.TurnsExpectation}");
            builder.AppendLine("Evaluate all recommended additions and cuts against this bracket target. Flag any card that would push the deck above or below the target bracket as a trap.");
        }

        if (isLateralOnly)
        {
            builder.AppendLine("Upgrade focus: lateral moves only.");
            builder.AppendLine("A lateral move fills the same role as a card already in the deck but offers a different angle, better synergy fit, or a more interesting effect at roughly the same power level.");
            builder.AppendLine("For every lateral move, identify the current deck card it would replace and explain why the swap is worth considering.");
            builder.AppendLine("Do not recommend cards that are simply stronger — flag those as traps if they would create a bracket or power mismatch.");
        }
        else if (isStrictOnly)
        {
            builder.AppendLine("Upgrade focus: strict upgrades only.");
            builder.AppendLine("A strict upgrade does the same job as a card already in the deck but is meaningfully more powerful, more efficient, or more synergistic with the deck's strategy.");
            builder.AppendLine("For every strict upgrade, name the card it replaces and explain precisely why it is better in this deck's context.");
            builder.AppendLine("Do not recommend lateral moves or speculative includes that are not clearly better than what the deck already runs.");
        }
        else if (isBoth)
        {
            builder.AppendLine("Upgrade focus: strict upgrades and lateral moves.");
            builder.AppendLine("Strict upgrade: meaningfully more powerful or efficient than a card already in the deck. Name the card being replaced and explain why it is better.");
            builder.AppendLine("Lateral move: fills the same role as an existing card but offers a different angle, better synergy fit, or more interesting effect at roughly the same power level. Name the card being replaced and explain why the swap is worth considering.");
            builder.AppendLine("Label each recommendation clearly as 'Strict Upgrade' or 'Lateral Move'.");
        }

        builder.AppendLine();
        builder.AppendLine("Return readable analysis first with:");
        builder.AppendLine("- Per-set analysis for each selected set including top adds, suggested removals, traps, and speculative tests.");
        builder.AppendLine("- A final cross-set ranked shortlist with must_test, optional, and skip recommendations.");
        builder.AppendLine("- For every top add and every shortlist entry (must_test and optional), set card_text to that card's full rules text copied verbatim from <set_packet>. Do not paraphrase, summarize, or invent card text; leave card_text empty only when the card is not present in the set packet.");
        builder.AppendLine("- A standalone discussion_summary.txt-style notes section that condenses the per-set analysis, final recommendations, key add/cut reasoning, and direct answers to the analysis questions.");
        builder.AppendLine("After the readable analysis, return a single JSON object matching <output_schema>.");
        builder.AppendLine("Return a complete set_upgrade_report JSON. You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.");
        builder.AppendLine("</" + "task>");

        return builder.ToString().TrimEnd();
    }
}

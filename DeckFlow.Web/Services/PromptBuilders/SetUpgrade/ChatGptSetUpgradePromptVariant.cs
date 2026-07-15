using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.SetUpgrade;

// Helpers used: NormalizeSingleLine [promoted to internal on DeckAnalysisPacketService],
// FormatBannedCardsLine [promoted to internal on DeckAnalysisPacketService].
// CommanderBracketCatalog, JsonTextFormatterService are public statics.

/// <summary>
/// Builds a set-upgrade prompt body formatted for ChatGPT (markdown-headed, fenced JSON output).
/// </summary>
internal sealed class ChatGptSetUpgradePromptVariant : ISetUpgradePromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.ChatGpt;

    /// <summary>
    /// Builds the ChatGPT-targeted set-upgrade prompt text for the given request.
    /// Body is a byte-for-byte copy of the pre-refactor BuildSetUpgradePromptChatGpt switch arm (Phase 15-02).
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

        builder.AppendLine("EXECUTE NOW — perform the entire task defined below and output the complete result in this reply. Do not ask which task to run, do not ask for confirmation, and do not wait for further instructions; the full task is specified below.");
        builder.AppendLine();

        builder.AppendLine("You are an expert Magic: The Gathering deck analyst specializing in Commander set reviews and upgrade evaluation.");
        builder.AppendLine();
        builder.AppendLine("Analyze each selected set for possible additions to this deck, suggested removals for those additions, and any traps.");
        builder.AppendLine("Read all supplied deck profile, decklist, and set packet data before beginning.");
        builder.AppendLine();

        // --- Deck context first ---
        builder.AppendLine("## DECK CONTEXT");
        builder.AppendLine($"format: {DeckAnalysisPacketService.NormalizeSingleLine(request.Format, "Commander")}");
        if (!string.IsNullOrWhiteSpace(commanderName))
        {
            builder.AppendLine($"commander: {commanderName}");
        }
        if (bracket is not null)
        {
            builder.AppendLine($"target_bracket: {bracket.Label}");
        }

        builder.AppendLine();

        // --- Bracket guidance ---
        if (bracket is not null)
        {
            builder.AppendLine("## BRACKET GUIDANCE");
            builder.AppendLine($"Target the Commander experience of {bracket.Label}.");
            builder.AppendLine($"Bracket summary: {bracket.Summary}");
            builder.AppendLine($"Turn expectation: {bracket.TurnsExpectation}");
            builder.AppendLine("Evaluate all recommended additions and cuts against this bracket target. Flag any card that would push the deck above or below the target bracket as a trap.");
            builder.AppendLine();
        }

        // --- Evidence rules ---
        builder.AppendLine("## EVIDENCE RULES");
        builder.AppendLine("- Use the deck profile as authoritative for the deck's plan, strengths, weaknesses, and replaceable slots.");
        builder.AppendLine("- Use the set mechanics and card reference as authoritative for set cards.");
        builder.AppendLine("- Do not invent card text or rules.");
        builder.AppendLine("- When a conclusion is based on the deck profile or set card text, say so briefly.");
        builder.AppendLine("- When a conclusion is based on inference from deck construction or play patterns, label it as an inference.");
        builder.AppendLine("- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.");
        builder.AppendLine("- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.");
        builder.AppendLine("- Cards listed under Possible Includes are not part of the current deck. Treat them only as candidate additions.");
        builder.AppendLine($"- Do not recommend cards from the official Commander banned list: {DeckAnalysisPacketService.FormatBannedCardsLine(bannedCards)}");

        // --- Upgrade focus ---
        if (isLateralOnly)
        {
            builder.AppendLine();
            builder.AppendLine("## UPGRADE FOCUS: LATERAL MOVES ONLY");
            builder.AppendLine("A lateral move fills the same role as a card already in the deck but offers a different angle, better synergy fit, or a more interesting effect at roughly the same power level.");
            builder.AppendLine("For every lateral move, identify the current deck card it would replace and explain why the swap is worth considering.");
            builder.AppendLine("Do not recommend cards that are simply stronger — flag those as traps if they would create a bracket or power mismatch.");
        }
        else if (isStrictOnly)
        {
            builder.AppendLine();
            builder.AppendLine("## UPGRADE FOCUS: STRICT UPGRADES ONLY");
            builder.AppendLine("A strict upgrade does the same job as a card already in the deck but is meaningfully more powerful, more efficient, or more synergistic with the deck's strategy.");
            builder.AppendLine("For every strict upgrade, name the card it replaces and explain precisely why it is better in this deck's context.");
            builder.AppendLine("Do not recommend lateral moves or speculative includes that are not clearly better than what the deck already runs.");
        }
        else if (isBoth)
        {
            builder.AppendLine();
            builder.AppendLine("## UPGRADE FOCUS: STRICT UPGRADES AND LATERAL MOVES");
            builder.AppendLine("Strict upgrade: meaningfully more powerful or efficient than a card already in the deck. Name the card being replaced and explain why it is better.");
            builder.AppendLine("Lateral move: fills the same role as an existing card but offers a different angle, better synergy fit, or more interesting effect at roughly the same power level. Name the card being replaced and explain why the swap is worth considering.");
            builder.AppendLine("Label each recommendation clearly as 'Strict Upgrade' or 'Lateral Move'.");
        }

        builder.AppendLine();

        // --- Output format ---
        builder.AppendLine("## OUTPUT FORMAT");
        builder.AppendLine("Structure your response as follows:");
        builder.AppendLine();
        builder.AppendLine("A. Per-set analysis — for each selected set, include:");
        builder.AppendLine("   - Top adds from that set (with one sentence of reasoning each, tied to the deck profile)");
        builder.AppendLine("   - Suggested removals for each add (name the card being cut and why it is the weakest slot)");
        builder.AppendLine("   - Traps from that set (cards that look appealing but would hurt the deck's plan, bracket target, or consistency)");
        builder.AppendLine("   - Speculative tests from that set (cards worth trying that lack enough data to confidently recommend — e.g. novel mechanics, unproven synergies, or meta-dependent value)");
        builder.AppendLine();
        builder.AppendLine("B. Final cross-set ranked shortlist:");
        builder.AppendLine("   - must_test: cards you would actively slot in and play immediately — each entry MUST include a short reason, a suggested card to cut from the current deck to make room, and the cut reason.");
        builder.AppendLine("   - optional: cards worth considering but not priority — each entry MUST include a short reason, a suggested card to cut, and the cut reason.");
        builder.AppendLine("   - skip: cards to pass on — bare card names only, no explanation needed.");
        builder.AppendLine("   Every add/cut reason must connect to the deck profile.");
        builder.AppendLine("   For every top add and every shortlist entry (must_test and optional), set card_text to that card's full rules text, copied verbatim from its line in the SET PACKET below. Do not paraphrase, summarize, or invent card text. Leave card_text empty only when the card is not present in the set packet.");
        builder.AppendLine();
        builder.AppendLine("C. Return a complete set_upgrade_report JSON matching the schema at the end of this prompt. You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.");

        // --- Data sections ---
        builder.AppendLine();
        builder.AppendLine("## DECK PROFILE");
        builder.AppendLine(deckProfileJson);
        builder.AppendLine();
        builder.AppendLine("## DECKLIST");
        builder.AppendLine(decklistText);
        builder.AppendLine();
        builder.AppendLine("## SET PACKET");
        var setPacket = !string.IsNullOrWhiteSpace(request.SetPacketText)
            ? request.SetPacketText
            : generatedSetPacket;
        if (string.IsNullOrWhiteSpace(setPacket))
        {
            builder.AppendLine("Paste the condensed set packet here.");
        }
        else
        {
            builder.AppendLine(setPacket.Trim());
        }

        // --- JSON schema at the end (referenced by step C above) ---
        builder.AppendLine();
        builder.AppendLine("## SET UPGRADE REPORT JSON SCHEMA");
        builder.AppendLine("```json");
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
        builder.AppendLine("```");

        return builder.ToString().TrimEnd();
    }
}

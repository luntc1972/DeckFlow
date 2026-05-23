using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Analysis;

// Helpers used: NormalizeSingleLine [promoted to internal on DeckAnalysisPacketService],
// ParseCardNameList [promoted to internal on DeckAnalysisPacketService],
// BuildComboReferenceText [promoted to internal on DeckAnalysisPacketService],
// FormatBannedCardsLine [promoted to internal on DeckAnalysisPacketService].
// CommanderBracketCatalog, AnalysisQuestionCatalog, JsonTextFormatterService are public statics.

/// <summary>
/// Builds a deck-analysis prompt body formatted for Claude (XML-tagged prompts with direct JSON fenced-block output).
/// </summary>
internal sealed class ClaudeAnalysisPromptVariant : IAnalysisPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Claude;

    /// <summary>
    /// Builds the Claude-targeted analysis prompt text for the given request.
    /// Body is a byte-for-byte copy of the pre-refactor BuildAnalysisPromptClaude switch arm (Phase 15-02).
    /// </summary>
    public string Build(
        DeckAnalysisRequest request,
        string decklistText,
        string referenceText,
        string deckProfileSchemaJson,
        string? commanderName,
        IReadOnlyList<string> selectedQuestionIds,
        IReadOnlyList<string> bannedCards,
        CommanderSpellbookResult? comboResult,
        bool includeCardVersions)
    {
        var bracket = CommanderBracketCatalog.Find(request.TargetCommanderBracket);
        var selectedQuestions = AnalysisQuestionCatalog.ResolveTexts(
            selectedQuestionIds,
            request.CardSpecificQuestionCardNames,
            request.BudgetUpgradeAmount);
        var allRequestedQuestions = selectedQuestions.ToList();
        if (!string.IsNullOrWhiteSpace(request.FreeformQuestion))
        {
            allRequestedQuestions.Add(request.FreeformQuestion.Trim());
        }

        var requiresFullDecklists = AnalysisQuestionCatalog.RequiresFullDecklistOutput(selectedQuestionIds);
        var requiresCategoryOutput = AnalysisQuestionCatalog.RequiresCategoryOutput(selectedQuestionIds);
        var preferredCategories = DeckAnalysisPacketService.ParseCardNameList(request.PreferredCategories);
        var protectedCards = DeckAnalysisPacketService.ParseCardNameList(request.ProtectedCards);
        var builder = new StringBuilder();

        builder.AppendLine("<role>");
        builder.AppendLine("You are an expert Magic: The Gathering deck analyst specializing in Commander.");
        builder.AppendLine("</role>");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(commanderName))
        {
            builder.AppendLine($"<commander>{commanderName}</commander>");
            builder.AppendLine();
        }

        if (bracket is not null)
        {
            builder.AppendLine("<bracket>");
            builder.AppendLine($"target_bracket: {bracket.Label}");
            builder.AppendLine($"summary: {bracket.Summary}");
            builder.AppendLine($"turns_expectation: {bracket.TurnsExpectation}");
            builder.AppendLine("All bracket options:");
            foreach (var bracketOption in CommanderBracketCatalog.Options)
            {
                builder.AppendLine($"- {bracketOption.Label}: {bracketOption.Summary} {bracketOption.TurnsExpectation}");
            }
            builder.AppendLine("</bracket>");
            builder.AppendLine();
        }

        builder.AppendLine("<deck>");
        builder.AppendLine($"format: {DeckAnalysisPacketService.NormalizeSingleLine(request.Format, "Commander")}");
        if (!string.IsNullOrWhiteSpace(request.DeckName))
        {
            builder.AppendLine($"deck_name: {DeckAnalysisPacketService.NormalizeSingleLine(request.DeckName, string.Empty)}");
        }
        if (!string.IsNullOrWhiteSpace(request.StrategyNotes))
        {
            builder.AppendLine($"strategy_notes: {DeckAnalysisPacketService.NormalizeSingleLine(request.StrategyNotes, string.Empty)}");
        }
        if (!string.IsNullOrWhiteSpace(request.MetaNotes))
        {
            builder.AppendLine($"meta_notes: {DeckAnalysisPacketService.NormalizeSingleLine(request.MetaNotes, string.Empty)}");
        }
        builder.AppendLine("decklist:");
        builder.AppendLine(decklistText);
        builder.AppendLine("</deck>");
        builder.AppendLine();

        builder.AppendLine("<reference>");
        builder.AppendLine("  <cards>");
        builder.AppendLine(referenceText);
        builder.AppendLine("  </cards>");
        var comboReferenceText = DeckAnalysisPacketService.BuildComboReferenceText(comboResult);
        if (!string.IsNullOrWhiteSpace(comboReferenceText))
        {
            builder.AppendLine("  <combos>");
            builder.AppendLine(comboReferenceText);
            builder.AppendLine("  </combos>");
        }
        builder.AppendLine("  <banlist>");
        builder.AppendLine($"official_commander_banned_cards: {DeckAnalysisPacketService.FormatBannedCardsLine(bannedCards)}");
        builder.AppendLine("  </banlist>");
        builder.AppendLine("</reference>");
        builder.AppendLine();

        builder.AppendLine("<questions>");
        for (var i = 0; i < allRequestedQuestions.Count; i++)
        {
            builder.AppendLine($"{i + 1}. {allRequestedQuestions[i]}");
        }
        builder.AppendLine("</questions>");
        builder.AppendLine();

        builder.AppendLine("<output_schema>");
        builder.AppendLine(deckProfileSchemaJson);
        builder.AppendLine("</output_schema>");
        builder.AppendLine();

        // Emit literal <task> and </task> tags via concatenation to avoid in-file parser false positives.
        builder.AppendLine("<" + "task>");
        builder.AppendLine("Read every section above before responding.");
        builder.AppendLine("Use the mechanic definitions and card reference in <reference><cards> as authoritative. Do not invent card text or rules.");
        builder.AppendLine("When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly. Label inferences from deck construction, curve, redundancy, or play patterns explicitly.");
        builder.AppendLine("If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.");
        builder.AppendLine("If you encounter a card name you do not recognize, look it up at https://scryfall.com/search?q=!\"Card Name\" before assuming what it does. Some cards are alternate-art or Universe Beyond printings with unfamiliar names.");
        builder.AppendLine("Cards labeled candidate_include in <reference><cards> are not part of the current deck — treat them only as candidate additions.");
        builder.AppendLine("Do not recommend cards listed in <reference><banlist>.");
        builder.AppendLine();
        builder.AppendLine("Answer every numbered question in <questions> with 6-12 sentences of detailed reasoning that cites specific cards from <deck> or <reference>. Do not skip, merge, or partially answer any question.");
        builder.AppendLine("After writing the readable analysis, copy every answer into the JSON object's question_answers array with the same numbering and the same full answer text expanded to JSON form.");
        builder.AppendLine("Return a Requested Question Answers section first, then recommendation sections for Top Adds and Top Cuts before the final structured output.");
        builder.AppendLine();

        if (requiresFullDecklists)
        {
            builder.AppendLine("For every requested deck-version or upgrade-path question, output the full 100-card Commander decklist.");
            builder.AppendLine("List every card on its own line — 1 commander and 99 other cards.");
            builder.AppendLine("After writing each list, count the total lines. If the count is not exactly 100, add or remove cards until it is. Show the count at the end as `// Total: 100`.");
            builder.AppendLine("When a question asks for 3 upgrade paths, produce 3 separate full decklists — one per path.");
            var exportFormat = request.DecklistExportFormat.Trim();
            if (string.Equals(exportFormat, "moxfield", StringComparison.OrdinalIgnoreCase))
            {
                if (requiresCategoryOutput)
                {
                    builder.AppendLine("Format for Moxfield bulk edit: one card per line as 'quantity CardName (SET) collectorNumber #Category1 #Category2' (e.g. '1 Sol Ring (CMM) 1 #Ramp #ManaRocks'). Category names must be single words with no spaces — use CamelCase or hyphens. List all 100 cards together with no section headers. The commander line needs no category tags.");
                }
                else
                {
                    builder.AppendLine("Format for Moxfield bulk edit: one card per line as 'quantity CardName (SET) collectorNumber' (e.g. '1 Sol Ring (CMM) 1'). List all 100 cards together with no section headers.");
                }
            }
            else if (string.Equals(exportFormat, "archidekt", StringComparison.OrdinalIgnoreCase))
            {
                if (requiresCategoryOutput)
                {
                    builder.AppendLine("Format for Archidekt bulk edit: start with a '// Commander' section header, then '1 CommanderName (SET) collectorNumber [Commander]', then '// Mainboard', then remaining 99 cards as 'quantity CardName (SET) collectorNumber [Category1,Category2]'. Categories are comma-delimited inside square brackets — no spaces around commas, no quotes.");
                }
                else
                {
                    builder.AppendLine("Format for Archidekt bulk edit: start with a '// Commander' section header, then '1 CommanderName (SET) collectorNumber [Commander]', then '// Mainboard', then remaining 99 cards as 'quantity CardName (SET) collectorNumber'.");
                }
            }
            else
            {
                builder.AppendLine("Format as plain text: quantity CardName (SET) collectorNumber (one card per line, e.g. '1 Sol Ring (CMM) 1'). Start with the commander line.");
            }

            if (requiresCategoryOutput)
            {
                builder.AppendLine("Do NOT use basic card types as categories (Creature, Instant, Sorcery, Enchantment, Artifact, Planeswalker, Battle). Use functional role categories (e.g. Ramp, CardDraw, Removal, Wipe, Tutor, WinCondition, Protection).");
                builder.AppendLine("Return the categorized decklist only inside a fenced ```text code block.");
                if (preferredCategories.Count > 0)
                {
                    builder.AppendLine($"preferred_categories: {string.Join(", ", preferredCategories)}");
                    builder.AppendLine("Use these names wherever they fit. Create additional categories only when none of the preferred names apply.");
                }
            }

            if (includeCardVersions)
            {
                builder.AppendLine("includeCardVersions: true");
                builder.AppendLine("For cards retained from the original deck, use the exact set code and collector number from the decklist below.");
                builder.AppendLine("For newly added cards, omit the set code and collector number — the deck builder will pick the default printing.");
            }

            builder.AppendLine("Return each full list in its own clearly labeled ```text fenced code block (e.g. ```text Budget Efficiency).");
            builder.AppendLine("The goal is a list that can be pasted directly into the deck builder's bulk-edit field.");
            builder.AppendLine("After each complete decklist, output:");
            builder.AppendLine("- Cards Added — a bulleted list of every card in the new deck that was NOT in the original.");
            builder.AppendLine("- Cards Cut — a bulleted list of every card in the original deck that is NOT in the new deck.");
            builder.AppendLine("- A deck_profile JSON block for this version, using the same schema as the main deck_profile. Return it in a ```json fenced code block labeled with the version name (e.g. ```json deck_profile — Budget Efficiency).");
        }

        if (protectedCards.Count > 0 && requiresFullDecklists)
        {
            builder.AppendLine($"protected_cards: {string.Join(", ", protectedCards)}");
            builder.AppendLine("Keep every protected card in all requested deck versions and upgrade paths.");
            builder.AppendLine("You may still mention them as potential cuts in the general top-cuts analysis if warranted.");
        }

        builder.AppendLine();
        builder.AppendLine("After the full analysis, return a JSON object matching <output_schema> with one question_answers entry per question, in the same order as <questions>.");
        builder.AppendLine("The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.");
        builder.AppendLine("Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.");
        builder.AppendLine("Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.");
        builder.AppendLine("Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.");
        if (requiresFullDecklists)
        {
            builder.AppendLine("The deck_versions array must contain one entry per requested deck version or upgrade path.");
            builder.AppendLine("Each entry's decklist field must contain the complete 100-card list (one card per line, same format as the text code blocks above).");
            builder.AppendLine("Do not abbreviate or truncate any decklist in the JSON — every card must be present.");
        }
        builder.AppendLine("Field-level detail requirements for the deck_profile JSON:");
        builder.AppendLine("- game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.");
        builder.AppendLine("- speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.");
        builder.AppendLine("- strengths: each item should be 1-2 sentences with a specific card or interaction reference.");
        builder.AppendLine("- weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.");
        builder.AppendLine("- deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.");
        builder.AppendLine("- weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.");
        builder.AppendLine();
        builder.AppendLine("You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.");
        builder.AppendLine("</" + "task>");

        return builder.ToString().TrimEnd();
    }
}

using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Analysis;

// Helpers used: NormalizeSingleLine [promoted to internal on DeckAnalysisPacketService],
// ParseCardNameList [promoted to internal on DeckAnalysisPacketService],
// BuildComboReferenceText [promoted to internal on DeckAnalysisPacketService].
// CommanderBracketCatalog, AnalysisQuestionCatalog, JsonTextFormatterService are public statics.

/// <summary>
/// Builds a deck-analysis prompt body formatted for Gemini (markdown persona-scaffold with schema-strictness language).
/// </summary>
internal sealed class GeminiAnalysisPromptVariant : IAnalysisPromptVariant
{
    private const int DefensivePromptCharCap = 50000;

    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Gemini;

    /// <summary>
    /// Builds the Gemini-targeted analysis prompt text for the given request.
    /// Body is a byte-for-byte copy of the pre-refactor BuildAnalysisPromptGemini switch arm (Phase 15-02).
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
        bool includeCardVersions,
        string? companionName = null)
    {
        var bracket = CommanderBracketCatalog.Find(request.TargetCommanderBracket);
        var selectedQuestions = AnalysisQuestionCatalog.ResolveTexts(selectedQuestionIds, request.CardSpecificQuestionCardNames, request.BudgetUpgradeAmount);
        var allRequestedQuestions = selectedQuestions.ToList();
        if (!string.IsNullOrWhiteSpace(request.FreeformQuestion))
        {
            allRequestedQuestions.Add(request.FreeformQuestion.Trim());
        }
        var requiresFullDecklists = AnalysisQuestionCatalog.RequiresFullDecklistOutput(selectedQuestionIds);
        var requiresCategoryOutput = AnalysisQuestionCatalog.RequiresCategoryOutput(selectedQuestionIds);
        var builder = new StringBuilder();

        builder.AppendLine("You are an expert Magic: The Gathering analyst with deep cEDH metagame knowledge.");
        builder.AppendLine("You analyze Commander decks rigorously and base every conclusion on observable card text and deck composition.");
        builder.AppendLine();
        builder.AppendLine("Think carefully through the problem before responding. Read every supplied section in full before forming any conclusion. When in doubt, prefer evidence-based caveats over confident speculation.");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(commanderName))
        {
            builder.AppendLine($"Title this chat: {commanderName} | Deck Analysis");
            builder.AppendLine();
        }

        builder.AppendLine("Analyze this Magic: The Gathering deck. Read all supplied card reference, bracket guidance, and decklist data before beginning.");
        builder.AppendLine();

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

        builder.AppendLine();

        builder.AppendLine("## EVIDENCE RULES");
        builder.AppendLine("- Use the mechanic definitions and card reference supplied below as authoritative. Read all supplied card entries before beginning the analysis.");
        builder.AppendLine("- Do not invent card text or rules.");
        builder.AppendLine("- When a conclusion is based on supplied card text, rules text, or bracket guidance, say so briefly.");
        builder.AppendLine("- When a conclusion is based on inference from deck construction, curve, redundancy, or play patterns, label it as an inference.");
        builder.AppendLine("- If the supplied data is insufficient to support a claim, say that directly instead of overstating confidence.");
        builder.AppendLine("- If you encounter a card name you do not recognize, look it up at https://scryfall.com/search?q=!\"Card Name\" before assuming what it does. Some cards are alternate-art or Universe Beyond printings with unfamiliar names.");
        builder.AppendLine("- Cards labeled candidate_include in the reference are not part of the current deck. Treat them only as candidate additions.");
        builder.AppendLine("- Do not recommend cards from the official Commander banned list (see banned list in the reference section below).");
        builder.AppendLine("- Modal double-faced cards (MDFCs) with a land back face (e.g. Sea Gate Restoration // Sea Gate Sortie) count toward the deck's land total — include them when assessing land count and mana base. Weight them higher than a plain land, since they can be cast as a spell or played as a land and add consistency and flexibility. Such cards are flagged [MDFC-land] in the reference data.");
        builder.AppendLine();

        builder.AppendLine("## BRACKET GUIDANCE");
        builder.AppendLine("Commander bracket definitions:");
        foreach (var bracketOption in CommanderBracketCatalog.Options)
        {
            builder.AppendLine($"- {bracketOption.Label}: {bracketOption.Summary} {bracketOption.TurnsExpectation}");
        }
        builder.AppendLine("The turn on which the deck can realistically START winning — deploy a lethal or game-ending line — is the single most important factor in bracket placement. Weight it above card quality, interaction density, mana base, or any other factor.");
        builder.AppendLine("Pay special attention to the Bracket 3 / Bracket 4 boundary: a deck that can consistently begin its winning line by about turn 4 belongs in Bracket 4 (Optimized) or higher even if other elements look casual, while a deck that cannot reliably threaten a win until around turn 6 belongs in Bracket 3 (Upgraded) or lower.");
        builder.AppendLine("Weight just as heavily the deck's ability to STOP an opponent from winning on that same turn — its density of interaction (counterspells, instant-speed removal, free interaction, protection) able to answer a lethal line. A deck that can both threaten its own win and disrupt opponents' wins around the same turn sits higher in its bracket.");
        builder.AppendLine("Weight the win turn by reliability, not raw speed: a fragile, unprotected line that opponents can easily answer, or one the deck cannot reassemble, should not push the deck up a bracket on speed alone. A consistently protected or redundant win line counts for more than a faster but flimsy one.");
        if (bracket is not null)
        {
            builder.AppendLine($"Target the Commander experience of {bracket.Label}.");
            builder.AppendLine($"Bracket summary: {bracket.Summary}");
            builder.AppendLine($"Turn expectation: {bracket.TurnsExpectation}");
            builder.AppendLine("Use that bracket target when evaluating speed, card quality, interaction density, and suggested improvements.");
        }
        builder.AppendLine();

        builder.AppendLine("## ANALYSIS QUESTIONS");
        builder.AppendLine("Answer each question below. Use the same numbering in your response.");
        for (var i = 0; i < allRequestedQuestions.Count; i++)
        {
            builder.AppendLine($"{i + 1}. {allRequestedQuestions[i]}");
        }
        builder.AppendLine();

        builder.AppendLine("## OUTPUT FORMAT");
        builder.AppendLine("Place your readable analysis BEFORE the <result> tag. Inside the <result> wrapper, return ONLY a single JSON object — no prose, no markdown, no commentary inside the tags. The JSON must conform exactly to the schema below: no extra fields, no missing fields, no narrative wrappers.");
        builder.AppendLine();
        builder.AppendLine("Structure your readable analysis (placed BEFORE the <result> wrapper) as follows:");
        builder.AppendLine();
        builder.AppendLine("A. Start with a section titled Requested Question Answers.");
        builder.AppendLine("   - Answer every question using the same numbering from the ANALYSIS QUESTIONS section.");
        builder.AppendLine("   - For each answer, state the conclusion first, then give 6-12 sentences of detailed reasoning that cites specific card names, interactions, and strategic rationale.");
        builder.AppendLine("   - Do not skip, merge, or partially answer any question.");
        builder.AppendLine("   - After writing the readable analysis, copy every answer into deck_profile.question_answers with the same numbering and the same full answer text expanded to JSON form.");
        builder.AppendLine();
        builder.AppendLine("B. After the question answers, include these recommendation sections:");
        builder.AppendLine("   - Top Adds: 5-10 cards with one sentence of reasoning per card, tied to the deck's plan, bracket target, or weaknesses.");
        builder.AppendLine("   - Top Cuts: 5-10 cards with one sentence of reasoning per card.");
        if (requiresFullDecklists)
        {
            builder.AppendLine();
            builder.AppendLine("C. Full decklist output requirements:");
            builder.AppendLine("   For every requested deck-version or upgrade-path question, output the full 100-card Commander decklist.");
            builder.AppendLine("   List every card on its own line — 1 commander and 99 other cards.");
            builder.AppendLine("   After writing each list, count the total lines. If the count is not exactly 100, add or remove cards until it is. Show the count at the end as `// Total: 100`.");
            builder.AppendLine("   When a question asks for 3 upgrade paths, produce 3 separate full decklists — one per path.");
            var exportFormat = request.DecklistExportFormat.Trim();
            if (string.Equals(exportFormat, "moxfield", StringComparison.OrdinalIgnoreCase))
            {
                if (requiresCategoryOutput)
                    builder.AppendLine("   Format for Moxfield bulk edit: one card per line as 'quantity CardName (SET) collectorNumber #Category1 #Category2' (e.g. '1 Sol Ring (CMM) 1 #Ramp #ManaRocks'). Category names must be single words with no spaces — use CamelCase or hyphens. List all 100 cards together with no section headers. The commander line needs no category tags.");
                else
                    builder.AppendLine("   Format for Moxfield bulk edit: one card per line as 'quantity CardName (SET) collectorNumber' (e.g. '1 Sol Ring (CMM) 1'). List all 100 cards together with no section headers.");
            }
            else if (string.Equals(exportFormat, "archidekt", StringComparison.OrdinalIgnoreCase))
            {
                if (requiresCategoryOutput)
                    builder.AppendLine("   Format for Archidekt bulk edit: start with a '// Commander' section header, then '1 CommanderName (SET) collectorNumber [Commander]', then '// Mainboard', then remaining 99 cards as 'quantity CardName (SET) collectorNumber [Category1,Category2]'. Categories are comma-delimited inside square brackets — no spaces around commas, no quotes.");
                else
                    builder.AppendLine("   Format for Archidekt bulk edit: start with a '// Commander' section header, then '1 CommanderName (SET) collectorNumber [Commander]', then '// Mainboard', then remaining 99 cards as 'quantity CardName (SET) collectorNumber'.");
            }
            else
            {
                builder.AppendLine("   Format as plain text: quantity CardName (SET) collectorNumber (one card per line, e.g. '1 Sol Ring (CMM) 1'). Start with the commander line.");
            }
            if (requiresCategoryOutput)
            {
                builder.AppendLine("   Do NOT use basic card types as categories (Creature, Instant, Sorcery, Enchantment, Artifact, Planeswalker, Battle). Use functional role categories (e.g. Ramp, CardDraw, Removal, Wipe, Tutor, WinCondition, Protection).");
                builder.AppendLine("   Return the categorized decklist only inside a fenced ```text code block.");
                var preferredCats = DeckAnalysisPacketService.ParseCardNameList(request.PreferredCategories);
                if (preferredCats.Count > 0)
                {
                    builder.AppendLine($"   Preferred category names: {string.Join(", ", preferredCats)}");
                    builder.AppendLine("   Use these names wherever they fit. Create additional categories only when none of the preferred names apply.");
                }
            }
            if (includeCardVersions)
            {
                builder.AppendLine("   For cards retained from the original deck, use the exact set code and collector number from the decklist below.");
                builder.AppendLine("   For newly added cards, omit the set code and collector number — the deck builder will pick the default printing.");
            }
            builder.AppendLine("   Return each full list in its own clearly labeled ```text fenced code block (e.g. ```text Budget Efficiency).");
            builder.AppendLine("   The goal is a list that can be pasted directly into the deck builder's bulk-edit field.");
            builder.AppendLine();
            builder.AppendLine("   After each complete decklist, output:");
            builder.AppendLine("   - Cards Added — a bulleted list of every card in the new deck that was NOT in the original.");
            builder.AppendLine("   - Cards Cut — a bulleted list of every card in the original deck that is NOT in the new deck.");
            builder.AppendLine("   - A deck_profile JSON block for this version, using the same schema as the main deck_profile. Return it in a ```json fenced code block labeled with the version name (e.g. ```json deck_profile — Budget Efficiency).");
        }
        var protectedCards = DeckAnalysisPacketService.ParseCardNameList(request.ProtectedCards);
        if (protectedCards.Count > 0 && requiresFullDecklists)
        {
            builder.AppendLine();
            builder.AppendLine($"   Protected cards: {string.Join(", ", protectedCards)}");
            builder.AppendLine("   Keep every protected card in all requested deck versions and upgrade paths.");
            builder.AppendLine("   You may still mention them as potential cuts in the general top-cuts analysis if warranted.");
        }

        builder.AppendLine();
        builder.AppendLine("D. After the full analysis, return a JSON object named deck_profile matching the schema below.");
        builder.AppendLine("   You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block.");
        builder.AppendLine("   The question_answers array must contain one entry per question, in the same order as the numbered list above.");
        builder.AppendLine("   Do not omit any question. If there are 8 questions, return exactly 8 question_answers entries numbered 1 through 8.");
        builder.AppendLine("   The JSON question_answers entries must mirror the readable Requested Question Answers section one-for-one.");
        builder.AppendLine("   Each answer field must be a thorough response (6-12 sentences minimum) — not a brief summary. Cite specific card names and interactions.");
        builder.AppendLine("   Do not collapse multiple questions into one JSON entry, and do not replace full answers with shorthand summaries in the JSON.");
        builder.AppendLine("   Before returning the JSON, count the numbered questions above and verify that question_answers has the same count.");
        if (requiresFullDecklists)
        {
            builder.AppendLine("   The deck_versions array must contain one entry per requested deck version or upgrade path.");
            builder.AppendLine("   Each entry's decklist field must contain the complete 100-card list (one card per line, same format as the text code blocks above).");
            builder.AppendLine("   Do not abbreviate or truncate any decklist in the JSON — every card must be present.");
        }
        builder.AppendLine();
        builder.AppendLine(JsonTextFormatterService.ResultWrapInstruction);
        builder.AppendLine();
        builder.AppendLine("   Field-level detail requirements for the deck_profile JSON:");
        builder.AppendLine("   - game_plan: 2-4 sentences describing the deck's primary win condition, game plan, and how it closes games.");
        builder.AppendLine("   - speed: 2-3 sentences characterizing the deck's speed, threat deployment, and typical turn progression.");
        builder.AppendLine("   - estimated_win_turn: the earliest turn the deck can realistically START a lethal or game-ending line, as an integer. This is the single most important driver of bracket placement.");
        builder.AppendLine("   - can_answer_win_turn: true if the deck has interaction (counterspells, instant-speed removal, free interaction, protection) able to stop an opponent from winning on or around that same turn; otherwise false.");
        builder.AppendLine("   - assessed_bracket: your bracket verdict for this deck (e.g. \"Bracket 3: Upgraded\"), driven primarily by estimated_win_turn and can_answer_win_turn.");
        builder.AppendLine("   - bracket_justification: 2-3 sentences justifying the assessed bracket, citing the win turn and interaction density above any other factor.");
        builder.AppendLine("   - strengths: each item should be 1-2 sentences with a specific card or interaction reference.");
        builder.AppendLine("   - weaknesses: each item should be 1-2 sentences with a specific card or interaction reference.");
        builder.AppendLine("   - deck_needs: each item should be 1-2 sentences identifying a gap and what kind of card fills it.");
        builder.AppendLine("   - weak_slots.reason: 2-3 sentences explaining why this slot is weak and what would improve it.");
        builder.AppendLine();
        builder.AppendLine(deckProfileSchemaJson);

        var comboReferenceText = DeckAnalysisPacketService.BuildComboReferenceText(comboResult);
        if (!string.IsNullOrWhiteSpace(comboReferenceText))
        {
            builder.AppendLine();
            builder.AppendLine(comboReferenceText);
        }

        builder.AppendLine();
        builder.AppendLine("## REFERENCE DATA");
        builder.AppendLine(referenceText);

        builder.AppendLine();
        builder.AppendLine("## DECKLIST");
        builder.AppendLine(decklistText);
        builder.AppendLine();
        builder.AppendLine(JsonTextFormatterService.GeminiJsonMandate);

        return builder.ToString().TrimEnd();
    }
}

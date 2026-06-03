using System.Text;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.MetaGap;

// Helpers used: BuildCompactDecklist, BuildCompactRefDecklist, BuildComboReferenceText
// [promoted to internal on MetaGapService].
// JsonTextFormatterService is a public static.

/// <summary>
/// Builds a cEDH meta-gap prompt body formatted for Gemini (markdown persona-scaffold with schema-strictness language).
/// </summary>
internal sealed class GeminiMetaGapPromptVariant : IMetaGapPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Gemini;

    /// <summary>
    /// Builds the Gemini-targeted meta-gap prompt text for the given request.
    /// Body is a byte-for-byte copy of the pre-refactor BuildPromptGemini switch arm (Phase 15-02).
    /// </summary>
    public string Build(
        string commanderName,
        IReadOnlyList<DeckEntry> myDeckEntries,
        CommanderSpellbookResult? myDeckCombos,
        IReadOnlyList<EdhTop16Entry> selectedEntries,
        IReadOnlyList<CommanderSpellbookResult?> referenceDeckCombos,
        IReadOnlyDictionary<string, string> oracleNameMap,
        string schemaJson)
    {
        var refCount = selectedEntries.Count;
        var builder = new StringBuilder();
        builder.AppendLine("You are an expert Magic: The Gathering analyst with deep cEDH metagame knowledge.");
        builder.AppendLine("You analyze Commander decks rigorously and base every conclusion on observable card text and deck composition.");
        builder.AppendLine();
        builder.AppendLine("Think carefully through the problem before responding. Read every supplied section in full before forming any conclusion. When in doubt, prefer evidence-based caveats over confident speculation.");
        builder.AppendLine();
        builder.AppendLine($"Title this chat: {commanderName} | cEDH Meta Gap");
        builder.AppendLine();
        builder.AppendLine("ROLE:");
        builder.AppendLine("You are a cEDH deck optimization analyst.");
        builder.AppendLine($"Compare MY_DECK against {refCount} REF deck(s).");
        builder.AppendLine();

        builder.AppendLine("EVIDENCE PRIORITY:");
        builder.AppendLine("1. Use the supplied decklists as the primary evidence.");
        builder.AppendLine("2. Use the supplied Commander Spellbook combo sections as verified combo evidence.");
        builder.AppendLine("3. Only infer patterns that are strongly supported by the supplied cards.");
        builder.AppendLine("4. If Commander Spellbook evidence and deck-reading inference conflict, prefer the Commander Spellbook evidence.");
        builder.AppendLine();

        builder.AppendLine("RULES:");
        builder.AppendLine("- Read every supplied decklist before answering.");
        builder.AppendLine("- Base every conclusion ONLY on observable card overlap and deck construction.");
        builder.AppendLine("- Do NOT assume combo lines unless supported by card presence in the lists.");
        builder.AppendLine("- Cite specific card names as evidence.");
        builder.AppendLine("- Clearly label any interpretation as inference.");
        builder.AppendLine("- If evidence is weak or unclear, explicitly say so in the relevant field.");
        builder.AppendLine("- Do NOT invent card text or interactions.");
        builder.AppendLine();

        builder.AppendLine("INPUT DATA:");
        builder.AppendLine($"MY_DECK ({commanderName}):");
        builder.AppendLine(MetaGapService.BuildCompactDecklist(myDeckEntries, oracleNameMap));
        builder.AppendLine();
        builder.AppendLine(MetaGapService.BuildComboReferenceText("MY_DECK", myDeckCombos));
        builder.AppendLine();

        for (var index = 0; index < refCount; index++)
        {
            var entry = selectedEntries[index];
            builder.Append($"R{index + 1} (");
            builder.Append(string.IsNullOrWhiteSpace(entry.PlayerName) ? "?" : entry.PlayerName);
            builder.Append($", #{entry.Standing}");
            if (!string.IsNullOrWhiteSpace(entry.TournamentName))
            {
                builder.Append($", {entry.TournamentName}");
            }

            if (entry.TournamentDate.HasValue)
            {
                builder.Append($", {entry.TournamentDate.Value:yyyy-MM-dd}");
            }

            builder.AppendLine("):");
            builder.AppendLine(MetaGapService.BuildCompactRefDecklist(entry, oracleNameMap));
            builder.AppendLine();

            var comboResult = index < referenceDeckCombos.Count ? referenceDeckCombos[index] : null;
            builder.AppendLine(MetaGapService.BuildComboReferenceText($"R{index + 1}", comboResult));
            builder.AppendLine();
        }

        builder.AppendLine("ANALYSIS TASK:");
        builder.AppendLine("Use the input data above and complete every section below.");
        builder.AppendLine();

        builder.AppendLine("1. WIN CONDITIONS");
        builder.AppendLine("- Identify primary and backup win lines in MY_DECK.");
        builder.AppendLine("- Identify primary and backup win lines across REF decks (consensus).");
        builder.AppendLine("- List win lines present in multiple REF decks but missing in MY_DECK.");
        builder.AppendLine();

        builder.AppendLine("2. INTERACTION AUDIT");
        builder.AppendLine("- Count and compare counterspells, removal, free interaction, and stax pieces.");
        builder.AppendLine("- Determine if MY_DECK is under, over, or aligned vs REF decks.");
        builder.AppendLine("- Identify key missing interaction pieces.");
        builder.AppendLine();

        builder.AppendLine("3. SPEED & TEMPO");
        builder.AppendLine("- Classify each deck as turbo (T2-3), fast (T3-4), mid (T4-5), or grind (T5+).");
        builder.AppendLine("- Estimate MY_DECK vs REF average goldfish speed.");
        builder.AppendLine("- Identify cards contributing to faster starts (fast mana, free spells).");
        builder.AppendLine();

        builder.AppendLine("4. MANA EFFICIENCY");
        builder.AppendLine("- Compare fast mana count (0-1 CMC ramp), total ramp density, and land count.");
        builder.AppendLine("- Count modal double-faced cards (MDFCs) with a land back face toward each deck's land total, and weight them higher than a plain land since they double as flexible land/spell slots that improve consistency.");
        builder.AppendLine("- Identify missing high-impact acceleration pieces.");
        builder.AppendLine();

        builder.AppendLine("5. CARD OVERLAP ANALYSIS");
        builder.AppendLine($"- Core convergence: cards in all {refCount} REF decks. Flag whether MY_DECK has them.");
        builder.AppendLine("- High-frequency staples: cards in 2+ REF decks but not in MY_DECK = missing staples.");
        builder.AppendLine("- Cards unique to MY_DECK (in 0 REF decks) = potential cuts.");
        builder.AppendLine("- Categorize each by role: ramp, interaction, draw, wincon, protection, stax, tutor, utility, land.");
        builder.AppendLine();

        builder.AppendLine("6. CONSISTENCY & REDUNDANCY");
        builder.AppendLine("- Compare tutor density, redundant combo pieces, and draw engine count.");
        builder.AppendLine("- Determine whether MY_DECK is more or less consistent than the REF sample.");
        builder.AppendLine();

        builder.AppendLine("7. TOP IMPROVEMENTS");
        builder.AppendLine("- Top 5-10 adds: include what each replaces and justify using overlap evidence.");
        builder.AppendLine("- Top 5-10 cuts: explain why each is low-impact or non-meta.");
        builder.AppendLine();

        builder.AppendLine("8. META POSITIONING");
        builder.AppendLine("- Determine if MY_DECK is faster or slower than the field, more or less interactive.");
        builder.AppendLine("- Identify which archetype it most resembles (turbo, midrange, control, stax).");
        builder.AppendLine("- Assign a 1-10 cEDH readiness score with 2-sentence justification.");
        builder.AppendLine();

        builder.AppendLine("OUTPUT CONTRACT:");
        builder.AppendLine("Place your readable analysis BEFORE the <result> tag. Inside the <result> wrapper, return ONLY a single JSON object — no prose, no markdown, no commentary inside the tags. The JSON must conform exactly to the schema below: no extra fields, no missing fields, no narrative wrappers.");
        builder.AppendLine("- First, provide a concise human-readable meta gap summary.");
        builder.AppendLine("- Then return the JSON inside a fenced ```json code block (triple-backtick json) whose top-level object is meta_gap. Do not return raw JSON outside a code block.");
        builder.AppendLine($"- {JsonTextFormatterService.ResultWrapInstruction}");
        builder.AppendLine("- The prose summary must come before the JSON block.");
        builder.AppendLine("- Fill every field in meta_gap.");
        builder.AppendLine("- Use empty strings, 0, 0.0, false, or [] when evidence is missing.");
        builder.AppendLine("- Keep all detail and justification concise, specific, and evidence-based.");
        builder.AppendLine("- Put the consistency/redundancy summary and meta-positioning summary into meta_summary and optimization_path.");
        builder.AppendLine("- Do not add any extra sections after the JSON block.");
        builder.AppendLine();
        builder.AppendLine("JSON SHAPE:");
        builder.AppendLine("Use this exact shape:");
        builder.AppendLine(schemaJson);
        builder.AppendLine();
        builder.AppendLine(JsonTextFormatterService.GeminiJsonMandate);
        return builder.ToString().TrimEnd();
    }
}

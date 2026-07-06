using System.Text;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.MetaGap;

// Helpers used: BuildCompactDecklist, BuildCompactRefDecklist, BuildComboReferenceText
// [promoted to internal on MetaGapService].
// JsonTextFormatterService is a public static.

/// <summary>
/// Builds a cEDH meta-gap prompt body formatted for Claude (XML-tagged prompts with direct JSON fenced-block output).
/// </summary>
internal sealed class ClaudeMetaGapPromptVariant : IMetaGapPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Claude;

    /// <summary>
    /// Builds the Claude-targeted meta-gap prompt text for the given request.
    /// Body is a byte-for-byte copy of the pre-refactor BuildPromptClaude switch arm (Phase 15-02).
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
        builder.AppendLine("<role>");
        builder.AppendLine("You are a cEDH deck optimization analyst.");
        builder.AppendLine("</role>");
        builder.AppendLine();
        builder.AppendLine("<my_deck>");
        builder.AppendLine($"  <commander>{commanderName}</commander>");
        builder.AppendLine("  <list>");
        builder.AppendLine(MetaGapService.BuildCompactDecklist(myDeckEntries, oracleNameMap));
        builder.AppendLine("  </list>");
        builder.AppendLine("  <combos>");
        builder.AppendLine(MetaGapService.BuildComboReferenceText("MY_DECK", myDeckCombos));
        builder.AppendLine("  </combos>");
        builder.AppendLine("</my_deck>");
        builder.AppendLine();
        builder.AppendLine("<reference_decks>");
        for (var index = 0; index < refCount; index++)
        {
            var entry = selectedEntries[index];
            builder.AppendLine("  <reference>");
            builder.Append("  player: ");
            builder.AppendLine(string.IsNullOrWhiteSpace(entry.PlayerName) ? "?" : entry.PlayerName);
            builder.Append("  standing: #");
            builder.AppendLine(entry.Standing.ToString());
            if (!string.IsNullOrWhiteSpace(entry.TournamentName))
            {
                builder.Append("  tournament: ");
                builder.AppendLine(entry.TournamentName);
            }

            if (entry.TournamentDate.HasValue)
            {
                builder.Append("  tournament_date: ");
                builder.AppendLine(entry.TournamentDate.Value.ToString("yyyy-MM-dd"));
            }

            builder.AppendLine("  <list>");
            builder.AppendLine(MetaGapService.BuildCompactRefDecklist(entry, oracleNameMap));
            builder.AppendLine("  </list>");
            builder.AppendLine("  <combos>");
            var comboResult = index < referenceDeckCombos.Count ? referenceDeckCombos[index] : null;
            builder.AppendLine(MetaGapService.BuildComboReferenceText($"R{index + 1}", comboResult));
            builder.AppendLine("  </combos>");
            builder.AppendLine("  </reference>");
        }
        builder.AppendLine("</reference_decks>");
        builder.AppendLine();
        builder.AppendLine("<output_schema>");
        builder.AppendLine(schemaJson);
        builder.AppendLine("</output_schema>");
        builder.AppendLine();
        builder.AppendLine("<" + "task>");
        builder.AppendLine($"Compare MY_DECK against {refCount} REF deck(s).");
        builder.AppendLine("Use the supplied decklists as the primary evidence.");
        builder.AppendLine("Use the supplied Commander Spellbook combo sections as verified combo evidence.");
        builder.AppendLine("Only infer patterns that are strongly supported by the supplied cards.");
        builder.AppendLine("If Commander Spellbook evidence and deck-reading inference conflict, prefer the Commander Spellbook evidence.");
        builder.AppendLine("Read every supplied decklist before answering.");
        builder.AppendLine("Base every conclusion ONLY on observable card overlap and deck construction.");
        builder.AppendLine("Do NOT assume combo lines unless supported by card presence in the lists.");
        builder.AppendLine("Cite specific card names as evidence.");
        builder.AppendLine("Clearly label any interpretation as inference.");
        builder.AppendLine("If evidence is weak or unclear, explicitly say so in the relevant field.");
        builder.AppendLine("Do NOT invent card text or interactions.");
        builder.AppendLine("Treat every infinite or near-infinite result in the supplied combo sections (Infinite/Near-infinite blinking, landfall triggers, ETB/LTB, mana, mill, untap) as an ACTIVE win engine, and scan MY_DECK for any card whose repeated trigger under that loop is game-ending (e.g. a land or permanent with an ETB/landfall ping, drain, or mill) - such a payoff is a win condition even if it is in no combo line and no REF deck.");
        builder.AppendLine("A card that appears in a supplied combo line, or that pays off one of MY_DECK's own combo/loop results, is a PROTECTED combo piece or win condition: never place it in potential_cuts or top_10_cuts, never name it in the replaces field of a top_10_adds entry, classify its role as wincon or combo, and reconcile every proposed cut or replacement against MY_DECK's combo evidence first.");
        builder.AppendLine("A tutor or fetch effect that can retrieve one of MY_DECK's combo pieces is itself a PROTECTED combo enabler - do not cut it or name it in a replaces field. Exception to the observable-overlap rule, solely to identify such tutors: you may infer a card's hidden tutor ability from well-known printed text (e.g. cycling / typecycling such as wizardcycling, transmute, or explicit 'search your library' text), label that as inference, and do not use inferred card text for any other conclusion.");
        builder.AppendLine("When assessing mana efficiency, count modal double-faced cards (MDFCs) with a land back face toward each deck's land total, and weight them higher than a plain land since they double as flexible land/spell slots that improve consistency.");
        builder.AppendLine();
        builder.AppendLine("Provide readable analysis first covering:");
        builder.AppendLine("- WIN CONDITIONS");
        builder.AppendLine("- INTERACTION AUDIT");
        builder.AppendLine("- SPEED & TEMPO");
        builder.AppendLine("- MANA EFFICIENCY");
        builder.AppendLine("- CARD OVERLAP ANALYSIS");
        builder.AppendLine("- CONSISTENCY & REDUNDANCY");
        builder.AppendLine("- TOP IMPROVEMENTS");
        builder.AppendLine("- META POSITIONING");
        builder.AppendLine("After the readable summary, return a single JSON object matching <output_schema>.");
        builder.AppendLine("Return the JSON inside a fenced ```json code block (triple-backtick json) whose top-level object is meta_gap. Do not return raw JSON outside a code block.");
        builder.AppendLine("</" + "task>");
        return builder.ToString().TrimEnd();
    }
}

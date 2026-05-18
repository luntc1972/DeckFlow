using System.Text;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.MetaGap;

// Helpers used: BuildCompactDecklist, BuildCompactRefDecklist, BuildComboReferenceText
// [promoted to internal on MetaGapService].
// JsonTextFormatterService is a public static.

/// <summary>
/// Builds a cEDH meta-gap prompt body formatted for Claude (XML-tagged prompts with result-wrapped output).
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
        builder.AppendLine(JsonTextFormatterService.ResultWrapInstruction);
        builder.AppendLine("Wrap your final structured output in <result>...</result> tags. Inside <result>, return a single JSON object matching <output_schema>. Place the readable answer prose BEFORE the <result> tag (outside it). Do not put prose inside <result>; do not put JSON outside <result>.");
        builder.AppendLine("</" + "task>");
        return builder.ToString().TrimEnd();
    }
}

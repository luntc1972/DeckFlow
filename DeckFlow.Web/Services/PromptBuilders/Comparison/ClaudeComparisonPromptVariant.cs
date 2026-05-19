using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.Comparison;

// Helpers used: AppendComparisonPromptDeckXml, IndentJson [promoted to internal on DeckComparisonService].
// JsonTextFormatterService is a public static.

/// <summary>
/// Builds a deck-comparison prompt body formatted for Claude (XML-tagged prompts with result-wrapped output).
/// </summary>
internal sealed class ClaudeComparisonPromptVariant : IComparisonPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Claude;

    /// <summary>
    /// Builds the Claude-targeted comparison prompt text for the given request.
    /// Body is a byte-for-byte copy of the pre-refactor BuildComparisonPromptClaude switch arm (Phase 15-02).
    /// </summary>
    public string Build(
        DeckComparisonService.DeckComparisonDeckSummary deckA,
        DeckComparisonService.DeckComparisonDeckSummary deckB,
        string deckAListText,
        string deckBListText,
        string deckAComboText,
        string deckBComboText,
        string comparisonContextText,
        string comparisonSchemaJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<role>");
        builder.AppendLine("You are an expert Magic: The Gathering deck analyst specializing in Commander.");
        builder.AppendLine("</role>");
        builder.AppendLine();

        DeckComparisonService.AppendComparisonPromptDeckXml(builder, "deck_a", deckA, deckAListText, deckAComboText);
        builder.AppendLine();
        DeckComparisonService.AppendComparisonPromptDeckXml(builder, "deck_b", deckB, deckBListText, deckBComboText);
        builder.AppendLine();

        builder.AppendLine("<comparison_context>");
        builder.AppendLine(comparisonContextText);
        builder.AppendLine("</comparison_context>");
        builder.AppendLine();

        builder.AppendLine("<output_schema>");
        builder.AppendLine("{");
        builder.AppendLine("  \"deck_comparison\": " + DeckComparisonService.IndentJson(comparisonSchemaJson, 2));
        builder.AppendLine("}");
        builder.AppendLine("</output_schema>");
        builder.AppendLine();

        builder.AppendLine("<" + "task>");
        builder.AppendLine("Based only on the provided deck contents and supplied context, compare the decks in a typical multiplayer Commander environment.");
        builder.AppendLine("Provide a grounded, evidence-based comparison instead of a speculative matchup prediction.");
        builder.AppendLine("Read all supplied deck data and context before beginning the comparison.");
        builder.AppendLine("Treat the supplied decklists, commander names, bracket selections, combo findings, and derived comparison context as the source of truth.");
        builder.AppendLine("Do not invent cards, colors, commander identities, or card text not supported by the provided context.");
        builder.AppendLine("Do not assume a card's role unless it is supported by the deck contents or provided context.");
        builder.AppendLine("Do not claim exact card text unless it is included in the packet.");
        builder.AppendLine("If a conclusion is not well-supported by the provided deck contents, say that explicitly instead of guessing.");
        builder.AppendLine("If you encounter a card name you do not recognize, look it up at https://scryfall.com/search?q=!\"Card Name\" before assuming what it does. Some cards are alternate-art or Universe Beyond printings with unfamiliar names.");
        builder.AppendLine("When uncertain, mark the statement as low-confidence and add the reason to confidence_notes.");
        builder.AppendLine("For each major conclusion, reference the deck patterns, card packages, or commander incentives that support it.");
        builder.AppendLine("Base conclusions on observable deck construction rather than vague impressions.");
        builder.AppendLine("Do not make claims about exact metagames unless explicitly provided.");
        builder.AppendLine("If the two decks target different brackets, note the mismatch prominently and explain how it affects the comparison.");
        builder.AppendLine();
        builder.AppendLine("Write readable comparison prose first with:");
        builder.AppendLine($"- Commander role and game plan for {deckA.Name}");
        builder.AppendLine($"- Commander role and game plan for {deckB.Name}");
        builder.AppendLine("- Speed and setup tempo");
        builder.AppendLine("- Ramp");
        builder.AppendLine("- Draw");
        builder.AppendLine("- Spot interaction");
        builder.AppendLine("- Sweepers");
        builder.AppendLine("- Recursion");
        builder.AppendLine("- Closing power, including complete combos and near-combos as part of the win-condition comparison");
        builder.AppendLine("- Resilience");
        builder.AppendLine("- Consistency");
        builder.AppendLine("- Mana stability");
        builder.AppendLine("- Dependence on commander");
        builder.AppendLine("- Likely table fit");
        builder.AppendLine("- Major overlap and major differences");
        builder.AppendLine("- Five concrete cards or packages that best explain the gap between the two decks, with one sentence of reasoning each.");
        builder.AppendLine("- A final verdict naming which deck is stronger overall and why, in 2-4 sentences.");
        builder.AppendLine("After the readable comparison, return a single JSON object matching <output_schema>.");
        builder.AppendLine("You MUST return the JSON inside a fenced ```json code block (triple-backtick json). Do not return raw JSON outside a code block. The top-level object must be named deck_comparison.");
        builder.AppendLine("</" + "task>");
        return builder.ToString().TrimEnd();
    }
}

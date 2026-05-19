using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.FollowUp;

// Helpers used: IndentJson [promoted to internal on DeckComparisonService].
// JsonTextFormatterService is a public static.

/// <summary>
/// Builds a deck-comparison follow-up prompt body formatted for Claude (XML-tagged prompts with result-wrapped output).
/// </summary>
internal sealed class ClaudeFollowUpPromptVariant : IFollowUpPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.Claude;

    /// <summary>
    /// Builds the Claude-targeted follow-up prompt text for the given comparison schema.
    /// Body is a byte-for-byte copy of the pre-refactor BuildFollowUpPromptClaude switch arm (Phase 15-02).
    /// </summary>
    public string Build(string comparisonSchemaJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<role>");
        builder.AppendLine("You are an expert Magic: The Gathering deck analyst specializing in Commander.");
        builder.AppendLine("</role>");
        builder.AppendLine();
        builder.AppendLine("<output_schema>");
        builder.AppendLine("{");
        builder.AppendLine("  \"deck_comparison\": " + DeckComparisonService.IndentJson(comparisonSchemaJson, 2));
        builder.AppendLine("}");
        builder.AppendLine("</output_schema>");
        builder.AppendLine();
        builder.AppendLine("<" + "task>");
        builder.AppendLine("Revise the existing deck comparison using the follow-up questions and answers in this chat.");
        builder.AppendLine("Re-read the original decklists and packet context before revising.");
        builder.AppendLine("Preserve the original comparison structure: readable summary, side-by-side comparison, verdict, then JSON.");
        builder.AppendLine("Incorporate the new follow-up Q&A without contradicting the supplied deck contents or packet context.");
        builder.AppendLine("Keep using the decklists and packet context as the source of truth.");
        builder.AppendLine("Do not invent cards, colors, or card text not supported by the provided context.");
        builder.AppendLine("If you encounter a card name you do not recognize, look it up at https://scryfall.com/search?q=!\"Card Name\" before assuming what it does. Some cards are alternate-art or Universe Beyond printings with unfamiliar names.");
        builder.AppendLine("If a new conclusion is uncertain, mark it as low-confidence and explain why in confidence_notes.");
        builder.AppendLine("For each revised conclusion, reference the deck patterns, card packages, or commander incentives that support it.");
        builder.AppendLine("Return updated readable comparison prose first with 2-4 sentences per axis that changed, then a revised verdict.");
        builder.AppendLine("After the readable revision, return a single JSON object matching <output_schema>.");
        builder.AppendLine("Regenerate the full JSON inside a fenced ```json code block (triple-backtick json) with the top-level object named deck_comparison. Do not return raw JSON outside a code block.");
        builder.AppendLine("</" + "task>");
        return builder.ToString().TrimEnd();
    }
}

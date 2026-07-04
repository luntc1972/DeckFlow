using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.PromptBuilders.FollowUp;

// Helpers used: IndentJson [promoted to internal on DeckComparisonService].
// JsonTextFormatterService is a public static.

/// <summary>
/// Builds a deck-comparison follow-up prompt body formatted for ChatGPT (markdown-headed, fenced JSON output).
/// </summary>
internal sealed class ChatGptFollowUpPromptVariant : IFollowUpPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    public AiPlatform Platform => AiPlatform.ChatGpt;

    /// <summary>
    /// Builds the ChatGPT-targeted follow-up prompt text for the given comparison schema.
    /// Body is a byte-for-byte copy of the pre-refactor BuildFollowUpPromptChatGpt switch arm (Phase 15-02).
    /// </summary>
    public string Build(string comparisonSchemaJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are an expert Magic: The Gathering deck analyst specializing in Commander.");
        builder.AppendLine();
        builder.AppendLine("## TASK");
        builder.AppendLine("Revise the existing deck comparison using the follow-up questions and answers in this chat.");
        builder.AppendLine("Re-read the original decklists and packet context before revising.");
        builder.AppendLine();
        builder.AppendLine("## RULES");
        builder.AppendLine("- Preserve the original comparison structure: readable summary, side-by-side comparison, verdict, then JSON.");
        builder.AppendLine("- Incorporate the new follow-up Q&A without contradicting the supplied deck contents or packet context.");
        builder.AppendLine("- Keep using the decklists and packet context as the source of truth.");
        builder.AppendLine("- Do not invent cards, colors, or card text not supported by the provided context.");
        builder.AppendLine("- If you encounter a card name you do not recognize, treat it as unknown instead of guessing. Do not invent its rules text; flag it as unrecognized. Some cards are alternate-art or Universe Beyond printings with unfamiliar names, so match by exact name before concluding a card is unknown.");
        builder.AppendLine("- If a new conclusion is uncertain, mark it as low-confidence and explain why in confidence_notes.");
        builder.AppendLine("- For each revised conclusion, reference the deck patterns, card packages, or commander incentives that support it.");
        builder.AppendLine();
        builder.AppendLine("## COMPARISON AXES");
        builder.AppendLine("Re-evaluate every axis from the original comparison where the follow-up information is relevant:");
        builder.AppendLine("commander role, game plan, speed, ramp, draw, spot interaction, sweepers, recursion, closing power, resilience, consistency, mana stability, commander dependence, and table fit.");
        builder.AppendLine();
        builder.AppendLine("## OUTPUT FORMAT");
        builder.AppendLine("- Return the updated readable comparison with 2-4 sentences per axis that changed.");
        builder.AppendLine("- Include a revised verdict.");
        builder.AppendLine("- Then regenerate the full JSON inside a fenced ```json code block (triple-backtick json) with the top-level object named deck_comparison. Do not return raw JSON outside a code block.");
        builder.AppendLine("- Keep the JSON valid and include every required field from this schema:");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine("{");
        builder.AppendLine("  \"deck_comparison\": " + DeckComparisonService.IndentJson(comparisonSchemaJson, 2));
        builder.AppendLine("}");
        builder.AppendLine("```");
        return builder.ToString().TrimEnd();
    }
}

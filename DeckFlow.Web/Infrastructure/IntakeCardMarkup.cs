using System.Text.Encodings.Web;

namespace DeckFlow.Web.Infrastructure;

internal static class IntakeCardMarkup
{
    public static string Open(IntakeCardOptions options, HtmlEncoder encoder)
    {
        if (!options.HasResult)
        {
            return "<div class=\"cutlab-intake cutlab-intake--empty\">";
        }

        var label = string.IsNullOrWhiteSpace(options.ResultLabel) ? "Deck input" : options.ResultLabel;
        return $"<details class=\"cutlab-intake\" data-cut-lab-intake-summary><summary class=\"cutlab-intake-summary\"><span class=\"cutlab-intake-summary__commander\">{encoder.Encode(label)}</span><span class=\"cutlab-intake-summary__change\">{encoder.Encode(options.ChangeLabel)}</span></summary>";
    }

    public static string Close(bool hasResult) => hasResult ? "</details>" : "</div>";
}

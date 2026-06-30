using System.Text.Json;
using DeckFlow.Core.Bracket;

namespace DeckFlow.Web.Models;

/// <summary>Selectable Commander bracket option used to describe expected deck power and pace.</summary>
/// <param name="Value">Stable bracket value posted by forms and APIs.</param>
/// <param name="Label">Human-readable bracket label shown in the UI.</param>
/// <param name="Summary">Short description of the bracket's expected deck shape.</param>
/// <param name="TurnsExpectation">Expected turn range for wins or losses in the bracket.</param>
public sealed record CommanderBracketOption(
    string Value,
    string Label,
    string Summary,
    string TurnsExpectation);

/// <summary>
/// Provides the Commander bracket options used by deck analysis prompts.
/// Why: The tier literal was migrated to bracket-data.json per BRACKET-02 so the
/// canonical bracket data lives in a single versioned source (DeckFlow.Web/Data/bracket-data.json),
/// rather than being duplicated between CommanderBracketCatalog.cs and the JSON seed file.
/// This shim reads that same file once via a Lazy static so all 17 flag-independent
/// callers (analysis/primer/set-upgrade prompts) continue to work without DI.
/// </summary>
public static class CommanderBracketCatalog
{
    private static readonly Lazy<IReadOnlyList<CommanderBracketOption>> _options =
        new Lazy<IReadOnlyList<CommanderBracketOption>>(LoadOptions, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>Ordered Commander bracket options from casual exhibition through cEDH.</summary>
    public static IReadOnlyList<CommanderBracketOption> Options => _options.Value;

    /// <summary>Finds a Commander bracket option by its posted value.</summary>
    /// <param name="value">Bracket value to match, ignoring case.</param>
    /// <returns>The matching bracket option, or null when the value is unknown.</returns>
    public static CommanderBracketOption? Find(string? value)
    {
        return Options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns whether the posted bracket value resolves to the canonical cEDH bracket.</summary>
    /// <param name="bracketValue">Bracket value to evaluate.</param>
    /// <returns>True when the value resolves to cEDH; otherwise false.</returns>
    public static bool IsCedh(string? bracketValue)
    {
        return string.Equals(Find(bracketValue)?.Value, "cEDH", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<CommanderBracketOption> LoadOptions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "bracket-data.json");
        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var catalog = JsonSerializer.Deserialize<GameChangerCatalog>(json, opts)
            ?? throw new InvalidOperationException("bracket-data.json could not be deserialized");

        return catalog.Tiers
            .Select(tier => new CommanderBracketOption(
                Value: tier.Name,
                Label: tier.Label,
                Summary: tier.Summary,
                TurnsExpectation: tier.TurnsExpectation))
            .ToList();
    }
}

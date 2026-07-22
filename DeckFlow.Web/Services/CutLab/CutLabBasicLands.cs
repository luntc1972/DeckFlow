using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Known basic-land constants used to materialize added basics without a Scryfall lookup.</summary>
public static class CutLabBasicLands
{
    private static readonly IReadOnlyDictionary<string, Definition> Definitions =
        new Dictionary<string, Definition>(CutLabCardNames.Comparer)
        {
            ["Plains"] = new("Basic Land — Plains", ["W"], ["W"]),
            ["Island"] = new("Basic Land — Island", ["U"], ["U"]),
            ["Swamp"] = new("Basic Land — Swamp", ["B"], ["B"]),
            ["Mountain"] = new("Basic Land — Mountain", ["R"], ["R"]),
            ["Forest"] = new("Basic Land — Forest", ["G"], ["G"]),
            ["Snow-Covered Plains"] = new("Basic Snow Land — Plains", ["W"], ["W"]),
            ["Snow-Covered Island"] = new("Basic Snow Land — Island", ["U"], ["U"]),
            ["Snow-Covered Swamp"] = new("Basic Snow Land — Swamp", ["B"], ["B"]),
            ["Snow-Covered Mountain"] = new("Basic Snow Land — Mountain", ["R"], ["R"]),
            ["Snow-Covered Forest"] = new("Basic Snow Land — Forest", ["G"], ["G"]),
            ["Wastes"] = new("Basic Land", [], ["C"]),
        };

    /// <summary>Resolved metadata for one known added-basic entry.</summary>
    /// <param name="TypeLine">Type line used for role assignment and land checks.</param>
    /// <param name="ColorIdentity">Deckbuilding color identity for the land.</param>
    /// <param name="ProducedMana">Mana letters the land can produce.</param>
    public sealed record Definition(
        string TypeLine,
        IReadOnlyList<string> ColorIdentity,
        IReadOnlyList<string> ProducedMana)
    {
        /// <summary>Always true because every definition in this table is a land.</summary>
        public bool IsLand { get; init; } = true;
    }

    /// <summary>Known basic-land names that can be added without a live lookup.</summary>
    public static IReadOnlyCollection<string> Names { get; } = Definitions.Keys.ToArray();

    /// <summary>True when the provided card name is one of the known basic-land constants.</summary>
    /// <param name="name">Display card name.</param>
    /// <returns><see langword="true"/> when the name resolves in the constants table.</returns>
    public static bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Definitions.ContainsKey(name);
    }

    /// <summary>Attempts to resolve a known basic land into its synthetic metadata row.</summary>
    /// <param name="name">Display card name.</param>
    /// <param name="definition">Resolved definition when found.</param>
    /// <returns><see langword="true"/> when the name is a supported basic-land constant.</returns>
    public static bool TryResolve(string name, out Definition? definition)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Definitions.TryGetValue(name, out definition);
    }

    /// <summary>Builds a synthetic Scryfall land payload for a known added basic.</summary>
    /// <param name="name">Display card name.</param>
    /// <returns>Synthetic Scryfall data with land facts needed by downstream analysis.</returns>
    public static ScryfallCardData SyntheticCardData(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!TryResolve(name, out Definition? definition) || definition is null)
        {
            throw new ArgumentOutOfRangeException(nameof(name), $"Unknown Cut Lab basic land '{name}'.");
        }

        return new ScryfallCardData
        {
            Name = name,
            TypeLine = definition.TypeLine,
            ColorIdentity = definition.ColorIdentity,
            ProducedMana = definition.ProducedMana,
            Cmc = 0,
            Layout = "normal",
            OracleText = BuildOracleText(definition.ProducedMana),
        };
    }

    private static string BuildOracleText(IReadOnlyList<string> producedMana)
        => producedMana.Count == 1
            ? $"{{T}}: Add {{{producedMana[0]}}}."
            : "{T}: Add one mana.";
}

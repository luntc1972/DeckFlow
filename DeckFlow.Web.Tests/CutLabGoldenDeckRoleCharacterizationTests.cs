using System.Text.Json;

using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.CutLab;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Characterizes current role assignment using real Scryfall facts for every exported board.</summary>
public sealed class CutLabGoldenDeckRoleCharacterizationTests
{
    private const string FactsFile = "golden-niv-mizzet-ghost-counsel-facts.json";
    private const string RolesFile = "golden-niv-mizzet-ghost-counsel-roles.json";

    // Why: CutLabRequest.cs:37 defaults PlayExperience to empty; CutLabRoleAssigner.cs:77-89
    // maps that to Casual, as consumed by CutLabAnalysisContextBuilder.cs:222.
    private static readonly ManabaseMode Mode = CutLabRoleAssigner.ResolveMode(new CutLabRequest().PlayExperience);

    // Source: DeckFlow.CLI export-moxfield --url https://moxfield.com/decks/nS1W7itI2EuuZwY0RhbQbw.
    // Facts: Scryfall collection API -> ScryfallCardDataMapper -> ScryfallCardFactMapper,
    // matching ManabaseFlagBaselineHarness.LoadFactsAsync/ResolveAsync. Full export names are retained.
    // Boards metadata comes from MoxfieldApiDeckImporter; CardFactFixtureFile ignores that extra field.
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "CutLab", name);

    private static List<CardFact> LoadFacts() => CardFactFixtureFile.Load(FixturePath(FactsFile));

    private static Dictionary<string, string[]> LoadRoles() =>
        JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(FixturePath(RolesFile)))!;

    private static IReadOnlyList<string> Assign(CardFact fact) =>
        // Why: export carries no crowd categories; keep category signals empty.
        CutLabRoleAssigner.AssignRoles(fact, [], Mode);

    [Fact]
    public void AssignRoles_GoldenDeck_MatchesAllRecordedOrderedRoles()
    {
        List<CardFact> facts = LoadFacts();
        Dictionary<string, string[]> actual = facts.OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToDictionary(f => f.Name, f => Assign(f).ToArray(), StringComparer.Ordinal);

        // Opt in: DECKFLOW_REGENERATE_CUTLAB_GOLDEN_ROLES=1 dotnet test ... --filter FullyQualifiedName~CutLabGoldenDeck
        // Writes the source fixture and output copy; normal runs never write either file.
        if (Environment.GetEnvironmentVariable("DECKFLOW_REGENERATE_CUTLAB_GOLDEN_ROLES") == "1")
        {
            string json = JsonSerializer.Serialize(actual, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
            File.WriteAllText(Path.Combine(RepoPaths.Root(), "DeckFlow.Web.Tests", "Fixtures", "CutLab", RolesFile), json);
            File.WriteAllText(FixturePath(RolesFile), json);
        }

        Dictionary<string, string[]> expected = LoadRoles();
        string[] differences = expected.Keys.Union(actual.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Where(name => !expected.TryGetValue(name, out string[]? recorded)
                || !actual.TryGetValue(name, out string[]? assigned) || !recorded.SequenceEqual(assigned))
            .Select(name => $"{name}: expected [{(expected.TryGetValue(name, out var e) ? string.Join(", ", e) : "<missing>")}]; actual [{(actual.TryGetValue(name, out var a) ? string.Join(", ", a) : "<missing>")}]")
            .ToArray();
        Assert.True(differences.Length == 0, string.Join(Environment.NewLine, differences));
    }

    [Fact]
    public void AssignRoles_GoldenDeck_PinsNonlandOtherSummary()
    {
        List<CardFact> facts = LoadFacts();
        CardFact[] nonlands = facts.Where(f => !CutLabLockRules.IsLand(f.TypeLine) && !f.HasLandFace).ToArray();
        // Why: the assigner emits ["other"], never an empty list, when no role matches.
        string[] other = nonlands.Where(f => Assign(f).SequenceEqual(["other"]))
            .Select(f => f.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(ManabaseMode.Casual, Mode);
        Assert.Equal(129, facts.Count);
        Assert.Equal(106, nonlands.Length);
        Assert.Equal(40, other.Length);
        Assert.Equal<string>(
        [
            "Authority of the Consuls",
            "Bloodchief Ascension",
            "Bofur, Reliable Guardian // Concerted Care",
            "Bubbling Muck",
            "Chain of Smog",
            "Chaplain's Blessing",
            "Confessor",
            "Dauntless Dismantler",
            "Deathgreeter",
            "Delney, Streetwise Lookout",
            "Dr. Pulaski, Cynical Clinician",
            "Drana and Linvala",
            "Elixir of Immortality",
            "Enduring Tenacity",
            "Enlightened Confidant",
            "Feast of Sanity",
            "Imp's Mischief",
            "Kambal, Consul of Allocation",
            "Leonin Elder",
            "Linvala, Keeper of Silence",
            "Lurrus of the Dream-Den",
            "MacCready, Lamplight Mayor",
            "March of Wretched Sorrow",
            "Martyr of Sands",
            "Ocelot Pride",
            "Orim's Chant",
            "Peace of Mind",
            "Reward the Faithful",
            "Serra Ascendant",
            "Sheoldred, the Apocalypse",
            "Sickening Dreams",
            "Silence",
            "Soul Warden",
            "Soul's Attendant",
            "Tendrils of Agony",
            "The Queen of Dale",
            "Tinybones, Pocket Nuisance",
            "Toxic Deluge",
            "Voice of Victory",
            "Zuran Orb"
        ], other);
    }

    [Fact]
    public void Fixtures_GoldenDeck_HaveIdenticalUniqueCardNameSets()
    {
        List<CardFact> facts = LoadFacts();
        Assert.Equal(facts.Count, facts.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(facts.Select(f => f.Name).Order(StringComparer.Ordinal),
            LoadRoles().Keys.Order(StringComparer.Ordinal));
    }
}

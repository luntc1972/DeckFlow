using System.Text.Json;

using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.FeatureFlags;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Evaluates the committed proven-equivalence corpus (D-05) against the production
/// <see cref="CutLabStructuralFindings"/> equivalence predicate. Fails closed if the corpus is not
/// self-contained, bounded, fully labeled, or if the predicate ever produces a false positive
/// against a named negative/abstention case.
/// </summary>
public sealed class CutLabProvenEquivalenceEvaluationTests
{
    private const int MinimumCaseCount = 25;
    private const int MaximumCaseCount = 50;

    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "CutLab", "proven-equivalence-cases.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Corpus_FixtureFile_LoadsAndDeserializes()
    {
        FixtureCorpus corpus = LoadCorpus();

        Assert.NotEmpty(corpus.Cases);
    }

    [Fact]
    public void Corpus_CaseCount_IsBoundedBetween25And50()
    {
        FixtureCorpus corpus = LoadCorpus();

        Assert.InRange(corpus.Cases.Count, MinimumCaseCount, MaximumCaseCount);
    }

    [Fact]
    public void Corpus_EveryCase_HasIdLabelRationaleSourceAndAtLeastTwoCards()
    {
        FixtureCorpus corpus = LoadCorpus();

        foreach (FixtureCase testCase in corpus.Cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(testCase.Id), "case is missing an id");
            Assert.False(string.IsNullOrWhiteSpace(testCase.Rationale), $"case '{testCase.Id}' is missing a rationale");
            Assert.False(string.IsNullOrWhiteSpace(testCase.Source), $"case '{testCase.Id}' is missing a source");
            Assert.True(
                testCase.Label is LabelEquivalent or LabelNotEquivalent or LabelAbstain,
                $"case '{testCase.Id}' has an unrecognized label: '{testCase.Label}'");
            Assert.True(testCase.Cards.Count >= 2, $"case '{testCase.Id}' needs at least two cards to express a relation");
        }

        // Why (WR-12): a copy-paste duplicate id previously passed silently and would make failure
        // messages from every other Fact in this class ambiguous (which of the two same-id cases
        // failed?).
        string[] ids = corpus.Cases.Select(testCase => testCase.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Corpus_PositiveSet_IsNonEmpty()
    {
        FixtureCorpus corpus = LoadCorpus();

        Assert.Contains(corpus.Cases, testCase => testCase.Label == LabelEquivalent);
    }

    [Fact]
    public void Corpus_PositiveSet_ContainsAtLeastOneRealDistinctNameFunctionalReprint()
    {
        FixtureCorpus corpus = LoadCorpus();

        // Why: D-05 requires at least one real (not constructed-only) distinct-name positive.
        // Every positive case in this corpus sources real Scryfall card data, so this asserts the
        // set is non-empty and every member's cards carry distinct, non-synthetic Oracle IDs (a
        // constructed fixture card's OracleId is prefixed "synthetic-" by convention below).
        FixtureCase[] positives = corpus.Cases.Where(testCase => testCase.Label == LabelEquivalent).ToArray();
        Assert.NotEmpty(positives);

        bool hasRealPositive = positives.Any(testCase =>
            testCase.Cards.Count >= 2
            && testCase.Cards.Select(card => card.OracleId).Distinct(StringComparer.Ordinal).Count() == testCase.Cards.Count
            && testCase.Cards.All(card => card.OracleId is { Length: > 0 } oracleId && !oracleId.StartsWith("synthetic-", StringComparison.Ordinal)));

        Assert.True(hasRealPositive, "no positive case has distinct, real (non-synthetic) Oracle IDs across all its cards");
    }

    [Fact]
    public void Corpus_RealOracleIds_MatchScryfallUuidShape()
    {
        // Why (WR-12): "realness" was established only by a "synthetic-" prefix naming convention on
        // constructed fixture cards -- any well-formed but fabricated UUID for a "real" card would
        // pass silently. This doesn't verify the value against live Scryfall data (out of scope for a
        // unit test; re-verifiable by hand from each case's "source" field), but it does catch a
        // malformed or truncated id and documents the shape a real Oracle ID must have.
        FixtureCorpus corpus = LoadCorpus();
        string[] realOracleIds = corpus.Cases
            .SelectMany(testCase => testCase.Cards)
            .Select(card => card.OracleId)
            .Where(oracleId => oracleId is { Length: > 0 } && !oracleId.StartsWith("synthetic-", StringComparison.Ordinal))
            .Cast<string>()
            .ToArray();

        Assert.NotEmpty(realOracleIds);
        Assert.All(realOracleIds, oracleId => Assert.Matches(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            oracleId));
    }

    /// <summary>
    /// Runs the evaluator over the full committed corpus and pins its four counters.
    /// </summary>
    /// <remarks>
    /// WR-01 (honesty note): "Precision == 1.0" and "FalsePositives == 0" here are guaranteed by
    /// construction, not evidence about real-world false-positive risk. Every not-equivalent/abstain
    /// case in this corpus differs from its matched baseline in a field that is itself part of
    /// SemanticKey (Oracle text, mana cost, type line, power, toughness, keywords, color identity, or
    /// role), so there is currently no case where the fingerprint matches but the true label is not
    /// "equivalent" -- what this method actually demonstrates is that every one of those fields is
    /// load-bearing (mutating any single one flips a would-be positive to a negative), not that the
    /// detector achieves 100% precision against realistic near-miss card pairs. If a genuine hard
    /// negative (matching fingerprint, non-equivalent true label) is ever found in real Scryfall data,
    /// it belongs in this corpus and this method's name/counters would then mean what they say.
    /// </remarks>
    [Fact]
    public void Evaluator_AgainstEveryFixtureCase_HasFullRecallAndEveryKeyFieldIsLoadBearing()
    {
        EvaluationResult first = Evaluate(LoadCorpus());
        EvaluationResult second = Evaluate(LoadCorpus());

        // Why: EQUIV-03/EQUIV-04 require deterministic output; running the evaluator twice over an
        // independently re-loaded corpus must produce byte-identical counters.
        Assert.Equal(first, second);

        Assert.True(first.TruePositives > 0, "expected at least one true positive from the labeled corpus");
        Assert.Equal(0, first.FalseNegatives);
        Assert.Equal(0, first.FalsePositives);
        Assert.Equal(1.0, first.Precision);
    }

    [Fact]
    public void FlagKey_MatchesRegisteredCatalogKey()
    {
        Assert.Equal("analysis.cut-lab.proven-equivalence", CutLabStructuralFindings.ProvenEquivalenceFlagKey);
        Assert.Equal("analysis.cut-lab.proven-equivalence", FeatureFlagCatalog.Descriptions.Keys.Single(key => key == CutLabStructuralFindings.ProvenEquivalenceFlagKey));
    }

    /// <summary>Runs the production predicate over one fixture case's cards in isolation.</summary>
    private static CutLabFinding[] RunEquivalenceFindings(FixtureCase testCase)
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = testCase.Cards.Select(ToAnalyzedCard).ToArray();
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            [],
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            comboDataAvailable: true,
            categoryDataAvailable: true,
            provenEquivalenceEnabled: true);

        return result.Findings.Where(finding => finding.Kind == CutLabFindingKind.ProvenEquivalence).ToArray();
    }

    /// <summary>
    /// Whether the production predicate produced a <see cref="CutLabFindingKind.ProvenEquivalence"/>
    /// finding covering every card in the case (recall check for positive-labeled cases).
    /// </summary>
    private static bool PredictsEquivalent(FixtureCase testCase)
    {
        HashSet<string> expectedNames = testCase.Cards.Select(card => card.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return RunEquivalenceFindings(testCase).Any(finding =>
            finding.Evidence.Select(evidence => evidence.CardName).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expectedNames));
    }

    /// <summary>
    /// Whether the production predicate produced ANY <see cref="CutLabFindingKind.ProvenEquivalence"/>
    /// finding over the case's cards (false-positive check for non-positive-labeled cases). Why:
    /// PredictsEquivalent's full-set SetEquals would silently score a proper-subset false positive
    /// on a 3+ card negative case as a correct abstention (WR-03); this checks presence, not shape.
    /// </summary>
    private static bool ProducesAnyEquivalence(FixtureCase testCase) => RunEquivalenceFindings(testCase).Length > 0;

    private static CutLabAnalyzedCard ToAnalyzedCard(FixtureCard card) =>
        new(card.Name, 1, false, card.Roles ?? [], [])
        {
            // Why: the fixture's "name" field is the case's canonical card name (real Scryfall name
            // for real cases, the constructed display name for synthetic cases), so it is also the
            // correct OracleName for self-name redaction -- mirrors how CutLabAnalysisContextBuilder
            // wires the resolved Scryfall Oracle name, never the pool entry's raw decklist string.
            SemanticProfile = new CutLabSemanticProfile(
                card.OracleId,
                card.ManaCost,
                card.TypeLine,
                card.Power,
                card.Toughness,
                card.Keywords,
                card.ColorIdentity,
                card.OracleText,
                card.Layout,
                OracleName: card.Name),
        };

    private static EvaluationResult Evaluate(FixtureCorpus corpus)
    {
        int truePositives = 0;
        int falseNegatives = 0;
        int falsePositives = 0;

        foreach (FixtureCase testCase in corpus.Cases)
        {
            bool expectedEquivalent = testCase.Label == LabelEquivalent;

            // Why (WR-03): positive cases are scored on exact-set recall (PredictsEquivalent);
            // non-positive cases are scored on presence of ANY equivalence finding
            // (ProducesAnyEquivalence), so a multi-card negative where the detector matches a proper
            // subset still counts as a false positive instead of silently passing as an abstention.
            if (expectedEquivalent)
            {
                if (PredictsEquivalent(testCase))
                {
                    truePositives++;
                }
                else
                {
                    falseNegatives++;
                }
            }
            else if (ProducesAnyEquivalence(testCase))
            {
                falsePositives++;
            }
        }

        double precision = truePositives + falsePositives == 0
            ? 0.0
            : (double)truePositives / (truePositives + falsePositives);

        return new EvaluationResult(truePositives, falseNegatives, falsePositives, precision);
    }

    private static FixtureCorpus LoadCorpus()
    {
        string json = File.ReadAllText(FixturePath);
        FixtureCorpus? corpus = JsonSerializer.Deserialize<FixtureCorpus>(json, SerializerOptions);
        Assert.NotNull(corpus);
        return corpus!;
    }

    private sealed record EvaluationResult(int TruePositives, int FalseNegatives, int FalsePositives, double Precision);

    private const string LabelEquivalent = "equivalent";
    private const string LabelNotEquivalent = "not-equivalent";
    private const string LabelAbstain = "abstain";

    private sealed record FixtureCorpus(IReadOnlyList<FixtureCase> Cases);

    private sealed record FixtureCase(string Id, string Label, string Rationale, string Source, IReadOnlyList<FixtureCard> Cards);

    private sealed record FixtureCard(
        string Name,
        string? OracleId,
        string? ManaCost,
        string? TypeLine,
        string? Power,
        string? Toughness,
        IReadOnlyList<string>? Keywords,
        IReadOnlyList<string>? ColorIdentity,
        string? OracleText,
        string? Layout,
        IReadOnlyList<string>? Roles);
}

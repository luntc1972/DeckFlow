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
    public void Evaluator_AgainstEveryFixtureCase_HasZeroFalsePositivesAndFullRecallAtPrecision1()
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

    /// <summary>
    /// Runs the production predicate over one fixture case's cards in isolation and returns whether
    /// it produced a <see cref="CutLabFindingKind.ProvenEquivalence"/> finding covering every card in
    /// the case.
    /// </summary>
    private static bool PredictsEquivalent(FixtureCase testCase)
    {
        IReadOnlyList<CutLabAnalyzedCard> pool = testCase.Cards.Select(ToAnalyzedCard).ToArray();
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            [],
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            comboDataAvailable: true,
            categoryDataAvailable: true,
            provenEquivalenceEnabled: true);

        CutLabFinding[] equivalenceFindings = result.Findings.Where(finding => finding.Kind == CutLabFindingKind.ProvenEquivalence).ToArray();
        HashSet<string> expectedNames = testCase.Cards.Select(card => card.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return equivalenceFindings.Any(finding =>
            finding.Evidence.Select(evidence => evidence.CardName).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expectedNames));
    }

    private static CutLabAnalyzedCard ToAnalyzedCard(FixtureCard card) =>
        new(card.Name, 1, false, card.Roles ?? [], [])
        {
            SemanticProfile = new CutLabSemanticProfile(
                card.OracleId,
                card.ManaCost,
                card.TypeLine,
                card.Power,
                card.Toughness,
                card.Keywords,
                card.ColorIdentity,
                card.OracleText,
                card.Layout),
        };

    private static EvaluationResult Evaluate(FixtureCorpus corpus)
    {
        int truePositives = 0;
        int falseNegatives = 0;
        int falsePositives = 0;

        foreach (FixtureCase testCase in corpus.Cases)
        {
            bool predictedEquivalent = PredictsEquivalent(testCase);
            bool expectedEquivalent = testCase.Label == LabelEquivalent;

            if (expectedEquivalent && predictedEquivalent)
            {
                truePositives++;
            }
            else if (expectedEquivalent && !predictedEquivalent)
            {
                falseNegatives++;
            }
            else if (!expectedEquivalent && predictedEquivalent)
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

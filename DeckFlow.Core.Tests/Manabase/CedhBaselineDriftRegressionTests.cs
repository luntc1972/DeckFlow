using System.Text.Json;
using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests.Manabase;

/// <summary>
/// Pins the drift guard against the 2026-07-27 corruption incident, where a double-faced-card
/// resolution bug dropped 208 card names and produced a snapshot that under-counted roughly
/// 1.9 lands per deck while the pipeline reported success.
/// </summary>
/// <remarks>
/// These fixtures are reconstructions from the recorded incident measurements, not raw pipeline
/// output; the corrupt artifact was overwritten before it could be preserved. The point of this
/// test is that any future widening of the thresholds must still reject the corrupt candidate.
/// </remarks>
public sealed class CedhBaselineDriftRegressionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "cedh-drift", name);

    private static CedhLandBaselineSnapshot LoadSnapshot(string name) =>
        JsonSerializer.Deserialize<CedhLandBaselineSnapshot>(File.ReadAllText(FixturePath(name)), JsonOptions)
        ?? throw new InvalidOperationException($"Fixture {name} did not deserialize.");

    private static CedhDriftThresholds LoadCommittedThresholds()
    {
        string path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "scripts", "cedh-baseline", "drift-thresholds.json"));
        return CedhDriftThresholds.FromJson(File.ReadAllText(path));
    }

    [Fact]
    public void CommittedThresholds_RejectTheJuly2026CorruptSnapshot()
    {
        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(
            LoadSnapshot("previous-2026-07-11.json"),
            LoadSnapshot("candidate-corrupt.json"),
            LoadCommittedThresholds());

        Assert.False(
            verdict.Passed,
            "The committed thresholds must still reject the July 2026 corruption. If this fails, a "
            + "threshold was widened past the incident it was calibrated against.");
        Assert.Contains(verdict.Findings, f => f.Rule == "DroppedEstablishedCommander");
        Assert.Contains(verdict.Findings, f => f.Rule == "SampleCollapse");
        Assert.Contains(verdict.Findings, f => f.Rule == "OneSidedDrift");
    }

    [Fact]
    public void CommittedThresholds_AcceptTheCorrectedSnapshot()
    {
        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(
            LoadSnapshot("previous-2026-07-11.json"),
            LoadSnapshot("candidate-corrected.json"),
            LoadCommittedThresholds());

        Assert.True(
            verdict.Passed,
            "The corrected July 2026 refresh must pass. If this fails, a threshold is too tight and "
            + "will reject legitimate monthly refreshes.");
    }
}

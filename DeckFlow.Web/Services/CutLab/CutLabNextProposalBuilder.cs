using DeckFlow.Web.Models.Api;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds the server-authored DTO for the current Cut Lab proposal.</summary>
internal static class CutLabNextProposalBuilder
{
    internal static CutLabDecideNextProposalDto Build(CutLabRoundPlan roundPlan, CutLabStructuralFindingsResult findings)
    {
        if (roundPlan.NextProposal is null)
        {
            return new CutLabDecideNextProposalDto
            {
                IsTerminal = true,
                IsAtTarget = roundPlan.CardsRemainingToTarget == 0,
                IsNothingToCut = roundPlan.CardsRemainingToTarget > 0,
            };
        }

        string normalizedProposalCardName = CutLabCardNames.Normalize(roundPlan.NextProposal.CardName);
        string[] chips = findings.Findings
            .Where(finding => finding.Evidence.Any(evidence =>
                CutLabCardNames.Comparer.Equals(
                    CutLabCardNames.Normalize(evidence.CardName),
                    normalizedProposalCardName)))
            .Select(finding => finding.Heading)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CutLabDecideNextProposalDto
        {
            CardName = roundPlan.NextProposal.CardName,
            RoundKey = roundPlan.NextProposal.RoundKey,
            RoundLabel = roundPlan.NextProposal.RoundLabel,
            RoundBannerBody = CutLabCutRoundEngine.RoundBannerBodyFor(roundPlan.NextProposal.RoundKey),
            FindingCount = roundPlan.NextProposal.FindingCount,
            FindingChips = chips,
        };
    }
}

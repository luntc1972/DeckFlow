using System.Reflection;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabRemainderReachabilityTests
{
    [Fact]
    public void BuildRemainderReachability_SeededSmallPools_MatchesOriginalSubsetSum()
    {
        Random random = new(261110);
        for (int sample = 0; sample < 500; sample++)
        {
            int target = random.Next(0, 25);
            CutLabRoundInputCard[] cards = Enumerable.Range(0, random.Next(0, 12))
                .Select(index => new CutLabRoundInputCard($"Card {index}", random.Next(0, 10), "", false, false, 0, false, [], []))
                .ToArray();
            bool[] actual = (bool[])typeof(CutLabCutRoundEngine)
                .GetMethod("BuildRemainderReachability", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [cards, target])!;
            for (int excluded = 0; excluded < cards.Length; excluded++)
            {
                Assert.Equal(OriginalSubsetSum(cards, excluded, target), actual[excluded]);
            }
        }
    }

    private static bool OriginalSubsetSum(IReadOnlyList<CutLabRoundInputCard> cards, int excluded, int target)
    {
        int remainder = target - cards[excluded].Quantity;
        if (remainder < 0)
        {
            return false;
        }

        bool[] reachable = new bool[remainder + 1];
        reachable[0] = true;
        for (int index = 0; index < cards.Count; index++)
        {
            if (index == excluded)
            {
                continue;
            }

            for (int sum = remainder; sum >= cards[index].Quantity; sum--)
            {
                reachable[sum] |= reachable[sum - cards[index].Quantity];
            }
        }

        return reachable[remainder];
    }
}

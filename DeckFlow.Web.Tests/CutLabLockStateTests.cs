using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for immutable Cut Lab lock-state mutations and the commander-lock invariant.</summary>
public sealed class CutLabLockStateTests
{
    [Fact]
    public void EnforceCommanderLock_CommanderSubmittedUnlocked_ForcesCommanderLocked()
    {
        var state = CreateState(
            new CutLabPoolCard { Name = "Atraxa, Praetors' Voice", Quantity = 1, IsCommander = true, IsLocked = false },
            new CutLabPoolCard { Name = "Swords to Plowshares", Quantity = 1, IsLocked = false });

        var result = CutLabLockRules.EnforceCommanderLock(state);

        Assert.True(result.Pool.Single(card => card.IsCommander).IsLocked);
        Assert.False(result.Pool.Single(card => !card.IsCommander).IsLocked);
    }

    [Fact]
    public void EnforceCommanderLock_CommanderAlreadyLocked_IsIdempotent()
    {
        var state = CreateState(
            new CutLabPoolCard { Name = "Atraxa, Praetors' Voice", Quantity = 1, IsCommander = true, IsLocked = true },
            new CutLabPoolCard { Name = "Swords to Plowshares", Quantity = 1, IsLocked = true });

        var once = CutLabLockRules.EnforceCommanderLock(state);
        var twice = CutLabLockRules.EnforceCommanderLock(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void LockCard_MatchingCard_SetsLockTrue()
    {
        var state = CreateState(
            new CutLabPoolCard { Name = "Arcane Signet", Quantity = 1, IsLocked = false },
            new CutLabPoolCard { Name = "Cultivate", Quantity = 1, IsLocked = false });

        var result = CutLabLockRules.LockCard(state, "arcane signet");

        Assert.True(result.Pool.Single(card => card.Name == "Arcane Signet").IsLocked);
        Assert.False(result.Pool.Single(card => card.Name == "Cultivate").IsLocked);
    }

    [Fact]
    public void UnlockCard_NonCommanderCard_SetsLockFalse()
    {
        var state = CreateState(
            new CutLabPoolCard { Name = "Atraxa, Praetors' Voice", Quantity = 1, IsCommander = true, IsLocked = true },
            new CutLabPoolCard { Name = "Cultivate", Quantity = 1, IsLocked = true });

        var result = CutLabLockRules.UnlockCard(state, "Cultivate");

        Assert.True(result.Pool.Single(card => card.IsCommander).IsLocked);
        Assert.False(result.Pool.Single(card => card.Name == "Cultivate").IsLocked);
    }

    [Fact]
    public void UnlockCard_CommanderCard_LeavesCommanderLocked()
    {
        var state = CreateState(
            new CutLabPoolCard { Name = "Atraxa, Praetors' Voice", Quantity = 1, IsCommander = true, IsLocked = true },
            new CutLabPoolCard { Name = "Cultivate", Quantity = 1, IsLocked = true });

        var result = CutLabLockRules.UnlockCard(state, "atraxa, praetors' voice");

        Assert.True(result.Pool.Single(card => card.IsCommander).IsLocked);
    }

    [Fact]
    public void LockPackage_MatchingPackage_LocksPackageAndMemberCards()
    {
        var state = CreateState(
            [
                new CutLabPoolCard { Name = "Sol Ring", Quantity = 1, PackageId = "ramp", IsLocked = false },
                new CutLabPoolCard { Name = "Cultivate", Quantity = 1, PackageId = "ramp", IsLocked = false },
                new CutLabPoolCard { Name = "Counterspell", Quantity = 1, PackageId = "interaction", IsLocked = false },
            ],
            [
                new CutLabPackage { Id = "ramp", Name = "Ramp Core", Locked = false },
                new CutLabPackage { Id = "interaction", Name = "Interaction", Locked = false },
            ]);

        var result = CutLabLockRules.LockPackage(state, "RAMP");

        Assert.True(result.Packages.Single(package => package.Id == "ramp").Locked);
        Assert.True(result.Pool.Single(card => card.Name == "Sol Ring").IsLocked);
        Assert.True(result.Pool.Single(card => card.Name == "Cultivate").IsLocked);
        Assert.False(result.Pool.Single(card => card.Name == "Counterspell").IsLocked);
    }

    [Fact]
    public void UnlockPackage_CommanderInPackage_PreservesCommanderLockWhileUnlockingOtherMembers()
    {
        var state = CreateState(
            [
                new CutLabPoolCard { Name = "Atraxa, Praetors' Voice", Quantity = 1, PackageId = "engine", IsCommander = true, IsLocked = true },
                new CutLabPoolCard { Name = "Deepglow Skate", Quantity = 1, PackageId = "engine", IsLocked = true },
                new CutLabPoolCard { Name = "Swords to Plowshares", Quantity = 1, PackageId = "interaction", IsLocked = true },
            ],
            [
                new CutLabPackage { Id = "engine", Name = "Counters Engine", Locked = true },
                new CutLabPackage { Id = "interaction", Name = "Interaction", Locked = true },
            ]);

        var result = CutLabLockRules.UnlockPackage(state, "engine");

        Assert.False(result.Packages.Single(package => package.Id == "engine").Locked);
        Assert.True(result.Pool.Single(card => card.IsCommander).IsLocked);
        Assert.False(result.Pool.Single(card => card.Name == "Deepglow Skate").IsLocked);
        Assert.True(result.Pool.Single(card => card.Name == "Swords to Plowshares").IsLocked);
    }

    private static CutLabState CreateState(params CutLabPoolCard[] pool)
        => CreateState(pool, []);

    private static CutLabState CreateState(IReadOnlyList<CutLabPoolCard> pool, IReadOnlyList<CutLabPackage> packages)
        => new()
        {
            Commander = pool.FirstOrDefault(card => card.IsCommander)?.Name ?? string.Empty,
            Pool = pool,
            Packages = packages,
            Intent = new CutLabIntent { PrimaryPlan = "Win with value" },
        };
}

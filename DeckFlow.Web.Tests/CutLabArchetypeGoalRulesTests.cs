using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for archetype-seeded Cut Lab goal rules.</summary>
public sealed class CutLabArchetypeGoalRulesTests
{
    [Fact]
    public void ApplyArchetype_FromGlobalDefaults_Replaces()
    {
        var result = CutLabArchetypeGoalRules.Apply(new CutLabGoalSettings(), null, "stax");

        Assert.Equal(CutLabArchetypeCatalog.Entries.Single(entry => entry.Slug == "stax").DefaultGoals, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Replaced, result.Outcome);
    }

    [Fact]
    public void ApplyArchetype_FromOtherArchetypeDefaults_Replaces()
    {
        var current = CutLabArchetypeCatalog.Entries.Single(entry => entry.Slug == "stax").DefaultGoals;

        var result = CutLabArchetypeGoalRules.Apply(current, "stax", "control");

        Assert.Equal(CutLabArchetypeCatalog.Entries.Single(entry => entry.Slug == "control").DefaultGoals, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Replaced, result.Outcome);
    }

    [Fact]
    public void ApplyArchetype_CustomGoals_KeptAndReported()
    {
        var current = new CutLabGoalSettings { CommanderByTurn = 9 };

        var result = CutLabArchetypeGoalRules.Apply(current, "stax", "control");

        Assert.Equal(current, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Kept, result.Outcome);
    }

    [Fact]
    public void ApplyArchetype_SameArchetype_Unchanged()
    {
        var current = new CutLabGoalSettings { CommanderByTurn = 9 };

        var result = CutLabArchetypeGoalRules.Apply(current, "stax", "stax");

        Assert.Equal(current, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Unchanged, result.Outcome);
    }

    [Fact]
    public void ApplyArchetype_Cleared_Unchanged()
    {
        var current = new CutLabGoalSettings { CommanderByTurn = 9 };

        var result = CutLabArchetypeGoalRules.Apply(current, "stax", null);

        Assert.Equal(current, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Unchanged, result.Outcome);
    }

    [Fact]
    public void ApplyArchetype_ChainReturnsToStart_RestoresChainStartGoals()
    {
        var chainStartGoals = new CutLabGoalSettings();
        var intermediateGoals = CutLabArchetypeCatalog.Entries.Single(entry => entry.Slug == "stax").DefaultGoals;

        var result = CutLabArchetypeGoalRules.Apply(intermediateGoals, "stax", null, null, chainStartGoals);

        Assert.Equal(chainStartGoals, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Unchanged, result.Outcome);
    }

    [Fact]
    public void ApplyArchetype_CoalescedReturnToIntermediateArchetype_ReportsNetReplacement()
    {
        var chainStartGoals = new CutLabGoalSettings();
        var staxGoals = CutLabArchetypeCatalog.Entries.Single(entry => entry.Slug == "stax").DefaultGoals;

        var result = CutLabArchetypeGoalRules.Apply(staxGoals, "stax", "stax", null, chainStartGoals);

        Assert.Equal(staxGoals, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Replaced, result.Outcome);
    }

    [Fact]
    public void ApplyArchetype_CoalescedReturnToIntermediateArchetypeWithCustomChainStartGoals_KeepsGoals()
    {
        var chainStartGoals = new CutLabGoalSettings { CommanderByTurn = 9 };
        var staxGoals = CutLabArchetypeCatalog.Entries.Single(entry => entry.Slug == "stax").DefaultGoals;

        var result = CutLabArchetypeGoalRules.Apply(staxGoals, "stax", "stax", null, chainStartGoals);

        Assert.Equal(chainStartGoals, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Kept, result.Outcome);
    }

    [Fact]
    public void ApplyArchetype_PriorUnknownSlug_TreatedAsNull()
    {
        var result = CutLabArchetypeGoalRules.Apply(new CutLabGoalSettings(), "not-real", "control");

        Assert.Equal(CutLabArchetypeCatalog.Entries.Single(entry => entry.Slug == "control").DefaultGoals, result.Goals);
        Assert.Equal(CutLabArchetypeGoalOutcome.Replaced, result.Outcome);
    }
}

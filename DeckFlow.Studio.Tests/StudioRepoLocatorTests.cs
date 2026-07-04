using System;
using System.IO;
using DeckFlow.Studio.Services;
using Xunit;

namespace DeckFlow.Studio.Tests;

public sealed class StudioRepoLocatorTests
{
    [Fact]
    public void ResolveStartDirectory_EnvSet_ReturnsTrimmedOverride()
    {
        var result = StudioRepoLocator.ResolveStartDirectory("  C:\\repos\\deckflow  ");
        Assert.Equal("C:\\repos\\deckflow", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveStartDirectory_BlankOrNull_FallsBackToCurrentDirectory(string? value)
    {
        var result = StudioRepoLocator.ResolveStartDirectory(value);
        Assert.Equal(Directory.GetCurrentDirectory(), result);
    }

    [Fact]
    public void ResolveStartDirectory_ReadsEnvironmentVariable()
    {
        // Proves the public overload reads DECKFLOW_REPO_ROOT. Save/restore so the process
        // env is left as found — no other test reads this variable.
        var original = Environment.GetEnvironmentVariable(StudioRepoLocator.RepoRootEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(StudioRepoLocator.RepoRootEnvironmentVariable, "D:\\some\\repo");
            Assert.Equal("D:\\some\\repo", StudioRepoLocator.ResolveStartDirectory());

            Environment.SetEnvironmentVariable(StudioRepoLocator.RepoRootEnvironmentVariable, null);
            Assert.Equal(Directory.GetCurrentDirectory(), StudioRepoLocator.ResolveStartDirectory());
        }
        finally
        {
            Environment.SetEnvironmentVariable(StudioRepoLocator.RepoRootEnvironmentVariable, original);
        }
    }
}

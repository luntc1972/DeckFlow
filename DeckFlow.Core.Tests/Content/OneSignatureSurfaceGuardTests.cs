using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Core.Tests.Content;

/// <summary>
/// Regression tripwire for the SYNC-02 invariant (D-03): exactly one row-content signature
/// surface — <c>ContentSiteIndexContentSignature</c> — may exist under
/// <c>DeckFlow.Core/Content</c>. Phase 89-03 deleted the divergent
/// <c>ContentSyncDiffClassifier.Fingerprint</c> subset scheme; this test scans the source tree
/// (not just the compiled assembly) so a future edit that reintroduces a second
/// signature/fingerprint-style method fails loudly instead of silently drifting DirectPush, Pull,
/// and reconcile back apart.
/// </summary>
public sealed class OneSignatureSurfaceGuardTests
{
    // Matches a static method DEFINITION line: leading whitespace, an access modifier, "static",
    // a return type token, the method name, then "(". Anchored to line-start (Multiline) so call
    // sites (e.g. "ContentNaturalKey.TryDerive(...)") never match — only declarations do.
    private static readonly Regex MethodDefinitionPattern = new(
        @"^\s*(?:public|private|internal|protected)\s+static\s+\S+\s+(\w+)\s*\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void Fingerprint_IsNotDefinedAnywhereInContentDirectory()
    {
        var contentDirectory = GetContentDirectory();

        foreach (var file in Directory.EnumerateFiles(contentDirectory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var methodNames = FindStaticMethodDefinitionNames(File.ReadAllText(file));

            Assert.DoesNotContain(
                "Fingerprint",
                methodNames);
        }
    }

    [Fact]
    public void BuildSignature_IsDefinedInExactlyOneFile()
    {
        var contentDirectory = GetContentDirectory();

        var filesDefiningBuildSignature = Directory
            .EnumerateFiles(contentDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(file => FindStaticMethodDefinitionNames(File.ReadAllText(file)).Contains("BuildSignature"))
            .Select(Path.GetFileName)
            .ToList();

        // SYNC-02 (D-03): BuildSignature is the one canonical signature-building surface. If a
        // second file starts defining its own BuildSignature (or any future rename that collides
        // on this name), the "one signature, one home" invariant has silently regressed.
        var single = Assert.Single(filesDefiningBuildSignature);
        Assert.Equal("ContentSiteIndexContentSignature.cs", single);
    }

    private static IReadOnlyCollection<string> FindStaticMethodDefinitionNames(string source)
        => MethodDefinitionPattern
            .Matches(source)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string GetContentDirectory()
    {
        var repoRoot = GetRepoRoot();
        var contentDirectory = Path.Combine(repoRoot, "DeckFlow.Core", "Content");

        if (!Directory.Exists(contentDirectory))
        {
            throw new InvalidOperationException($"Could not locate DeckFlow.Core/Content at '{contentDirectory}'.");
        }

        return contentDirectory;
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".editorconfig"))
                && File.Exists(Path.Combine(directory.FullName, "DeckFlow.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the current test base directory.");
    }
}

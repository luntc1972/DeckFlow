using System;
using System.Collections.Generic;
using System.IO;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbArtifactPathResolver"/> ordered-candidate base resolution
/// (D-22B): config override → ContentRootPath → ContentRootPath/.. → CWD, picking the first
/// candidate whose <c>content-kb</c> subdirectory exists.
/// </summary>
public sealed class ContentKbArtifactPathResolverTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    [Fact]
    public void ContentBase_PrefersConfigOverride_WhenContentKbExistsThere()
    {
        var configBase = CreateTempWithContentKb();
        var contentRoot = CreateTempWithContentKb(); // also valid, but config wins by order

        var resolver = Build(contentRoot, new() { ["ContentKb:ContentBase"] = configBase });

        Assert.Equal(Path.GetFullPath(configBase), resolver.ContentBase);
    }

    [Fact]
    public void ContentBase_UsesContentRootPath_WhenNoConfigAndContentKbThere()
    {
        var contentRoot = CreateTempWithContentKb();

        var resolver = Build(contentRoot, new());

        Assert.Equal(Path.GetFullPath(contentRoot), resolver.ContentBase);
    }

    [Fact]
    public void ContentBase_FallsBackToParent_WhenContentKbOnlyInParent()
    {
        var parent = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(parent, "content-kb"));
        var child = Path.Combine(parent, "web");
        Directory.CreateDirectory(child);

        var resolver = Build(child, new());

        Assert.Equal(Path.GetFullPath(parent), resolver.ContentBase);
    }

    [Fact]
    public void ContentBase_FallsBackToContentRootPath_WhenNoCandidateHasContentKb()
    {
        var contentRoot = CreateTempDir(); // no content-kb anywhere up the chain

        var resolver = Build(contentRoot, new());

        Assert.Equal(Path.GetFullPath(contentRoot), resolver.ContentBase);
    }

    [Fact]
    public void SeedFilePath_IsContentKbSeedIndexJson_UnderBase()
    {
        var contentRoot = CreateTempWithContentKb();
        var resolver = Build(contentRoot, new());

        Assert.Equal(
            Path.Combine(Path.GetFullPath(contentRoot), "content-kb", "seed", "index-seed.json"),
            resolver.SeedFilePath);
    }

    [Fact]
    public void ResolveArtifactFullPath_CombinesBaseWithStoredPath()
    {
        var contentRoot = CreateTempWithContentKb();
        var resolver = Build(contentRoot, new());

        var resolved = resolver.ResolveArtifactFullPath("content-kb/edhrecast/abc.md");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(contentRoot, "content-kb", "edhrecast", "abc.md")),
            resolved);
    }

    private ContentKbArtifactPathResolver Build(string contentRootPath, Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var environment = new StubWebHostEnvironment(contentRootPath);
        return new ContentKbArtifactPathResolver(
            environment,
            configuration,
            NullLogger<ContentKbArtifactPathResolver>.Instance);
    }

    private string CreateTempWithContentKb()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "content-kb"));
        return dir;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kbres-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
            WebRootPath = contentRootPath;
            WebRootFileProvider = new NullFileProvider();
        }

        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}

using System;
using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Studio;
using Xunit;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Covers <see cref="AutoApproveSettingsStore"/>: defaults, persistence across a fresh instance
/// (D-07), corrupt-file safety, and semantic clamping of a parsed-but-invalid cutoff (T-59-03).
/// </summary>
public sealed class AutoApproveSettingsStoreTests : IDisposable
{
    private readonly string _dir;

    public AutoApproveSettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "deckflow-autoapprove-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // best-effort temp cleanup
        }
    }

    [Fact]
    public void Default_returns_enabled_with_default_cutoff()
    {
        var settings = AutoApproveSettings.Default;

        Assert.True(settings.Enabled);
        Assert.Equal(ClipCountAutoApproveSignal.DefaultCutoff, settings.Cutoff);
        Assert.Equal(5, settings.Cutoff);
    }

    [Fact]
    public void Load_returns_default_when_file_absent()
    {
        var store = new AutoApproveSettingsStore(_dir);

        var settings = store.Load();

        Assert.True(settings.Enabled);
        Assert.Equal(5, settings.Cutoff);
    }

    [Fact]
    public void Save_then_fresh_store_reads_back_values()
    {
        new AutoApproveSettingsStore(_dir).Save(new AutoApproveSettings(false, 7));

        // A NEW instance over the same directory simulates a Studio restart (D-07).
        var reloaded = new AutoApproveSettingsStore(_dir).Load();

        Assert.False(reloaded.Enabled);
        Assert.Equal(7, reloaded.Cutoff);
    }

    [Fact]
    public void Save_writes_named_json_file_in_data_directory()
    {
        new AutoApproveSettingsStore(_dir).Save(new AutoApproveSettings(true, 5));

        Assert.True(File.Exists(Path.Combine(_dir, "auto-approve-settings.json")));
    }

    [Fact]
    public void Load_returns_default_on_corrupt_file_without_throwing()
    {
        File.WriteAllText(Path.Combine(_dir, "auto-approve-settings.json"), "{ this is not valid json");

        var settings = new AutoApproveSettingsStore(_dir).Load();

        Assert.True(settings.Enabled);
        Assert.Equal(5, settings.Cutoff);
    }

    [Fact]
    public void Load_clamps_negative_cutoff_to_default()
    {
        // JSON parses fine; only the value is semantically bad (T-59-03 / Codex MEDIUM).
        File.WriteAllText(
            Path.Combine(_dir, "auto-approve-settings.json"),
            "{\"Enabled\":true,\"Cutoff\":-3}");

        var settings = new AutoApproveSettingsStore(_dir).Load();

        Assert.Equal(ClipCountAutoApproveSignal.DefaultCutoff, settings.Cutoff);
    }

    [Fact]
    public void Load_clamps_absurdly_high_cutoff_to_max()
    {
        File.WriteAllText(
            Path.Combine(_dir, "auto-approve-settings.json"),
            "{\"Enabled\":true,\"Cutoff\":100000}");

        var settings = new AutoApproveSettingsStore(_dir).Load();

        Assert.Equal(AutoApproveSettingsStore.MaxCutoff, settings.Cutoff);
    }

    [Fact]
    public void Save_clamps_negative_cutoff_before_it_reaches_disk()
    {
        new AutoApproveSettingsStore(_dir).Save(new AutoApproveSettings(true, -10));

        var reloaded = new AutoApproveSettingsStore(_dir).Load();

        Assert.Equal(ClipCountAutoApproveSignal.DefaultCutoff, reloaded.Cutoff);
    }
}

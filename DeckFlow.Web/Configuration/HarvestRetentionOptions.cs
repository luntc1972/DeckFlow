using System.ComponentModel.DataAnnotations;
using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeckFlow.Web.Configuration;

/// <summary>Configures automatic post-harvest retention for OQ6, D2, and D3.</summary>
public sealed class HarvestRetentionOptions
{
    private static readonly DeckPruningOptions Defaults = new();

    /// <summary>Configuration section name for OQ6 retention settings.</summary>
    public const string SectionName = "HarvestRetention";

    /// <summary>Gets or sets the D2 per-commander cap from <see cref="DeckPruningOptions.PerCommanderCap"/>.</summary>
    [Range(1, int.MaxValue)]
    public int PerCommanderCap { get; set; } = Defaults.PerCommanderCap;

    /// <summary>Gets or sets the OQ6 batch size from <see cref="DeckPruningOptions.BatchSize"/>.</summary>
    [Range(1, int.MaxValue)]
    public int BatchSize { get; set; } = Defaults.BatchSize;

    /// <summary>Gets or sets the OQ6 pause from <see cref="DeckPruningOptions.Pause"/> in milliseconds.</summary>
    [Range(0, int.MaxValue)]
    public int PauseMilliseconds { get; set; } = (int)Defaults.Pause.TotalMilliseconds;

    /// <summary>Gets or sets the D2 disk floor from <see cref="DeckPruningOptions.MinFreeDiskBytes"/>.</summary>
    [Range(typeof(long), "0", "9223372036854775807")]
    public long MinFreeDiskBytes { get; set; } = Defaults.MinFreeDiskBytes;

    /// <summary>Gets or sets the D3 maximum age in days, when age retention is enabled.</summary>
    // Why: values above this can pass validation but cannot be represented by TimeSpan.FromDays.
    [Range(1, 10_675_199)]
    public int? MaxAgeDays { get; set; }

    /// <summary>Gets or sets the OQ6 maximum removals after one harvest from <see cref="DeckPruningOptions.DefaultPostHarvestMaxDecks"/>.</summary>
    [Range(0, int.MaxValue)]
    public int PostHarvestMaxDecks { get; set; } = DeckPruningOptions.DefaultPostHarvestMaxDecks;

    /// <summary>Converts bound web settings to validated core pruning settings.</summary>
    public DeckPruningOptions ToPruningOptions()
        => new(PerCommanderCap, BatchSize, TimeSpan.FromMilliseconds(PauseMilliseconds), MinFreeDiskBytes, MaxAgeDays is null ? null : TimeSpan.FromDays(MaxAgeDays.Value));

    /// <summary>Adds and validates OQ6 retention configuration.</summary>
    public static OptionsBuilder<HarvestRetentionOptions> AddTo(IServiceCollection services, IConfiguration configuration)
        // Why: one binding chain keeps startup and tests from drifting.
        => services.AddOptions<HarvestRetentionOptions>().Bind(configuration.GetSection(SectionName)).ValidateDataAnnotations().ValidateOnStart();
}

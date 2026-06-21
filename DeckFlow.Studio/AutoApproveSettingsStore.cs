using System.Text.Json;
using DeckFlow.Core.Content;

namespace DeckFlow.Studio;

/// <summary>
/// File-backed persistence for <see cref="AutoApproveSettings"/> (D-07). Reads/writes a single
/// JSON file in the studio data directory so the on/off + cutoff survive Studio restarts — unlike
/// the session-only <see cref="SessionCapOverride"/>. Single-operator local tool: no locking
/// beyond a straightforward write. NEVER stores secrets — the two persisted values are
/// non-sensitive scalars only (T-59-04).
/// </summary>
public sealed class AutoApproveSettingsStore
{
    /// <summary>
    /// Upper bound for a persisted cutoff. A stored value above this is clamped down on load/save —
    /// a parsed-but-absurd cutoff is not trusted (T-59-03 / Codex MEDIUM).
    /// </summary>
    public const int MaxCutoff = 1000;

    private const string SettingsFileName = "auto-approve-settings.json";

    private readonly string _settingsFilePath;

    /// <summary>
    /// Creates the store over the supplied studio data directory; the settings file is
    /// <c>auto-approve-settings.json</c> within it.
    /// </summary>
    /// <param name="studioDataDirectory">The studio data directory (same dir as <c>content-kb.db</c>).</param>
    public AutoApproveSettingsStore(string studioDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studioDataDirectory);
        _settingsFilePath = Path.Combine(studioDataDirectory, SettingsFileName);
    }

    /// <summary>
    /// Loads the persisted settings, returning <see cref="AutoApproveSettings.Default"/> when the
    /// file is missing or unparseable (T-59-03 — never throws to the UI). A parsed-but-invalid
    /// cutoff is clamped via <see cref="Sanitize"/> (negative → default cutoff, &gt; <see cref="MaxCutoff"/> → <see cref="MaxCutoff"/>).
    /// </summary>
    /// <returns>The persisted, sanitized settings, or the safe defaults.</returns>
    public AutoApproveSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return AutoApproveSettings.Default;
            }

            var json = File.ReadAllText(_settingsFilePath);
            var loaded = JsonSerializer.Deserialize<AutoApproveSettings>(json);
            if (loaded is null)
            {
                return AutoApproveSettings.Default;
            }

            // Why: the JSON may parse fine yet carry a semantically-bad cutoff (negative / absurd).
            // Clamp rather than trust a file an attacker with local FS access could edit (T-59-03).
            return loaded with { Cutoff = Sanitize(loaded.Cutoff) };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Why: corrupt/unreadable file falls back to safe defaults without surfacing to the UI.
            return AutoApproveSettings.Default;
        }
    }

    /// <summary>
    /// Persists the settings to <c>auto-approve-settings.json</c>, creating the directory if needed.
    /// The cutoff is sanitized on write too, so a bad value can never reach disk (T-59-03).
    /// </summary>
    /// <param name="settings">The settings to persist.</param>
    public void Save(AutoApproveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var sanitized = settings with { Cutoff = Sanitize(settings.Cutoff) };
        var json = JsonSerializer.Serialize(sanitized);
        File.WriteAllText(_settingsFilePath, json);
    }

    // Why: a negative persisted cutoff maps to the default (5) — it can no longer reach the signal;
    // a value above MaxCutoff is clamped down. Distinct from the corrupt-JSON path (the value parsed).
    private static int Sanitize(int cutoff) =>
        cutoff < 0 ? ClipCountAutoApproveSignal.DefaultCutoff : Math.Min(cutoff, MaxCutoff);
}

using System.Collections.Frozen;
using System.Text.Json;
using DeckFlow.Core.Edhrec;
using DeckFlow.Core.Normalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Supplies locally bundled EDHREC staple cards for a solo commander.</summary>
public interface ICommanderStapleProvider
{
    /// <summary>Gets normalized staple card names when exactly one commander resolves.</summary>
    /// <param name="commanderNames">Resolved commander names.</param>
    /// <returns>Normalized staple names, or an empty set when unavailable or not applicable.</returns>
    IReadOnlySet<string> GetStapleCardNames(IReadOnlyList<string> commanderNames);
}

/// <inheritdoc />
public sealed class CommanderStapleProvider : ICommanderStapleProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly FrozenSet<string> EmptyStaples = FrozenSet<string>.Empty;

    private readonly string _dataFilePath;
    private readonly Func<string, string> _fileReader;
    private readonly ILogger _logger;
    private readonly Lazy<IReadOnlyDictionary<string, IReadOnlySet<string>>> _lookup;

    /// <summary>DI constructor — locates <c>Data/commander-staples/latest.json</c> in the content root.</summary>
    public CommanderStapleProvider(IWebHostEnvironment env, ILogger<CommanderStapleProvider>? logger = null)
        : this(Path.Combine(env.ContentRootPath, "Data", "commander-staples", "latest.json"), logger)
    {
    }

    /// <summary>Test-seam constructor with an explicit staple snapshot path.</summary>
    internal CommanderStapleProvider(string dataFilePath, ILogger? logger = null)
        : this(File.ReadAllText, dataFilePath, logger)
    {
    }

    internal CommanderStapleProvider(Func<string, string> fileReader, string dataFilePath, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(fileReader);
        ArgumentNullException.ThrowIfNull(dataFilePath);
        _fileReader = fileReader;
        _dataFilePath = dataFilePath;
        _logger = logger ?? NullLogger.Instance;
        _lookup = new Lazy<IReadOnlyDictionary<string, IReadOnlySet<string>>>(LoadLookup);
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetStapleCardNames(IReadOnlyList<string> commanderNames)
    {
        ArgumentNullException.ThrowIfNull(commanderNames);

        if (commanderNames.Count != 1 || string.IsNullOrWhiteSpace(commanderNames[0]))
        {
            return EmptyStaples;
        }

        string commanderKey = CutLabCardNames.Normalize(commanderNames[0]);
        return _lookup.Value.TryGetValue(commanderKey, out IReadOnlySet<string>? staples)
            ? staples
            : EmptyStaples;
    }

    private IReadOnlyDictionary<string, IReadOnlySet<string>> LoadLookup()
    {
        try
        {
            string json = _fileReader(_dataFilePath);
            CommanderStaplesSnapshot? snapshot = JsonSerializer.Deserialize<CommanderStaplesSnapshot>(json, JsonOptions);
            Dictionary<string, IReadOnlySet<string>> lookup = new(StringComparer.Ordinal);
            foreach (CommanderStaplesCommander? commander in snapshot?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander?.Name) || commander.Staples is null)
                {
                    continue;
                }

                HashSet<string> staples = new(StringComparer.Ordinal);
                foreach (string? staple in commander.Staples)
                {
                    if (!string.IsNullOrWhiteSpace(staple))
                    {
                        staples.Add(CutLabCardNames.Normalize(staple));
                    }
                }

                lookup[CutLabCardNames.Normalize(commander.Name)] = staples.ToFrozenSet(StringComparer.Ordinal);
            }

            return lookup.ToFrozenDictionary(StringComparer.Ordinal);
        }
        // Why: fail-open contract; Lazy caches unhandled exceptions.
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Commander staples unavailable at {DataFilePath}; continuing without them.", _dataFilePath);
            return FrozenDictionary<string, IReadOnlySet<string>>.Empty;
        }
    }
}

internal sealed class NullCommanderStapleProvider : ICommanderStapleProvider
{
    internal static readonly NullCommanderStapleProvider Instance = new();

    private NullCommanderStapleProvider()
    {
    }

    public IReadOnlySet<string> GetStapleCardNames(IReadOnlyList<string> commanderNames) => FrozenSet<string>.Empty;
}

using System.Text.Json;
using DeckFlow.Core.Research;
using DeckFlow.Web.Services.CutLab;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies the commander role-floor baseline provider's lookups, fail-open behavior, and caching.
/// </summary>
public sealed class RoleFloorBaselineProviderTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public RoleFloorBaselineProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"role-floor-baseline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "latest.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void TryGetRoleFloor_CommanderAndRolePresent_ReturnsFloor()
    {
        WriteSnapshot(
            new RoleFloorBaselineSnapshot
            {
                Generated = "2026-07-29",
                SampleSize = 1,
                AdoptedPairs = 1,
                Commanders = new Dictionary<string, RoleFloorCommanderSnapshot>(StringComparer.Ordinal)
                {
                    ["Brago, King Eternal"] = new()
                    {
                        N = 173,
                        Floors = new Dictionary<string, int>(StringComparer.Ordinal)
                        {
                            ["engines"] = 9,
                        },
                    },
                },
            });

        bool found = CreateProvider().TryGetRoleFloor(["Brago, King Eternal"], "engines", out int floor);

        Assert.True(found);
        Assert.Equal(9, floor);
    }

    [Fact]
    public void TryGetRoleFloor_CommanderPresentRoleAbsent_ReturnsFalse()
    {
        WriteSnapshot(
            new RoleFloorBaselineSnapshot
            {
                Generated = "2026-07-29",
                SampleSize = 1,
                AdoptedPairs = 1,
                Commanders = new Dictionary<string, RoleFloorCommanderSnapshot>(StringComparer.Ordinal)
                {
                    ["Brago, King Eternal"] = new()
                    {
                        N = 173,
                        Floors = new Dictionary<string, int>(StringComparer.Ordinal)
                        {
                            ["engines"] = 9,
                        },
                    },
                },
            });

        bool found = CreateProvider().TryGetRoleFloor(["Brago, King Eternal"], "payoffs", out int floor);

        Assert.False(found);
        Assert.Equal(0, floor);
    }

    [Fact]
    public void TryGetRoleFloor_CommanderAbsent_ReturnsFalse()
    {
        WriteSnapshot(
            new RoleFloorBaselineSnapshot
            {
                Generated = "2026-07-29",
                SampleSize = 1,
                AdoptedPairs = 1,
                Commanders = new Dictionary<string, RoleFloorCommanderSnapshot>(StringComparer.Ordinal)
                {
                    ["Brago, King Eternal"] = new()
                    {
                        N = 173,
                        Floors = new Dictionary<string, int>(StringComparer.Ordinal)
                        {
                            ["engines"] = 9,
                        },
                    },
                },
            });

        bool found = CreateProvider().TryGetRoleFloor(["Atraxa, Praetors' Voice"], "engines", out int floor);

        Assert.False(found);
        Assert.Equal(0, floor);
    }

    [Fact]
    public void TryGetRoleFloor_PartnerPair_ReturnsFalse()
    {
        WriteSnapshot(
            new RoleFloorBaselineSnapshot
            {
                Generated = "2026-07-29",
                SampleSize = 2,
                AdoptedPairs = 1,
                Commanders = new Dictionary<string, RoleFloorCommanderSnapshot>(StringComparer.Ordinal)
                {
                    ["Halana, Kessig Ranger"] = new()
                    {
                        N = 41,
                        Floors = new Dictionary<string, int>(StringComparer.Ordinal)
                        {
                            ["interaction-targeted"] = 4,
                        },
                    },
                },
            });

        // Why: D-10 measured zero partner-pair keys in the corpus, so partner decks correctly
        // resolve nothing here and fall back to the bracket floor rather than fabricating data.
        bool found = CreateProvider().TryGetRoleFloor(
            ["Halana, Kessig Ranger", "Alena, Kessig Trapper"],
            "interaction-targeted",
            out int floor);

        Assert.False(found);
        Assert.Equal(0, floor);
    }

    [Fact]
    public void TryGetRoleFloor_DoubleFacedName_MatchesFullKey()
    {
        WriteSnapshot(
            new RoleFloorBaselineSnapshot
            {
                Generated = "2026-07-29",
                SampleSize = 1,
                AdoptedPairs = 1,
                Commanders = new Dictionary<string, RoleFloorCommanderSnapshot>(StringComparer.Ordinal)
                {
                    ["Ojer Axonil // Temple of Power"] = new()
                    {
                        N = 102,
                        Floors = new Dictionary<string, int>(StringComparer.Ordinal)
                        {
                            ["engines"] = 1,
                        },
                    },
                },
            });

        bool found = CreateProvider().TryGetRoleFloor(["Ojer Axonil // Temple of Power"], "engines", out int floor);

        Assert.True(found);
        Assert.Equal(1, floor);
    }

    [Fact]
    public void TryGetRoleFloor_MissingFile_ReturnsFalseAndDoesNotThrow()
    {
        var provider = new RoleFloorBaselineProvider(
            Path.Combine(Path.GetTempPath(), $"missing-role-floor-{Guid.NewGuid():N}.json"),
            NewCache());

        bool found = provider.TryGetRoleFloor(["Brago, King Eternal"], "engines", out int floor);

        Assert.False(found);
        Assert.Equal(0, floor);
    }

    [Fact]
    public void TryGetRoleFloor_CorruptJson_ReturnsFalseAndDoesNotThrow()
    {
        File.WriteAllText(_path, "{ not json");

        bool found = CreateProvider().TryGetRoleFloor(["Brago, King Eternal"], "engines", out int floor);

        Assert.False(found);
        Assert.Equal(0, floor);
    }

    [Fact]
    public void TryGetRoleFloor_FailedLoad_IsCached()
    {
        var cache = NewCache();
        var provider = new RoleFloorBaselineProvider(_path, cache);

        bool firstFound = provider.TryGetRoleFloor(["Brago, King Eternal"], "engines", out int firstFloor);

        WriteSnapshot(
            new RoleFloorBaselineSnapshot
            {
                Generated = "2026-07-29",
                SampleSize = 1,
                AdoptedPairs = 1,
                Commanders = new Dictionary<string, RoleFloorCommanderSnapshot>(StringComparer.Ordinal)
                {
                    ["Brago, King Eternal"] = new()
                    {
                        N = 173,
                        Floors = new Dictionary<string, int>(StringComparer.Ordinal)
                        {
                            ["engines"] = 9,
                        },
                    },
                },
            });

        bool secondFound = provider.TryGetRoleFloor(["Brago, King Eternal"], "engines", out int secondFloor);

        Assert.False(firstFound);
        Assert.Equal(0, firstFloor);
        Assert.False(secondFound);
        Assert.Equal(0, secondFloor);
    }

    [Fact]
    public void TryGetRoleFloor_CommittedSnapshot_ResolvesAKnownCommander()
    {
        string snapshotPath = ResolveCommittedSnapshotPath();
        RoleFloorBaselineSnapshot snapshot = JsonSerializer.Deserialize<RoleFloorBaselineSnapshot>(
            File.ReadAllText(snapshotPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        KeyValuePair<string, RoleFloorCommanderSnapshot> commander = snapshot.Commanders.First();
        KeyValuePair<string, int> role = commander.Value.Floors.First();
        var provider = new RoleFloorBaselineProvider(snapshotPath, NewCache());

        bool found = provider.TryGetRoleFloor([commander.Key], role.Key, out int floor);

        Assert.True(found);
        Assert.Equal(role.Value, floor);
    }

    private RoleFloorBaselineProvider CreateProvider()
        => new(_path, NewCache());

    private void WriteSnapshot(RoleFloorBaselineSnapshot snapshot)
        => File.WriteAllText(
            _path,
            JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static IMemoryCache NewCache()
        => new MemoryCache(new MemoryCacheOptions());

    private static string ResolveCommittedSnapshotPath()
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, "Data", "role-floor-baseline", "latest.json");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        string probe = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(probe))
        {
            string candidate = Path.Combine(probe, "DeckFlow.Web", "Data", "role-floor-baseline", "latest.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            string? parent = Directory.GetParent(probe)?.FullName;
            if (string.Equals(parent, probe, StringComparison.Ordinal))
            {
                break;
            }

            probe = parent ?? string.Empty;
        }

        throw new FileNotFoundException("Could not locate DeckFlow.Web/Data/role-floor-baseline/latest.json.");
    }
}

using System.Reflection;
using DeckFlow.Web.Configuration;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Guards the retention rollout runbook's settings table.</summary>
public sealed class HarvestRetentionRunbookTests
{
    [Fact]
    public void Runbook_DocumentsEverySettingWithItsDefault()
    {
        var path = Path.Combine(RepoPaths.Root(), "docs", "ops", "harvest-retention-rollout.md");
        Assert.True(File.Exists(path), $"Missing {path}");
        var text = File.ReadAllText(path);
        var expected = new HarvestRetentionOptions();
        var properties = typeof(HarvestRetentionOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite).ToDictionary(property => property.Name, property => property.GetValue(expected) is null ? "null" : Convert.ToString(property.GetValue(expected), System.Globalization.CultureInfo.InvariantCulture)!);
        var rows = System.Text.RegularExpressions.Regex.Matches(text, @"^\|\s*`(?<name>\w+)`\s*\|\s*(?<value>[^|]+)\|", System.Text.RegularExpressions.RegexOptions.Multiline)
            .ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value.Trim(), StringComparer.Ordinal);
        Assert.Equal(properties.Count, rows.Count);
        foreach (var (name, value) in properties)
        {
            Assert.True(rows.TryGetValue(name, out var documented), $"Missing settings row for {name}");
            Assert.True(string.Equals(value, documented, StringComparison.Ordinal), $"Wrong default for {name}: expected {value}, actual {documented}");
        }
    }
}

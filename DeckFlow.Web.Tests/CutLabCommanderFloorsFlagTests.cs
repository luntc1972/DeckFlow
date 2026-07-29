using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Guards the Cut Lab commander-floors dark-launch view-model seam.</summary>
public sealed class CutLabCommanderFloorsFlagTests
{
    [Fact]
    public void From_CommanderFloorsDisabled_HidesCommanderColumns()
    {
        var request = new CutLabRequest
        {
            PlayExperience = "Focused",
        };
        var result = new CutLabProcessResult
        {
            HasResult = true,
            ResolvedFloors =
            [
                new CutLabResolvedFloor
                {
                    Role = "engines",
                    Floor = 6,
                    DefaultValue = 6,
                    BracketValue = 6,
                    CommanderValue = 9,
                    ResolvedBracket = 4,
                },
            ],
            RoleAssignmentsByCardName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            CommanderFloorsEnabled = false,
        };

        CutLabViewModel model = CutLabViewModel.From(request, result);

        Assert.False(model.CommanderFloorsEnabled);
        CutLabFloorRowView row = Assert.Single(model.FloorRows);
        Assert.Equal("9", row.CommanderDisplay);
        Assert.True(row.SupportsCommanderFloor);
    }
}

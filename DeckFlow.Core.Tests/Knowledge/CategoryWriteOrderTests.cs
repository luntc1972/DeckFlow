using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class CategoryWriteOrderTests
{
    [Fact]
    public void CardNames_ReversedInput_OrdersNormalizedNamesOrdinally()
    {
        Assert.Equal(new[] { "Alpha", "Beta" }, CategoryWriteOrder.CardNames(new[] { "Beta", "Alpha", "ALPHA" }));
    }

    [Fact]
    public void Observations_CaseAndBoard_OrdersEveryKeyOrdinally()
    {
        var rows = new[]
        {
            (CardId: 2L, Category: "ramp", Board: "sideboard"),
            (CardId: 1L, Category: "ramp", Board: "mainboard"),
            (CardId: 1L, Category: "Ramp", Board: "sideboard"),
            (CardId: 1L, Category: "Ramp", Board: "mainboard")
        };

        var ordered = CategoryWriteOrder.Observations(rows, row => row.CardId, row => row.Category, row => row.Board).ToArray();

        Assert.Equal(new[] { rows[3], rows[2], rows[1], rows[0] }, ordered);
    }

    [Fact]
    public void Deltas_DeleteAndReinsertSameKey_OmitsNetZero()
    {
        var order = new CategoryWriteOrder();
        order.Add(1, "Ramp", -1);
        order.Add(1, "Ramp", 1);

        Assert.Empty(order.Deltas());
    }

    [Fact]
    public void Deltas_Deletion_ProducesNegativeDelta()
    {
        var order = new CategoryWriteOrder();
        order.Add(2, "ramp", -1);
        order.Add(1, "Ramp", -1);

        Assert.Equal(new[] { (1L, "Ramp", -1), (2L, "ramp", -1) }, order.Deltas());
    }
}

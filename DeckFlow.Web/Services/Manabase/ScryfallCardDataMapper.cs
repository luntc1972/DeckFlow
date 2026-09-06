using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Services.Manabase;

/// <summary>
/// Maps the Web-layer Scryfall payload (<see cref="ScryfallCard"/>) onto the Core mana-base
/// input shape (<see cref="ScryfallCardData"/>). Pure mapping — no HTTP — so the Core analyzer
/// stays free of any Web/Scryfall dependency. Multi-faced cards carry their faces through so
/// the Core mapper can pick the front-face cost and detect a land back.
/// </summary>
public static class ScryfallCardDataMapper
{
    /// <summary>Convert one <see cref="ScryfallCard"/> to a <see cref="ScryfallCardData"/>.</summary>
    public static ScryfallCardData ToCardData(ScryfallCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return new ScryfallCardData
        {
            Name = card.Name,
            ManaCost = card.ManaCost,
            Cmc = card.Cmc,
            TypeLine = card.TypeLine,
            OracleText = card.OracleText,
            OracleId = card.OracleId,
            Keywords = card.Keywords,
            Power = card.Power,
            Toughness = card.Toughness,
            ProducedMana = card.ProducedMana,
            ColorIdentity = card.ColorIdentity,
            Rarity = card.Rarity,
            Set = card.SetCode,
            CollectorNumber = card.CollectorNumber,
            Layout = card.Layout,
            CardFaces = card.CardFaces is { Count: > 0 } faces
                ? faces.Select(ToFaceData).ToList()
                : null,
        };
    }

    private static ScryfallFaceData ToFaceData(ScryfallCardFace face) => new()
    {
        Name = face.Name,
        ManaCost = face.ManaCost,
        TypeLine = face.TypeLine,
        OracleText = face.OracleText,
        Power = face.Power,
        Toughness = face.Toughness,
    };
}

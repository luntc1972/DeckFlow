using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Validates and applies imported primary category edits.</summary>
public static class CutLabCategoryRules
{
    /// <summary>Maximum persisted category edits.</summary>
    public const int MaxCategoryOverrides = 200;
    /// <summary>Maximum category text length.</summary>
    public const int MaxCategoryLength = 64;

    // Why: Importers strip board tokens and exporters synthesize them; Uncategorized is the group label.
    private static readonly string[] Reserved = ["Commander", "Maybeboard", "Sideboard", "Considering", "Uncategorized"];

    /// <summary>Trims and collapses whitespace runs.</summary>
    public static string NormalizeInput(string? value) => string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Checks category length and builder export syntax.</summary>
    public static bool IsValidCategory(string normalized) => normalized.Length is > 0 and <= MaxCategoryLength
        && !normalized.Any(c => char.IsControl(c) || ",#[]{}^".Contains(c));

    /// <summary>Checks reserved board and group labels.</summary>
    public static bool IsReserved(string normalized) => Reserved.Contains(normalized, StringComparer.OrdinalIgnoreCase);

    /// <summary>Splits imported categories and preserves the first nonreserved spelling.</summary>
    public static IReadOnlyList<string> SplitTokens(string? category) => (category ?? "").Split(',')
        .Select(token => token.Trim()).Where(token => token.Length > 0 && !IsReserved(token))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Unions imported categories across every row for a card.</summary>
    public static IReadOnlyList<string> ImportedCategoryTokens(IReadOnlyList<CutLabOriginalEntry> originalEntries, string cardName) => originalEntries
        .Where(entry => CutLabCardNames.Normalize(entry.Name) == CutLabCardNames.Normalize(cardName))
        .SelectMany(entry => SplitTokens(entry.Category)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Finds the last edit for a normalized card name.</summary>
    public static CutLabCategoryOverride? Find(IReadOnlyList<CutLabCategoryOverride>? overrides, string cardName) => (overrides ?? [])
        .LastOrDefault(item => item is not null && CutLabCardNames.Normalize(item.Name) == CutLabCardNames.Normalize(cardName));

    /// <summary>Replaces the primary imported category while keeping secondary tokens.</summary>
    public static IReadOnlyList<string> EffectiveCategoryTokens(IReadOnlyList<CutLabOriginalEntry> originalEntries, IReadOnlyList<CutLabCategoryOverride> categoryOverrides, string cardName)
    {
        IReadOnlyList<string> imported = ImportedCategoryTokens(originalEntries, cardName);
        CutLabCategoryOverride? edit = Find(categoryOverrides, cardName);
        return edit is null ? imported : new[] { edit.Category }.Concat(imported.Skip(1)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Applies effective tokens to a row, keeping its board tokens.</summary>
    public static string? ApplyToCategory(string? rowCategory, IReadOnlyList<string>? effectiveTokens)
    {
        if (effectiveTokens is null)
        {
            return rowCategory;
        }
        string result = string.Join(",", (rowCategory ?? "").Split(',').Select(token => token.Trim()).Where(IsReserved)
            .Concat(effectiveTokens).Distinct(StringComparer.OrdinalIgnoreCase));
        return result.Length == 0 ? null : result;
    }

    /// <summary>Collects sorted effective category suggestions across the pool.</summary>
    public static IReadOnlyList<string> CategoryOptions(CutLabState state) => state.Pool
        .SelectMany(card => EffectiveCategoryTokens(state.OriginalEntries, state.CategoryOverrides, card.Name))
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Canonicalizes persisted state while preserving unchanged instances.</summary>
    public static CutLabState Sanitize(CutLabState state)
    {
        IReadOnlyList<CutLabCategoryOverride> result = Sanitize(state.CategoryOverrides, state.Pool);
        return state.CategoryOverrides is not null && result.SequenceEqual(state.CategoryOverrides) ? state : state with { CategoryOverrides = result };
    }

    /// <summary>Bounds and validates untrusted category overrides.</summary>
    public static IReadOnlyList<CutLabCategoryOverride> Sanitize(IReadOnlyList<CutLabCategoryOverride>? overrides, IReadOnlyList<CutLabPoolCard> pool)
    {
        Dictionary<string, CutLabPoolCard> cards = CutLabCardNames.ToLastWinsDictionary(pool.Where(card => !string.IsNullOrWhiteSpace(card.Name)), card => card.Name, card => card);
        Dictionary<string, (int Order, CutLabCategoryOverride Edit)> result = new(CutLabCardNames.Comparer);
        int order = 0;
        foreach (CutLabCategoryOverride? edit in (overrides ?? []).Take(MaxCategoryOverrides))
        {
            if (edit is null || string.IsNullOrWhiteSpace(edit.Name))
            {
                continue;
            }
            string category = NormalizeInput(edit.Category);
            if (!IsValidCategory(category) || IsReserved(category))
            {
                continue;
            }
            string key = CutLabCardNames.Normalize(edit.Name);
            cards.TryGetValue(key, out CutLabPoolCard? card);
            result[key] = (order++, new() { Name = card?.Name ?? edit.Name.Trim(), Category = category });
        }
        return result.Values.OrderBy(item => item.Order).Select(item => item.Edit).ToArray();
    }

    /// <summary>Applies an explicit category operation to one pool card.</summary>
    public static bool TryApplyEdit(CutLabState state, string cardName, string? category, CutLabCategoryOverrideOperation operation, out CutLabState updated, out string? error)
    {
        updated = state;
        error = null;
        CutLabPoolCard? card = state.Pool.LastOrDefault(item => CutLabCardNames.Normalize(item.Name) == CutLabCardNames.Normalize(cardName));
        if (card is null)
        {
            return Refuse(CutLabMessages.CategoryOverrideCardNotInPoolMessage, out error);
        }
        if (operation is not CutLabCategoryOverrideOperation.Set and not CutLabCategoryOverrideOperation.Reset)
        {
            return Refuse(CutLabMessages.CategoryOverrideRefusedMessage, out error);
        }
        string value = NormalizeInput(category);
        string? primary = ImportedCategoryTokens(state.OriginalEntries, card.Name).FirstOrDefault();
        if (operation == CutLabCategoryOverrideOperation.Set)
        {
            if (value.Length == 0)
            {
                return Refuse(CutLabMessages.CategoryOverrideBlankMessage, out error);
            }
            if (!IsValidCategory(value))
            {
                return Refuse(CutLabMessages.CategoryOverrideInvalidMessage, out error);
            }
            if (IsReserved(value))
            {
                return Refuse(CutLabMessages.CategoryOverrideReservedMessage, out error);
            }
            if (Find(state.CategoryOverrides, card.Name) is null && state.CategoryOverrides.Count >= MaxCategoryOverrides
                && !string.Equals(primary, value, StringComparison.OrdinalIgnoreCase))
            {
                return Refuse(CutLabMessages.CategoryOverrideLimitMessage, out error);
            }
            value = CategoryOptions(state).FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? value;
        }
        List<CutLabCategoryOverride> edits = state.CategoryOverrides.Where(item => CutLabCardNames.Normalize(item.Name) != CutLabCardNames.Normalize(card.Name)).ToList();
        if (operation == CutLabCategoryOverrideOperation.Set && !string.Equals(primary, value, StringComparison.OrdinalIgnoreCase))
        {
            edits.Add(new() { Name = card.Name, Category = value });
        }
        IReadOnlyList<CutLabCategoryOverride> result = Sanitize(edits, state.Pool);
        if (!result.SequenceEqual(state.CategoryOverrides))
        {
            // Why: Categories feed no simulation, so edits preserve all baseline snapshots and targets.
            updated = state with { CategoryOverrides = result };
        }
        return true;
    }

    private static bool Refuse(string message, out string? error)
    {
        error = message;
        return false;
    }
}

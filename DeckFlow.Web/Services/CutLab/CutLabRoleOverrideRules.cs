using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Validates and applies persisted per-card role overrides.</summary>
public static class CutLabRoleOverrideRules
{
    /// <summary>Maximum persisted role-override entries.</summary>
    public const int MaxRoleOverrides = 200;

    /// <summary>Roles a user may edit, in picker order.</summary>
    public static readonly IReadOnlyList<string> EditableRoleKeys = CutLabFloorRules.RoleKeys
        .Where(role => role != "lands")
        .ToArray();

    // Why: AssignRoles emits these keys in this order, so overrides retain classifier ordering.
    internal static readonly IReadOnlyList<string> CanonicalOrder =
    [
        "lands",
        "ramp",
        "draw",
        "tutor",
        "interaction-targeted",
        "interaction-mass",
        "protection",
        "engines",
        "payoffs",
        "wincons",
        "other",
    ];

    /// <summary>Finds an override by normalized card name.</summary>
    public static CutLabRoleOverride? Find(IReadOnlyList<CutLabRoleOverride> overrides, string cardName)
    {
        return (overrides ?? []).LastOrDefault(item => item is not null
            && CutLabCardNames.Normalize(item.Name) == CutLabCardNames.Normalize(cardName));
    }

    /// <summary>Canonicalizes persisted overrides in session state.</summary>
    public static CutLabState Sanitize(CutLabState state)
    {
        IReadOnlyList<CutLabRoleOverride> sanitized = Sanitize(
            state.RoleOverrides,
            state.Pool,
            CutLabCommanderNames.Resolve(state));

        return StructuralEquals(sanitized, state.RoleOverrides)
            ? state
            : state with { RoleOverrides = sanitized };
    }

    /// <summary>Canonicalizes untrusted overrides against the current pool.</summary>
    public static IReadOnlyList<CutLabRoleOverride> Sanitize(
        IReadOnlyList<CutLabRoleOverride>? overrides,
        IReadOnlyList<CutLabPoolCard> pool,
        IReadOnlyList<string> commanderNames)
    {
        // Why: A card may occur in multiple boards; match Cut Lab's last-wins lookup semantics.
        Dictionary<string, CutLabPoolCard> cards = CutLabCardNames.ToLastWinsDictionary(
            pool.Where(card => !string.IsNullOrWhiteSpace(card.Name)), card => card.Name, card => card);
        HashSet<string> commanders = commanderNames
            .Select(CutLabCardNames.Normalize)
            .ToHashSet(CutLabCardNames.Comparer);
        Dictionary<string, (int Order, CutLabRoleOverride Override)> result = new(CutLabCardNames.Comparer);
        int order = 0;

        foreach (CutLabRoleOverride? item in (overrides ?? []).Take(MaxRoleOverrides))
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            string normalizedName = CutLabCardNames.Normalize(item.Name);
            if (!cards.TryGetValue(normalizedName, out CutLabPoolCard? card)
                || card.IsCommander
                || commanders.Contains(normalizedName))
            {
                continue;
            }

            IReadOnlyList<string> added = Canonicalize(item.Added);
            IReadOnlyList<string> removed = Canonicalize(item.Removed);
            HashSet<string> overlap = added.Intersect(removed, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            added = added.Where(role => !overlap.Contains(role)).ToArray();
            removed = removed.Where(role => !overlap.Contains(role)).ToArray();

            if (added.Contains("interaction-targeted") && added.Contains("interaction-mass"))
            {
                added = added.Where(role => role is not "interaction-targeted" and not "interaction-mass").ToArray();
            }
            else if (added.Contains("interaction-targeted"))
            {
                removed = Canonicalize(removed.Concat(["interaction-mass"]));
            }
            else if (added.Contains("interaction-mass"))
            {
                removed = Canonicalize(removed.Concat(["interaction-targeted"]));
            }

            if (IsLand(card) && added.Contains("ramp"))
            {
                added = added.Where(role => role != "ramp").ToArray();
            }

            if (added.Count == 0 && removed.Count == 0)
            {
                continue;
            }

            // Why: The last valid edit wins and retains its last-occurrence position.
            result[normalizedName] = (order++, new CutLabRoleOverride { Name = card.Name, Added = added, Removed = removed });
        }

        return result.Values.OrderBy(item => item.Order).Select(item => item.Override).ToArray();
    }

    /// <summary>Applies one override to classifier roles.</summary>
    public static IReadOnlyList<string> ApplyToRoles(IReadOnlyList<string> baseRoles, CutLabRoleOverride? roleOverride)
    {
        if (roleOverride is null || baseRoles.Count == 0)
        {
            return baseRoles;
        }

        HashSet<string> roles = baseRoles
            .Where(role => role != "other")
            .ToHashSet(StringComparer.Ordinal);

        foreach (string role in roleOverride.Removed ?? [])
        {
            if (role != "lands")
            {
                roles.Remove(role);
            }
        }

        foreach (string role in roleOverride.Added ?? [])
        {
            if (role != "lands" && !(role == "ramp" && roles.Contains("lands")))
            {
                roles.Add(role);
            }
        }

        if (roles.Contains("interaction-targeted") && roles.Contains("interaction-mass"))
        {
            roles.Remove((roleOverride.Added ?? []).Contains("interaction-targeted", StringComparer.Ordinal)
                ? "interaction-mass"
                : "interaction-targeted");
        }

        string[] ordered = CanonicalOrder.Where(roles.Contains).ToArray();
        return ordered.Length == 0 ? ["other"] : ordered;
    }

    /// <summary>Applies a requested edit to session state.</summary>
    public static bool TryApplyEdit(
        CutLabState state,
        string cardName,
        string? role,
        CutLabRoleOverrideOperation operation,
        out CutLabState updated,
        out string? error)
    {
        updated = state;
        error = null;
        CutLabPoolCard? card = state.Pool.FirstOrDefault(candidate =>
            CutLabCardNames.Normalize(candidate.Name) == CutLabCardNames.Normalize(cardName));

        if (card is null)
        {
            return Refuse(CutLabMessages.RoleOverrideCardNotInPoolMessage, out error);
        }

        if (card.IsCommander || CutLabCommanderNames.Resolve(state)
            .Select(CutLabCardNames.Normalize)
            .Contains(CutLabCardNames.Normalize(card.Name)))
        {
            return Refuse(CutLabMessages.RoleOverrideCommanderMessage, out error);
        }

        if (operation == CutLabRoleOverrideOperation.Unspecified)
        {
            return Refuse(CutLabMessages.RoleOverrideRefusedMessage, out error);
        }

        List<CutLabRoleOverride> entries = (state.RoleOverrides ?? []).ToList();
        if (operation == CutLabRoleOverrideOperation.Reset)
        {
            entries.RemoveAll(item => CutLabCardNames.Normalize(item.Name) == CutLabCardNames.Normalize(card.Name));
        }
        else
        {
            string? key = EditableRoleKeys.FirstOrDefault(candidate =>
                string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));

            if (string.Equals(role, "lands", StringComparison.OrdinalIgnoreCase))
            {
                return Refuse(CutLabMessages.RoleOverrideLandsLockedMessage, out error);
            }

            if (key is null)
            {
                return Refuse(CutLabMessages.RoleOverrideRefusedMessage, out error);
            }

            if (operation == CutLabRoleOverrideOperation.Add && key == "ramp" && IsLand(card))
            {
                return Refuse(CutLabMessages.RoleOverrideRampOnLandMessage, out error);
            }

            CutLabRoleOverride current = Find(entries, card.Name) ?? new CutLabRoleOverride { Name = card.Name };
            entries.RemoveAll(item => CutLabCardNames.Normalize(item.Name) == CutLabCardNames.Normalize(card.Name));
            List<string> added = (current.Added ?? []).ToList();
            List<string> removed = (current.Removed ?? []).ToList();

            if (operation == CutLabRoleOverrideOperation.Add)
            {
                removed.RemoveAll(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
                added.Add(key);

                if (key == "interaction-targeted")
                {
                    added.RemoveAll(item => string.Equals(item, "interaction-mass", StringComparison.OrdinalIgnoreCase));
                    removed.Add("interaction-mass");
                }
                else if (key == "interaction-mass")
                {
                    added.RemoveAll(item => string.Equals(item, "interaction-targeted", StringComparison.OrdinalIgnoreCase));
                    removed.Add("interaction-targeted");
                }
            }
            else
            {
                added.RemoveAll(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
                removed.Add(key);
            }

            entries.Add(new CutLabRoleOverride { Name = card.Name, Added = added, Removed = removed });
        }

        IReadOnlyList<CutLabRoleOverride> sanitized = Sanitize(entries, state.Pool, CutLabCommanderNames.Resolve(state));
        if (StructuralEquals(sanitized, state.RoleOverrides ?? []))
        {
            return true;
        }

        updated = state with
        {
            RoleOverrides = sanitized,
            BaselineSnapshot = null,
            BaselineActualLands = null,
            BaselineTargetLands = null,
        };
        return true;
    }

    internal static string Fingerprint(IReadOnlyList<CutLabRoleOverride>? overrides)
    {
        return string.Join("|", (overrides ?? [])
            .Select(item => $"{CutLabCardNames.Normalize(item.Name)}:{string.Join(',', Canonicalize(item.Added))}:{string.Join(',', Canonicalize(item.Removed))}")
            .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static IReadOnlyList<string> Canonicalize(IEnumerable<string>? values)
    {
        return CanonicalOrder
            .Where(key => key != "lands" && key != "other"
                && (values ?? []).Any(value => string.Equals(value, key, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static bool StructuralEquals(IReadOnlyList<CutLabRoleOverride> left, IReadOnlyList<CutLabRoleOverride> right)
    {
        return left.Count == right.Count && left.Zip(right).All(pair =>
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal)
            && pair.Second.Added is not null
            && pair.Second.Removed is not null
            && pair.First.Added.SequenceEqual(pair.Second.Added, StringComparer.Ordinal)
            && pair.First.Removed.SequenceEqual(pair.Second.Removed, StringComparer.Ordinal));
    }

    private static bool IsLand(CutLabPoolCard card)
    {
        return card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Refuse(string message, out string? error)
    {
        error = message;
        return false;
    }
}

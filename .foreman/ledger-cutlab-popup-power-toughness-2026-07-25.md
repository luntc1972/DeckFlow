# Foreman Ledger — Cut Lab card popup: show Power/Toughness for creatures
BASELINE: 74b456ba0926d3176c99130b575d18b2a2fa6e11 | worktree busy: sticky-bar (bmgtwd535, done, verifying) then restart-rounds (queued) ahead of this | 2026-07-25T10:22:00-06:00

## Root cause (fully verified by reading the code, not assumed)
Confirmed end-to-end data-flow gap, NOT limited to Cut Lab — it's a broader mapper bug that
happens to surface here first:

1. `ScryfallCard`/`ScryfallCardFace` (DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs
   ~line 39-81) ALREADY have `Power` and `Toughness` (both `string?`,
   `[JsonPropertyName("power"/"toughness")]`) at both card-level and per-face — the raw
   Scryfall data is fully present, nothing missing upstream.
2. `ScryfallCardData`/`ScryfallFaceData` (DeckFlow.Core/Manabase/ScryfallCardData.cs
   ~line 11-88) has `Power` (string?) on BOTH types already declared — but has **NO
   `Toughness` property at all**.
3. `ScryfallCardDataMapper.ToCardData`/`ToFaceData`
   (DeckFlow.Web/Services/Manabase/ScryfallCardDataMapper.cs ~line 15-45) — the object
   initializers for both `ScryfallCardData` and `ScryfallFaceData` **never set `Power`**
   despite the property existing on the target type. This mapper is the ONLY path that
   populates `ScryfallCardData` for Cut Lab (`CutLabPageService.cs:264`,
   `CutLabSimulationService.cs:233`) AND is reused broadly by Manabase
   (`ManabaseAnalysisService.cs:736,1085,1095,1141`) — so `ScryfallCardData.Power` is
   effectively a dead, always-null field everywhere this mapper is used, not just in Cut Lab.
4. `CutLabCardTextView` (DeckFlow.Web/Models/CutLabViewModel.cs ~line 8-24) has no
   Power/Toughness fields, and `CutLabPageService.BuildCardTextByCardName`
   (~line 716-742) doesn't set any.
5. CutLab.cshtml's data-island builder (~line 230-260ish) manually copies
   `cardText.TypeLine`/`ManaCost`/`SetCode`/`CollectorNumber` into a
   `Dictionary<string,string>` keyed by camelCase names (`typeLine`, `manaCost`, `setCode`,
   `collectorNumber`) — no power/toughness keys added, matching the gap above.
6. `CutLabCardTextEntry` (TS interface, cut-lab.ts ~line 60-67) mirrors those same camelCase
   fields client-side — also missing power/toughness.
7. `getCardMetaLine` (cut-lab.ts ~line 1446-1474) builds the popup's meta line from
   typeLine/manaCost/printing — has no P/T segment to add.
8. Card popup markup (`openCardModal`, cut-lab.ts ~line 1500+) writes `metaLine` into
   `[data-cutlab-modal-meta]` — confirmed this is the single render point for meta info.

**Established correct precedent to mirror** for the front-face/back-face P/T fallback logic
(a DIFFERENT, unrelated Scryfall pipeline already solves this correctly — mirror it, don't
reinvent): `DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs` ~line 305-333:
```
var power = card.Power;
var toughness = card.Toughness;
if (string.IsNullOrWhiteSpace(card.OracleText)
    && (string.IsNullOrWhiteSpace(power) || string.IsNullOrWhiteSpace(toughness))
    && card.CardFaces is { Count: > 0 })
{
    // only fall back to front (cast) face P/T for genuine transform/MDFC cards
    power = card.CardFaces[0].Power;
    toughness = card.CardFaces[0].Toughness;
}
if (!string.IsNullOrWhiteSpace(power) && !string.IsNullOrWhiteSpace(toughness))
{
    parts.Add($"{power}/{toughness}");
}
```
Note this codebase's own earlier bugfix today (commit 48daa680, role classification) already
established "front-face-first, JSON parent-empty gates the fallback" as the correct MDFC
precedence pattern for oracle text — this P/T logic is the same shape, already proven
correct and shipped elsewhere in the app.

## Plan (single Codex ticket — small, mechanical, well-understood, low risk)
1. Add `Toughness` (string?, `[JsonPropertyName("toughness")]`) to both `ScryfallCardData`
   and `ScryfallFaceData` (DeckFlow.Core/Manabase/ScryfallCardData.cs), mirroring the
   existing `Power` property exactly (placement, doc comment style).
2. Fix `ScryfallCardDataMapper.ToCardData` and `ToFaceData`
   (DeckFlow.Web/Services/Manabase/ScryfallCardDataMapper.cs): add `Power = card.Power,` +
   `Toughness = card.Toughness,` to `ToCardData`'s initializer, and `Power = face.Power,` +
   `Toughness = face.Toughness,` to `ToFaceData`'s initializer. **Verify this doesn't change
   any EXISTING behavior/tests that assumed Power was always null** — grep for
   `.Power` reads on `ScryfallCardData`/`ScryfallFaceData` elsewhere in the codebase before
   assuming this is purely additive (Manabase's `ManabaseAnalysisService.cs` and any
   classifiers/tests reading `.Power` off these specific types, not the raw `ScryfallCard`,
   need checking — a previously-always-null field suddenly having real values could change
   behavior somewhere unexpected; if such a read exists and depended on null, flag it,
   don't just silently "fix" it as a byproduct).
3. Add `Power`/`Toughness` (string?) to `CutLabCardTextView`
   (DeckFlow.Web/Models/CutLabViewModel.cs ~line 8-24).
4. In `CutLabPageService.BuildCardTextByCardName`
   (DeckFlow.Web/Services/CutLab/CutLabPageService.cs ~line 716-742), populate them,
   applying the SAME front-face-fallback pattern as the `ScryfallSetService.cs` precedent
   above (gate the CardFaces[0] fallback on the same condition already used there — parent
   OracleText empty AND (power or toughness) empty AND CardFaces present — for consistency;
   Cut Lab's resolvedCard is `ScryfallCardData`/`ScryfallFaceData`, not `ScryfallCard`, so
   adapt field access accordingly but keep the same fallback SHAPE).
5. Add `power?: string; toughness?: string;` to `CutLabCardTextEntry`
   (DeckFlow.Web/wwwroot/ts/cut-lab.ts ~line 60-67).
6. In CutLab.cshtml's data-island loop (~line 236-260), add
   `if (!string.IsNullOrWhiteSpace(cardText.Power)) entry["power"] = cardText.Power;` and
   same for Toughness, matching the exact existing pattern for typeLine/manaCost/etc.
7. In `getCardMetaLine` (cut-lab.ts ~line 1446-1474), add a P/T segment when BOTH power and
   toughness are present (mirror the `!string.IsNullOrWhiteSpace(power) &&
   !string.IsNullOrWhiteSpace(toughness)` pairing precedent — don't show a lone "3/" or
   "/3"). Exact placement/format is Codex's call (e.g. "3/3" as its own segment, or appended
   to typeLine like "Creature — Elf Warrior (3/3)") — keep it terse, consistent with the
   existing `·`-joined segments style; a standalone `parts.push('${power}/${toughness}')`
   segment joined by the same `' · '` separator is simplest and matches the C# precedent's
   shape most directly.
8. No Toughness is needed for non-creature cards (lands, sorceries, etc.) — Scryfall simply
   returns null/absent for those, so the existing "only add if non-blank" gating handles
   this for free; no explicit "IsCreature" type-line check needed. Vehicles/other
   P-T-bearing non-Creature types will correctly also show P/T, which is desired (Scryfall's
   own signal, not ours to second-guess).

## Tests
- xUnit: extend/add coverage for `ScryfallCardDataMapper.ToCardData`/`ToFaceData` asserting
  Power/Toughness now copy through (check for an existing `ScryfallCardDataMapperTests.cs`;
  create if absent, mirroring sibling mapper test conventions). Extend
  `CutLabPageService`/`CutLabViewModel`-adjacent test coverage (check
  `CutLabViewModelWordingTests.cs` or wherever `BuildCardTextByCardName` is indirectly
  tested) for a creature card showing Power/Toughness, a non-creature card showing none, and
  an MDFC creature-back-face card falling back correctly per the established pattern.
- vitest: extend whatever test file covers `getCardMetaLine`/`openCardModal` (search
  cut-lab.ts test files for "getCardMetaLine" or "modal" coverage) with a creature-with-P/T
  case and a non-creature (no P/T shown) case.
- e2e: if an existing Playwright spec already opens the card modal and asserts meta-line
  content (search for `cutlab-modal-meta`/`data-cutlab-card-open` in e2e/*.spec.ts), extend
  it with a P/T assertion for a known creature in the fixture deck; add a small one if none
  exists.

## Routing
- Single Codex WORKHORSE ticket (gpt-5.4 medium) — small, mechanical, cross-layer (Core
  model + mapper + Web view model + service + Razor + TS), all pieces already traced above
  so there's no discovery work left for the worker, just implementation.
- Verification: Claude foreman-verifier (blind, cross-family), focused on: (a) the mapper
  fix doesn't silently break any existing behavior that depended on Power being null
  (item 2's caveat), (b) MDFC front/back-face P/T precedence matches the cited precedent
  shape, (c) non-creature cards show no P/T segment (no stray "/" artifacts).

## Tasks
| id | lifecycle | owned paths (WRITE SET) | job id |
|---|---|---|---|
| T1 | DISPATCHED | DeckFlow.Core/Manabase/ScryfallCardData.cs, DeckFlow.Web/Services/Manabase/ScryfallCardDataMapper.cs, DeckFlow.Web/Models/CutLabViewModel.cs, DeckFlow.Web/Services/CutLab/CutLabPageService.cs, DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/wwwroot/ts/cut-lab.ts, plus new/extended test files (DeckFlow.Core.Tests or DeckFlow.Web.Tests for the mapper, DeckFlow.Web.Tests for CutLabPageService/ViewModel, DeckFlow.Web/ts-tests for the TS meta-line, DeckFlow.Web/e2e for the modal) | bash bg job bnonx6wo1 |

## Attempts
- T1 | attempt 1 | Codex WORKHORSE gpt-5.4 medium | rev1 | dispatched 2026-07-25T12:35 — pending

## Decisions
- Confirmed via grep that `ScryfallCardDataMapper.ToCardData`/`ToFaceData` is the sole
  populator of `ScryfallCardData` for both Cut Lab and Manabase — fixing Power/Toughness
  here benefits both, not just this popup. Explicitly flagged as a caveat to check for
  behavior dependent on Power's prior always-null state before treating this as pure upside.
- Reused `ScryfallSetService.cs`'s existing, already-correct P/T fallback pattern rather than
  inventing new MDFC precedence logic — avoids repeating this morning's oracle-text
  precedence bug class.

## Scratch
(none yet)

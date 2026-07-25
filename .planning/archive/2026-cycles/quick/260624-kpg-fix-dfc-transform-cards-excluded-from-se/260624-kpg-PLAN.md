---
phase: quick-260624-kpg
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs
  - DeckFlow.Web.Tests/ScryfallSetServiceTests.cs
autonomous: true
requirements: [DFC-FIX-01]

must_haves:
  truths:
    - "A transform/MDFC card (parent oracle_text null, parent mana_cost empty, real text/cost on card_faces[]) is INCLUDED in the set-packet top-60 when on-theme."
    - "The emitted card-line for a transform card shows the front-face mana cost instead of an empty cost field."
    - "The emitted card-line for a transform card shows face oracle text (e.g. prowess) instead of an empty text field."
    - "Single-face cards (CardFaces null) produce byte-identical packet lines and scores as before the change."
  artifacts:
    - path: "DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs"
      provides: "Face-aware NormalizeOracleText + shared face-aware mana-cost helper used by score, tiebreak, and card-line render"
      contains: "CardFaces"
    - path: "DeckFlow.Web.Tests/ScryfallSetServiceTests.cs"
      provides: "Transform-included + single-face-regression coverage via the internal search-delegate seam"
      contains: "CardFaces"
  key_links:
    - from: "ScoreSetCard mana-value curve bonus (:368)"
      to: "shared face-aware mana-cost helper"
      via: "replace ParseManaValue(card.ManaCost) with helper(card)"
      pattern: "ParseManaValue\\("
    - from: "BuildCompactCardPacket tiebreak ThenBy (:321)"
      to: "shared face-aware mana-cost helper"
      via: "replace ParseManaValue(entry.Card.ManaCost) with helper(entry.Card)"
      pattern: "ThenBy\\(entry"
    - from: "card-line render (:215)"
      to: "shared face-aware mana-cost helper"
      via: "replace card.ManaCost ?? string.Empty with helper(card)"
      pattern: "card.Name. \\| "
---

<objective>
Fix double-faced / transform / MDFC cards scoring near-zero and getting cut from the
set-packet top-60 in `ScryfallSetService`. For a Scryfall `layout: "transform"` card the
PARENT object has `oracle_text = null` and `mana_cost = ""` — all real text and cost live
only in `card_faces[]`. The current scorer and card-line renderer read only parent fields,
so on-theme transform cards (e.g. "Monica Rambeau // Photon, Living Light", a W {2}{W} card
with prowess + flying) score near-zero and never reach the prompt.

Purpose: The set-packet is a core ChatGPT-ready artifact. Silently dropping on-theme
transform cards corrupts that output — the single most important value of the app.

Output: Face-aware scoring + card-line rendering, fully back-compatible for single-face
cards, plus regression + inclusion tests. No public signature changes; output-only behavior change.

This fix was already reviewed and APPROVED by Codex (gpt-5.5). Implement exactly the
three changes below — do NOT redesign, do NOT add new scoring buckets.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/STATE.md

<interfaces>
<!-- Extracted from DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs — use directly, no exploration. -->

ScryfallCard (positional record, DeckFlow.Web.Services namespace):
- Required positional args, in order:
  string Name, string? ManaCost, string TypeLine, string? OracleText,
  string? Power, string? Toughness, IReadOnlyList<string>? Keywords,
  IReadOnlyList<string>? ColorIdentity, string? SetCode, string? SetName,
  string? CollectorNumber
- Optional named args (have defaults): CardFaces (IReadOnlyList<ScryfallCardFace>? = null),
  Id, Layout, ReleasedAt, Cmc, ProducedMana, Rarity
- So a transform card is constructed in tests as:
  new ScryfallCard("Monica Rambeau // Photon", "", "Creature — ... // Creature — ...", null,
    null, null, [], ["W"], "mar", "Marvel", "1",
    CardFaces: [ frontFace, backFace ])

ScryfallCardFace (positional record):
- string Name, string? ManaCost, string? TypeLine, string? OracleText, string? Power, string? Toughness

Internal test seam on ScryfallSetService (already used by every test in
ScryfallSetServiceTests.cs):
- TestServiceFactory.CreateScryfallSetService(cache, mechanicLookup,
    executeSetListAsync: ..., executeSearchAsync: ...)
- executeSearchAsync returns RestResponse<ScryfallSearchResponse> with
  Data = new ScryfallSearchResponse([ cards... ], hasMore: false, nextPage: null)
- FakeMechanicLookupService (private nested class in the test file) returns Found=false.
</interfaces>

<current_behavior>
<!-- Lines that change, from ScryfallSetService.cs as it stands today. -->
- :215  builder.AppendLine($"{card.Name} | {card.ManaCost ?? string.Empty} | {card.TypeLine} | {NormalizeOracleText(card)}");
- :296  NormalizeOracleText(ScryfallCard) — reads ONLY card.OracleText / card.Power / card.Toughness.
- :321  .ThenBy(entry => ParseManaValue(entry.Card.ManaCost))   // tiebreak in BuildCompactCardPacket
- :368  var manaValue = ParseManaValue(card.ManaCost);          // curve bonus in ScoreSetCard
- :518  ParseManaValue(string?) — unchanged; helper feeds it the face-aware cost string.
ScoreSetCard already calls NormalizeOracleText(card) at :331, so fixing NormalizeOracleText
fixes the relevance text-signal score automatically. The mana-value path is the second fix.
</current_behavior>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Make NormalizeOracleText and mana-cost resolution face-aware</name>
  <files>DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs</files>
  <behavior>
    After this change, given a transform ScryfallCard (parent OracleText null, parent
    ManaCost "" or null, CardFaces = [front {2}{W} "Flying, prowess ... +1/+1 counter",
    back present]):
    - NormalizeOracleText returns the joined face oracle text (front + back) and uses the
      FRONT face Power/Toughness when parent P/T are null/empty.
    - The face-aware mana-cost helper returns "{2}{W}" (front face cost), so ParseManaValue
      of it is 3 — NOT int.MaxValue — so the card no longer hits the false `>=7` `-1`
      curve penalty and gets the manaValue<=4 `+1` bonus.
    Given a single-face card (CardFaces null): NormalizeOracleText and the mana-cost helper
    return byte-identical results to today (every fallback is a no-op).
  </behavior>
  <action>
Apply EXACTLY the three Codex-approved changes — no new scoring buckets, no special-casing
of split/adventure layouts.

(1) NormalizeOracleText (currently at ~:296): when parent `card.OracleText` is null/empty,
fall back to `card.CardFaces`. Join ALL faces' non-empty oracle text (the back face often
holds the payoff text) into the oracle portion, collapsing whitespace per face via the
existing CollapseWhitespace helper. Keep the existing parent-oracle path when parent
OracleText is present (do NOT also append faces in that case). For the Power/Toughness
portion: when parent `card.Power`/`card.Toughness` are null/empty AND CardFaces is non-empty,
use the FRONT face `CardFaces[0].Power`/`.Toughness`. Single-face cards have CardFaces null,
so all of this is a no-op and the method returns exactly what it returns today.

(2) Add ONE shared private static helper that resolves a face-aware mana-cost STRING from a
ScryfallCard (e.g. `ResolveManaCost(ScryfallCard card)` returning string?). Logic: when parent
`card.ManaCost` is non-empty, return it as-is — do NOT special-case split/adventure, their
parent fields are already representative. ONLY when parent ManaCost is null/empty AND CardFaces
is non-empty, return `card.CardFaces[0].ManaCost` (front / cast face). Otherwise return the
parent value. Then use this helper in BOTH score paths so they feed ParseManaValue the
face-aware cost:
  - ScoreSetCard curve bonus (~:368): replace `ParseManaValue(card.ManaCost)` with
    `ParseManaValue(ResolveManaCost(card))`.
  - BuildCompactCardPacket tiebreak (~:321): replace
    `ParseManaValue(entry.Card.ManaCost)` with `ParseManaValue(ResolveManaCost(entry.Card))`.
This makes empty-cost DFCs stop hitting the false `>=7` `-1` penalty (ParseManaValue("")
returns int.MaxValue today) and stop sorting last among same-score cards.

(3) Card-line render (~:215): replace `card.ManaCost ?? string.Empty` with
`ResolveManaCost(card) ?? string.Empty` so the emitted prompt line shows the front-face cost
for transform cards. NormalizeOracleText(card) is already in that interpolation, so change (1)
covers the oracle-text portion of the line automatically.

Respect CLAUDE.md carve-outs: do not convert `{ get; init; }`, do not inline `[Attribute]`,
do not re-indent raw strings, preserve switch expressions, LF endings. Touch only the lines
that change (changed-lines format gate). Allman braces, file-scoped namespace already in place.
  </action>
  <verify>
    <automated>dotnet build /mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/DeckFlow.Web.csproj -c Debug 2>&1 | tail -20</automated>
  </verify>
  <done>
Build is clean (0 errors, 0 new warnings). NormalizeOracleText falls back to CardFaces oracle
text + front-face P/T only when parent fields are empty. A single `ResolveManaCost` helper
exists and is called at all three sites (:215 render, :321 tiebreak, :368 curve bonus). No
public signatures changed; single-face behavior unchanged by inspection.
  </done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Add transform-inclusion and single-face-regression tests</name>
  <files>DeckFlow.Web.Tests/ScryfallSetServiceTests.cs</files>
  <behavior>
    - Transform card flows through BuildSetPacketAsync and appears in the cards: block,
      with the emitted line containing the front-face cost `{2}{W}` and face oracle text
      (the "prowess" / counter line).
    - A control single-face card in the same packet renders its line exactly as today
      (cost + oracle text from parent fields) — guards byte-identical behavior.
  </behavior>
  <action>
Add new [Fact] tests to the existing `ScryfallSetServiceTests` class, mirroring the seam
already used by `BuildSetPacketAsync_ExcludesLowSignalLandsAndAddsSelectionNotes`
(TestServiceFactory.CreateScryfallSetService + executeSetListAsync returning the set +
executeSearchAsync returning a ScryfallSearchResponse of cards). Use the private nested
FakeMechanicLookupService already in the file.

Test A — transform card is INCLUDED with front-face cost + face text:
  Build a transform ScryfallCard using the positional ctor with parent OracleText = null and
  parent ManaCost = "" (empty), and CardFaces supplied via the named `CardFaces:` arg:
    - front face: ScryfallCardFace("Monica Rambeau", "{2}{W}", "Legendary Creature — Hero",
      "Flying, prowess\nWhenever this attacks, put a +1/+1 counter on it.", "2", "2")
    - back face:  ScryfallCardFace("Photon, Living Light", null, "Legendary Creature — Hero",
      "Flying\nWhenever you cast a noncreature spell, this deals 2 damage to any target.",
      "3", "3")
  Also include one plain single-face control card (e.g. an instant or creature with parent
  fields populated) so the packet has >1 card. Set the set as "mar" expansion in
  executeSetListAsync. Call BuildSetPacketAsync(["mar"], ["W"]) (or no color filter).
  Assert: packet Contains "Monica Rambeau"; the transform card's emitted line Contains "{2}{W}"
  (front-face cost surfaced, NOT empty); packet Contains "prowess" (face oracle text surfaced).
  Prefer asserting against the specific transform line (split packet on newlines, find the line
  starting with the card name) so an empty cost field would fail the assertion.

Test B — single-face regression / byte-identical line:
  Build a packet whose only card is a plain single-face card (CardFaces null) with parent
  ManaCost "{1}{G}", parent OracleText "Draw a card.", P/T "2"/"2", an on-theme line so it
  scores > 0. Assert the emitted line equals the exact expected
  `Name | {1}{G} | <TypeLine> | Draw a card. 2/2` string (matching today's NormalizeOracleText
  output: oracle text then "P/T"). This locks in that the fallback paths are no-ops for
  single-face cards. If matching the full line is brittle against surrounding format, assert
  the line Contains "{1}{G}" AND Contains "Draw a card." AND Contains "2/2".

If feasible via the seam, add a focused scoring assertion: include the transform card AND a
clearly-cut low-signal card in the same search response and assert the transform card is
present while confirming it cleared the > 0 cut — i.e. it appears in the cards: block. (No
direct access to ScoreSetCard; assert via packet inclusion.)

Match existing test conventions: `public sealed class` already declared, xUnit `[Fact]`,
`Assert.Contains` / `Assert.DoesNotContain`, descriptive `Method_Scenario_ExpectedResult`
names (e.g. `BuildSetPacketAsync_TransformCard_IncludedWithFrontFaceCostAndText`,
`BuildSetPacketAsync_SingleFaceCard_LineUnchanged`). xUnit per project rules (.NET Core test
project). LF endings, changed-lines format gate.
  </action>
  <verify>
    <automated>dotnet build /mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -c Debug 2>&1 | tail -20</automated>
  </verify>
  <done>
Test project builds clean. Two+ new [Fact] tests exist: one asserting the transform card is
included with front-face cost `{2}{W}` and face oracle text in its emitted line, one asserting
a single-face card's line is unchanged. Tests use the existing internal search-delegate seam
and FakeMechanicLookupService. (VSTest is unreliable in WSL — see verification note; run is
deferred to push-and-watch CI or a manual harness.)
  </done>
</task>

</tasks>

<verification>
- `dotnet build` of DeckFlow.Web and DeckFlow.Web.Tests is clean (0 errors, 0 new warnings).
- ScryfallSetService.cs: NormalizeOracleText reads CardFaces only when parent fields empty; a
  single ResolveManaCost helper is wired to :215, :321, :368; no other call sites of
  `card.ManaCost`/`entry.Card.ManaCost` for scoring remain.
- New tests present and compile.
- VSTest is unreliable under WSL (per CLAUDE.md). Test EXECUTION is not a build-time gate here;
  validate by `dotnet build` clean + reasoning, then run the two new tests via push-and-watch
  CI or a manual harness (e.g. `dotnet test --filter "FullyQualifiedName~ScryfallSetServiceTests"`
  if a working runner is available). Note this in the SUMMARY.
- README: no behavior/feature doc references the set-packet top-60 selection heuristic by
  mechanism, so no README change is required (confirm with a quick grep; update only if a doc
  describes the selection behavior that this changes).
</verification>

<success_criteria>
- A transform/MDFC card with empty parent cost/text but on-theme face text is INCLUDED in the
  set-packet top-60 and its emitted line shows the front-face mana cost and face oracle text.
- Single-face cards produce byte-identical packet lines and scores (regression guaranteed by
  no-op fallbacks + Test B).
- No public signature changes; output-only behavior change.
- Out of scope and NOT touched: prowess/noncreature-cast scoring bucket, deck synergy_tags /
  primary_axes threading.
</success_criteria>

<output>
Create `.planning/quick/260624-kpg-fix-dfc-transform-cards-excluded-from-se/260624-kpg-SUMMARY.md` when done.
</output>

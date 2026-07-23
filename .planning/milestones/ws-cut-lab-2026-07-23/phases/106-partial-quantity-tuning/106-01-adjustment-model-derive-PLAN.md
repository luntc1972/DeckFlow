---
phase: 106-partial-quantity-tuning
plan: 01
type: execute
wave: 1
depends_on: []
autonomous: true
requirements: [EDIT-01, EDIT-02, EDIT-03]
files_modified:
  - DeckFlow.Web/Models/CutLab/CutLabState.cs
  - DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs
  - DeckFlow.Web/Services/CutLab/CutLabBasicLands.cs
  - DeckFlow.Web/Services/CutLab/CutLabLegality.cs
  - DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs
  - DeckFlow.Web.Tests/CutLabStateSerializerTests.cs
  - DeckFlow.Web.Tests/CutLabWorkingListTests.cs
  - DeckFlow.Web.Tests/CutLabBasicLandsTests.cs
  - DeckFlow.Web.Tests/CutLabLegalityTests.cs

must_haves:
  truths:
    - "A signed per-name copy delta plus an added-basic flag persists in CutLabState and survives serialize/deserialize"
    - "Derive folds adjustments onto the decision-derived list: existing entries clamp to [0, legalMax] and drop at 0; added basics materialize as land entries from constants"
    - "Only basics and the recognized any-number cards accept a quantity above 1; every other card caps at 1"
    - "Pre-106 JSON blobs (no quantityAdjustments key) deserialize cleanly to an empty adjustment list"
    - "CutLabBasicLands can synthesize a ScryfallCardData for an added basic (land type line, color identity, produced mana, cmc 0) so downstream role assignment and simulation need no Scryfall lookup"
  artifacts:
    - path: "DeckFlow.Web/Models/CutLab/CutLabState.cs"
      provides: "CutLabQuantityAdjustment record + CutLabState.QuantityAdjustments property"
      contains: "record CutLabQuantityAdjustment"
    - path: "DeckFlow.Web/Services/CutLab/CutLabBasicLands.cs"
      provides: "Constants table for the 5 basics + Snow-Covered variants + Wastes, plus a synthetic ScryfallCardData factory for added basics"
      contains: "SyntheticCardData"
    - path: "DeckFlow.Web/Services/CutLab/CutLabLegality.cs"
      provides: "Legal-multiple predicate (basics + any-number list) and legal max resolver"
    - path: "DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs"
      provides: "Derive overload taking IReadOnlyList<CutLabQuantityAdjustment>"
      contains: "QuantityAdjustments"
  key_links:
    - from: "CutLabWorkingList.Derive(pool, decisions, adjustments)"
      to: "CutLabWorkingList.Derive(pool, decisions)"
      via: "old overload delegates with empty adjustments"
      pattern: "Derive\\(pool, decisions, \\[\\]\\)|Array.Empty"
    - from: "CutLabWorkingList.Derive"
      to: "CutLabBasicLands"
      via: "materialize added-basic pool card from constants"
    - from: "CutLabBasicLands.SyntheticCardData"
      to: "DeckFlow.Core.Manabase.ScryfallCardData"
      via: "synthetic land card data (type/identity/produced mana) for added basics"
    - from: "CutLabStateSerializer.Deserialize"
      to: "CutLabState.QuantityAdjustments"
      via: "bound collection + clamp per-entry delta"
---

<objective>
Add the additive quantity-adjustment data layer (Approach B) that every later plan builds on: the
`CutLabQuantityAdjustment` model, the no-Scryfall basics constants (including a synthetic `ScryfallCardData`
factory so added basics analyze without a network lookup), the singleton-legality predicate, the new
`CutLabWorkingList.Derive` overload that folds adjustments onto the decision-derived list, and serializer
bounds/clamp/back-compat.

Purpose: Establish the single source of truth for copy-level tuning without touching the Option-A cut engine or
the whole-entry decision model (per 106-DESIGN "Approach B"). The synthetic-card factory is required because the
real role/simulation code paths key off `ScryfallCardData`, not `CutLabPoolCard.TypeLine` (see interfaces).
Output: Data model + constants + synthetic-card factory + legality helper + Derive overload + serializer, unit-tested.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-DESIGN.md
@.planning/workstreams/cut-lab/STATE.md
@./CLAUDE.md

<interfaces>
Current relevant contracts (already in the codebase — do not re-derive):

DeckFlow.Web/Models/CutLab/CutLabState.cs
- `public sealed record CutLabState` with init-only lists (Pool, Packages, Decisions, OriginalEntries, RoleFloors, Goals, Intent). Empty-initializer pattern `= []` keeps old blobs deserializing.
- `public sealed record CutLabPoolCard { string Name; int Quantity; string TypeLine; bool IsCommander; bool IsLocked; string? PackageId; }`

DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs
- `public static IReadOnlyList<CutLabPoolCard> Derive(IReadOnlyList<CutLabPoolCard> pool, IReadOnlyList<CutLabDecision> decisions)` — filters out accepted names.
- `AcceptedCardNames(...)`, `LatestDecisionsByCard(...)` helpers (leave unchanged).

DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs
- `MaxUploadBytes = 262_144`, `MaxDecisions = 500`, `MaxOriginalEntries = 200`. Deserialize does `state with { ... .Take(Max...) }` then re-locks commander + clamps floors/goals.

DeckFlow.Web/Services/CutLab/CutLabLockRules.cs
- `public static bool IsLand(string? typeLine)` — used to confirm materialized basics register as lands.

DeckFlow.Core/Manabase/ScryfallCardData.cs (the synthetic factory's return type — Core, referenced not modified)
- `sealed record ScryfallCardData { required string Name; string? ManaCost; double Cmc; string? TypeLine;
  string? OracleText; IReadOnlyList<string>? ProducedMana; IReadOnlyList<string>? ColorIdentity; string? Layout; ... }`
- Analysis keys off this: CutLabAnalysisContextBuilder maps it via ScryfallCardFactMapper.ToCardFact → role
  assignment + simulation facts. An added basic with NO ScryfallCardData gets no land role and no sim facts, so
  the factory must produce a valid land card (see HIGH-1 rationale in 106-02).
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: QuantityAdjustment model + serializer bounds/clamp/back-compat</name>
  <read_first>
    - DeckFlow.Web/Models/CutLab/CutLabState.cs (add the record + property here, mirror CutLabDecision/CutLabOriginalEntry record shape and the `= []` empty-initializer)
    - DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs (analog: how Decisions/OriginalEntries are bounded via `.Take(MaxDecisions)` and filtered on deserialize)
    - DeckFlow.Web.Tests/CutLabStateSerializerTests.cs (analog test patterns: round-trip, pre-102 back-compat, tampered-clamp)
  </read_first>
  <behavior>
    - Round-trip: a state with QuantityAdjustments serializes and deserializes to an equal list.
    - Back-compat: a pre-106 JSON blob (no `quantityAdjustments` key) deserializes to an empty list, not null.
    - Bounds: a payload with more than MaxQuantityAdjustments entries is truncated on deserialize.
    - Clamp: an entry whose Delta is outside [-MaxCopyDelta, +MaxCopyDelta] is clamped to the range; blank-name entries are dropped.
  </behavior>
  <action>
    Add `public sealed record CutLabQuantityAdjustment { string Name = ""; int Delta; bool IsAddedBasic; }` to
    CutLabState.cs with xmldoc per project convention. Add `public IReadOnlyList<CutLabQuantityAdjustment>
    QuantityAdjustments { get; init; } = [];` to CutLabState with an xmldoc noting the empty initializer keeps
    pre-106 blobs deserializing. In CutLabStateSerializer add `private const int MaxQuantityAdjustments = 300;`
    (comment the rationale: mirrors MaxDecisions headroom under MaxUploadBytes) and `private const int
    MaxCopyDelta = 150;` (comment: a 150-card pool bounds any single legal delta). In Deserialize's `state with`
    block, project QuantityAdjustments: drop blank-Name entries, clamp Delta to `Math.Clamp(delta, -MaxCopyDelta,
    MaxCopyDelta)`, then `.Take(MaxQuantityAdjustments)`. Keep `{ get; init; }` accessors (never convert to
    get-only — System.Text.Json carve-out in CLAUDE.md).
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabStateSerializerTests" 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - CutLabState.cs declares `record CutLabQuantityAdjustment` with `Name`, `Delta` (int), `IsAddedBasic` (bool) and a `QuantityAdjustments` property initialized to `[]`.
    - New serializer tests prove: round-trip equality; pre-106 blob → empty list; >300 entries truncated; out-of-range Delta clamped; blank-name dropped.
    - `dotnet build DeckFlow.Web.Tests` is clean and the CutLabStateSerializerTests filter is all-green.
    - CarveOutGuard test still passes (no `{ get; init; }` → `{ get; }` conversion).
  </acceptance_criteria>
  <done>QuantityAdjustments persists, is bounded/clamped, and pre-106 blobs still load.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Basics constants + synthetic ScryfallCardData factory + singleton-legality predicate</name>
  <read_first>
    - DeckFlow.Web/Services/CutLab/CutLabLockRules.cs (analog: `IsLand(string? typeLine)` — materialized basics must satisfy this)
    - DeckFlow.Web/Services/CutLab/CutLabCardNames.cs (analog: Ordinal comparer + normalization helper used across Cut Lab)
    - DeckFlow.Core/Manabase/ScryfallCardData.cs (the synthetic factory's return type; fields Name/TypeLine/ColorIdentity/ProducedMana/Cmc/Layout)
    - DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs (reference only: ScryfallCardFactMapper.ToCardFact + role assignment consume ScryfallCardData — the synthetic card must satisfy it so an added basic gets a land role)
  </read_first>
  <behavior>
    - Basics lookup: "Island" resolves to typeLine "Basic Land — Island", colorIdentity ["U"], isLand true.
    - Snow variant: "Snow-Covered Swamp" resolves to typeLine "Basic Snow Land — Swamp", colorIdentity ["B"].
    - Wastes: resolves to typeLine "Basic Land", colorIdentity [] (colorless), isLand true.
    - Synthetic card: SyntheticCardData("Island") returns a ScryfallCardData with Name "Island", TypeLine "Basic Land — Island", ColorIdentity ["U"], ProducedMana ["U"], Cmc 0, Layout "normal"; SyntheticCardData("Wastes") produces ProducedMana ["C"], ColorIdentity []; and CutLabLockRules.IsLand(card.TypeLine) is true for every synthetic basic.
    - Legality: IsLegalMultiple("Forest") true; IsLegalMultiple("Relentless Rats") true; IsLegalMultiple("Sol Ring") false.
    - LegalMax returns a large cap (e.g. int.MaxValue or 150) for legal-multiples and 1 otherwise.
  </behavior>
  <action>
    Create CutLabBasicLands.cs (static class) exposing an ordinal-keyed table of the 11 basics — Plains(["W"]),
    Island(["U"]), Swamp(["B"]), Mountain(["R"]), Forest(["G"]), the five Snow-Covered variants (typeLine
    "Basic Snow Land — {Subtype}", same identities), and Wastes (typeLine "Basic Land", identity [], produced
    ["C"]). Each row yields `{ typeLine, IReadOnlyList<string> colorIdentity, IReadOnlyList<string> producedMana,
    isLand:true }`. Add `TryResolve(string name, out ...)`, an `IReadOnlyCollection<string> Names` accessor for
    the UI/whitelist, `bool Contains(string name)`, and `ScryfallCardData SyntheticCardData(string name)` that
    builds a synthetic land ScryfallCardData (Name, TypeLine, ColorIdentity, ProducedMana, Cmc = 0, Layout =
    "normal", OracleText = a simple mana-tap line) from the table so the analysis/simulation paths need no
    Scryfall lookup. Create CutLabLegality.cs (static class) with `IsLegalMultiple(string cardName)` — true when
    the name is in CutLabBasicLands.Names OR in the hard-coded any-number list, else false — and
    `LegalMax(string cardName)` returning the legal-multiple cap or 1. The any-number list is EXACTLY: Persistent
    Petitioners, Dragon's Approach, Relentless Rats, Rat Colony, Shadowborn Apostle, Slime Against Humanity,
    Templar Knights, Nazgûl, Seven Dwarves (case-insensitive match). No Scryfall, no HTTP, no new packages.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabBasicLandsTests|FullyQualifiedName~CutLabLegalityTests" 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - CutLabBasicLands resolves all 11 names to the correct typeLine + colorIdentity, and every resolved typeLine satisfies `CutLabLockRules.IsLand`.
    - `CutLabBasicLands.SyntheticCardData` returns a valid land ScryfallCardData for each of the 11 basics with the correct ColorIdentity + ProducedMana (Wastes → ["C"], identity []), Cmc 0, and IsLand-true type line.
    - CutLabLegality.IsLegalMultiple returns true for each of the 5 basics, each Snow variant, Wastes, and each of the 9 any-number cards, and false for a normal singleton (e.g. "Sol Ring").
    - `CutLabLegality.LegalMax("Sol Ring") == 1`.
    - New xUnit tests cover all three basic categories, the synthetic factory, and both legality branches.
  </acceptance_criteria>
  <done>Basics resolve from constants, added basics can be synthesized as land ScryfallCardData, and legality distinguishes legal-multiples from singletons.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: CutLabWorkingList.Derive adjustment overload</name>
  <read_first>
    - DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs (the file being modified — add overload, keep old delegating)
    - DeckFlow.Web/Services/CutLab/CutLabBasicLands.cs + CutLabLegality.cs (from Task 2 — used for materialization + clamp cap)
    - DeckFlow.Web.Tests/CutLabWorkingListTests.cs (analog test file for Derive behavior)
  </read_first>
  <behavior>
    - Compose order: apply Decisions first (unchanged), THEN fold adjustments onto the result.
    - Existing entry + delta: quantity becomes clamp(qty + delta, 0, CutLabLegality.LegalMax(name)); an entry reaching 0 is dropped.
    - Added-basic (IsAddedBasic true) with no matching entry: a new CutLabPoolCard land entry is materialized from CutLabBasicLands with Quantity = max(delta, 0); a zero/negative net is dropped.
    - Added-basic that later also matches an existing entry name folds by name (no duplicate entry).
    - Old two-arg Derive returns identical output to the three-arg overload called with an empty adjustment list.
  </behavior>
  <action>
    Add `public static IReadOnlyList<CutLabPoolCard> Derive(IReadOnlyList<CutLabPoolCard> pool,
    IReadOnlyList<CutLabDecision> decisions, IReadOnlyList<CutLabQuantityAdjustment> adjustments)`. Implement:
    compute the decision-derived list exactly as today, then fold adjustments grouped by normalized name (use
    CutLabCardNames comparer). For a name matching a derived entry, set Quantity = Math.Clamp(qty + netDelta, 0,
    CutLabLegality.LegalMax(name)); drop entries whose result is 0. For an IsAddedBasic name with no derived
    entry and net delta > 0, materialize a CutLabPoolCard { Name, Quantity = netDelta, TypeLine from
    CutLabBasicLands, IsCommander=false, IsLocked=false } — clamp to LegalMax. Ignore adjustments whose name is
    neither a derived entry nor a valid added basic (defense in depth; endpoint already validates). Change the
    existing two-arg `Derive` body to delegate: `Derive(pool, decisions, [])`. Preserve deterministic ordering
    (existing pool order first, materialized basics appended in a stable order).
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabWorkingListTests" 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - `CutLabWorkingList.Derive` has an overload taking `IReadOnlyList<CutLabQuantityAdjustment>`; the two-arg overload delegates to it with an empty list.
    - Tests prove: fold on existing entry, clamp-to-zero drops the entry, added-basic materialization as a land, and compose-with-decisions order (decisions applied before adjustments).
    - A regression test asserts old two-arg Derive output equals three-arg-with-empty output for a representative pool.
    - `dotnet build DeckFlow.Web` clean; CutLabWorkingListTests filter all-green.
  </acceptance_criteria>
  <done>Derive is the single fold point for decisions + adjustments; back-compat overload intact.</done>
</task>

</tasks>

<verification>
- `dotnet build DeckFlow.sln` clean, no new warnings.
- New/updated xUnit suites green: CutLabStateSerializerTests, CutLabWorkingListTests, CutLabBasicLandsTests, CutLabLegalityTests.
- CarveOutGuard test green (no init→get-only churn).
- LF endings preserved; `scripts/format-check-changed.sh staged` clean on changed lines.
</verification>

<success_criteria>
The adjustment model persists (bounded/clamped/back-compat), basics resolve from constants and can be
synthesized as land ScryfallCardData for lookup-free analysis, legality distinguishes legal-multiples from
singletons, and Derive folds adjustments onto the decision-derived list as the single source of truth — with the
old Derive overload unchanged in behavior.
</success_criteria>

<line_endings>
Every touched file: preserve its existing line endings exactly (repo enforces LF via .gitattributes). Change only
lines whose content actually changes; leave all other lines byte-for-byte identical. Do not normalize or reflow
untouched code. New files use LF.
</line_endings>

<output>
Create `.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-01-SUMMARY.md` when done.
</output>

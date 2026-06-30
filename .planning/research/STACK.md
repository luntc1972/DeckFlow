# Stack Research

**Domain:** Cycle 13 — Bracket Classifier + Balancer, Multi-Axis Deck Score, Auto-Refreshing Primer, Tap Analyzer Surface
**Researched:** 2026-06-27
**Confidence:** HIGH (existing codebase verified directly; Scryfall API verified via live curl; bracket definitions verified against WotC official announcement pages)

---

## Scope

This research covers ONLY the STACK ADDITIONS needed for the 4 Cycle 13 features. The pinned stack
(ASP.NET 10, Razor MVC, RestSharp + Polly v8, Npgsql/SQLite, IMemoryCache, Serilog) is not
re-researched. All four features can be built entirely from existing in-solution dependencies.

**Verdict: zero new NuGet packages. Zero new npm packages.**

---

## Feature 1 — Bracket Classifier + Balancer

### Data Source: Game Changers List

**Authoritative source:** Scryfall `is:gamechanger` search filter.

Scryfall tracks the official WotC Commander Game Changers list and updates within hours of each WotC
announcement. The API endpoint is clean JSON:

```
GET https://api.scryfall.com/cards/search?q=is%3Agamechanger&order=name&unique=names
```

Response shape (verified live, June 2026):
```json
{
  "object": "list",
  "total_cards": 53,
  "has_more": false,
  "data": [
    { "name": "Ad Nauseam", "oracle_id": "...", "type_line": "Instant", ... },
    ...
  ]
}
```

53 cards as of June 2026. The 53 cards include (not exhaustive):
Ad Nauseam, Ancient Tomb, Aura Shards, Biorhythm, Bolas's Citadel, Braids (Cabal Minion),
Chrome Mox, Coalition Victory, Consecrated Sphinx, Crop Rotation, Cyclonic Rift, Demonic Tutor,
Drannith Magistrate, Enlightened Tutor, Farewell, Field of the Dead, Fierce Guardianship,
Force of Will, Gaea's Cradle, Gamble, Gifts Ungiven, Glacial Chasm, Grand Arbiter Augustin IV,
Grim Monolith, Humility, Imperial Seal, Intuition, Jeska's Will, Lion's Eye Diamond, Mana Vault,
Mishra's Workshop, Mox Diamond, Mystical Tutor, Narset (Parter of Veils), Natural Order,
Necropotence, Notion Thief, Opposition Agent, Orcish Bowmasters, Panoptic Mirror, Rhystic Study,
Seedborn Muse, Serra's Sanctum, Smothering Tithe, Survival of the Fittest, Teferi's Protection,
The One Ring, The Tabernacle at Pendrell Vale, Underworld Breach, Vampiric Tutor, Worldly Tutor.

(Two more names were added in Feb 2026: Farewell and Biorhythm.)

**Ingestion mechanism:** A new `IGameChangersService` in `DeckFlow.Web/Services/`, following the
exact pattern of the existing `ICommanderBanListService`:
- RestSharp GET to the Scryfall search endpoint (reusing the `scryfall-rest` named `IHttpClientFactory`
  client and the existing `scryfall` `ResiliencePipeline<RestResponse>`)
- Cache result in `IMemoryCache` with 24-hour TTL (the list changes at most a few times per year)
- Return `IReadOnlyList<string>` of card names
- `ScryfallThrottle.ExecuteAsync` wraps the call (same as all other Scryfall calls)

**No new NuGet package required.** The RestSharp + Polly + IMemoryCache pattern is already in-place.

**Refresh cadence:** 24-hour IMemoryCache TTL is sufficient. Updates happen at official bracket
announcements (roughly quarterly). If exact real-time freshness is needed, bump TTL to 6 hours —
still zero new dependency. Do NOT store the list in Postgres; it is read-only reference data.

### Bracket Definitions (to hardcode)

The 5-tier WotC bracket system. Definitions are stable (rules updated quarterly at most):

| Bracket | Name | Game Changers | Two-Card Combos | Mass Land Denial | Extra Turns |
|---------|------|---------------|-----------------|------------------|-------------|
| B1 | Exhibition | 0 | Prohibited | Prohibited | Prohibited |
| B2 | Core | 0 | Prohibited | Prohibited | Minimal, not chained |
| B3 | Upgraded | 1–3 | No early-game | Prohibited | Allowed |
| B4 | Optimized | Any | Allowed | Allowed | Allowed |
| B5 | cEDH | Any | Allowed | Allowed | Allowed |

Bracket assignment algorithm (pure C# in `DeckFlow.Core/Bracket/BracketClassifier.cs`):
1. Count Game Changers in deck (intersect normalized card names with the cached list from Feature 1).
2. Count two-card combos via Commander Spellbook results (reuse existing `ICommanderSpellbookService`).
3. Check for mass land denial cards (short hardcoded list: Armageddon, Ravages of War, Catastrophe,
   Obliterate, Jokulhaups, Cataclysm, Winter Orb — these are not Game Changers but are B1/B2
   prohibited).
4. Check for extra-turn spells (hardcoded list: Time Warp, Temporal Manipulation, Capture of Jingzhou,
   Temporal Mastery, Nexus of Fate, etc.).
5. Apply bracket rules top-down: any condition that disqualifies a lower bracket escalates to next.

**Balancer prompt**: after computing bracket, build a "cuts to reach bracket N" suggestion list:
- Flag each Game Changer present, sorted by replaceability
- Flag two-card combos present (Spellbook data already supplies this)
- This feeds a new prompt artifact (ChatGpt/Claude/Gemini variants, per ADR-0001)

**No new NuGet package required.** `ICommanderSpellbookService` already exists and is already
called from `DeckPrimerPacketService`.

---

## Feature 2 — Multi-Axis Deck Score

### Score Dimensions (all computable from existing data)

| Axis | 0–5 scale | Input Source | Already Available? |
|------|-----------|-------------|-------------------|
| Power | Combo density + Game Changers count + tutor count | CommanderSpellbook (combos), Game Changers from Feature 1, card name/type matching | YES (after F1) |
| Speed | Avg MV + ramp quantity + fast mana count | `ManabaseDeck.AverageManaValue`, `ManabaseDeck.RampAndDrawUnderThree`, `ManabaseDeck.FastMana` | YES — already in ManabaseDeck |
| Control | Interaction count (removal + counterspells) | `CategoryKnowledgeStore` category labels ("Removal", "Counterspell", "Interaction") | YES |
| Consistency | Tutor count + draw count + single-combo redundancy | `ManabaseDeck.DrawPieceCount`, tutor count (name/oracle text), Spellbook near-combos | YES |

All four axis inputs come from existing computed data. No new external API call needed.

**Implementation:** A new static class `DeckScorer` in `DeckFlow.Core/Bracket/DeckScorer.cs`:
- Pure function: `DeckScore Score(ManabaseDeck deck, IReadOnlyList<SpellbookCombo> combos, IReadOnlyList<string> gameChangers, IReadOnlyList<CategoryKnowledgeEntry> categories)`
- Returns a `sealed record DeckScore(int Power, int Speed, int Control, int Consistency)` with
  each axis 0–5 and a derived `int Overall` (average, rounded)
- Scorer thresholds hardcoded with named constants (same pattern as manabase mode thresholds)

**Integration point:** `DeckScorer` is called from `DeckAnalysisPacketService` and the new
Bracket service. Scores fold into the paste artifact packet as a new section in the prompt text.

**No new NuGet package required.**

---

## Feature 3 — Auto-Refreshing Primer

### Deck Fingerprint

A deck fingerprint is already computable: `PacketSessionCache.ComputeKey(fieldBag)` performs
SHA-256 over a deterministic JSON-serialized field bag (`System.Security.Cryptography.SHA256`,
BCL — no package needed). The fingerprint for auto-refresh uses only the deck content fields
(normalized decklist text or URL), NOT the section selections or AI platform, so only actual
deck changes trigger staleness.

**Storage:** The existing `PacketArtifactStore` persists primer zip artifacts to the `/data`
disk. Add a new column `deck_fingerprint TEXT` to the primer artifact metadata row. On each
primer generation, store the fingerprint alongside the artifact. On each primer page load:

1. Hash the current deck input (URL or text, normalized).
2. Query the stored fingerprint for the most recent artifact for this commander/deck.
3. If hashes differ → show "Stale — your deck changed since this primer was written. Regenerate?"
4. If hashes match → show artifact as current.

**Schema change:** One new column on the existing `packet_artifacts` table (or equivalent
primer-specific table). Uses the existing `IRelationalDialect` + `RelationalDatabaseConnection`
dialect-pluggable pattern. No new ORM or migration framework.

**Staleness signal in the prompt artifact:** When the user regenerates, the new artifact
replaces the old one and updates the stored fingerprint. The AI artifact itself can include
a "primer generated on [date] for deck version [short hash]" line — uses existing `DateTime`
and `Convert.ToHexString` (BCL only).

**No new NuGet package required.** `System.Security.Cryptography.SHA256` is BCL; the rest
reuses `PacketArtifactStore` + `RelationalDatabaseConnection`.

---

## Feature 4 — Tap Analyzer Surface

### What Already Exists

The manabase engine already models untapped vs. tapped:

- `ManaSource.EntersUntapped` (bool, `ManabaseModels.cs:27`) — set by `ManabaseClassifier.EntersTapped()` from
  oracle text heuristics.
- `ManabaseAnalyzer.EffectiveSources(deck, color, untappedOnly: true)` — already called internally
  for turn-1 color access in `Analyze()` (`ManabaseAnalyzer.cs:442-443`).
- `CastabilitySimulator` tracks `CardKind.UntappedLand` vs `CardKind.TappedLand` in every trial.

**What is missing:** These computed values are not surfaced in `ManabaseReport`. They are computed
and used internally but discarded before the report is returned.

### What to Add

New fields on `ManabaseReport` (no schema change, no API change — just new init-properties on the
existing sealed record):

```csharp
/// <summary>Count of lands that enter untapped in the deck.</summary>
public int UntappedLandCount { get; init; }

/// <summary>Count of lands that enter tapped in the deck.</summary>
public int TappedLandCount { get; init; }

/// <summary>
/// Fraction of lands that enter untapped (0.0–1.0). Convenience for the view.
/// </summary>
public double UntappedLandFraction => UntappedLandCount + TappedLandCount > 0
    ? (double)UntappedLandCount / (UntappedLandCount + TappedLandCount) : 0;

/// <summary>
/// Per-color untapped source count for turn-1 access (the count that
/// ManabaseAnalyzer already computes via EffectiveSources(untappedOnly: true)).
/// Keyed by ManaColor; empty for colorless/mono decks that never call turn-1 check.
/// </summary>
public IReadOnlyDictionary<ManaColor, double> UntappedSourcesByColor { get; init; }
    = new Dictionary<ManaColor, double>();
```

The `ManabaseAnalyzer.Analyze()` method already computes `untappedSources` per color in its
`BuildColorFindings` loop — it just needs to be captured and set on the report instead of
discarded. The land counts come from iterating `deck.Sources` and counting
`IsLand && EntersUntapped` vs. `IsLand && !EntersUntapped`.

**No new NuGet package required.** All computation is pure C# in `DeckFlow.Core`.

**UI surface:** The existing `/manabase` page renders `ManabaseReport`. The new fields render
as a new "Land Quality" row in the land-count table and a per-color untapped breakdown column
in the color-source table. Uses existing Razor + site-common.css; no TypeScript changes needed.

**Paste artifact integration:** `ManabaseReportTextBuilder` and `ManabaseSwapPromptBuilder`
already emit the manabase data as text blocks. Add "untapped land %, turn-1 untapped by color"
lines to the text output. These feed the deck analysis paste packet.

---

## Recommended Stack Summary

### Core Technologies (all in-solution — no changes)

| Technology | Version | Purpose | Notes |
|------------|---------|---------|-------|
| ASP.NET Core MVC 10.0 | 10.0 | HTTP controllers + Razor views | Pinned; no change |
| RestSharp | 114.0.0 | HTTP client for all upstream calls including new Game Changers fetch | Reuse `scryfall-rest` named client |
| Polly v8 | 8.x | Resilience for Game Changers Scryfall call | Reuse existing `scryfall` named pipeline |
| IMemoryCache (BCL) | built-in | 24-hour cache for Game Changers list | Same as banlist cache pattern |
| System.Security.Cryptography (BCL) | built-in | SHA-256 for deck fingerprint (Auto-Refresh Primer) | Already used in `PacketSessionCache` |
| RelationalDatabaseConnection | in-solution | Store primer deck fingerprint | One new column, existing dialect |
| DeckFlow.Core/Manabase | in-solution | Tap Analyzer — expose untapped metrics | New fields on `ManabaseReport` |

### Supporting Libraries (all already in solution)

| Library | Used For | Feature |
|---------|---------|---------|
| `ICommanderSpellbookService` | Combo count for Bracket Classifier + Power axis | F1, F2 |
| `ICategoryKnowledgeStore` | Interaction/draw counts for Multi-Axis Score | F2 |
| `ManabaseDeck.AverageManaValue`, `FastMana`, `RampAndDrawUnderThree` | Speed axis | F2 |
| `PacketArtifactStore` | Store primer fingerprint | F3 |
| `PacketSessionCache.ComputeKey()` | Deck fingerprint hashing | F3 |
| `ScryfallThrottle` | Rate-gate the Game Changers refresh call | F1 |

### What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Any external bracket API or WotC scraper | WotC has no bracket API; their pages are not stable scrape targets; the bracket RULES change rarely | Hardcode bracket rules as C# constants; fetch only the Game Changers CARD LIST via Scryfall `is:gamechanger` |
| MTGGoldfish / EDHREC card-power APIs | No public machine-readable API; rate-limit hostile; single-source risk | Pure in-house scoring on parsed deck composition, already-available Scryfall data, and SpellbookService combos |
| A dedicated card-tagging service for bracket card classification | Adds a new external dependency with no uptime guarantee | Use Scryfall `is:gamechanger` (already proven, already integrated) + hardcoded lists for mass land denial / extra turns |
| Any scoring NuGet library | No .NET library covers EDH-specific scoring axes | Pure domain logic in `DeckFlow.Core/Bracket/DeckScorer.cs` |
| An ORM (EF Core, Dapper) for the primer fingerprint column | Already ruled out by project patterns; existing `RelationalDatabaseConnection` + raw SQL suffices | Existing `IRelationalDialect` + inline SQL |
| `Microsoft.Extensions.Http.Resilience` standard handler | Project constraint: do NOT migrate from RestSharp + Polly v8 direct pattern | RestSharp + Polly v8 (existing) |

---

## Integration Map

```
Cycle 13 Features — Integration with Existing Code
═══════════════════════════════════════════════════

Feature 1: Bracket Classifier
  ← IGameChangersService (new, DeckFlow.Web/Services)
      uses: RestSharp scryfall-rest + Polly scryfall + IMemoryCache (all existing)
      calls: Scryfall api.scryfall.com/cards/search?q=is:gamechanger
  ← BracketClassifier (new, DeckFlow.Core/Bracket)
      uses: IGameChangersService + ICommanderSpellbookService (existing) + hardcoded lists
  → DeckBracketPacketService (new) → 3 prompt variants (ChatGpt/Claude/Gemini, ADR-0001)

Feature 2: Multi-Axis Deck Score
  ← DeckScorer (new, DeckFlow.Core/Bracket)
      uses: ManabaseDeck (existing fields) + BracketClassifier output (F1) +
            CommanderSpellbook combos (existing) + CategoryKnowledgeStore (existing)
  → score fields on DeckAnalysisPacketResult (existing record, new fields)
  → 3 decoupled prompt variant blocks (per ADR-0001)

Feature 3: Auto-Refreshing Primer
  ← PacketSessionCache.ComputeKey() (existing SHA-256 primitive)
      scoped to decklist only (URL or normalized text, not sections/platform)
  ← PacketArtifactStore schema: new deck_fingerprint column (existing dialect)
  → staleness badge in DeckPrimerViewModel (new bool DeckChanged)
  → "Stale" UI state triggers regenerate; artifact includes generation date + short hash

Feature 4: Tap Analyzer Surface
  ← ManabaseAnalyzer.Analyze() (existing)
      - capture untappedSources (already computed, currently discarded)
      - count IsLand&&EntersUntapped vs IsLand&&!EntersUntapped (deck.Sources loop)
  → new fields on ManabaseReport: UntappedLandCount, TappedLandCount,
      UntappedSourcesByColor (dict, new; computed alongside existing ColorFindings)
  → existing /manabase Razor view: new "Land Quality" row
  → ManabaseReportTextBuilder: new lines in text output for paste artifact
```

---

## Data Sources (Authoritative)

### Bracket Definitions

Source: WotC official announcement pages (not an API — read as documentation):
- Introduction: https://magic.wizards.com/en/news/announcements/introducing-commander-brackets-beta
- April 2025 update: https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-april-22-2025
- October 2025 update: https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-october-21-2025
- February 2026 update: https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-february-9-2026

**These are read-only documentation** — hardcode the bracket rule set in C# constants and update
them when WotC publishes a new announcement (roughly quarterly). The CARD LIST is not hardcoded;
only the RULES are.

### Game Changers Card List

Source: Scryfall `is:gamechanger` search (live, JSON, no auth required):
```
https://api.scryfall.com/cards/search?q=is%3Agamechanger&order=name&unique=names
```
- Returns `total_cards: 53` as of June 2026 (verified via live curl)
- Scryfall updates this within hours of WotC announcements
- Confidence: HIGH — Scryfall explicitly maintains this as a tracked filter

---

## Version Compatibility

All Cycle 13 features use BCL types and in-solution packages. No new version-pinning is needed.

| Component | Package | Version | Note |
|-----------|---------|---------|------|
| SHA-256 fingerprint | System.Security.Cryptography | BCL | Already used in PacketSessionCache |
| Scryfall Game Changers API | RestSharp | 114.0.0 (existing) | Reuse scryfall-rest named client |
| Manabase untapped surface | DeckFlow.Core | in-solution | New fields on existing record |
| Bracket/Scoring logic | DeckFlow.Core | in-solution | New classes in new Bracket/ subfolder |

---

## Sources

- `DeckFlow.Core/Manabase/ManabaseModels.cs` — verified `EntersUntapped` on `ManaSource`; `IReadOnlyDictionary<ManaColor, double>` pattern
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs:442-443` — verified `untappedSources` already computed per-color; confirmed it is currently discarded
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs:384` — verified `UntappedLand`/`TappedLand` enum in simulator
- `DeckFlow.Web/Services/CommanderBanListService.cs` — verified the RestSharp + IMemoryCache pattern to replicate for `IGameChangersService`
- `DeckFlow.Web/Services/PacketSessionCache.cs:57` — verified `SHA256.HashData` already in use
- `DeckFlow.Web/Models/DeckPrimerRequest.cs` — verified existing `TargetCommanderBracket` field on primer request
- `DeckFlow.Web/Services/PromptBuilders/Primer/ChatGptPrimerPromptVariant.cs` — confirmed ADR-0001 three-variant decoupled prompt pattern applies to bracket/score artifacts too
- [Scryfall `is:gamechanger` API](https://api.scryfall.com/cards/search?q=is%3Agamechanger&order=name&unique=names) — live curl returning `total_cards: 53`, confirmed JSON structure
- [WotC Commander Brackets introduction](https://magic.wizards.com/en/news/announcements/introducing-commander-brackets-beta) — B1 Exhibition / B2 Core / B3 Upgraded (≤3 GC) / B4 Optimized / B5 cEDH definitions
- [WotC October 2025 bracket update](https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-october-21-2025) — 10 cards removed; confirmed 47 remaining post-update
- [WotC February 2026 bracket update](https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-february-9-2026) — Farewell + Biorhythm added; total 53 cards confirmed
- [ScrollVault Game Changers list](https://scrollvault.net/guides/game-changers.html) — cross-checked 53 card count, June 2026
- [Scryfall is:gamechanger search](https://scryfall.com/search?q=is%3Agamechanger) — confirmed Scryfall tracks the WotC list as a named filter

---
*Stack research for: DeckFlow Cycle 13 — Bracket Classifier, Multi-Axis Score, Auto-Refreshing Primer, Tap Analyzer*
*Researched: 2026-06-27*

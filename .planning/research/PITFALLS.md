# Pitfalls Research

**Domain:** Adding 4 deck-evaluation features (Bracket Classifier + Balancer, Multi-Axis Deck Score, Auto-Refreshing Primer, Tap Analyzer) to an existing ASP.NET 10 Razor Commander/cEDH paste-artifact engine
**Researched:** 2026-06-27
**Confidence:** HIGH for codebase-integration pitfalls (traced to specific files); MEDIUM for the WotC bracket/Game-Changers domain pitfalls (the official list is externally maintained and changes — verified the feature exists in incumbents via the commander-feature-wants report, but the exact card list and update cadence is a moving target)

These pitfalls are specific to ADDING these 4 features to DeckFlow as it exists today. The core-value constraint dominates everything: **every artifact must paste into ChatGPT/Claude/Gemini and produce a useful answer in one round-trip with zero reformatting.** A feature that produces a subtly-wrong artifact is worse than no feature.

---

## Critical Pitfalls

### Pitfall 1: The Bracket / Game-Changers Staleness Treadmill

**What goes wrong:**

The official WotC Commander bracket system (B1–B5) and its associated **Game Changers list** are externally maintained by Wizards and the Commander format team, and they change over time (cards are added/removed from the Game Changers list; bracket definitions get reworded). If DeckFlow hard-codes the Game Changers card list and the bracket rubric into C# constants, then every WotC update silently makes DeckFlow's bracket classification *wrong* — a deck DeckFlow calls "Bracket 2" is actually Bracket 3 because a card it runs was just added to Game Changers. Because the output is a paste artifact the user trusts, a wrong classification is a credibility hit, not a cosmetic bug. Worse, you enter a maintenance treadmill: every WotC announcement becomes an emergency code change + redeploy.

**Why it happens:**

The fastest way to ship is a `static readonly string[] GameChangers = { ... }` baked into the assembly, mirroring how `CommanderBracketCatalog.cs` already hard-codes the five bracket option records. That works on day one and rots silently. There is no upstream API DeckFlow already consumes for this; the list lives in WotC articles and community trackers.

**How to avoid:**

- **Externalize the Game Changers list and bracket rubric as DATA, not code.** Put it in a versioned data file (JSON under `/data` or a DB table) with a `source_version` / `effective_date` stamp, loaded at startup — the same pattern the codebase already uses for `ContentKbSeedLoader` (seed JSON + `EnsureSchemaAsync`). Updating the list then becomes editing a data file, not a code change.
- **Stamp the classification artifact with the list version and date** ("Classified against WotC Game Changers list dated YYYY-MM-DD") so a stale classification is self-disclosing in the pasted output. This matches the existing manabase honesty pattern (`UnsupportedInteractions` discloses what the model approximates).
- **Make the artifact resilient to staleness by design:** have the prompt itself instruct the AI to re-confirm Game Changers membership ("the following cards were Game Changers as of <date>; flag any you believe have since changed"). The one-round-trip artifact then degrades gracefully instead of asserting a stale fact.
- **Do NOT scrape WotC live per-request.** That adds an upstream dependency, a Polly pipeline, a 512MB-cap memory cost, and a new failure mode for the core path. A periodically-refreshed local data file is correct.

**Warning signs:**

- A Game Changers card list appears as a `string[]`/`HashSet<string>` literal inside a `.cs` file.
- The classifier has no version/date field in its output.
- A WotC bracket-update article requires a code edit + redeploy to honor.

**Phase to address:** Bracket Classifier + Balancer phase (the earliest of the four). The data-file externalization + version stamping must be a design requirement of that phase, not a later fix — retrofitting a baked-in list into a data file is a rewrite.

---

### Pitfall 2: Mis-Detecting Bracket Gating Mechanics (Mass Land Denial, Extra Turns, 2-Card Combos, Game Changers)

**What goes wrong:**

The bracket rubric gates on specific mechanics: mass land denial, chained extra-turn effects, and infinite/early 2-card combos push a deck up brackets; Game Changers count has hard thresholds. DeckFlow's existing detection is **oracle-text substring matching** (`DeckStatClassifier.IsClosingPowerCard` matches `"extra turn"`, `IsBoardWipeCard` matches `"destroy all"`). Substring matching is both over- and under-inclusive: it will miss *Armageddon* as mass-land-denial (it says "Destroy all lands" — matches the board-wipe heuristic but is a *different* gate), false-positive on cards that say "you can't take extra turns," and completely miss 2-card combos (which require relationship data, not single-card text). A mis-detected gating mechanic produces a wrong bracket, which (per Pitfall 1) is a trust-breaking artifact.

**Why it happens:**

The substring classifiers in `DeckStatClassifier.cs` were built for advisory role-counts (ramp/draw/interaction tallies) where a few false hits don't matter. Reusing them for bracket gating — where a single Armageddon flips a bracket — applies a fuzzy tool to a precise job. 2-card combo detection specifically needs the Commander Spellbook relationship data, which DeckFlow already integrates (`ICommanderSpellbookService.FindCombosAsync`) but which returns `null` on failure (graceful degradation) — so a combo gate can silently under-count.

**How to avoid:**

- **For the Game Changers gate, use exact card-name matching against the externalized list (Pitfall 1), not oracle text.** Game Changers is a named-card list; match names, normalized via the existing `CardNormalizer`.
- **For mass-land-denial / extra-turns, maintain a small curated named-card list (data file)** rather than oracle-text heuristics — the universe of format-relevant cards here is small and well-known (Armageddon, Ravages of War, Jokulhaups, Time Warp family, etc.). A curated list is auditable and avoids the substring false-positive class.
- **For 2-card combos, lean on the existing Commander Spellbook integration but treat `null` as "unknown, disclose it," not "zero combos."** The artifact must say "combo detection unavailable for this run" rather than silently asserting a combo-free deck — otherwise the bracket is wrong AND the user can't tell.
- **Make the AI a second check:** the paste artifact should list what DeckFlow detected and ask the AI to confirm/augment, leveraging the one-round-trip model so the AI catches DeckFlow's detection gaps.

**Warning signs:**

- Bracket gating reuses `DeckStatClassifier` substring methods directly.
- Armageddon-class cards classify identically to ordinary board wipes.
- Spellbook `null` (API down) silently yields "no combos" in the bracket result.

**Phase to address:** Bracket Classifier + Balancer phase. The detection-precision approach (named lists + Spellbook-null disclosure) is a core design decision of that phase.

---

### Pitfall 3: "Balancing" Subjectivity — Over-Promising Which Cuts Hit a Target Bracket

**What goes wrong:**

The headline differentiator is bracket *balancing* — "cuts to move this deck to target Bracket N." But which cuts move a deck between brackets is genuinely subjective and contextual (cutting one Game Changer might drop a bracket; cutting a tutor might not). If DeckFlow's artifact asserts deterministic cuts ("Cut these 4 cards to reach Bracket 2") and the cuts are wrong or debatable, the tool looks authoritative-but-wrong — the worst outcome for a trust product. Bracket definitions are partly social/rules-committee guidance, not a closed formula.

**Why it happens:**

It's tempting to compute a crisp "cut list" because that's the demo-able artifact. But the bracket system intentionally has soft edges (the rules committee describes intent, not a checklist), so any deterministic balancer over-claims precision it cannot have.

**How to avoid:**

- **Frame the balancer as "candidate cuts for the AI to evaluate," not a verdict.** DeckFlow identifies the *gating mechanics present* (e.g., "you run 5 Game Changers; Bracket 2 allows 0; these are the 5") and hands the AI a structured ask to recommend cuts. This plays to the one-round-trip strength rather than fighting the subjectivity.
- **Anchor cuts to the OBJECTIVE gates** (Game Changers count, mass-land-denial presence, extra-turn presence) where the rubric IS crisp, and explicitly defer the SUBJECTIVE balancing (overall power feel) to the AI.
- **Never present a cut list as final/authoritative** in the artifact prose. Use language like "these cards trigger the Bracket-N ceiling" not "cut these to reach Bracket N."

**Warning signs:**

- The artifact contains an imperative deterministic cut list with no AI-evaluation framing.
- Balancing logic encodes power judgments (not just objective-gate membership) in C#.

**Phase to address:** Bracket Classifier + Balancer phase, specifically the artifact-prose design (which must be triplicated per ADR-0001 — see Pitfall 9).

---

### Pitfall 4: Multi-Axis Scores With Arbitrary, Unjustifiable Weights That Don't Match Player Intuition

**What goes wrong:**

Power / Speed / Control / Consistency on a 0–5 scale requires turning card data into four numbers. The failure mode: the axis formulas use weights pulled from nowhere ("interaction × 0.3 + counterspells × 0.5"), nobody can justify them, and the scores don't match what an experienced player feels about the deck. A cEDH player sees their tuned deck scored "Consistency 3/5" and dismisses the whole tool. Because the score lands in the paste packet, a wrong-feeling score taints the artifact's credibility.

**Why it happens:**

Multi-axis scoring looks like a tidy weighted-sum problem, so developers invent weights to make it compile. There's no ground-truth dataset to calibrate against, and the four axes overlap (fast mana feeds both Speed and Power), so naive sums double-count. The codebase already has the raw tallies (`FastMana`, ramp/draw/interaction counts via `DeckStatClassifier`) which makes it *easy* to slap together an unjustified formula.

**How to avoid:**

- **Derive axes from observable, defensible signals and DOCUMENT the rationale inline** (`// Why:` per the project comment convention). Speed ← fast-mana count + avg MV + manabase cast-turn (DeckFlow HAS the castability sim output `AverageDelay` / `CastPercent`); Consistency ← tutor count + combo density + card-advantage count; etc. Tie each axis to data DeckFlow actually computes.
- **Calibrate against the bracket as a sanity anchor:** a Bracket 5 deck should generally score higher on Power/Speed than a Bracket 2 deck. If the multi-axis scores contradict the bracket classification on the same deck, the weights are wrong. Add a cross-check test.
- **Under-claim precision: present 0–5 as coarse bands with labels, not false-decimal precision.** Bands ("Speed: 4/5 — explosive") read as judgment, not as spurious measurement.
- **Let the AI re-evaluate:** include the raw signals (not just the final score) in the artifact so the AI can correct an off score — same disclosure pattern as the manabase model.
- **Avoid double-counting across axes:** be explicit about which signal feeds which axis; fast mana should not naively inflate both Power and Speed without acknowledgment.

**Warning signs:**

- Axis weights are magic numbers with no comment explaining them.
- A known cEDH list scores low on Consistency, or a battlecruiser deck scores high on Speed.
- Multi-axis scores contradict the bracket classification for the same deck.

**Phase to address:** Multi-Axis Deck Score phase. The "derive from existing computed signals + document rationale + bracket cross-check" approach is the core design requirement.

---

### Pitfall 5: Multi-Axis Scoring Needs Card-Level Data DeckFlow May Not Have for Every Card

**What goes wrong:**

The axes need per-card attributes (is-tutor, is-interaction, is-fast-mana, MV, color pips). DeckFlow enriches via Scryfall, but Scryfall calls are throttled (`ScryfallThrottle`, ~5 req/s, global static gate) and can fail/timeout. If the scorer requires full card data for all ~100 cards and Scryfall is slow or partial, the score is computed on incomplete data and is silently wrong, or the request blows the request timeout. A score computed on 80% of the deck is not disclosed as such.

**Why it happens:**

The scorer is downstream of the same Scryfall pipeline that already gates the analysis path. Under load or upstream failure, partial enrichment yields partial scores with no signal to the user.

**How to avoid:**

- **Reuse the existing per-request packet session cache** (the v1.3 cache keyed by request hash that eliminated Scryfall replay on preview→download) so the scorer shares enrichment with the rest of the packet rather than issuing its own Scryfall storm.
- **Disclose coverage in the artifact:** "Scored on 97/100 cards (3 unresolved)" — mirror the existing `UnsupportedInteractions` honesty pattern. Never present a score as complete when enrichment was partial.
- **Degrade gracefully, never throw:** a missing card lowers confidence, it does not fail the packet. Follow the established `FindCombosAsync`-returns-null graceful-degradation convention.

**Warning signs:**

- Scorer issues its own Scryfall calls outside the shared cache / outside `ScryfallThrottle`.
- No "cards scored / cards total" coverage number in the output.

**Phase to address:** Multi-Axis Deck Score phase (must share the existing enrichment cache + throttle).

---

### Pitfall 6: Auto-Refreshing Primer — Regeneration Cost and Thrashing on Tiny Edits

**What goes wrong:**

The primer artifact is the heaviest artifact DeckFlow builds (`DeckPrimerPacketService` is ~750 LOC, pulls category knowledge, Commander Spellbook combos, EdhTop16 matchups, builds 3 variants). "Auto-refresh on deck change" can mean re-running that whole pipeline. If it regenerates on every trivial edit (swap one basic land, reorder the list), you get: (a) repeated Scryfall/Spellbook/EdhTop16 upstream load, (b) CPU/memory churn against the 512MB cap, and (c) if any LLM/compute spend is ever wired in, real money per thrash. A user editing a deck live could trigger dozens of full regenerations.

**Why it happens:**

"Auto-refresh" naively maps to "regenerate whenever the deck text differs." But deck text differs on whitespace/order/quantity-display changes that don't change the deck. The primer pipeline is expensive and was built for explicit user-triggered builds, not continuous regeneration.

**How to avoid:**

- **Stale-FLAG by default, regenerate on EXPLICIT user action.** The cheap, correct design is: detect that the deck changed, mark the existing primer "stale," and show a "Regenerate" affordance — do NOT auto-rebuild. This closes the "primers decay" gap (the actual user need) without a regeneration treadmill. The expensive full rebuild stays user-initiated.
- **Compute staleness from a CANONICAL deck hash, not raw text.** Hash the normalized card multiset (name+quantity, sorted) via the existing `CardNormalizer` + the canonical decklist the codebase already builds (`BuildDecklistText`). Whitespace/order/printing changes must NOT flip the hash. `DeckPrimerPacketService` already has `TryComputeCacheKeyAsync` — extend that hash as the staleness key rather than inventing a new one.
- **Debounce / single-flight any auto-trigger** if auto-regeneration is ever added: one in-flight regeneration at a time, coalesce rapid edits.
- **Reuse the existing `IMemoryCache` + packet session cache** so an unchanged deck never re-hits upstreams.

**Warning signs:**

- Primer regenerates when only whitespace/order/quantity-display changed.
- Staleness key is raw textarea content, not a normalized multiset hash.
- Upstream (Scryfall/Spellbook/EdhTop16) call volume spikes during a live-edit session.

**Phase to address:** Auto-Refreshing Primer phase. "Stale-flag, not auto-rebuild" + "canonical-hash staleness key" are the two core design requirements.

---

### Pitfall 7: Primer Staleness False-Positives (and False-Negatives)

**What goes wrong:**

If the staleness check is too sensitive, every reopen flags "stale" even when nothing changed — users learn to ignore the flag (cry-wolf), defeating the feature. If too lax (e.g., hashes only the commander, or ignores sideboard/maybeboard moves), it misses real changes and shows a confidently-outdated primer. Both erode trust in the one artifact whose whole value proposition is "stays current."

**Why it happens:**

Getting the equivalence relation right is subtle: which deck mutations should invalidate a primer? Card swaps yes; printing/art changes no; reordering no; quantity changes yes. The existing zip round-trip already wrestled with "original vs canonical" deck text (a logged Key Decision) — the same alphabetize-vs-preserve mismatch can produce false staleness.

**How to avoid:**

- **Define the equivalence relation explicitly and test it:** golden tests asserting (reorder → not stale), (printing swap → not stale), (card swap → stale), (quantity change → stale). The codebase already has round-trip tests (`DeckPrimerResultRoundTripTests`) to extend.
- **Hash the same canonical representation the zip uses** so primer staleness and zip identity agree — don't invent a second normalization.
- **Show WHAT changed, not just "stale":** "3 cards changed since this primer" turns a vague flag into a trustworthy signal.

**Warning signs:**

- Reopening an unchanged deck flags stale.
- A printing/art swap flags stale.
- A real card swap does NOT flag stale.

**Phase to address:** Auto-Refreshing Primer phase (the staleness equivalence relation + its golden tests).

---

### Pitfall 8: Tap Analyzer — Misreading the Sim's Internal Tapped/Untapped State

**What goes wrong:**

The Tap Analyzer must surface "untapped-source frequency" and "opening-turn (turn-1) untapped availability." The castability Monte-Carlo sim (`CastabilitySimulator.cs`) already models tapped state internally with subtle, hard-won semantics: a tapped land played turn T is `OnlineTurn = currentTurn + 1` and contributes NOTHING that turn (FINDING-1 HIGH fix — tapped lands previously inflated mana+color). The board tracks `(colorMask, OnlineTurn)` per land. A naive Tap Analyzer that re-derives tap stats from `ManaSource.EntersUntapped` at the deck level (static count) will report a DIFFERENT, inconsistent number than what the sim actually experienced (which accounts for fetch/MDFC/sequencing). Two "untapped frequency" numbers that disagree in the same report is a credibility bug.

**Why it happens:**

There are two places tap state lives: the static deck classification (`EntersUntapped` flag per source, used by `EffectiveSources(untappedOnly: true)`) and the dynamic per-trial board inside the sim loop. They answer different questions (deck composition vs. realized draws). Picking the wrong one, or mixing them, yields a number that contradicts the cast-rate the sim already reports.

**How to avoid:**

- **Decide which question the metric answers and source it from ONE place.** "Opening-turn untapped availability" is a *realized* draw statistic → it MUST come from inside the sim loop (the turn-1 board state across trials), not from a static deck count. "Untapped-source frequency" (what fraction of color sources enter untapped) is a *composition* stat → from the static classification. Label each clearly so they're not confused.
- **Surface the sim's EXISTING internal state rather than recomputing.** The sim already knows, per trial, the turn-1 untapped board. Add accumulators to the existing loop (Pitfall 11) and emit them — do not build a parallel calculation that can drift from the sim's own cast-rate.
- **Reconcile against the cast-rate:** if the Tap Analyzer says "turn-1 untapped availability is great" but the sim's turn-1 cast rate is poor, one of them is wrong. Add a consistency test.

**Warning signs:**

- Tap stats come from `EntersUntapped` counts while cast-rate comes from the sim — and they tell contradictory stories.
- "Opening-turn" availability is computed without reference to the per-trial board.

**Phase to address:** Tap Analyzer phase. The "single source of truth, surfaced from existing sim state" decision is the core design requirement.

---

### Pitfall 9: The 3-Variant Decoupling (ADR-0001) Makes Every New Artifact Triplicate — and Easy to Drift

**What goes wrong:**

ADR-0001 mandates that ChatGPT/Claude/Gemini prompt variants stay decoupled — no shared helper (the prose duplication is intentional; see `PrimerPromptVariantRegistry` with three hand-maintained `*PrimerPromptVariant.cs`). Every new artifact in this cycle (bracket result, multi-axis score block, refreshed primer delta) must be authored THREE times. The failure mode: a developer adds the bracket block to the ChatGPT variant, forgets Claude/Gemini, and ships an artifact that's correct in one AI and missing/broken in the other two. Or "DRYs up" the triplication, violating the ADR and the carve-out that keeps the variants independently tunable. Both break the sacred core value (correct paste artifact) for 2 of 3 platforms.

**Why it happens:**

Triplication feels like a bug to a SOLID-trained developer, so the instinct is to extract a shared builder — exactly what the ADR forbids. Conversely, the manual 3x authoring is easy to do incompletely under time pressure.

**How to avoid:**

- **Treat "new artifact section" as a 3-variant checklist item, every time.** Add the section to ChatGPT, Claude, AND Gemini variants in the same change. The existing structure (`IPrimerPromptVariant` + per-AI files + registry) is the pattern to follow — extend each, do NOT add a shared base.
- **Add a per-variant parity TEST** (the codebase has `PrimerPromptVariantTests`, `AiPlatformPhase10RoundTripTests`) asserting each new section appears in all three variants. This catches the "forgot Gemini" drift mechanically.
- **Mind the Gemini paste cap (~30k):** new artifact sections add length; Gemini is flag-gated partly because the full packet can exceed its paste ceiling. Adding bracket + multi-axis + tap blocks to the primer could push Gemini over. Measure variant length; keep Gemini within budget or the flag stays off (acceptable, but must be a conscious call).
- **Do NOT refactor the three variants into one** to save effort — that violates ADR-0001 and the documented carve-out.

**Warning signs:**

- A new section exists in `ChatGpt*PromptVariant` but not the Claude/Gemini siblings.
- A PR adds a shared base class across the three variants.
- Gemini variant length crosses ~30k after adding new blocks.

**Phase to address:** EVERY feature phase that emits artifact text (Bracket, Multi-Axis, Primer-refresh). Each phase's success criteria must include "all 3 variants updated + parity test."

---

## Moderate Pitfalls

### Pitfall 10: Adding New Dependencies Without Approval (Bracket/Score Libs, Hash Libs)

**What goes wrong:** A developer reaches for a NuGet package (a fuzzy-matching lib for card names, a stats lib for scoring, a JSON-schema validator for the externalized bracket data). The project rule is explicit: **no new packages without asking.** A silent dependency add violates the constraint and inflates the 512MB-cap container.

**How to avoid:** Build on what's present — `CardNormalizer` for name matching, `System.Text.Json` for the bracket data file, plain C# for the weighted scores, `System.Numerics`/existing RNG for the sim. If a package genuinely seems needed, state name+version+why and ASK first.

**Phase to address:** All four phases (planning gate).

---

### Pitfall 11: Monte-Carlo Perf Regression From Extra Tap-Stat Accumulation in the Hot Loop

**What goes wrong:** The sim runs `DefaultTrials = 20_000` per spell. The Tap Analyzer adds accumulators (turn-1 untapped board, untapped-source-hit counts) inside that loop. If implemented carelessly — allocating per trial, LINQ inside the inner loop, boxing the `readonly struct LibraryCard` — it multiplies cost by (trials × spells) and can blow request latency and GC pressure against the 512MB cap.

**How to avoid:** Accumulate into pre-allocated primitive counters (int/long arrays), no allocations inside the trial loop, no LINQ in the hot path (the sim already uses structs and bit masks for this reason). Add the accumulation to the EXISTING single pass — do NOT run a second simulation pass for tap stats. Benchmark before/after on a representative deck.

**Warning signs:** Manabase/primer request latency rises after the Tap Analyzer lands; GC time climbs; a second sim pass appears.

**Phase to address:** Tap Analyzer phase.

---

### Pitfall 12: Feature-Flag Gating Omitted or Inconsistent Across the New Tools

**What goes wrong:** The project gates new tools behind flags (the namespaced `tool.*` / `analysis.* / manabase.*` flag scheme, with prod seeding OFF and operator flip). A new tool shipped without a flag, or with a flag that doesn't cascade to tile + nav + help (the Cycle 11 tool-visibility registry), goes live unguarded — and if its artifact is wrong, it's wrong in prod with no kill switch.

**How to avoid:** Each new feature gets a flag key in the established namespace, seeded OFF in prod (idempotent migration carries operator toggles across deploy — the known flag-key-namespacing pattern), wired into the tool-visibility registry so one toggle cascades to tile/nav/help. Follow the manabase-flag precedent (shipped OFF, operator flips).

**Phase to address:** Each feature phase (flag is part of the definition of done).

---

### Pitfall 13: Bracket/Score Numbers Disagreeing Across Tools (Internal Inconsistency)

**What goes wrong:** The same deck gets a bracket from the Bracket tool, a Speed axis from the Multi-Axis tool, and a cast-rate from the manabase tool — computed by separate code paths that can tell contradictory stories (Bracket 5 but Speed 2/5; "fast mana heavy" bracket but "slow" tap analysis). Users notice cross-tool contradictions immediately and lose trust.

**How to avoid:** Share the underlying signals (one `FastMana`/ramp/tutor tally feeding bracket, score, AND the manabase budget). Add cross-tool consistency tests on a few golden decks (a known cEDH list, a known battlecruiser list). Reuse `DeckStatClassifier` tallies as the single source feeding all three.

**Phase to address:** Multi-Axis phase (it sits between bracket and manabase; natural place for the cross-check), plus a milestone-level consistency test.

---

### Pitfall 14: CRLF / Format-Gate / Carve-Out Violations on New Files

**What goes wrong:** New `.cs` files or the externalized bracket JSON get written with CRLF (Windows `File.WriteAllText` default) or reflow existing lines, tripping the changed-lines format gate (CI `format-gate`) and the `.gitattributes` LF enforcement. Worse, a formatter auto-converts `{ get; init; }` → `{ get; }` on a new model — which silently breaks `System.Text.Json` deserialization of the externalized bracket data (the exact carve-out bug that broke `EdhTop16Client`), or re-indents a raw-string prompt literal, changing the bytes shipped to the AI (corrupting the sacred artifact).

**How to avoid:** New files LF-only; touch only the lines you change (changed-lines gate); honor the five carve-outs (`{ get; init; }` preserved, no attribute inlining, no raw-string-literal reindent, switch expressions preserved, xmldoc single-space). Run `scripts/format-check-changed.sh staged` locally; verify the bracket JSON loads round-trip. The `CarveOutGuard` test guards the carve-outs — keep it green.

**Warning signs:** `git diff --check` whitespace errors; format-gate CI red; a new prompt variant's raw-string literal got reindented; bracket JSON deserializes to nulls (get-only property symptom).

**Phase to address:** All four phases (mechanical gate on every change).

---

### Pitfall 15: New Artifact Sections Silently Bloating the Packet Past Paste Ceilings

**What goes wrong:** Bracket block + multi-axis block + tap block + refreshed primer all add length to the paste packet. ChatGPT/Claude tolerate large pastes; Gemini has a ~30k ceiling (the reason it's flag-gated). Cumulative additions can push even the analysis packet toward limits, truncating instructions mid-artifact — which silently breaks the one-round-trip guarantee (the AI gets a cut-off prompt).

**How to avoid:** Measure each variant's length after adding sections. Budget Gemini explicitly. Prefer compact structured blocks over prose where possible. Keep the "wait for AI to finish / truncated-JSON" UX (v1.3) in place for response-side truncation, but the prompt-side bloat needs its own length check.

**Phase to address:** Each artifact-emitting phase; explicit length check in the Multi-Axis and Primer-refresh phases (the biggest additions).

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Hard-code Game Changers list as a C# `string[]` | Ships in one commit | Maintenance treadmill; silent wrong classifications on every WotC update (Pitfall 1) | Never — externalize as versioned data from day one |
| Reuse `DeckStatClassifier` substring matching for bracket gating | No new detection code | Armageddon-class false negatives, wrong brackets (Pitfall 2) | Never for objective gates; OK only as an advisory hint the AI re-checks |
| Auto-regenerate primer on any deck-text difference | "It just works" feel | Regeneration treadmill, upstream load, 512MB churn, possible spend (Pitfall 6) | Never — stale-flag + explicit regenerate |
| Invent multi-axis weights to make it compile | Ships a score | Scores don't match intuition; tool dismissed (Pitfall 4) | Never without documented rationale + bracket cross-check |
| Recompute tap stats from static `EntersUntapped` counts | Simple, no sim changes | Numbers contradict the sim's own cast-rate (Pitfall 8) | Only for the composition-stat metric, clearly labeled as such |
| Extract a shared 3-variant prompt builder | Less duplication | Violates ADR-0001; loses per-AI tunability (Pitfall 9) | Never |
| Add a NuGet package for fuzzy-match/stats/schema | Faster feature code | Violates no-new-deps rule; container bloat (Pitfall 10) | Only after explicit user approval |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| WotC Game Changers list | Hard-code in `.cs`, or scrape live per-request | Versioned local data file with effective-date stamp; refresh out-of-band; stamp the artifact |
| Commander Spellbook (`FindCombosAsync`) | Treat `null` (API down) as "zero combos" for the 2-card-combo bracket gate | Treat `null` as "unknown — disclose in artifact," never as zero |
| Scryfall (`ScryfallThrottle`, ~5 req/s global) | Scorer/bracket issues its own Scryfall storm outside the shared cache | Share the per-request packet session cache; disclose partial coverage |
| Castability sim internal tap state | Re-derive tap stats in a parallel calc that drifts from the sim | Accumulate inside the existing single sim pass; reconcile against cast-rate |
| EdhTop16 (`IEdhTop16Client`, get-only-property JSON carve-out) | Reflow `{ get; init; }` → `{ get; }` on new related models | Honor the carve-out; `CarveOutGuard` test must stay green |
| Prompt variant registry (3x per ADR-0001) | Add a section to one variant, forget the other two | 3-variant checklist + per-variant parity test in the same change |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Tap-stat accumulation allocating inside the 20k-trial sim loop | Manabase/primer latency + GC time rise after Tap Analyzer | Pre-allocated primitive counters; no LINQ/alloc in hot loop; single pass | Any deck once the loop allocates per trial × spells |
| Primer auto-regeneration thrashing on live edits | Upstream call spikes, CPU/mem churn near 512MB cap | Stale-flag + explicit regenerate; debounce/single-flight; canonical-hash gate | A user editing a deck interactively |
| Scorer issuing its own Scryfall calls per request | Throttle backpressure, slow packets, partial scores | Share the existing enrichment cache + `ScryfallThrottle` | Concurrent users or upstream slowness |
| Cumulative artifact bloat past paste ceilings | Gemini truncates instructions mid-prompt | Per-variant length budget; compact blocks | Gemini ~30k; eventually the analysis packet too |
| Loading the bracket data file per request | Disk/parse cost on the hot path | Load once at startup into memory (seed-loader pattern); cache | Every request if not cached |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Bracket data file fetched from an untrusted URL at runtime | Supply-chain / SSRF on the core path | Ship the data file in-repo / on `/data`; refresh out-of-band, never per-request fetch |
| New tool endpoint skips `SameOriginRequestValidator` | CSRF on the new JSON APIs (bracket/score/tap) | Apply the existing same-origin guard to every new API endpoint (established anti-pattern) |
| Externalized list parsing trusts arbitrary JSON shape | Malformed data crashes startup / the core path | Validate on load; fail safe (fall back to last-good or disclose "list unavailable"), don't hard-crash the app |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Bracket/score asserted as authoritative when it's debatable | User dismisses tool on first disagreement | Frame as "DeckFlow detected X; AI, confirm/refine" — play to one-round-trip |
| Primer "stale" flag that cries wolf on unchanged decks | Users ignore the flag; feature is dead | Canonical-hash staleness; show WHAT changed, not just "stale" |
| Two contradictory tap/untapped numbers in one report | Looks broken; erodes manabase credibility | Single source of truth per metric, clearly labeled (realized vs composition) |
| Multi-axis 0–5 shown with false-decimal precision | Over-claims measurement | Coarse labeled bands ("4/5 — explosive") |
| New tools live without a flag/kill-switch | A wrong artifact ships to prod unguarded | Flag-gate OFF in prod, operator flip, cascade to tile/nav/help |

---

## "Looks Done But Isn't" Checklist

- [ ] **Bracket classifier:** Game Changers list is EXTERNAL DATA with an effective-date stamp — not a `.cs` literal. Artifact discloses the list version. Verify a WotC update needs only a data edit, no redeploy.
- [ ] **Bracket gating:** Armageddon-class (mass land denial) classifies distinctly from ordinary board wipes. Spellbook `null` disclosed as "unknown," not "no combos."
- [ ] **Multi-axis score:** Every weight has a `// Why:` rationale. Scores cross-checked against the bracket (cEDH list scores high Power/Speed). Coverage ("N/100 cards scored") disclosed.
- [ ] **Auto-refresh primer:** Staleness key is a CANONICAL multiset hash — reorder/printing-swap do NOT flag stale; card/quantity swap DOES. Golden tests assert both directions. Default is stale-FLAG, regenerate on explicit action only.
- [ ] **Tap Analyzer:** Each metric sourced from ONE place (realized-from-sim vs composition-from-classification), labeled. Reconciles with the sim's cast-rate. No second sim pass; no per-trial allocation.
- [ ] **3-variant parity:** Every new artifact section exists in ChatGPT, Claude, AND Gemini variants. Parity test green. Gemini variant length within ~30k.
- [ ] **Flags:** Each new tool flag-gated OFF in prod, namespaced, cascading to tile/nav/help, idempotent migration carries operator toggles.
- [ ] **Format/CRLF:** New files LF-only; changed-lines gate green; carve-outs intact (`{ get; init; }` preserved on new JSON models); `CarveOutGuard` green.
- [ ] **No new deps:** No NuGet added without approval.

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Game Changers list went stale (wrong classifications shipped) | LOW (if externalized) / HIGH (if baked in) | If data-file: edit file + refresh, no deploy. If `.cs` literal: code change + redeploy + retroactive trust damage |
| Multi-axis weights feel wrong | MEDIUM | Recalibrate against bracket + a panel of known decks; the score is flag-gated so flip OFF while fixing |
| Primer staleness false-positives | LOW | Fix the equivalence relation + add golden test; flag-gated, flip OFF meanwhile |
| Tap numbers contradict cast-rate | MEDIUM | Re-source the metric from the sim's internal state; add reconciliation test |
| A variant section drifted (missing in Gemini/Claude) | LOW | Parity test catches it; add the missing section; the flag holds the tool OFF until parity |
| Sim perf regressed | MEDIUM | Profile the trial loop; remove allocations/LINQ; fold tap accumulation into the single pass |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1. Game Changers / bracket staleness treadmill | Bracket Classifier + Balancer | Update simulated via data-file edit only (no code/redeploy); artifact shows list date |
| 2. Mis-detecting gating mechanics | Bracket Classifier + Balancer | Armageddon vs board-wipe test; Spellbook-null disclosed test |
| 3. Balancing subjectivity / over-claiming cuts | Bracket Classifier + Balancer | Artifact prose frames cuts as AI-evaluated, anchored to objective gates only |
| 4. Arbitrary multi-axis weights | Multi-Axis Deck Score | Every weight commented; bracket cross-check test on golden decks |
| 5. Missing card-level data for scoring | Multi-Axis Deck Score | Shares enrichment cache + throttle; coverage number in output |
| 6. Primer regeneration cost / thrashing | Auto-Refreshing Primer | Stale-flag default; canonical-hash gate; no upstream spike on whitespace edit |
| 7. Staleness false-positive/negative | Auto-Refreshing Primer | Golden equivalence tests (reorder/printing=not stale; swap/qty=stale) |
| 8. Misreading sim tapped/untapped state | Tap Analyzer | Single source per metric; reconciliation test vs cast-rate |
| 9. 3-variant triplication drift | Every artifact-emitting phase | Per-variant parity test; ADR-0001 honored (no shared base) |
| 10. New deps without approval | All phases (planning gate) | No new NuGet in diff |
| 11. Monte-Carlo perf regression | Tap Analyzer | Before/after benchmark; single pass; zero hot-loop allocation |
| 12. Flag-gating omitted/inconsistent | Each feature phase | Flag OFF in prod, cascades to tile/nav/help, idempotent migration |
| 13. Cross-tool number disagreement | Multi-Axis + milestone consistency test | Golden-deck cross-tool consistency test |
| 14. CRLF / format-gate / carve-out | All phases | format-gate CI green; `CarveOutGuard` green; LF only |
| 15. Artifact bloat past paste ceilings | Multi-Axis + Primer-refresh phases | Per-variant length measured; Gemini within ~30k |

---

## Sources

- `DeckFlow.Web/Models/CommanderBracketCatalog.cs` — confirmed brackets are currently hard-coded option records (staleness risk, Pitfall 1)
- `DeckFlow.Core/Analysis/DeckStatClassifier.cs` — confirmed substring-based role classifiers (`IsClosingPowerCard` matches `"extra turn"`, `IsBoardWipeCard` matches `"destroy all"`) — fuzzy for advisory tallies, wrong for precise bracket gating (Pitfall 2)
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — `DefaultTrials = 20_000`; `CardKind.{Untapped,Tapped}Land`; per-land `(colorMask, OnlineTurn)` board where a tapped land is `OnlineTurn = currentTurn + 1` (FINDING-1 HIGH) — the internal tap state the Tap Analyzer must read, not re-derive (Pitfalls 8, 11)
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — `EffectiveSources(untappedOnly:)` static composition view vs the sim's realized view (the two-sources-of-truth risk, Pitfall 8)
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — `CardCastability` (`CastPercent`, `AverageDelay`, `LimitingFactor`), `FastMana`, `UnsupportedInteractions` honesty pattern (Pitfalls 4, 5)
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` (~750 LOC) — `TryComputeCacheKeyAsync` (extend as staleness hash), category-knowledge + Spellbook + EdhTop16 pipeline (regeneration cost, Pitfall 6)
- `DeckFlow.Web/Services/PromptBuilders/Primer/{ChatGpt,Claude,Gemini}PrimerPromptVariant.cs` + `PrimerPromptVariantRegistry.cs` — the intentionally-triplicated 3-variant structure per ADR-0001 (Pitfall 9)
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — `FindCombosAsync` returns `null` on failure (graceful degradation → must not be read as "zero combos", Pitfall 2)
- `DeckFlow.Core/Manabase` `ScryfallThrottle` (~5 req/s global static) + v1.3 packet session cache Key Decision — shared enrichment to avoid Scryfall storms (Pitfall 5)
- PROJECT.md / CLAUDE.md — 512MB cap, no-new-deps rule, ADR-0001 prompt-variant decoupling, ADR-0002 CalVer, flag-key namespacing (`tool.*`/`analysis.*`/`manabase.*`, prod-seed-OFF + idempotent migration), Cycle 11 tool-visibility registry, `.editorconfig` changed-lines format gate + five carve-outs + `CarveOutGuard`, `.gitattributes` LF, Gemini ~30k paste ceiling / flag-gating
- `scratchpad-research/commander-feature-wants-report.md` — incumbent evidence: official 5-tier bracket + Game Changers (Rate My Decks), bracket *balancing* gap (deckcheck.co), EDHRank multi-axis (Power/Speed/Control/Consistency), Salubrious Snail Tap Analyzer (untapped frequency + opening-turn), primers manual + decaying (auto-refresh gap)

---
*Pitfalls research for: DeckFlow Cycle 13 — Deck Evaluation & Creator Output*
*Researched: 2026-06-27*

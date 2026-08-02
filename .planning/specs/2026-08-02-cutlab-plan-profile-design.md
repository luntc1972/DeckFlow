# Cut Lab Plan Profile — Design Spec

**Date:** 2026-08-02
**Status:** Approved by user (brainstorming session, research-validated)
**Origin:** "Improve Cut Lab without AI prompts — plan as checkbox options, commander-usage-derived"

## Problem

Cut Lab's `PrimaryPlan` / `SecondaryPlan` intent fields are free text and deterministically
inert: the engine consumes only `Intent.Bracket` and `Intent.PlayExperience`
(`CutLabFloorRules.cs:150`, `CutLabFloorResolver.cs:52`). Two decks with opposite plans get
identical cut proposals at the same bracket. The plan must become machine-readable so the
deterministic engine can act on it.

## Research findings (2026-08-02)

- **Niche open.** No deterministic cut advisor exists. Only competitor (DeckCheck DeckTrim)
  is LLM-powered and credit-metered. All existing intent taxonomies (Archidekt tags,
  Moxfield hubs, TappedOut hubs) are discovery metadata feeding no analysis. Deterministic
  plan-driven cutting is unprecedented and matches DeckFlow's anti-AI-slop positioning.
- **EDHREC JSON feasible.** `https://json.edhrec.com/pages/commanders/<slug>.json` is static
  S3/CloudFront, no bot defense, ~100 KB/page, etag + conditional GET supported.
  - Themes: `$.panels.taglinks[]` → `{count, slug, value}`, sorted by deck count.
  - Theme card lists: `/<commander-slug>/<theme-slug>.json` →
    `$.container.json_dict.cardlists[]` (`highsynergycards`, `topcards`, type lists;
    ~270 cards/page) with per-card `synergy`, `num_decks`, `potential_decks`
    (theme-scoped inclusion rate).
  - **Gotcha:** missing page returns HTTP 403 with S3 XML `AccessDenied`, not 404.
  - Same fetch also carries `bracket_counts`, `similar` commanders, mana curve (future use;
    see todos `bracket-derived-plan-presets`, `what-if-add-suggestions`).
  - License: EDHREC's only binding term is non-commercial use — already relied on by the
    role-floor corpus.
- **Local vocabulary mismatch.** `CategoryKnowledgeStore` tags are role-level (`Ramp`,
  `Sac Outlet`, `Drain`, `Stax`) — archetype names like "aristocrats" never appear. So
  EDHREC theme card lists are the **primary** archetype-membership source; local category
  data serves generic strategies via a role-proxy table.
- **Prior-art rules adopted:** curated finite checkbox list (never freeform — Moxfield tag
  fatigue); every checkbox shows a one-line definition plus its mechanical consequence
  (TappedOut's top complaint is undocumented labels); every cut reason is phrased against
  the user's selected plan; multi-select composition semantics must be explicit because no
  incumbent solves overlap.

## Decisions (locked during brainstorming)

1. **Both layers:** fixed generic strategy checkboxes + commander-specific EDHREC themes.
2. **All four engine effects:** protect on-plan cards, reorder proposals, adjust role
   floors, flag off-plan findings.
3. **Blend mapping, EDHREC primary:** EDHREC theme card lists for archetype membership;
   CategoryKnowledgeStore role-proxies for generic strategies.
4. **UI placement:** single plan-selection panel post-process (intake stays minimal:
   URL + bracket + experience), before cut rounds begin.

## Design

### 1. Data model

New `CutLabPlanProfile` record on `CutLabIntent` (`CutLabState.cs:189`), replacing the
free-text fields in the intake form:

- `GenericStrategies` — checked entries from a fixed enum-backed list (~12): combo,
  aristocrats, voltron, tokens, spellslinger, stax, reanimator, landfall, lifegain,
  +1/+1 counters, combat/battlecruiser, control.
- `CommanderThemes` — checked EDHREC themes: `{ Slug, DisplayName, DeckCount }`.

Serializes with existing session state. Old `PrimaryPlan`/`SecondaryPlan` kept read-only
on `CutLabIntent` so in-flight sessions deserialize; removed from the intake form.

### 2. EDHREC commander theme service

New `EdhrecCommanderThemeService` (`DeckFlow.Web/Services/CutLab/` or `Services/Http/`
per existing egress layout), RestSharp + direct Polly v8 pattern (per repo constraint —
no standard-handler migration):

- Fetch commander page once → theme list (taglinks).
- Lazy fetch per **checked** theme → card list + synergy scores (main-page card lists are
  not theme-scoped, so one fetch per checked theme).
- Disk cache per commander and per commander/theme, beside the role-floor corpus; etag
  revalidation honoring `cache-control` (~27 min max-age upstream; cache TTL can be days —
  theme membership is slow-moving).
- 403 + XML body → treated as "page does not exist", not an error.
- Fail-open: on fetch failure the commander-theme section renders "unavailable"; generic
  strategies keep working. No ScryfallThrottle involvement (different host, static CDN).

### 3. Plan affinity resolver

New `CutLabPlanAffinityResolver` producing per-card
`PlanAffinity { OnPlanThemes, OffPlanThemes, Score }`:

- **Archetype membership (primary):** card appears in a checked EDHREC theme's card lists.
- **Generic strategy membership:** static role-proxy table mapping each generic strategy to
  CategoryKnowledge tags (e.g. aristocrats → `Sac Outlet` | `Drain` | `Recursion`;
  tokens → `Tokens` | `Anthem`). Table lives in Core, unit-tested.
- Card-name matching through existing normalization (DFC-aware — known past bug class).

### 4. Engine effects

Composition semantics for overlapping selections: **union** of protections, **max** of
floor deltas per role, ordering weights **additive with a cap**.

- **Protect:** on-plan cards join the existing combo-protected pattern in the proposal
  queue — pushed to back, user can still cut them; nothing is immune.
- **Reorder:** `CutLabNextProposalBuilder` gains an off-plan weight term; off-plan cards
  surface first in Round 1/2/3.
- **Floors:** plan→floor-delta table beside `CutLabFloorDefaults` (combo → +tutor,
  +protection; combat → +wincon-creature). Deltas clamped by existing `CutLabFloorRules`
  validation; EDHREC baseline floors remain the base.
- **Finding:** new detector in `CutLabStructuralFindings` — "stranded off-plan package":
  ≥N cards (threshold TBD in planning, default 4) supporting an unchecked theme, message
  phrased against the user's selection ("5 cards support Tokens — not in your plan").

### 5. UI

Single plan panel after deck processing, before cut rounds (natural step 2 of the Phase 7
workflow wizard):

- Generic strategy checkboxes always shown.
- Commander themes section sorted by EDHREC deck count; top 3 pre-checked when each holds
  ≥5% of the commander's total theme deck count (planning may tune, default stands).
- Every checkbox: one-line definition + mechanical consequence line
  ("protects sac outlets, raises creature floor to 28").
- Proposals and findings cite the plan ("off-plan: supports Tokens — unchecked").
- Layout CSS in `site-common.css` (guild-theme constraint); both viewport classes tested.

### 6. Error handling

- EDHREC unreachable/403 → commander section "unavailable", generic layer unaffected.
- Cache read failure → refetch; cache write failure → log, continue (in-memory result).
- Zero checkboxes checked → engine behaves exactly as today (all effects no-op); panel
  states this plainly.

### 7. Testing

Core-heavy, existing conventions (`Fake*` stateful doubles, `[InternalsVisibleTo]` seams):

- Resolver: membership from fake theme payloads, role-proxy table, DFC name matching,
  composition semantics (union/max/additive-cap) — including mutation checks on the cap
  and threshold constants (fixture must count *eligible* population, per prior lesson).
- Theme service: 403-XML handling, etag revalidation, cache hit/miss, fail-open.
- Floors: delta application + clamp interaction with `CutLabFloorRules`.
- Detector: threshold boundary, phrasing against selection.
- E2E: one Playwright pass over the plan panel, 2 viewports, headless per repo rules.

### 8. Phasing

| Phase | Scope | Ships |
|-------|-------|-------|
| P1 | `CutLabPlanProfile` + generic strategies + role-proxy resolver + protect/reorder | Usable, zero new HTTP |
| P2 | `EdhrecCommanderThemeService` + commander theme UI + pre-check | Commander-aware |
| P3 | Floor deltas + off-plan finding detector | Full effect set |

All behind the existing Cut Lab flag (prod currently OFF).

## Out of scope (captured as todos, committed `5c8bbfbb`)

- Bracket-derived plan presets (bracket auto-checks strategies).
- What-if ADD suggestions from EDHREC high-synergy lists.

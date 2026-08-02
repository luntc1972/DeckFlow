# Phase 8: Plan Profile — Checkbox Plan Selection - Context

**Gathered:** 2026-08-02
**Status:** Ready for planning
**Source:** PRD Express Path (.planning/specs/2026-08-02-cutlab-plan-profile-design.md)

<domain>
## Phase Boundary

Replace Cut Lab's deterministically-inert free-text `PrimaryPlan`/`SecondaryPlan` intent
fields with a machine-readable plan profile: fixed generic strategy checkboxes plus
commander-specific EDHREC themes, driving four engine effects — protect on-plan cards,
reorder proposals (off-plan first), plan→floor deltas, and a "stranded off-plan package"
structural finding.

**In scope:** `CutLabPlanProfile` data model, `EdhrecCommanderThemeService`,
`CutLabPlanAffinityResolver`, the four engine effects, single plan-selection panel UI,
error handling, tests. All behind the existing Cut Lab flag (prod currently OFF).

**Out of scope (captured as todos, committed `5c8bbfbb`):** bracket-derived plan presets;
what-if ADD suggestions from EDHREC high-synergy lists.

**Sequencing constraint:** engine plans are independent and parallel-safe. The plan-panel
UI plan is **gated on Phase 7** — it inserts into the wizard step slot Phase 7 reserves,
and touches the same two files Phase 7 rewrites (`CutLab.cshtml`, `wwwroot/ts/cut-lab.ts`).

</domain>

<decisions>
## Implementation Decisions

### Data model
- New `CutLabPlanProfile` record on `CutLabIntent` (`CutLabState.cs:189`), replacing the
  free-text fields in the intake form.
- `GenericStrategies`: fixed enum-backed list (~12): combo, aristocrats, voltron, tokens,
  spellslinger, stax, reanimator, landfall, lifegain, +1/+1 counters,
  combat/battlecruiser, control.
- `CommanderThemes`: checked EDHREC themes as `{ Slug, DisplayName, DeckCount }`.
- Serializes with existing session state. Old `PrimaryPlan`/`SecondaryPlan` kept
  read-only on `CutLabIntent` so in-flight sessions deserialize; removed from intake form.

### EDHREC commander theme service
- New `EdhrecCommanderThemeService`, RestSharp + direct Polly v8 pattern (repo
  constraint — no standard-handler migration).
- Commander page fetched once → theme list from `$.panels.taglinks[]`
  (`{count, slug, value}`, sorted by deck count).
- Lazy fetch per **checked** theme → `/<commander-slug>/<theme-slug>.json`,
  card lists in `$.container.json_dict.cardlists[]` with per-card `synergy`,
  `num_decks`, `potential_decks`.
- Disk cache per commander and per commander/theme, beside the role-floor corpus;
  etag revalidation honoring `cache-control` (~27 min upstream max-age; cache TTL can be
  days — theme membership is slow-moving).
- HTTP 403 with S3 XML `AccessDenied` body = "page does not exist", not an error.
- Fail-open: on fetch failure the commander-theme section renders "unavailable";
  generic strategies keep working.
- **No ScryfallThrottle involvement** — different host, static CDN, no bot defense.

### Plan affinity resolver
- New `CutLabPlanAffinityResolver` producing per-card
  `PlanAffinity { OnPlanThemes, OffPlanThemes, Score }`.
- Archetype membership (**primary**): card appears in a checked EDHREC theme's card lists.
- Generic strategy membership: static role-proxy table mapping each generic strategy to
  CategoryKnowledge tags (e.g. aristocrats → `Sac Outlet` | `Drain` | `Recursion`;
  tokens → `Tokens` | `Anthem`). Table lives in Core, unit-tested.
- Card-name matching through existing normalization (DFC-aware — known past bug class).
- Rationale: local `CategoryKnowledgeStore` tags are role-level; archetype names never
  appear there, so EDHREC theme card lists are the primary archetype-membership source.

### Engine effects
- Composition semantics for overlapping selections: **union** of protections, **max** of
  floor deltas per role, ordering weights **additive with a cap**.
- Protect: on-plan cards join the existing combo-protected pattern in the proposal
  queue — pushed to back, user can still cut them; nothing is immune.
- Reorder: `CutLabNextProposalBuilder` gains an off-plan weight term; off-plan cards
  surface first in Rounds 1/2/3.
- Floors: plan→floor-delta table beside `CutLabFloorDefaults` (combo → +tutor,
  +protection; combat → +wincon-creature). Deltas clamped by existing
  `CutLabFloorRules` validation; EDHREC baseline floors remain the base.
- Finding: new detector in `CutLabStructuralFindings` — "stranded off-plan package":
  ≥N cards (default 4) supporting an unchecked theme, message phrased against the user's
  selection ("5 cards support Tokens — not in your plan").

### UI
- Single plan panel after deck processing, before cut rounds — natural step 2 of the
  Phase 7 workflow wizard, in the reserved slot.
- Intake stays minimal: URL + bracket + experience.
- Generic strategy checkboxes always shown.
- Commander themes sorted by EDHREC deck count; top 3 pre-checked when each holds ≥5% of
  the commander's total theme deck count.
- Every checkbox: one-line definition + mechanical consequence line (curated finite
  list, never freeform — prior-art rule; undocumented labels are TappedOut's top
  complaint).
- Proposals and findings cite the plan ("off-plan: supports Tokens — unchecked").
- Layout CSS in `site-common.css` (guild-theme constraint); both viewport classes tested.

### Error handling
- EDHREC unreachable/403 → commander section "unavailable", generic layer unaffected.
- Cache read failure → refetch; cache write failure → log, continue (in-memory result).
- Zero checkboxes checked → engine behaves exactly as today (all effects no-op); the
  panel states this plainly.

### Testing
- Core-heavy, existing conventions (`Fake*` stateful doubles, `[InternalsVisibleTo]`).
- Resolver: membership from fake theme payloads, role-proxy table, DFC name matching,
  composition semantics (union/max/additive-cap) — including mutation checks on the cap
  and threshold constants; fixtures must count the *eligible* population (prior lesson).
- Theme service: 403-XML handling, etag revalidation, cache hit/miss, fail-open.
- Floors: delta application + clamp interaction with `CutLabFloorRules`.
- Detector: threshold boundary, phrasing against selection.
- E2E: one Playwright pass over the plan panel, 2 viewports, headless per repo rules.

### Internal phasing (from spec §8)
| Slice | Scope | Ships |
|-------|-------|-------|
| P1 | `CutLabPlanProfile` + generic strategies + role-proxy resolver + protect/reorder | Usable, zero new HTTP |
| P2 | `EdhrecCommanderThemeService` + commander theme UI + pre-check | Commander-aware |
| P3 | Floor deltas + off-plan finding detector | Full effect set |

### Claude's Discretion
- Exact service directory placement: `DeckFlow.Web/Services/CutLab/` vs `Services/Http/`
  — follow the existing egress layout.
- Stranded-package threshold N: default 4; planning may tune.
- Pre-check share: top 3 themes at ≥5% each; planning may tune, default stands.
- Concrete values in the plan→floor-delta table and the additive ordering-weight cap.
- Role-proxy table contents beyond the two spec examples.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Design authority
- `.planning/specs/2026-08-02-cutlab-plan-profile-design.md` — the approved design spec
  this context derives from; decisions above are locked there.
- `.planning/workstreams/cycle21-cut-lab/ROADMAP.md` — Phase 8 entry, execution order,
  Phase 7 gating (wizard slot reservation).
- `.planning/workstreams/cycle21-cut-lab/phases/08-plan-profile-checkbox-selection/README.md`
  — adoption record and sequencing.

### Code surfaces named by the spec
- `DeckFlow.Web/Models/CutLab/CutLabState.cs` (line 189, verified) — `CutLabIntent`, where
  `CutLabPlanProfile` lands; session-state serialization. (The design spec says
  `Services/CutLab/` — that path is wrong; the state model lives under `Models/CutLab/`.)
- `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs` (line ~150) and
  `DeckFlow.Web/Services/CutLab/CutLabFloorResolver.cs` (line ~52) — the two consumers
  proving `PrimaryPlan`/`SecondaryPlan` are inert; floor clamp validation.
- `CutLabFloorDefaults` — floor baseline the delta table sits beside.
- `CutLabNextProposalBuilder` — proposal ordering; gains the off-plan weight term.
- `CutLabStructuralFindings` — detector home for "stranded off-plan package".
- `CategoryKnowledgeStore` — role-level tags backing the role-proxy table.
- `DeckFlow.Web/Views/Deck/CutLab.cshtml` + `DeckFlow.Web/wwwroot/ts/cut-lab.ts` —
  Phase 7-contested files; plan-panel UI plan only, gated on Phase 7.

### Constraints inherited from the repo
- RestSharp + direct Polly v8 for all egress; no `Microsoft.Extensions.Http.Resilience`.
- Layout CSS in `site-common.css`, never `site.css`.
- TS compiled by MSBuild; never stage `wwwroot/js/*.js`.
- Cut Lab flag `tool.cut-lab.enabled` is OFF in prod; all work ships dark.

</canonical_refs>

<specifics>
## Specific Ideas

- EDHREC endpoints: `https://json.edhrec.com/pages/commanders/<slug>.json` (themes at
  `$.panels.taglinks[]`) and `.../commanders/<commander-slug>/<theme-slug>.json` (card
  lists at `$.container.json_dict.cardlists[]`, ~270 cards/page, keys like
  `highsynergycards`, `topcards`, type lists).
- Missing EDHREC page: HTTP **403** with S3 XML `AccessDenied` — not 404.
- Same commander fetch carries `bracket_counts`, `similar`, mana curve — future use only
  (todos `bracket-derived-plan-presets`, `what-if-add-suggestions`).
- EDHREC license: only binding term is non-commercial use — already relied on by the
  role-floor corpus.
- Example finding copy: "5 cards support Tokens — not in your plan".
- Example consequence line: "protects sac outlets, raises creature floor to 28".

</specifics>

<deferred>
## Deferred Ideas

- Bracket-derived plan presets (bracket auto-checks strategies) — todo, committed `5c8bbfbb`.
- What-if ADD suggestions from EDHREC high-synergy lists — todo, committed `5c8bbfbb`.

</deferred>

---

*Phase: 08-plan-profile-checkbox-selection*
*Context gathered: 2026-08-02 via PRD Express Path*

# SEO Ladder P0-P3 — Owed Independent Codex Review Gate

**Date:** 2026-08-16
**Range:** `bbc85f9d..a5f51b0d` (19 commits, 46 files, +1381/-284)
**Branch:** `feat/seo-ladder` (already ff-merged to `main`, shipped 2026-08-05)
**Reviewer (stage 1):** `codex review --base bbc85f9d`, `gpt-5.6-sol` @ low
**Why this exists:** The ladder shipped on 2026-08-05 with this gate UNDISCHARGED — Codex was out
of credits until 2026-08-10 09:48 and the user chose not to wait. The local `cavecrew` review
(`.planning/reviews/2026-08-05-seo-ladder-review-cavecrew.md`) was advisory only.

Raw stage-1 transcript: scratchpad `codex-review-stage1.md` (1116 lines, not committed).

---

## Stage 1 — `codex review` (diff-scoped)

### F-1 [P2] Sitemap advertises `/help/{slug}` URLs that 404 when the global help gate is off — CONFIRMED

**Location:** `DeckFlow.Web/Controllers/SitemapController.cs:68-71`

**Claim:** When `tool.help.enabled` is disabled, every `/help/{slug}` returns 404 because of the
controller-level gate added in `4171f61a`, but the topics are still concatenated into the sitemap —
`IsTopicVisible` checks only each topic's *optional* per-topic flag.

**Verification — CONFIRMED against source, not just the diff:**

- `DeckFlow.Web/Services/HelpContentService.cs:53-54`
  `IsTopicVisible(topic, flags) => topic.RequiresFlag is null || flags.IsEnabled(topic.RequiresFlag)`
  — no reference to `tool.help.enabled`.
- `DeckFlow.Web/Controllers/HelpController.cs:31,41` — both `Index()` and `Topic(slug)` carry
  `[FeatureFlagGate("tool.help.enabled")]`.
- `SitemapController.cs:68` — the `SeoPaths.Indexable` side **is** flag-joined via `.Where(IsReachable)`.
  The `.Concat(...)` of help topics at `:69-71` bypasses that join entirely.

**Why it matters beyond the 404:** the ladder's headline design decision was that each page is
declared exactly once so sitemap / structured data / canonical URL *cannot* drift apart. This is
that drift, reintroduced by the fix for Finding 1 rather than by the original design. A gate was
added to the serving side (`HelpController`) without a census of the advertising side
(`SitemapController`).

**Suggested fix:** gate the topic concatenation on `tool.help.enabled` as well — e.g. join it
through the same `IsReachable` predicate the `SeoPaths.Indexable` side uses, so there is one
reachability rule rather than two.

**Status:** OPEN — not fixed in this session.

---

## Stage 2 — `codex exec` written brief (repo-scoped)

Stage 2 is MANDATORY here; three of the four CLAUDE.md conditions fire:

- (2) owed gate with no prior independent review;
- (3) the ladder's central claim ("the four `SeoPaths` consumers cannot drift") is a claim about the
  **repo**, not the diff;
- (4) URL normalization changed — `RouteOptions.LowercaseUrls = true` — and the ladder already found
  one casualty of exactly that (`cffb089c`, admin export cookie with a casing-sensitive path).

**Reviewer:** `codex exec`, `gpt-5.6-sol` @ medium, `-s read-only`, rooted at the seo-ladder worktree.
Full output: scratchpad `codex-stage2-final.md`. Verdict: **no BLOCK, no HIGH.**

### F-2 [MEDIUM as reported → LOW as attributed] Request-controlled host flows into canonical URLs, OG tags, JSON-LD and sitemap `<loc>`

**Location:** `Views/Shared/_Layout.cshtml:74,79`; `Program.cs:130-141`; `appsettings.json:8`;
`Controllers/SitemapController.cs:79`

**Claim (as reported):** `AllowedHosts` is `"*"`, `ForwardedHeaders.XForwardedHost` is enabled, and
both `KnownIPNetworks` and `KnownProxies` are cleared — so a request with an attacker-supplied host
makes the page self-canonicalize to that host across `<link rel=canonical>`, `og:url`, the OG image
URL, JSON-LD, the robots sitemap address, and every sitemap `<loc>`.

**Verified — the configuration claim is TRUE:**

- `appsettings.json:8` — `"AllowedHosts": "*"`.
- `Program.cs:132-134` — `XForwardedFor | XForwardedProto | XForwardedHost`.
- `Program.cs:139-140` — `KnownIPNetworks.Clear(); KnownProxies.Clear();` (deliberate; the comment
  explains Render/Cloudflare hops are not loopback, so the defaults would drop `X-Forwarded-Proto`
  and break https canonicals + CSRF).

**ATTRIBUTION CORRECTED — this is PRE-EXISTING, not introduced by the ladder.** The reviewer did not
check the before state. `git show bbc85f9d:_Layout.cshtml:74,78` shows `requestHost` and
`canonicalUrl` were **already** request-derived before this change; the ladder only inserted
`SeoPaths.Normalize` into the path segment. What the ladder *did* change is narrower: the old
`StructuredDataBuilder` cached a `WebSiteJson` fallback whose `url` was the hardcoded
`https://www.deckflow.gg` (`bbc85f9d:StructuredDataBuilder.cs:27,60`); that one fallback is now
request-derived too. So the ladder marginally widened an exposure that already covered canonical and
OG.

**Mitigations not weighed by the reviewer (it said so — it could not prove ingress behavior):**
`ForwardedHeadersMiddleware` reads the **rightmost** entry with the default `ForwardLimit = 1`, so a
client-injected `X-Forwarded-Host` sits to the left of Render's own appended value and is ignored.
A direct `Host:` override still requires reaching the container, which is only routable through
Render's ingress.

**Assessment:** real, worth closing, but LOW and pre-existing — it does not belong to this gate as a
ship-blocker. Fix if taken: set `AllowedHosts` to the real hostnames instead of `"*"`.

**Status:** OPEN — pre-existing, deferred.

### F-3 [LOW] The "cannot drift" invariant is asserted, not enforced

**Location:** `Seo/SeoPaths.cs:19`; `Views/Shared/_Layout.cshtml:78`

**Claim:** Canonical generation does not consume `Pages` — it builds from `Request.Path`. Route and
flag ownership stay separately declared in controller attributes and `ToolRegistry`. So renaming
`/bracket` to `/commander-bracket` in the controller and `ToolRegistry` while forgetting `SeoPaths`
yields a live page canonicalizing to `/commander-bracket` while the sitemap keeps emitting the dead
`/bracket`. `PageMetadataViewTests` maps the old path to the same Razor file and still passes;
`ToolRouteGateCoverageTests` validates the registry/controller pair without reconciling to `SeoPaths`.

**Assessment:** Correct, and it is the sharpest finding in the set — it goes at the ladder's own
headline design claim. The current route set is aligned, so nothing is broken today; the guarantee
is just weaker than the plan and README describe. Reviewer verified by reading `SeoPaths`, `_Layout`,
controller route annotations, `ToolRegistry`, and both test files.

**Status:** OPEN — no defect today; the claim in README/plan should be softened, or a test added
reconciling `SeoPaths.Pages` against the live route table.

### F-4 [LOW] `WebSiteJson` de-caching allocates and serializes per request

**Location:** `Seo/StructuredDataBuilder.cs:44`

**Claim:** `/about`, `/feedback`, `/help`, `/deckflow-bridge`, `/set-upgrade-analysis` and re-executed
branded 404s now build `WebSiteNode(baseUrl)` and serialize it on every request; previously they
returned one cached string. Bot-driven 404 traffic makes this a warm path.

**Assessment:** Accurate as a description; the tradeoff was made deliberately and is recorded in the
handoff decisions ("the caching win was not worth the deployment rigidity"). Small allocation, no
benchmark run. Note the 512MB Render web tier makes per-request allocation non-free, but this is a
few hundred bytes.

**Status:** ACCEPTED — deliberate tradeoff, no action.

---

## CLAIMS VERIFIED — what this gate actually closes

This is the part that discharges the gate. Stage 2 checked and found SOUND:

- **No sibling of F-1.** Every other `SeoPaths.Indexable` entry is correctly joined to its route flag
  through `ToolRegistry`, `AdditionalRoutes`, or an intentional ungated classification. F-1 is a
  one-off, not a pattern.
- **`LowercaseUrls` produced no further casing casualty.** The legacy extension redirect compares
  case-insensitively and runs before static files; admin/API/security path checks use
  `StartsWithSegments` or are case-insensitive; the export cookie's server and browser paths are both
  `/`; the sole `location.pathname` storage key only removes obsolete snapshots; **no server-side
  path-keyed `OrdinalIgnoreCase` map is paired with a case-sensitive browser lookup** (the known
  DeckFlow failure shape).
- **The gate-coverage reflection walk binds.** It covers GET and non-GET `HttpMethodAttribute`
  actions, controller-plus-action routes, and every `AdditionalRoutes` entry. No area controllers,
  inherited HTTP actions, controller-level gates, `AcceptVerbs` actions, or conventional
  un-attributed actions on gated controllers escape it. `tool.help.enabled` is the only permitted
  non-registry key.
- **Both exceptional page classifications hold everywhere.** `/content-kb` (`IsTool`, non-indexable):
  out of the sitemap, still flag-gated, gets the share bar and tool-page + breadcrumb JSON-LD.
  `/deckflow-bridge` (indexable, non-tool, ungated): in the sitemap and metadata census, no share bar,
  generic website JSON-LD. `/set-upgrade-analysis`: indexable non-tool, gated on
  `tool.deck-analysis.enabled` via deck-analysis's `AdditionalRoutes`.
- **`Indexable` preserves `Pages` declaration order; no consumer depends on `Tools` order; no consumer
  assumes the two are disjoint.**
- **No TypeScript or JavaScript consumer of `SeoPaths` / `SeoPage` / `Pages` / `Indexable` / `Tools`
  exists.** `SeoPage` and `Pages` are private to `SeoPaths`.

### The five P2 commits — the unrecovered-findings hunt

| Commit | Verdict |
|---|---|
| `ec1e2327` `/deckflow-bridge` landing page | Sound. Route reachable, old URL redirects permanently, the gitignored ZIP is generated at Build/Publish from the tracked `browser-extensions/deckflow-bridge` dir. |
| `7f46fb11` contextual tool links | Sound. All six links use the target tool's exact flag and route. |
| `5fd22478` homepage tile labels | Sound. Copy only — route, label, flag, tab, help metadata unchanged. |
| `93845666` admin export cookie casing | Sound. Correctly resolves the case-sensitive completion-cookie failure; no equivalent explicit cookie path remains. |
| `7de2786d` content-KB tool metadata | Sound. Preserves tool metadata while removing indexability. |

**Conclusion on findings 2-4:** an independent reviewer with repo access, told explicitly that
findings were missing and pointed at these five commits, found nothing on them. That is materially
stronger than the 2026-08-05 local review's failure to reproduce. The unrecovered findings are now
reasonably considered closed.

---

## GATE VERDICT

**DISCHARGED.** No BLOCK, no HIGH.

- 1 real defect introduced by this change and confirmed against source: **F-1**, P2, open.
- 1 pre-existing LOW mis-attributed to this change: **F-2**, open, deferred.
- 1 accurate critique of a design *claim* with no live defect: **F-3**, open.
- 1 deliberate, documented tradeoff: **F-4**, accepted.

Caveat, stated plainly: the review was **static and read-only** — the reviewer did not execute the
test suite, because the sandbox prohibited writes.

---

## Carried context — the unrecovered findings

The 2026-08-05 handoff recorded that **findings 2-4 of an earlier review were never recovered**:
they were held in conversation only and destroyed by a `/clear`. The 08-05 local review failed to
*reproduce* them, which is not the same as *clearing* them. Prior analysis put at most one genuinely
unaccounted-for finding, on the P2 commits:

`ec1e2327` `/deckflow-bridge` landing page · `7f46fb11` contextual tool links ·
`5fd22478` homepage tool tile labels · `93845666` admin export cookie casing scope ·
`7de2786d` content-KB tool metadata retention

Stage 2 targets these commits explicitly.

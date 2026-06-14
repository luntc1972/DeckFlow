---
phase: 44
slug: admin-grid-lazy-paging
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-14
---

# Phase 44 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Scope: data-tier index consolidation (44-01) + new admin-only AJAX PartialView endpoint (44-02) + client-side lazy-load wiring (44-03). One new network surface (`GET /Admin/Harvest/commanders`), behind admin BasicAuth + same-origin guard. No new packages.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| application → local SQLite/Postgres | `EnsureSchemaAsync` index DDL at startup; compile-time raw-string literal, no runtime input reaches it | index DDL (no data) |
| browser → `GET /Admin/Harvest/commanders` | New AJAX-only partial endpoint; cross-origin callers and attacker-controlled `page` query may target it; sits behind `BasicAuthMiddleware` on `/Admin/*` + `SameOriginRequestValidator` | curated harvested-commander rows (operator-owned, low sensitivity) |
| server HTML → browser `innerHTML` | The same-origin Razor partial is injected into `#commanders-grid-container` via `innerHTML` | Razor-encoded grid rows |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-44-DDL-01 | Denial of Service | `EnsureSchemaAsync` index DDL on a large `deck_queue` | accept (LOW) | `CREATE INDEX IF NOT EXISTS` (non-`CONCURRENT`) + `DROP INDEX IF EXISTS` (metadata-only, fast) inside the existing best-effort try/catch that swallows `DbException`. Prod table is small (harvested commanders, ~1.3k distinct). Same startup-DDL pattern as prior phases. No new attack surface. | closed |
| T-44-DDL-02 | Tampering / Injection | static index DDL string | accept (none) | The DDL is a compile-time C# raw-string literal (`CategoryKnowledgeRepository.cs:107-123`) with no interpolation or parameters — no injection vector. | closed |
| T-44-DDL-03 | Denial of Service | silent regression to no commander index | mitigate (MED) | The fail-swallowing try/catch could hide a failed `CREATE`. Mitigated by statement ORDER inside the single batched literal: new index `CREATE` (`:115`) precedes both `DROP`s (`:116-117`), so a failed create aborts the batch before the drops run and the old indexes survive — the commander query never loses index backing. EXPLAIN further confirms the count/paged queries remain index-backed (no full table scan) even when the planner selects `ix_deck_queue_processed`. | closed |
| T-44-csrf | Spoofing / Information Disclosure | `GET /Admin/Harvest/commanders` | mitigate (HIGH) | `SameOriginRequestValidator.IsValid(Request)` is the first statement of `Commanders` (`AdminHarvestController.cs:112`); a cross-origin request (Origin/Referer present + mismatched) → `StatusCode(403)` (`:115`). Verified by `Commanders_CrossOrigin_Returns403` (SC4) and live curl (Origin: evil.test → 403). A bare no-header direct-nav GET is allowed by validator design (`SameOriginRequestValidator.cs:31`) but stays behind `BasicAuthMiddleware` on `/Admin/*` — not an unauthenticated leak. | closed |
| T-44-page | Tampering | `page` query parameter | mitigate (LOW) | `page = Math.Max(page, 1)` (`:119`) then `page = Math.Min(page, deckTotalPages)` (`:123`) clamp BEFORE any DB read — prevents negative/overflow OFFSET. Guard runs after the same-origin check. | closed |
| T-44-xss-server | Information Disclosure (XSS) | `_CommandersGrid.cshtml` row rendering | accept (LOW) | All row fields (`@c.CommanderName`, `@c.DeckCount`, timestamp) are Razor-HTML-encoded by default; no `Html.Raw`; no user-controlled content concatenated into markup. | closed |
| T-44-xss-client | Information Disclosure (XSS) | `innerHTML` swap in `loadCommandersGrid` | accept (LOW) | `container.innerHTML = html` (`admin-harvest.ts:167`) injects only the server's own same-origin Razor partial (encoded server-side, fetched with `credentials:'same-origin'`). The loading/error HTML (`:156/161/178`) are static string constants with no input. No untrusted data is concatenated client-side. | closed |
| T-44-csrf-client | Spoofing | client fetch to the endpoint | mitigate (MED) | Client fetches with `credentials: 'same-origin'` (`:101/122`); the browser attaches the same-origin Origin header that the server-side validator checks (T-44-csrf). The server is authoritative; the client has no separate guard by design. | closed |
| T-44-SC | Tampering / Supply Chain | npm/pip/cargo/NuGet installs | n/a | Zero new packages. `git diff --stat f70953d~1..007c66b -- *.csproj */package.json *.sln` = empty. TypeScript change is hand-written, no new npm dep. RESEARCH.md Package Legitimacy Audit = N/A. | closed |

*Status: open · closed*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-44-1 | T-44-DDL-01 | Startup index DDL is non-concurrent but the prod `deck_queue` is small and the DDL matches the established best-effort startup pattern; brief lock acceptable. | Claude (secure-phase 44) | 2026-06-14 |
| AR-44-2 | T-44-xss-server / T-44-xss-client | XSS surface accepted because all injected HTML is server-rendered Razor (default-encoded) delivered same-origin; no client-side concatenation of untrusted data. Standard ASP.NET encoding is the control. | Claude (secure-phase 44) | 2026-06-14 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-14 | 9 | 9 | 0 | Claude (secure-phase 44; mitigations cross-verified against shipped commits f70953d + dc62109 + 4f6f486; controller/validator/DDL/TS code points grepped; live smoke confirmed cross-origin→403; Web.Tests 607/0/6skip incl SC4, Core 342/342) |

---

## Outstanding (non-blocking)

- **Not a security item — perf note (D-10):** SQLite's planner selects `ix_deck_queue_processed` over the new partial index on current data; queries remain index-backed (no full table scan), so the T-44-DDL-03 DoS concern is unaffected. Tracked for phase verification, not security.
- **Forward note:** Phase 49 (Dapper) will convert `CategoryKnowledgeRepository` query methods; the same-origin/clamp guards live in the controller (unaffected) and the index DDL stays raw — re-confirm no security regression when 49 lands.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / n/a)
- [x] Accepted risks documented in Accepted Risks Log (AR-44-1, AR-44-2)
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-14

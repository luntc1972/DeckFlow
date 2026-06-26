# Prod Probe Results — Content Artifact Reconcile

**Phase:** 65 — Prod Content Artifact Reconcile
**Run:** 2026-06-22, read-only (Render MCP `query_render_postgres`, postgresId `dpg-d7oj8iugvqtc73fso0g0-a`; `list_logs` on `srv-d7gmufkp3tds73a29m30`). No prod writes.

---

## Headline

**Published-orphan count = 10** (severity gate for Plan 02).

- Root cause is NOT "missing from `/data`". The live site serves from **`/app/content-kb`** (the
  committed repo tree baked into the Docker image), confirmed by the prod startup log. See
  `65-DATA01-DECISION.md` "Correction to the serving source".
- The 10 orphans are visible rows whose `.md` is absent from the committed repo `content-kb/` tree:
  - **9 × `salubrious-snail/*`** — a **slug mismatch**. The repo dir is `salubrioussnail` (no
    hyphen); all 9 ids exist there. The prod DB rows point at `salubrious-snail` (hyphen). Bodies
    exist in the image, just at a different path than the DB says.
  - **1 × `the-command-zone/e3qGnuupp8U.md`** — genuinely not committed (a Phase-58 dogfood
    distill; only `f8782tCIwmk.md` + `s_B1wCIWGR0.md` are committed for that creator).
- `content.kb.enabled = TRUE` in prod → `/content-kb` route is LIVE → these 10 detail pages render
  body-less (`ArtifactUnavailable: true`) right now.

This refutes RESEARCH assumption **A3** ("orphans predominantly `is_visible=FALSE`"): there ARE
published orphans, but far fewer than the 86 feared, and with a low-effort fix.

---

## Query A — rows by source + visibility

| source | total | visible | hidden |
|--------|------:|--------:|-------:|
| Based Deck Department | 13 | 0 | 13 |
| Commander Baumi | 17 | 3 | 14 |
| RebellLily | 20 | 9 | 11 |
| Sal2brious | 10 | 1 | 9 |
| Salubrious Snail | 27 | 9 | 18 |
| The Command Zone | 2 | 1 | 1 |
| The Trinket Mage | 20 | 2 | 18 |
| **Total** | **109** | **25** | **84** |

## Query B — counts by visibility tier

| is_visible | is_hidden | approval_status | n |
|-----------|-----------|-----------------|--:|
| true | false | approved | 25 |
| false | false | pending | 42 |
| false | true | pending | 42 |

→ 25 published (visible + not-hidden + approved); 84 not-visible (cosmetic — never served).

## Query C — visible artifact paths vs committed repo tree (main HEAD)

25 visible `artifact_path`s checked against `git cat-file -e HEAD:<path>` in the repo (= what's
baked at `/app`). **15 present, 10 missing.**

Present (serve fine): commander-baumi ×3, rebelllily ×9, sal2brious ×1, the-trinket-mage ×2.

Missing (published orphans):
```
content-kb/salubrious-snail/3B4UZgJ3LE0.md   (body exists at salubrioussnail/3B4UZgJ3LE0.md)
content-kb/salubrious-snail/7E4AwxKeyRY.md   (body exists at salubrioussnail/7E4AwxKeyRY.md)
content-kb/salubrious-snail/A4oASMevloo.md   (body exists at salubrioussnail/A4oASMevloo.md)
content-kb/salubrious-snail/C2k8XJ_jGKc.md   (body exists at salubrioussnail/C2k8XJ_jGKc.md)
content-kb/salubrious-snail/D2MRb3j4iYk.md   (body exists at salubrioussnail/D2MRb3j4iYk.md)
content-kb/salubrious-snail/ISr_7Z_lzWQ.md   (body exists at salubrioussnail/ISr_7Z_lzWQ.md)
content-kb/salubrious-snail/kR3C_OC5BzU.md   (body exists at salubrioussnail/kR3C_OC5BzU.md)
content-kb/salubrious-snail/mDHrUxOWNHA.md   (body exists at salubrioussnail/mDHrUxOWNHA.md)
content-kb/salubrious-snail/Oh_a34vdtIA.md   (body exists at salubrioussnail/Oh_a34vdtIA.md)
content-kb/the-command-zone/e3qGnuupp8U.md   (NOT committed anywhere — true missing artifact)
```

## /data file listing (operator follow-up — NOT the serving source)

Not required for severity: `/data` is not what the live site reads (resolver = `/app`). An SFTP
listing of `/data/content-kb` would only describe the dead DirectPush upload target. Skipped as
non-authoritative. If the publish model is later moved to `/data` (via `ContentKb__ContentBase`),
re-probe `/data` then.

---

## Severity gate → decision-tree branch (for Plan 02)

**Published-orphan count = 10 > 0.** Bodies for 9 of them EXIST in the repo (slug mismatch), so this
is NOT the research's "Option A re-upload from lost local artifacts" case. The right reconcile is a
**targeted re-point / unpublish**, decided per group in Plan 02:

- **9 salubrious-snail** → re-point the prod DB `artifact_path` slug `salubrious-snail` →
  `salubrioussnail` (bodies already live in the image). Operator-run prod-DB UPDATE (AI never writes
  prod). No re-upload, no information loss. ALSO fix the local Studio DB + the seed so a future
  deploy doesn't reintroduce the hyphen.
- **1 the-command-zone `e3qGnuupp8U`** → either commit the artifact to the repo `content-kb/` tree
  (if the operator has the distill output locally) and redeploy, or unpublish the row.

Plan 02 records and executes the chosen path; Plan 03's `content-kb-check` provides the repeatable
post-reconcile gate (point it at the repo root as the content base).

No secrets, connection strings, or raw exception text were recorded (D-07).

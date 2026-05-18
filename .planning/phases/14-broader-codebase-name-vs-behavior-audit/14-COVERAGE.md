# Phase 14 Coverage Report

**Generated:** 2026-05-18 by Plan 14-04
**HEAD:** 34332c79f2fec6783c7637f145c0ac1e1927de23

---

## Gate 1 — Warning count

- Baseline (14-BASELINE.md): 0
- HEAD: 0
- Status: **PASS**

Build command re-run (D-09 literal from 14-BASELINE.md):

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --configuration Release --no-incremental --nologo 2>&1 | tail -3
# Result: Build succeeded. 0 Warning(s) 0 Error(s)
```

---

## Gate 2 — XML Coverage Diff

- Script: see 14-AUDIT-REPORT.md `## XML Coverage Diff (AUDIT-03 verification mechanism)`
- Per-project coverage table (5 explicit rows):

| Project | Expected | Documented | Missing | InScope | AllowlistReason | GateStatus |
| ------- | -------- | ---------- | ------- | ------- | --------------- | ---------- |
| DeckFlow.Core | 27 | 62 | 0 | all | (none) | PASS |
| DeckFlow.CLI | 0 | 0 | 0 | all | (none — 0 public types) | PASS |
| DeckFlow.Core.Tests | 10 | 11 | 0 | all | (none) | PASS |
| DeckFlow.Web.Tests | 56 | 70 | 0 | all | (none) | PASS |
| DeckFlow.Web | 199 | 196 | 0 | 3 | v1.1-era NoWarn 1591/1573/1587 | PASS |

- In-scope-required for Web (verified PRESENT in DeckFlow.Web.xml): `ScryfallTaggerLookupService`, `IScryfallTaggerLookupService` (Plan 14-02 renames), `DeckPageTab` (Plan 14-03 opt-in).
- Allowlist: v1.1-era undoc'd Web types under Web.csproj `NoWarn 1591;1573;1587` per CONTEXT.md "Deferred Ideas" — out of Phase 14 scope.
- Status: **PASS**

### Backfill fixes applied during Gate 2

Two types in DeckFlow.Core were missing `<summary>` (Plan 14-03 gaps discovered during coverage diff):

1. `ArchidektCacheRunResult` (`DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs:185`) — added summary "Holds aggregate statistics for a completed Archidekt deck-cache run."
2. `CategoryKnowledgeRow` (`DeckFlow.Core/Reporting/CategoryKnowledgeReporter.cs` nested record) — added summary "Represents a single aggregated card-category observation row in the knowledge cache."

Committed as `refactor(14-04): backfill missed <summary> on ArchidektCacheRunResult and CategoryKnowledgeRow`.

Also fixed a broken `<see cref="MechanicLookupService"/>` in `DeckFlow.Web.Tests/MechanicLookupServiceTests.cs` — class is `WotcMechanicLookupService`; the broken cref produced CS1574 warning when `GenerateDocumentationFile` was enabled for Web.Tests. Fixed in the same commit as the Web.Tests csproj flip.

### Literal coverage diff script

```bash
# Step 1: Extract every public type per project from source
for proj in DeckFlow.Core DeckFlow.Web DeckFlow.CLI DeckFlow.Core.Tests DeckFlow.Web.Tests; do
  grep -rEn "^[[:space:]]*public +(sealed +)?(class|interface|record) +([A-Z][A-Za-z0-9_]*)" --include="*.cs" $proj/ \
    | grep -oE "(class|interface|record) +[A-Z][A-Za-z0-9_]*" \
    | awk '{print $2}' | sort -u > /tmp/expected-$proj.txt
done

# Also extract public enums for DeckFlow.Web
grep -rEn "^[[:space:]]*public +enum +([A-Z][A-Za-z0-9_]*)" --include="*.cs" DeckFlow.Web/ \
  | grep -oE "enum +[A-Z][A-Za-z0-9_]*" \
  | awk '{print $2}' >> /tmp/expected-DeckFlow.Web.txt
sort -u -o /tmp/expected-DeckFlow.Web.txt /tmp/expected-DeckFlow.Web.txt

# Step 2: Extract documented types from XML outputs
for proj in DeckFlow.Core DeckFlow.Web DeckFlow.CLI DeckFlow.Core.Tests DeckFlow.Web.Tests; do
  grep -hoE "<member name=\"T:[A-Za-z0-9._]+" $proj/bin/Release/net10.0/$proj.xml 2>/dev/null \
    | sed 's|.*\.||' | sort -u > /tmp/documented-$proj.txt
done

# Step 3: Per-project missing check
for proj in DeckFlow.Core DeckFlow.CLI DeckFlow.Core.Tests DeckFlow.Web.Tests; do
  comm -23 /tmp/expected-$proj.txt /tmp/documented-$proj.txt
done
# Above should produce no output when gate passes.

# Web in-scope-required check
cat > /tmp/web-inscope-required.txt <<'WEB_REQUIRED'
ScryfallTaggerLookupService
IScryfallTaggerLookupService
DeckPageTab
WEB_REQUIRED
sort -u -o /tmp/web-inscope-required.txt /tmp/web-inscope-required.txt
comm -23 /tmp/web-inscope-required.txt /tmp/documented-DeckFlow.Web.txt
# Should be empty (0 missing in-scope Web types)
```

---

## Gate 3 — Test discovery

- Baseline test count (14-BASELINE.md): 487
- HEAD discovered count: 487
- WSL discovery result: PASS (no timeout; completed within 90s timeout)
- Command: `timeout 90 "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln --no-build --configuration Release --list-tests`
- Status: **PASS**

---

## Final result

- Gate 1: PASS
- Gate 2: PASS
- Gate 3: PASS
- **Phase 14 AUDIT-03 verified.**

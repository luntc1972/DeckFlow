# Manabase Community Baseline — Increment 1b (UI) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (or subagent-driven-development) to implement task-by-task. Steps use checkbox (`- [ ]`) tracking.

**Goal:** Surface the Increment-1a community land baseline in the manabase UI: auto-classify the deck's bracket (reuse `IBracketClassificationService`), let the user override via a B2–B5 selector, and render the baseline line beside the Karsten target — all still behind `analysis.manabase.baseline` (OFF → byte-identical, no selector, no line, no classification call).

**Architecture:** The controller resolves an effective bracket (selector override → else, only when the flag is ON, auto-classify the deck; B1→B2; graceful null on failure) and threads it (with its provenance) into `ManabaseAnalysisOptions.Bracket` + `.BracketSource`. The 1a service resolver already turns that into the `ManabaseCommunityBaseline` block; 1b extends it to honor an `Auto` provenance. The block flows onto `ManabaseViewModel`; the view renders a baseline line beside the Karsten line and a B2–B5 pill selector (mirroring `Bracket.cshtml`), defaulted to the resolved bracket. No new NuGet deps; compiled JS never committed.

**Tech Stack:** C# 12/.NET 10, Razor, existing manabase TypeScript, xUnit (`DeckFlow.Web.Tests`), Playwright for themes/mobile. LF; changed lines pass the format gate.

**Build/test:**
- Build web: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj`
- Controller/service tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "ManabaseBracketResolution|ManabaseCommunityBaseline"`
- Full Web suite; UI checks via `scripts/run-web-test.sh` + `npx --no-install playwright test` (WSL headless, `env -u DISPLAY -u WAYLAND_DISPLAY`), NEVER a Windows-host browser.

---

## Patterns/anchors (confirmed file:line)

- **Controller entry points** (`DeckFlow.Web/Controllers/ManabaseController.cs`): `Manabase` POST (`:93`, builds the report `ManabaseViewModel` at `:117-139`), `Download` (`:153`), `Load` (`:63`); `NormalizeKnobs` (`:241-252`); `RunAnalysisAsync` (`:260-275`, builds `ManabaseAnalysisOptions`); `IsFocusedTierEnabled` (`:365`, shows `_featureFlags` is already injected). `BuildCommanderSelectionViewModel` (`:279-300`).
- **Classifier** (`DeckFlow.Web/Services/Bracket/IBracketClassificationService.cs:23-28`): `Task<BracketClassificationResult> ClassifyAsync(string deckSource, int? targetBracketNumber, string platform, string? deckName, CancellationToken)`. Result `.Classification.BracketNumber` (int 1–5, `DeckFlow.Core/Bracket/BracketClassification.cs:32-33`). Combos are graceful-null (never throws on Spellbook down).
- **1a service resolver** (`DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`): `ResolveBaseline(options)` → `(int Bracket, ManabaseBracketSource Source)`; `ManabaseAnalysisOptions` (`:51-73`); `ManabaseCommunityBaseline` block already attached to the success result. Flag const `BaselineFlagKey = "analysis.manabase.baseline"`; `IsFlagOn`.
- **Request** (`DeckFlow.Web/Models/ManabaseRequest.cs`): fields incl. `Mode`, computed `DeckSource` (`:95-96`).
- **View model** (`DeckFlow.Web/Models/ManabaseViewModel.cs`): add a `CommunityBaseline` prop.
- **View** (`DeckFlow.Web/Views/Deck/Manabase.cshtml`): Karsten line `<p class="manabase-summary-lands">` at `:431-442` (cEDH range block `:433-440` is the precedent for a beside-Karsten line); form/knobs area (Mode radios) ~`:180`.
- **Selector markup precedent** (`DeckFlow.Web/Views/Deck/Bracket.cshtml:75-88`): `manabase-segmented` / `manabase-pills` / `manabase-pill` radio-pill fieldset keyed on `tier.Number`. These CSS classes are the manabase page's own.
- **Bracket labels** (`DeckFlow.Web/Models/CommanderBracketCatalog.cs`): `Options` (`Value`/`Label`), `Find`, `IsCedh`.
- **Manabase TS**: `DeckFlow.Web/wwwroot/ts/manabase-overrides.ts` → gitignored `wwwroot/js/*.js`.
- **CSS**: layout in `wwwroot/css/site-common.css`; per-theme color only in `site-<guild>.css`.

---

## File Structure

**Modify:**
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — add `ManabaseAnalysisOptions.BracketSource`; extend `ResolveBaseline` to honor it.
- `DeckFlow.Web/Models/ManabaseRequest.cs` — add `int? Bracket`.
- `DeckFlow.Web/Controllers/ManabaseController.cs` — inject `IBracketClassificationService`; `NormalizeKnobs` bracket clamp; `ResolveEffectiveBracketAsync`; thread bracket+source into options; put `CommunityBaseline` on the view model.
- `DeckFlow.Web/Models/ManabaseViewModel.cs` — add `CommunityBaseline`.
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` — baseline line + B2–B5 selector.
- `DeckFlow.Web/wwwroot/css/site-common.css` — any baseline-line layout (only if needed).
- (Optional) `DeckFlow.Web/wwwroot/ts/manabase-overrides.ts` — submit-on-change for the selector.

**Create:**
- `DeckFlow.Web.Tests/ManabaseBracketResolutionTests.cs` — controller/service bracket-resolution + Auto/Override/Fallback provenance.
- (If added) a Playwright spec under the existing e2e folder for the baseline line/selector (themes + mobile).

---

## Task 1: Service — carry provenance through options (Auto vs Override)

**Files:** Modify `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`, Create `DeckFlow.Web.Tests/ManabaseBracketResolutionTests.cs`

- [ ] **Step 1: Write the failing service test** in `ManabaseBracketResolutionTests.cs` (mirror the 1a `ManabaseCommunityBaselineWiringTests` construction — same fakes for `IFeatureFlagCache` + `IManabaseBaselineProvider`). Assert that when `options.Bracket` is set with `BracketSource = Auto`, the resulting `CommunityBaseline.BracketSource == Auto` (today the 1a resolver hardcodes `Override` for any non-null bracket — this test fails until Step 2):

```csharp
// Flag ON, provider has B4, options.Bracket=4 with BracketSource=Auto → block reports Auto (not Override).
// (Construct the service like ManabaseCommunityBaselineWiringTests; pass options with Bracket=4, BracketSource=ManabaseBracketSource.Auto.)
```
Also keep cases: `Bracket=3, BracketSource=Override → Override`; `Bracket=null → Fallback + mode-derived` (unchanged from 1a).

- [ ] **Step 2: Add `BracketSource` to `ManabaseAnalysisOptions`** (after `Bracket`):

```csharp
    /// <summary>
    /// How <see cref="Bracket"/> was chosen (Auto = deck-classified, Override = user selector).
    /// Null lets the service label it (Override for an explicit bracket, Fallback for the mode default).
    /// </summary>
    public ManabaseBracketSource? BracketSource { get; init; }
```

- [ ] **Step 3: Honor it in `ResolveBaseline`:**

```csharp
    private static (int Bracket, ManabaseBracketSource Source) ResolveBaseline(ManabaseAnalysisOptions options)
        => options.Bracket is int explicitBracket
            ? (explicitBracket, options.BracketSource ?? ManabaseBracketSource.Override)
            : (options.Mode switch
            {
                ManabaseMode.Cedh => 5,
                ManabaseMode.Focused => 3,
                _ => 2,
            }, ManabaseBracketSource.Fallback);
```

- [ ] **Step 4:** Build web (0/0); `--filter "ManabaseBracketResolution|ManabaseCommunityBaselineWiring"` → PASS.
- [ ] **Step 5: Commit.** `git commit -m "feat(manabase): thread bracket provenance (Auto/Override) through analysis options"`

---

## Task 2: Request field + normalization

**Files:** Modify `DeckFlow.Web/Models/ManabaseRequest.cs`, `DeckFlow.Web/Controllers/ManabaseController.cs`

- [ ] **Step 1: Add `Bracket`** to `ManabaseRequest` (after `Mode`):

```csharp
    /// <summary>
    /// Optional user-selected community-baseline bracket (2-5). Null = auto-classify from the deck.
    /// Clamped to 2-5 in NormalizeKnobs (B1/Exhibition is unsupported → treated as null).
    /// </summary>
    public int? Bracket { get; set; }
```

- [ ] **Step 2: Clamp in `NormalizeKnobs`** (`:241-252`), after the importance clamp (a hand-crafted post can carry any int; only 2-5 are valid, everything else → null = auto):

```csharp
        request.Bracket = request.Bracket is >= 2 and <= 5 ? request.Bracket : null;
```

- [ ] **Step 3: Failing test** in `ManabaseBracketResolutionTests.cs`: NormalizeKnobs coerces `Bracket=1`→null, `Bracket=6`→null, `Bracket=3`→3. (NormalizeKnobs is private static — test via a small `[Theory]` calling the public `Manabase`/`Load` action with a stub service, or expose the clamp through the action's observable behavior; if the existing tests already reach `NormalizeKnobs` via the controller, mirror that.)
- [ ] **Step 4:** Build (0/0); tests PASS.
- [ ] **Step 5: Commit.** `git commit -m "feat(manabase): add optional Bracket selector field + clamp (2-5)"`

---

## Task 3: Controller — resolve effective bracket (override → auto-classify → null)

**Files:** Modify `DeckFlow.Web/Controllers/ManabaseController.cs`

- [ ] **Step 1: Inject `IBracketClassificationService`** into the controller ctor (mirror an existing injected dependency; `_featureFlags` is already present — reuse it). Add a private readonly field.

- [ ] **Step 2: Add a baseline-flag check + the resolver.** Mirror `IsFocusedTierEnabled` (`:365`) for the flag read:

```csharp
    private bool IsBaselineFlagEnabled()
        => _featureFlags is { } flags
            && flags.Snapshot().TryGetValue("analysis.manabase.baseline", out bool on)
            && on;

    // Explicit selector wins (Override). Otherwise, only when the baseline flag is on, auto-classify
    // the deck (B1/Exhibition -> B2). Classification failure or flag-off -> null, so the service falls
    // back to the mode-derived bracket. Never throws (the classifier is already graceful on combos).
    private async Task<(int? Bracket, ManabaseBracketSource? Source)> ResolveEffectiveBracketAsync(
        ManabaseRequest request, CancellationToken cancellationToken)
    {
        if (request.Bracket is int chosen)
        {
            return (chosen, ManabaseBracketSource.Override);
        }

        if (!IsBaselineFlagEnabled())
        {
            return (null, null);
        }

        try
        {
            BracketClassificationResult classification = await _bracketClassification.ClassifyAsync(
                request.DeckSource, targetBracketNumber: null, platform: "manabase",
                deckName: request.DeckName, cancellationToken);
            int bracket = Math.Max(2, classification.Classification.BracketNumber); // B1 -> B2
            return (bracket, ManabaseBracketSource.Auto);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Manabase bracket auto-classification failed; using mode-derived bracket.");
            return (null, null);
        }
    }
```
(Confirm the controller has an `ILogger`; if not, use the existing logging field name or drop the log line.)

- [ ] **Step 3: Thread it into `RunAnalysisAsync`.** Make `RunAnalysisAsync` resolve the bracket and set both options fields. Since it is called by `Manabase` and `Download`, both get a consistent baseline:

```csharp
    private async Task<ManabaseAnalysisResult> RunAnalysisAsync(
        ManabaseRequest request,
        IReadOnlyDictionary<string, string> overrides,
        CancellationToken cancellationToken)
    {
        (int? bracket, ManabaseBracketSource? bracketSource) =
            await ResolveEffectiveBracketAsync(request, cancellationToken);

        return await _manabaseAnalysisService.AnalyzeAsync(
            request.DeckSource,
            request.DeckName,
            new ManabaseAnalysisOptions
            {
                Mode = request.Mode,
                CommanderImportance = request.CommanderImportance,
                CompanionDesignator = request.CompanionName,
                SelectedCommander = request.SelectedCommander,
                CostOverrides = overrides,
                Bracket = bracket,
                BracketSource = bracketSource,
            },
            cancellationToken);
    }
```
(RunAnalysisAsync becomes `async` — update its signature/`await` callers if needed; the current callers already `await` it.)

- [ ] **Step 4: Put the block on the view model.** In the `Manabase` action's report `ManabaseViewModel` initializer (`:117-139`), add `CommunityBaseline = result.CommunityBaseline,`.

- [ ] **Step 5: Tests** in `ManabaseBracketResolutionTests.cs` (mirror existing controller tests that stub `_manabaseAnalysisService` + fakes): override → options.Bracket set + Source=Override + no classifier call; flag ON + no override → classifier called, B1 result → options.Bracket=2 + Source=Auto; classifier throws → options.Bracket=null (graceful); flag OFF → classifier NOT called + options.Bracket=null. Use a fake `IBracketClassificationService` recording calls + returning a canned `BracketNumber`.
- [ ] **Step 6:** Build (0/0); full Web suite → green. Commit: `git commit -m "feat(manabase): auto-classify deck bracket for the community baseline (flag-gated)"`

---

## Task 4: View model + Razor display line

**Files:** Modify `DeckFlow.Web/Models/ManabaseViewModel.cs`, `DeckFlow.Web/Views/Deck/Manabase.cshtml`

- [ ] **Step 1: Add the view-model prop:**

```csharp
    /// <summary>Optional empirical community land baseline (present only when the flag is on and data exists).</summary>
    public ManabaseCommunityBaseline? CommunityBaseline { get; init; }
```

- [ ] **Step 2: Render the baseline line** in `Manabase.cshtml` immediately after the Karsten `<p class="manabase-summary-lands">` block (after `:442`), guarded on the block. Use the resolved bracket label from `CommanderBracketCatalog`/a stable name map, the sample size, and a provenance hint:

```cshtml
@if (Model.CommunityBaseline is { } baseline)
{
    var bracketName = baseline.Bracket switch
    {
        2 => "Core",
        3 => "Upgraded",
        4 => "Optimized",
        5 => "cEDH",
        _ => $"B{baseline.Bracket}"
    };
    var detected = baseline.BracketSource == ManabaseBracketSource.Auto ? " (auto-detected)" : null;
    <p class="manabase-summary-lands manabase-community-baseline">
        <strong>Community baseline</strong> · @bracketName@detected
        (@baseline.DeckCount.ToString("N0") decks): <strong>~@baseline.AvgLands.ToString("F1") lands.</strong>
        <span class="manabase-baseline-source">Source: @(baseline.Source ?? "community sample").</span>
    </p>
}
```
(Do not touch the Karsten line or the cEDH-range block above it. This is additive markup only.)

- [ ] **Step 3:** If the new line needs any layout spacing, add a `.manabase-community-baseline` / `.manabase-baseline-source` rule to `wwwroot/css/site-common.css` (muted/secondary text for the source span). Keep color tokens theme-driven (`--panel`/`--muted`), no hardcoded colors. Only add CSS if the default inherited styling is inadequate.
- [ ] **Step 4:** Build (0/0). Commit: `git commit -m "feat(manabase): render community baseline line beside Karsten target"`

---

## Task 5: Bracket selector (B2–B5 pills)

**Files:** Modify `DeckFlow.Web/Views/Deck/Manabase.cshtml`, (optional) `DeckFlow.Web/wwwroot/ts/manabase-overrides.ts`

- [ ] **Step 1: Add the selector** to the form/knobs area (near the Mode radios ~`:180`), mirroring `Bracket.cshtml:75-88` but with the 4 supported brackets (B1/Exhibition omitted) and defaulted to the resolved bracket (`Model.CommunityBaseline?.Bracket ?? Model.Request.Bracket`). Only render it when the baseline flag is on for this request (gate with the same signal the controller uses — expose an `IsBaselineEnabled`/`ShowCommunityBaseline` bool on the view model set from the controller, OR render whenever `Model.CommunityBaseline is not null`; prefer a dedicated `ShowCommunityBaseline` view-model bool so the selector shows even before the first successful analysis). Markup:

```cshtml
@if (Model.ShowCommunityBaseline)
{
    var selectedBracket = Model.CommunityBaseline?.Bracket ?? Model.Request.Bracket;
    <fieldset class="manabase-segmented" role="radiogroup">
        <legend>Community baseline bracket</legend>
        <div class="manabase-pills">
            @foreach (var (num, name) in new[] { (2, "Core"), (3, "Upgraded"), (4, "Optimized"), (5, "cEDH") })
            {
                <label class="manabase-pill @(selectedBracket == num ? "is-selected" : null)">
                    <input type="radio" name="Bracket" value="@num"
                           checked="@(selectedBracket == num ? "checked" : null)" />
                    <span>B@(num) @name</span>
                </label>
            }
            <label class="manabase-pill @(selectedBracket is null ? "is-selected" : null)">
                <input type="radio" name="Bracket" value="" checked="@(selectedBracket is null ? "checked" : null)" />
                <span>Auto</span>
            </label>
        </div>
    </fieldset>
}
```
(The "Auto" pill posts an empty `Bracket` → `NormalizeKnobs` leaves it null → controller auto-classifies.)

- [ ] **Step 2: Add `ShowCommunityBaseline`** to `ManabaseViewModel` (bool, set from the controller's baseline-flag check in every render path — report AND commander-selection AND the initial GET so the selector is present). Wire it in the controller (`Manabase` `:117-139`, `BuildCommanderSelectionViewModel`, and the GET/`Index` action). Value = the controller's `IsBaselineFlagEnabled()`.
- [ ] **Step 3 (optional): submit-on-change.** If the UX should re-run analysis when the bracket changes without clicking Analyze, add a tiny handler in `manabase-overrides.ts` that submits the form on `change` of `input[name="Bracket"]`. Otherwise the user picks a bracket and clicks the existing Analyze button. Keep it minimal; do NOT commit compiled JS (`wwwroot/js/*.js` is gitignored).
- [ ] **Step 4:** Build (0/0). Commit: `git commit -m "feat(manabase): B2-B5 community-baseline bracket selector"`

---

## Task 6: UI verification (themes + mobile) + tests

- [ ] **Step 1: Start the test server** headless: `scripts/run-web-test.sh` (sets `DECKFLOW_DISABLE_AUTO_BROWSER=true`; NEVER opens a Windows-host browser). Confirm it is listening before driving.
- [ ] **Step 2: Enable the flag locally** for the check (seed/toggle `analysis.manabase.baseline` ON in the local SQLite flags DB, or via the admin flags UI) so the baseline line + selector render.
- [ ] **Step 3: Playwright checks** (`npx --no-install playwright test`, WSL headless `env -u DISPLAY -u WAYLAND_DISPLAY ... --headed=false`): analyze a sample deck; assert the community-baseline line renders with the bracket name + sample size; assert the B2–B5 selector renders (4 brackets + Auto, no Exhibition); pick a bracket → re-analyze → line reflects the chosen bracket + "Community baseline · <name>" (not "auto-detected"). Capture screenshots at **desktop + mobile** viewports and in **2 themes** (per the web-page-change convention). Assert flag-OFF → neither line nor selector renders.
- [ ] **Step 4: xUnit** full Web suite green (`... test DeckFlow.Web.Tests/...`); the new `ManabaseBracketResolutionTests` pass; flag-OFF byte-identical still holds (1a wiring tests unchanged).
- [ ] **Step 5:** Commit any test files: `git commit -m "test(manabase): community-baseline UI + bracket-resolution coverage"`

---

## Task 7: Review for simplification

- [ ] **Step 1:** Review the 1b diff for reuse/simplification (e.g. the bracket-name map appears in both the display line and the selector — extract to one helper on the view model or a shared Razor local if it reads cleaner). If your harness has `/simplify`, run it; else review by hand.
- [ ] **Step 2:** Re-run `--filter "ManabaseBracketResolution"` + the full Web suite → PASS.
- [ ] **Step 3:** Commit if changed: `git add -A && git commit -m "chore(manabase): simplify community-baseline UI" || echo "nothing to simplify"`

---

## Self-Review notes (author)

- **Spec coverage (Increment 1 UI):** Component C (auto-classify via existing `IBracketClassificationService`, B1→B2, graceful, flag-ON only), Component E (B2–B5 selector reusing the `manabase-pill` markup; baseline line beside Karsten with sample size + source + auto-detected hint; themes/mobile). Backend (1a) already done; 1b only adds the input + display + the Auto-provenance plumbing.
- **Byte-identical-OFF preserved:** classification runs ONLY when the flag is on (no Spellbook call otherwise); selector + line gate on `ShowCommunityBaseline`/`CommunityBaseline` which are flag-driven; OFF → no new markup, no new upstream call.
- **Provenance correctness:** the Auto-vs-Override distinction is threaded via `options.BracketSource` (Task 1) so the display "(auto-detected)" hint is truthful — the /simplify altitude fix from 1a (single `ResolveBaseline`) is the extension point.
- **Cost:** auto-classify adds one graceful `IBracketClassificationService` call (deck re-load + Commander-Spellbook combo lookup) on the manabase path, but only when the flag is ON and no override is set — acceptable per spec (Non-Goals / Open Questions).
- **Reuse:** selector mirrors `Bracket.cshtml` pills + the page's own `manabase-segmented/manabase-pill` CSS; flag read mirrors `IsFocusedTierEnabled`; the baseline line sits with the existing `manabase-summary-lands` styling.
- **Constraints:** no new deps; layout CSS in `site-common.css` only; compiled JS never committed; LF; changed-lines format gate. Web-page change → xUnit + Playwright (desktop+mobile, 2 themes) per the project convention.
- **Deferred (Increment 2, EDHREC-gated):** per-commander rows + ramp/draw + on-the-fly fetch; not in 1b.

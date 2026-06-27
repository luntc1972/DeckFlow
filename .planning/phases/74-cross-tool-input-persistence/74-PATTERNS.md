# Phase 74: Cross-Tool Deck-Input Persistence - Pattern Map

**Mapped:** 2026-06-27
**Files analyzed:** 7 (1 new TS module + 5 modified views + 1 modified TS file)
**Analogs found:** 7 / 7

---

## Critical Architecture Constraint

`tsconfig.json` sets `"module": "none"` — every `.ts` file is compiled to an
independent `.js` bundle with **no `import`/`export`**. Scripts communicate only
through the shared `window.DeckFlow` namespace object (see `deck-sync.ts:2788`).
The new shared module must register its functions on `window.DeckFlow` and must be
loaded in each page's `@section Scripts` **before** `deck-sync.js` so the store
is available when `deck-sync.ts` fires `DOMContentLoaded`.

---

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---------------------|------|-----------|----------------|---------------|
| `wwwroot/ts/deck-input-store.ts` (NEW) | utility / shared module | event-driven, state | `wwwroot/ts/card-lookup.ts` | role-match |
| `wwwroot/ts/deck-sync.ts` (MODIFY) | utility / page script | event-driven | self (add calls to store) | self |
| `Views/Deck/DeckAnalysis.cshtml` (MODIFY) | view | request-response | `Views/Deck/Manabase.cshtml` | exact |
| `Views/Deck/Manabase.cshtml` (MODIFY) | view | request-response | self | self |
| `Views/Deck/CedhMetaGap.cshtml` (MODIFY) | view | request-response | `Views/Deck/DeckConvert.cshtml` | role-match |
| `Views/Deck/DeckConvert.cshtml` (MODIFY) | view | request-response | self | self |
| `Views/Deck/DeckPrimer.cshtml` (MODIFY) | view | request-response | `Views/Deck/DeckAnalysis.cshtml` | exact |

---

## Verified Field Map (actual file:line references)

### DeckAnalysis.cshtml — split-field shape

File: `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml`

| Field | Element / selector | Line |
|-------|--------------------|------|
| inputSource select | `select[name="DeckInputSource"]` | 153 |
| URL panel wrapper | `[data-sync-panel="chatgpt-deck-url"]` | 158 |
| URL input | `input[name="DeckUrl"]` (inside URL panel) | 160 |
| text panel wrapper | `[data-sync-panel="chatgpt-deck-text"]` | 164 |
| deck text textarea | `textarea[name="DeckText"]` (inside text panel) | 166 |
| Scripts block | `@section Scripts { ... }` | 943–946 |
| Loaded scripts | `card-lookup.js` then `deck-sync.js` | 944–945 |

### Manabase.cshtml — split-field shape

File: `DeckFlow.Web/Views/Deck/Manabase.cshtml`

| Field | Element / selector | Line |
|-------|--------------------|------|
| inputSource select | `select[id="manabase-input-source"][name="DeckInputSource"]` | 39 |
| URL panel wrapper | `[data-sync-panel="manabase-deck-url"]` | 45 |
| URL input | `input[id="manabase-deck-url"][name="DeckUrl"]` | 47 |
| text panel wrapper | `[data-sync-panel="manabase-deck-text"]` | 52 |
| deck text textarea | `textarea[id="manabase-deck-text"][name="DeckText"]` | 54 |
| Scripts block | `@section Scripts { ... }` | 569–571 |
| Loaded scripts | `deck-sync.js` only | 570 |

### CedhMetaGap.cshtml — combined single-field shape

File: `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml`

| Field | Element / selector | Line |
|-------|--------------------|------|
| combined deck field | `textarea[name="DeckSource"]` | 116 |
| Scripts block | `@section Scripts { ... }` | 640–642 |
| Loaded scripts | `deck-sync.js` only | 641 |

**Note:** No radio/select for inputSource. The restore mapping is:
- If canonical `inputSource == "PublicUrl"` → write `deckUrl` into `DeckSource`
- If canonical `inputSource == "PasteText"` → write `deckText` into `DeckSource`

When saving FROM meta-gap TO the store, the module must heuristically classify:
detect `value.match(/^https?:\/\//i)` → set `inputSource="PublicUrl"`, `deckUrl=value`;
otherwise `inputSource="PasteText"`, `deckText=value`.

### DeckConvert.cshtml — split-field shape, different select name

File: `DeckFlow.Web/Views/Deck/DeckConvert.cshtml`

| Field | Element / selector | Line |
|-------|--------------------|------|
| inputSource select | `select[name="InputSource"]` (NOT DeckInputSource) | 45 |
| URL panel wrapper | `[data-convert-panel="url"]` (NOT data-sync-panel) | 53 |
| URL input | `input[name="DeckUrl"]` (inside URL panel) | 55 |
| text panel wrapper | `[data-convert-panel="text"]` | 61 |
| deck text textarea | `textarea[name="DeckText"]` (inside text panel) | 63 |
| Scripts block | `@section Scripts { ... }` | 101–103 |
| Loaded scripts | `deck-sync.js` only | 102 |

**Note:** Convert uses `InputSource` (not `DeckInputSource`) and `data-convert-panel`
(not `data-sync-panel`). The panel-toggle logic already lives in `deck-sync.ts:2792–2812`.

### DeckPrimer.cshtml — split-field shape; loads scripts INLINE (no @section Scripts)

File: `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml`

| Field | Element / selector | Line |
|-------|--------------------|------|
| inputSource select | `select[name="DeckInputSource"]` | 119 |
| URL panel wrapper | `[data-sync-panel="primer-deck-url"]` | 124 |
| URL input | `input[name="DeckUrl"]` (inside URL panel) | 126 |
| text panel wrapper | `[data-sync-panel="primer-deck-text"]` | 130 |
| deck text textarea | `textarea[name="DeckText"]` (inside text panel) | 132 |
| **Script tags** | **Inline in page body (no @section Scripts)** | **298–299** |
| Loaded scripts | `deck-sync.js` then `primer-selection.js` (inline) | 298–299 |

**DeckPrimer uses inline `<script>` tags, NOT `@section Scripts`.** Add `deck-input-store.js`
before line 298 as a plain `<script src="~/js/deck-input-store.js" asp-append-version="true"></script>`.

---

## Request Model Field Summary

| Model | DeckInputSource select name | URL field name | Text field name |
|-------|----------------------------|----------------|-----------------|
| `DeckAnalysisRequest` | `DeckInputSource` (enum) | `DeckUrl` | `DeckText` |
| `ManabaseRequest` | `DeckInputSource` (enum) | `DeckUrl` | `DeckText` |
| `MetaGapRequest` | *(none — single field)* | *(n/a)* | `DeckSource` (combined) |
| `DeckConvertRequest` | `InputSource` (enum, diff name) | `DeckUrl` | `DeckText` |
| `DeckPrimerRequest` | `DeckInputSource` (enum) | `DeckUrl` | `DeckText` |

---

## Pattern Assignments

### `wwwroot/ts/deck-input-store.ts` (NEW — utility, event-driven/state)

**Best analog:** `wwwroot/ts/card-lookup.ts` (lines 90–132)

The `SINGLE_CARD_STATE_KEY` / `saveSingleCardState` / `loadSingleCardState` trio in
`card-lookup.ts` is the closest pattern: typed store object, single key, try/catch
wrapping every storage call, guard against corrupt JSON, return `null` on failure.

**Key pattern: constant + typed stored shape + save/load/clear** (`card-lookup.ts:90–132`):
```typescript
const SINGLE_CARD_STATE_KEY = 'card-lookup-single-state';

type StoredSingleCardState = {
  cardName: string;
  verifiedText: string;
  mechanicRules: SingleCardMechanicRule[];
};

const saveSingleCardState = (state: StoredSingleCardState): void => {
  try {
    window.sessionStorage.setItem(SINGLE_CARD_STATE_KEY, JSON.stringify(state));
  } catch {
    // sessionStorage may be disabled (private mode quotas, etc.) — silently skip.
  }
};

const loadSingleCardState = (): StoredSingleCardState | null => {
  try {
    const raw = window.sessionStorage.getItem(SINGLE_CARD_STATE_KEY);
    if (!raw) { return null; }
    const parsed = JSON.parse(raw) as Partial<StoredSingleCardState> | null;
    if (!parsed || typeof parsed.cardName !== 'string' ...) { return null; }
    return { cardName: parsed.cardName, ... };
  } catch {
    return null;
  }
};
```

**Secondary analog for IIFE wrapper:** `wwwroot/ts/content-kb.ts` (lines 1–12):
```typescript
((): void => {
  'use strict';
  const FILTER_STORAGE_KEY = 'deckflow.kb.filters';
  ...
})();
```

**Key pattern: window.DeckFlow registration** (`deck-sync.ts:2788–2789`):
```typescript
deckFlowWindow.DeckFlow = deckFlowWindow.DeckFlow ?? {};
deckFlowWindow.DeckFlow.attachActionButtons = attachActionButtons;
```
The new module follows the same pattern — extend the `DeckFlowNamespace` interface and
register `getLastDeck` / `setLastDeck` on `window.DeckFlow` so `deck-sync.ts` can call
`deckFlowWindow.DeckFlow?.getLastDeck?.()` without a hard reference.

**Key pattern: DOMContentLoaded double-guard** (`deck-sync.ts:2847–2849`):
```typescript
document.addEventListener('DOMContentLoaded', bootstrapDeckSync);
if (document.readyState !== 'loading') {
  bootstrapDeckSync();
}
```

**NEW pattern (no existing analog): fill-if-empty guard**
The existing sessionStorage restores (`card-lookup.ts:370–374`, `category-suggestions.ts:341–354`)
restore unconditionally. Phase 74 requires checking `element.value.trim() === ''` before
setting, so server-rendered POST-echoed values win over the session store. There is no existing
analog — the implementer must add this guard:
```typescript
// Fill only when the server rendered an empty field (GET navigation, not POST round-trip).
if (urlInput && urlInput.value.trim() === '') {
  urlInput.value = stored.deckUrl;
}
```

**Concrete implementation shape for `deck-input-store.ts`:**

```typescript
((): void => {
  'use strict';

  // Why: namespace prefix matches other keys in the codebase (see content-kb.ts FILTER_STORAGE_KEY)
  const LAST_DECK_KEY = 'deckflow.last-deck';
  // Why: cap deckText to avoid quota errors on rare 5 MB+ pastes; URL is always stored
  const DECK_TEXT_MAX_BYTES = 100_000;

  type LastDeckState = {
    inputSource: string;   // 'PublicUrl' | 'PasteText'
    deckUrl: string;
    deckText: string;
  };

  const setLastDeck = (state: LastDeckState): void => {
    try {
      const text = state.deckText.length > DECK_TEXT_MAX_BYTES
        ? ''
        : state.deckText;
      window.sessionStorage.setItem(LAST_DECK_KEY, JSON.stringify({
        inputSource: state.inputSource,
        deckUrl: state.deckUrl,
        deckText: text,
      }));
    } catch {
      // sessionStorage unavailable (quota, private mode) — degrade silently
    }
  };

  const getLastDeck = (): LastDeckState | null => {
    try {
      const raw = window.sessionStorage.getItem(LAST_DECK_KEY);
      if (!raw) { return null; }
      const parsed = JSON.parse(raw) as Partial<LastDeckState> | null;
      if (!parsed || typeof parsed.inputSource !== 'string') { return null; }
      return {
        inputSource: parsed.inputSource,
        deckUrl: typeof parsed.deckUrl === 'string' ? parsed.deckUrl : '',
        deckText: typeof parsed.deckText === 'string' ? parsed.deckText : '',
      };
    } catch {
      return null;
    }
  };

  // Register on window.DeckFlow (module:none — no import/export available)
  type DeckFlowWindow = Window & {
    DeckFlow?: {
      getLastDeck?: () => LastDeckState | null;
      setLastDeck?: (state: LastDeckState) => void;
      [key: string]: unknown;
    };
  };
  const win = window as DeckFlowWindow;
  win.DeckFlow = win.DeckFlow ?? {};
  win.DeckFlow.getLastDeck = getLastDeck;
  win.DeckFlow.setLastDeck = setLastDeck;

  // ... wiring (restore on load + save on change) added here or in deck-sync.ts
})();
```

---

### `wwwroot/ts/deck-sync.ts` (MODIFY — add per-tool wiring calls)

**Analog:** Self — existing `attachConvertForm` function (lines 2791–2845) shows the
pattern for finding a form by data attribute, reading its fields, and attaching event listeners.

**Wiring pattern for split-field tools** (model after `attachConvertForm`, `deck-sync.ts:2791–2812`):
```typescript
const attachConvertForm = (): void => {
  const form = document.querySelector<HTMLFormElement>('form[data-cache-key="deck-convert"]');
  if (!form) return;

  const inputSourceSelect = form.querySelector<HTMLSelectElement>('select[name="InputSource"]');
  const urlPanel = form.querySelector<HTMLElement>('[data-convert-panel="url"]');
  const textPanel = form.querySelector<HTMLElement>('[data-convert-panel="text"]');
  ...
  inputSourceSelect?.addEventListener('change', syncConvertPanels);
  syncConvertPanels();
};
```

The new wiring functions follow the same structure: find form, find fields, attach
`input`/`change` listeners that call `window.DeckFlow?.setLastDeck?.(...)`.

**Panel-visibility dependency:** The `DeckInputSource` change listener that toggles
URL/text panels already runs inside `initializeSyncInputModeUi` (line 546). The new
`setLastDeck` call on change must happen AFTER that handler runs, not before, so the
currently-visible panel is already correct.

**DOMContentLoaded invocation point** (`deck-sync.ts:2785–2786`):
```typescript
  attachConvertForm();
};
```
New wiring calls attach at the same end-of-bootstrap point.

---

### `Views/Deck/DeckAnalysis.cshtml` (MODIFY — add script reference)

**Analog:** `Views/Deck/Manabase.cshtml` (lines 569–571) — identical `@section Scripts` pattern.

**Script loading pattern** (`DeckAnalysis.cshtml:943–946`):
```cshtml
@section Scripts {
    <script src="~/js/card-lookup.js" asp-append-version="true"></script>
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

Add `deck-input-store.js` **before** `deck-sync.js` so the store is on `window.DeckFlow`
when deck-sync initializes:
```cshtml
@section Scripts {
    <script src="~/js/card-lookup.js" asp-append-version="true"></script>
    <script src="~/js/deck-input-store.js" asp-append-version="true"></script>
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

---

### `Views/Deck/Manabase.cshtml` (MODIFY — add script reference)

**Analog:** Self / `DeckAnalysis.cshtml` — same `@section Scripts` pattern.

**Script loading pattern** (`Manabase.cshtml:569–571`):
```cshtml
@section Scripts {
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

Becomes:
```cshtml
@section Scripts {
    <script src="~/js/deck-input-store.js" asp-append-version="true"></script>
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

---

### `Views/Deck/CedhMetaGap.cshtml` (MODIFY — add script reference)

**Analog:** `Views/Deck/DeckConvert.cshtml` — same single-script `@section Scripts` pattern.

**Script loading pattern** (`CedhMetaGap.cshtml:640–642`):
```cshtml
@section Scripts {
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

Becomes:
```cshtml
@section Scripts {
    <script src="~/js/deck-input-store.js" asp-append-version="true"></script>
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

---

### `Views/Deck/DeckConvert.cshtml` (MODIFY — add script reference)

**Analog:** `Views/Deck/CedhMetaGap.cshtml` — same `@section Scripts` pattern.

**Script loading pattern** (`DeckConvert.cshtml:101–103`):
```cshtml
@section Scripts {
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

Becomes:
```cshtml
@section Scripts {
    <script src="~/js/deck-input-store.js" asp-append-version="true"></script>
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

---

### `Views/Deck/DeckPrimer.cshtml` (MODIFY — add script reference, INLINE not @section Scripts)

**Analog:** All other views use `@section Scripts`; Deck Primer is the exception.

**Current script loading** (`DeckPrimer.cshtml:298–299`) — inline in page body:
```cshtml
<script src="~/js/deck-sync.js" asp-append-version="true"></script>
<script src="@Url.Content("~/js/primer-selection.js")"></script>
```

Add `deck-input-store.js` before `deck-sync.js`:
```cshtml
<script src="~/js/deck-input-store.js" asp-append-version="true"></script>
<script src="~/js/deck-sync.js" asp-append-version="true"></script>
<script src="@Url.Content("~/js/primer-selection.js")"></script>
```

Note that `primer-selection.js` uses `@Url.Content(...)` without `asp-append-version`
while the others use `src="~/js/..."` with `asp-append-version="true"`. Match the
existing pattern for `deck-input-store.js`: use `src="~/js/..."` with
`asp-append-version="true"`.

---

## Shared Patterns

### sessionStorage try/catch wrapper (apply to ALL storage calls)

**Source:** `wwwroot/ts/card-lookup.ts:98–104` and `content-kb.ts:34–44`

```typescript
// card-lookup.ts pattern — always wrap storage in try/catch:
const saveSingleCardState = (state: StoredSingleCardState): void => {
  try {
    window.sessionStorage.setItem(SINGLE_CARD_STATE_KEY, JSON.stringify(state));
  } catch {
    // sessionStorage may be disabled (private mode quotas, etc.) — silently skip.
  }
};

// content-kb.ts pattern (inline in persist):
const persist = (): void => {
  try {
    ...
    sessionStorage.setItem(FILTER_STORAGE_KEY, JSON.stringify(state));
  } catch {
    // sessionStorage may be unavailable (private mode / quota) — non-fatal.
  }
};
```

Apply to all `sessionStorage.setItem`, `getItem`, and `removeItem` calls in
`deck-input-store.ts`. Every call must be wrapped; storage quota or private-mode
failures are non-fatal.

### window.DeckFlow namespace merging (apply to cross-file shared functions)

**Source:** `wwwroot/ts/deck-sync.ts:2788–2789`

```typescript
deckFlowWindow.DeckFlow = deckFlowWindow.DeckFlow ?? {};
deckFlowWindow.DeckFlow.attachActionButtons = attachActionButtons;
```

Apply in `deck-input-store.ts` to register `getLastDeck`/`setLastDeck`. Use
`win.DeckFlow = win.DeckFlow ?? {}` to merge (not overwrite) any functions
registered by previously loaded scripts.

### `@section Scripts` script ordering convention

**Source:** `Views/Deck/DeckAnalysis.cshtml:943–946`

```cshtml
@section Scripts {
    <script src="~/js/card-lookup.js" asp-append-version="true"></script>
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```

Always add `deck-input-store.js` before `deck-sync.js`. The store must be on
`window.DeckFlow` before the DOMContentLoaded wiring in `deck-sync.ts` fires.

### IIFE module wrapper (apply to `deck-input-store.ts`)

**Source:** `wwwroot/ts/category-suggestions.ts:1`, `content-kb.ts:1`

```typescript
((): void => {
  'use strict';
  ...
})();
```

Wrap the entire module in an IIFE with `'use strict'` to avoid polluting the global scope.

---

## No Analog Found

| Pattern | Reason |
|---------|--------|
| Fill-if-empty guard (restore only when DOM field is blank) | No existing sessionStorage restore in the codebase checks `element.value.trim() === ''` before writing. All existing restores (`card-lookup.ts:370–374`, `category-suggestions.ts:341–354`) overwrite unconditionally. Phase 74 requires checking for empty first so POST-echoed values win. Implementer adds this pattern from scratch. |
| URL heuristic for MetaGap's combined `DeckSource` field | MetaGap exposes a single `textarea[name="DeckSource"]` with no inputSource radio. Detecting URL vs text via `value.match(/^https?:\/\//i)` is a new pattern not present elsewhere. |

---

## Metadata

**Analog search scope:** `DeckFlow.Web/wwwroot/ts/`, `DeckFlow.Web/Views/Deck/`,
`DeckFlow.Web/Models/`

**Files scanned:** 12 TS files, 13 view files, 5 request model files

**Pattern extraction date:** 2026-06-27

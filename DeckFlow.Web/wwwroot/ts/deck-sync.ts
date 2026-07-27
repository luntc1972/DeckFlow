const togglePanel = (selector: string, shouldHide: boolean): void => {
  document.querySelectorAll<HTMLElement>(selector).forEach(element => {
    element.classList.toggle('hidden', shouldHide);
    element.style.display = shouldHide ? 'none' : '';
  });
};

const DeckInputSource = {
  PasteText: 'PasteText',
  PublicUrl: 'PublicUrl',
} as const;

// Resolves the effective deck value from split URL/text inputs, mirroring the server-side
// DeckInputReconciler: prefer the selected mode, then fall back to the other field if blank.
const resolveSplitDeckValue = (form: HTMLFormElement, prefix: string): string => {
  const source = form.querySelector<HTMLSelectElement>(`select[name="${prefix}InputSource"]`)?.value;
  const url = form.querySelector<HTMLInputElement>(`input[name="${prefix}Url"]`)?.value.trim() ?? '';
  const text = form.querySelector<HTMLTextAreaElement>(`textarea[name="${prefix}Text"]`)?.value.trim() ?? '';
  return source === DeckInputSource.PublicUrl ? (url || text) : (text || url);
};

type DeckInputSourceValue = (typeof DeckInputSource)[keyof typeof DeckInputSource];

type PanelConfig = {
  selectName: string;
  urlSelector: string;
  textSelector: string;
};

type DeckSyncApiResponse = {
  reportText: string;
  deltaText: string;
  fullImportText: string;
  instructionsText: string;
  sourceSystem: string;
  targetSystem: string;
  printingConflicts: Array<{
    cardName: string;
    archidektSetCode: string;
    archidektCollectorNumber: string;
    archidektCategory?: string | null;
    moxfieldSetCode?: string | null;
    moxfieldCollectorNumber?: string | null;
  }>;
};

type DeckSyncSystem = 'Moxfield' | 'Archidekt';

interface DeckFlowNamespace {
  attachTypeahead?: (
    input: HTMLInputElement,
    panel: HTMLDivElement,
    minChars: number,
    onPick: (name: string) => void
  ) => void;
  createTypeaheadPanel?: (anchor: HTMLElement) => HTMLDivElement;
  attachDfSelect?: () => void;
  refreshDfSelect?: (select: HTMLSelectElement) => void;
  attachActionButtons?: () => void;
}

type DeckFlowWindow = Window & {
  DeckFlow?: DeckFlowNamespace;
};

const deckFlowWindow = window as DeckFlowWindow;

const panelConfigs: PanelConfig[] = [
  {
    selectName: 'MoxfieldInputSource',
    urlSelector: '[data-sync-panel="moxfield-url"]',
    textSelector: '[data-sync-panel="moxfield-text"]',
  },
  {
    selectName: 'ArchidektInputSource',
    urlSelector: '[data-sync-panel="archidekt-url"]',
    textSelector: '[data-sync-panel="archidekt-text"]',
  },
  {
    selectName: 'DeckInputSource',
    urlSelector: '[data-sync-panel="prompt-deck-url"]',
    textSelector: '[data-sync-panel="prompt-deck-text"]',
  },
  {
    // Manabase reuses the DeckInputSource select; on the deck-analysis page these
    // manabase panels are absent so this config no-ops (togglePanel ignores missing
    // selectors), and vice versa on the manabase page.
    selectName: 'DeckInputSource',
    urlSelector: '[data-sync-panel="manabase-deck-url"]',
    textSelector: '[data-sync-panel="manabase-deck-text"]',
  },
  {
    // Deck Primer reuses the DeckInputSource select too; its panels are absent on the
    // other tools so this config no-ops there. Wires the Moxfield Bridge into Deck
    // Primer so its public-URL import goes through the extension like the other tools.
    selectName: 'DeckInputSource',
    urlSelector: '[data-sync-panel="primer-deck-url"]',
    textSelector: '[data-sync-panel="primer-deck-text"]',
  },
  {
    // Bracket Check reuses the DeckInputSource select; its panels are absent on other
    // tools so this config no-ops there (togglePanel ignores missing selectors).
    selectName: 'DeckInputSource',
    urlSelector: '[data-sync-panel="bracket-deck-url"]',
    textSelector: '[data-sync-panel="bracket-deck-text"]',
  },
  {
    // cEDH Meta-Gap reuses the DeckInputSource select; its panels are absent on other
    // tools so this config no-ops there (togglePanel ignores missing selectors).
    selectName: 'DeckInputSource',
    urlSelector: '[data-sync-panel="metagap-deck-url"]',
    textSelector: '[data-sync-panel="metagap-deck-text"]',
  },
  {
    // Cut Lab reuses the DeckInputSource select; its panels are absent on other
    // tools so this config no-ops there (togglePanel ignores missing selectors).
    selectName: 'DeckInputSource',
    urlSelector: '[data-sync-panel="cut-lab-deck-url"]',
    textSelector: '[data-sync-panel="cut-lab-deck-text"]',
  },
  {
    // Deck Comparison Deck A panels are absent on other tools so this config no-ops
    // there (togglePanel ignores missing selectors).
    selectName: 'DeckAInputSource',
    urlSelector: '[data-sync-panel="comparison-deckA-url"]',
    textSelector: '[data-sync-panel="comparison-deckA-text"]',
  },
  {
    // Deck Comparison Deck B panels are absent on other tools so this config no-ops
    // there (togglePanel ignores missing selectors).
    selectName: 'DeckBInputSource',
    urlSelector: '[data-sync-panel="comparison-deckB-url"]',
    textSelector: '[data-sync-panel="comparison-deckB-text"]',
  },
];

const updateSyncInputModeUi = (): void => {
  panelConfigs.forEach(config => {
    const select = document.querySelector<HTMLSelectElement>(`select[name="${config.selectName}"]`);
    if (!select) {
      return;
    }

    const selectedValue = select.value as DeckInputSourceValue;
    const showUrl = selectedValue === DeckInputSource.PublicUrl;
    const showText = selectedValue === DeckInputSource.PasteText;

    togglePanel(config.urlSelector, !showUrl);
    togglePanel(config.textSelector, !showText);
  });
};

const updateSyncDirectionUi = (): void => {
  const directionSelect = document.querySelector<HTMLSelectElement>('select[name="Direction"]');
  if (!directionSelect) {
    return;
  }

  const direction = directionSelect.value;
  const leftSystem = direction === 'ArchidektToArchidekt' ? 'Archidekt' : 'Moxfield';
  const rightSystem = direction === 'MoxfieldToMoxfield' ? 'Moxfield' : 'Archidekt';
  const leftIsSource = direction === 'MoxfieldToArchidekt' || direction === 'MoxfieldToMoxfield';
  const moxfieldStatus = document.querySelector<HTMLElement>('[data-sync-role="moxfield-status"]');
  const archidektStatus = document.querySelector<HTMLElement>('[data-sync-role="archidekt-status"]');
  const moxfieldTitle = document.querySelector<HTMLElement>('[data-sync-role="moxfield-title"]');
  const archidektTitle = document.querySelector<HTMLElement>('[data-sync-role="archidekt-title"]');
  const moxfieldDescription = document.querySelector<HTMLElement>('[data-sync-role="moxfield-description"]');
  const archidektDescription = document.querySelector<HTMLElement>('[data-sync-role="archidekt-description"]');
  const moxfieldUrlLabel = document.querySelector<HTMLElement>('[data-sync-role="moxfield-url-label"]');
  const archidektUrlLabel = document.querySelector<HTMLElement>('[data-sync-role="archidekt-url-label"]');
  const moxfieldTextLabel = document.querySelector<HTMLElement>('[data-sync-role="moxfield-text-label"]');
  const archidektTextLabel = document.querySelector<HTMLElement>('[data-sync-role="archidekt-text-label"]');
  const moxfieldHint = document.querySelector<HTMLElement>('[data-sync-role="moxfield-hint"]');
  const archidektHint = document.querySelector<HTMLElement>('[data-sync-role="archidekt-hint"]');
  const targetCategoryOption = document.querySelector<HTMLOptionElement>('[data-sync-role="category-mode-target"]');
  const sourceCategoryOption = document.querySelector<HTMLOptionElement>('[data-sync-role="category-mode-source"]');
  const moxfieldUrlInput = document.querySelector<HTMLInputElement>('input[name="MoxfieldUrl"]');
  const archidektUrlInput = document.querySelector<HTMLInputElement>('input[name="ArchidektUrl"]');
  const sourceLabelKind = leftIsSource
    ? (leftSystem === 'Archidekt' ? 'categories' : 'tags')
    : (rightSystem === 'Archidekt' ? 'categories' : 'tags');
  const targetLabelKind = leftIsSource
    ? (rightSystem === 'Archidekt' ? 'categories' : 'tags')
    : (leftSystem === 'Archidekt' ? 'categories' : 'tags');

  if (moxfieldStatus) {
    moxfieldStatus.textContent = leftIsSource ? 'Source deck' : 'Target deck';
  }

  if (archidektStatus) {
    archidektStatus.textContent = leftIsSource ? 'Target deck' : 'Source deck';
  }

  if (moxfieldTitle) {
    moxfieldTitle.textContent = leftSystem;
  }

  if (archidektTitle) {
    archidektTitle.textContent = rightSystem;
  }

  if (moxfieldDescription) {
    moxfieldDescription.textContent = `Provide the ${leftSystem} export or public URL for this deck.`;
  }

  if (archidektDescription) {
    archidektDescription.textContent = `Provide the ${rightSystem} export or public URL for this deck.`;
  }

  if (moxfieldUrlLabel) {
    moxfieldUrlLabel.textContent = `${leftSystem} public deck URL`;
  }

  if (archidektUrlLabel) {
    archidektUrlLabel.textContent = `${rightSystem} public deck URL`;
  }

  if (moxfieldTextLabel) {
    moxfieldTextLabel.textContent = `${leftSystem} export text`;
  }

  if (archidektTextLabel) {
    archidektTextLabel.textContent = `${rightSystem} export text`;
  }

  if (moxfieldHint) {
    moxfieldHint.textContent = `Use this when the ${leftSystem} deck is ${leftIsSource ? 'the source' : 'the target'}.`;
  }

  if (archidektHint) {
    archidektHint.textContent = `Use this when the ${rightSystem} deck is ${leftIsSource ? 'the target' : 'the source'}.`;
  }

  if (targetCategoryOption) {
    targetCategoryOption.textContent = `Use target ${targetLabelKind}`;
  }

  if (sourceCategoryOption) {
    sourceCategoryOption.textContent = `Use source ${sourceLabelKind}`;
  }

  if (moxfieldUrlInput) {
    moxfieldUrlInput.placeholder = leftSystem === 'Archidekt'
      ? 'https://archidekt.com/decks/...'
      : 'https://moxfield.com/decks/...';
  }

  if (archidektUrlInput) {
    archidektUrlInput.placeholder = rightSystem === 'Moxfield'
      ? 'https://moxfield.com/decks/...'
      : 'https://archidekt.com/decks/...';
  }
};

let syncInputModeInitialized = false;

const initializeSyncInputModeUi = (): void => {
  if (syncInputModeInitialized) {
    return;
  }

  syncInputModeInitialized = true;
  const inputSelectors = document.querySelectorAll<HTMLSelectElement>('select[name="MoxfieldInputSource"], select[name="ArchidektInputSource"], select[name="DeckInputSource"], select[name="DeckAInputSource"], select[name="DeckBInputSource"]');
  inputSelectors.forEach(element => {
    element.addEventListener('change', updateSyncInputModeUi);
  });

  const directionSelect = document.querySelector<HTMLSelectElement>('select[name="Direction"]');
  directionSelect?.addEventListener('change', updateSyncDirectionUi);

  updateSyncInputModeUi();
  updateSyncDirectionUi();
};

const scrollResults = (): void => {
  const anchor = document.getElementById('results-anchor');
  if (anchor) {
    anchor.scrollIntoView({ behavior: 'smooth' });
  }
};

const setAllPrintingChoices = (value: string): void => {
  const selector = `input[type="radio"][name^="Resolutions["][value="${value}"]`;
  document.querySelectorAll<HTMLInputElement>(selector).forEach(input => {
    input.checked = true;
  });
};

const copyElementValue = async (targetId: string): Promise<void> => {
  const normalizedTargetId = targetId.startsWith('#') ? targetId.slice(1) : targetId;
  const target = document.getElementById(normalizedTargetId);
  if (!target) {
    throw new Error(`Copy target "${normalizedTargetId}" was not found.`);
  }

  const text = target instanceof HTMLTextAreaElement || target instanceof HTMLInputElement
    ? target.value
    : target.textContent ?? '';

  if (!text.trim()) {
    throw new Error(`Copy target "${normalizedTargetId}" had no text.`);
  }

  await navigator.clipboard.writeText(text);
};

const announceToScreenReader = (message: string): void => {
  const announcer = document.querySelector<HTMLElement>('[data-copy-announcer]');
  if (!announcer) return;
  // Clearing then setting re-triggers the announcement for repeat copies.
  announcer.textContent = '';
  window.setTimeout(() => { announcer.textContent = message; }, 50);
};

const setTemporaryButtonText = (button: HTMLElement, text: string, durationMs = 1800): void => {
  const originalText = button.dataset.copyOriginalText ?? button.textContent?.trim() ?? 'Copy';
  button.dataset.copyOriginalText = originalText;
  button.textContent = text;
  const state = text === 'Copied' ? 'is-copied' : text === 'Copy failed' ? 'is-copy-failed' : null;
  if (state) {
    button.classList.add(state);
    announceToScreenReader(text);
  }

  window.setTimeout(() => {
    button.textContent = originalText;
    button.classList.remove('is-copied', 'is-copy-failed');
  }, durationMs);
};

const attachActionButtons = (): void => {
  document.querySelectorAll<HTMLElement>('[data-copy-target]').forEach(button => {
    button.addEventListener('click', async () => {
      const targetId = button.dataset.copyTarget;
      if (!targetId) {
        return;
      }

      try {
        await copyElementValue(targetId);
        setTemporaryButtonText(button, 'Copied');
      } catch {
        setTemporaryButtonText(button, 'Copy failed');
      }
    });
  });

  document.querySelectorAll<HTMLElement>('[data-select-all-choice]').forEach(button => {
    button.addEventListener('click', () => {
      const choice = button.dataset.selectAllChoice;
      if (!choice) {
        return;
      }

      setAllPrintingChoices(choice);
    });
  });

  document.querySelectorAll<HTMLButtonElement>('[data-expand-target]').forEach(button => {
    button.addEventListener('click', () => {
      const targetId = button.dataset.expandTarget;
      if (!targetId) {
        return;
      }

      const target = document.getElementById(targetId);
      if (!(target instanceof HTMLTextAreaElement)) {
        return;
      }

      const expanded = target.classList.toggle('prompt-artifact-textarea--expanded');
      button.textContent = expanded ? 'Collapse' : 'Expand';
      button.setAttribute('aria-expanded', expanded ? 'true' : 'false');
    });
  });
};

// Prompt session-zip download handler.
//
// Replaces the prior timed 3s debounce with a deterministic fetch+blob flow:
// the click is intercepted, the form payload is POSTed via fetch, the
// response body is materialized as a Blob, and a synthetic <a download>
// click triggers the browser save. The button is disabled for the entire
// in-flight window and re-enabled in finally — so the inactive state
// reflects the actual download lifetime, not a guessed timer. Safe to set
// `button.disabled = true` here because we preventDefault the original
// submit; the form never natively submits, so the disabled-submitter
// HTML-spec gotcha (regression d54da44) does not apply.
//
// Page-wide busy overlay is suppressed via the `data-no-busy` marker on
// each download button (kept for defense in depth — the form submit never
// fires anyway because of preventDefault).
const PROMPT_DOWNLOAD_FALLBACK_FILENAME = 'session.zip';

const parsePromptDownloadFilename = (header: string | null): string | null => {
  if (!header) {
    return null;
  }
  const star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(header);
  if (star) {
    try {
      return decodeURIComponent(star[1].trim().replace(/^"|"$/g, ''));
    } catch {
      // fall through to plain match
    }
  }
  const plain = /filename=("([^"]+)"|([^;]+))/i.exec(header);
  if (plain) {
    return (plain[2] ?? plain[3] ?? '').trim();
  }
  return null;
};

const triggerPromptBlobDownload = (blob: Blob, filename: string): void => {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  // Revoke after a tick so the browser has time to start the save.
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
};

const registerPromptDownloadHandler = (): void => {
  document.querySelectorAll<HTMLButtonElement>('button[data-prompt-download-submit]').forEach(button => {
    button.addEventListener('click', async (event: MouseEvent) => {
      const form = button.closest('form');
      if (!form) {
        return;
      }
      event.preventDefault();
      if (button.dataset.downloadInFlight === 'true') {
        return;
      }

      const action = button.formAction || form.action;
      const methodAttr = (button.getAttribute('formmethod') ?? form.method ?? 'POST').toUpperCase();
      const method = methodAttr === 'GET' ? 'GET' : 'POST';

      button.dataset.downloadInFlight = 'true';
      button.disabled = true;

      try {
        const body = method === 'GET' ? undefined : new FormData(form);
        const response = await fetch(action, {
          method,
          body,
          credentials: 'same-origin',
          headers: { Accept: 'application/zip,*/*' }
        });
        if (!response.ok) {
          window.alert(`Download failed (HTTP ${response.status}). Please try again.`);
          return;
        }
        // Validation failures on the server return a re-rendered HTML view
        // with status 200 — without a Content-Type/zip-header guard the
        // client would blob the HTML and save it as session.zip. Require
        // a real zip signal (Content-Type or X-DeckFlow-Filename or
        // Content-Disposition) before treating the body as a download.
        const contentType = (response.headers.get('Content-Type') ?? '').toLowerCase();
        const customFilename = response.headers.get('X-DeckFlow-Filename');
        const dispositionHeader = response.headers.get('Content-Disposition');
        const looksLikeZip = contentType.includes('application/zip')
          || contentType.includes('application/octet-stream')
          || !!customFilename
          || !!dispositionHeader;
        if (!looksLikeZip) {
          // Server returned a non-zip response (likely a re-rendered error
          // view). Replace the document so the user sees the error UI.
          const html = await response.text();
          document.open();
          document.write(html);
          document.close();
          return;
        }
        const blob = await response.blob();
        // Prefer the explicit X-DeckFlow-Filename header (set by all download
        // endpoints — bypasses Content-Disposition parsing fragility), then
        // fall back to Content-Disposition, then to the generic default.
        const dispositionFilename = parsePromptDownloadFilename(dispositionHeader);
        const filename = (customFilename && customFilename.trim()) || dispositionFilename || PROMPT_DOWNLOAD_FALLBACK_FILENAME;
        if (!customFilename && !dispositionFilename) {
          // Diagnostic for the user — surface the missing-header case in DevTools.
          console.warn('Prompt download: neither X-DeckFlow-Filename nor Content-Disposition was readable; falling back to generic filename.');
        }
        triggerPromptBlobDownload(blob, filename);
      } catch (err) {
        console.error('Prompt session download failed', err);
        window.alert('Download failed. Please try again.');
      } finally {
        delete button.dataset.downloadInFlight;
        button.disabled = false;
      }
    });
  });
};

// Print button on the Step 3 / Step 5 results panels. CSP blocks inline onclick
// (script-src has no 'unsafe-inline'), so window.print() is wired here. The
// @media print rules in site-common.css isolate the result panels on paper.
const registerPromptPrintHandler = (): void => {
  document.querySelectorAll<HTMLButtonElement>('button[data-prompt-print]').forEach(button => {
    button.addEventListener('click', () => {
      window.print();
    });
  });
};

const formStateStoragePrefix = 'decksync-form-state-';
const antiForgeryFieldName = '__RequestVerificationToken';
// Why: these field names are server-computed authoritative state, not
// recoverable user input. The cache captures form state on pagehide --
// before a POST's response is rendered -- so restoring them unconditionally
// silently reverts a freshly-rendered response to a stale pre-submit
// snapshot. Same bug class already fixed for HistoryJson on Deck History;
// see .planning/debug/resolved/deck-history-page-bugs.md.
const nonPersistedFieldNames = new Set([
  antiForgeryFieldName,
  'HistoryJson',
  'WorkflowStep',
  'FetchedEntriesJson',
  'MetaGapPromptText',
  'CutLabStateJson',
]);
// Phase 10 (D-15 race fix): track the auto-clear timer per form so a
// rapid second upload cancels the first upload's pending clear-timeout
// instead of letting it fire later and clobber the second upload's
// still-active skipPersistence flag.
const skipPersistenceTimers = new WeakMap<HTMLFormElement, number>();
const storageAvailable = (() => {
  try {
    const testKey = '__decksync_test_key__';
    window.sessionStorage.setItem(testKey, '1');
    window.sessionStorage.removeItem(testKey);
    return window.sessionStorage;
  } catch {
    return null;
  }
})();

const serializePersistedFormFields = (form: HTMLFormElement): Record<string, string[]> => {
  const state: Record<string, string[]> = {};
  const formData = new FormData(form);

  formData.forEach((value, key) => {
    if (typeof value !== 'string') {
      return;
    }

    if (nonPersistedFieldNames.has(key)) {
      return;
    }

    if (!state[key]) {
      state[key] = [];
    }

    state[key].push(value);
  });

  return state;
};

const serializeFormFields = (form: HTMLFormElement): Record<string, string> => {
  const state: Record<string, string> = {};
  form.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>('[name]').forEach(element => {
    if (element.disabled || !element.name) {
      return;
    }

    if (element instanceof HTMLInputElement && (element.type === 'checkbox' || element.type === 'radio')) {
      if (!element.checked) {
        return;
      }
    }

    state[element.name] = element.value;
  });

  return state;
};

const cardPickerFieldName = 'CardSpecificQuestionCardNames';

const getCardPickerRowsContainer = (container: HTMLElement): HTMLElement | null =>
  container.querySelector<HTMLElement>('[data-card-picker-rows]');

const getCardPickerRows = (container: HTMLElement): HTMLElement[] =>
  Array.from(container.querySelectorAll<HTMLElement>('[data-card-picker-row]'));

const cardPickerSvgNamespace = 'http://www.w3.org/2000/svg';

const createCardPickerIcon = (lineCoordinates: Array<[string, string, string, string]>): SVGSVGElement => {
  const icon = document.createElementNS(cardPickerSvgNamespace, 'svg');
  icon.setAttribute('width', '16');
  icon.setAttribute('height', '16');
  icon.setAttribute('viewBox', '0 0 24 24');
  icon.setAttribute('fill', 'none');
  icon.setAttribute('stroke', 'currentColor');
  icon.setAttribute('stroke-width', '2');
  icon.setAttribute('stroke-linecap', 'round');
  icon.setAttribute('role', 'img');
  icon.setAttribute('aria-hidden', 'true');

  lineCoordinates.forEach(([x1, y1, x2, y2]) => {
    const line = document.createElementNS(cardPickerSvgNamespace, 'line');
    line.setAttribute('x1', x1);
    line.setAttribute('y1', y1);
    line.setAttribute('x2', x2);
    line.setAttribute('y2', y2);
    icon.appendChild(line);
  });

  return icon;
};

const syncCardPickerRemoveButtons = (container: HTMLElement): void => {
  const rowsContainer = getCardPickerRowsContainer(container);
  if (!rowsContainer) {
    return;
  }

  Array.from(rowsContainer.children).forEach((child, index) => {
    if (!(child instanceof HTMLElement) || !child.hasAttribute('data-card-picker-row')) {
      return;
    }

    const removeButton = child.querySelector<HTMLButtonElement>('[data-card-picker-remove]');
    if (!removeButton) {
      return;
    }

    if (index === 0) {
      removeButton.hidden = true;
      removeButton.classList.add('hidden');
      return;
    }

    removeButton.hidden = false;
    removeButton.classList.remove('hidden');
  });
};

const createCardPickerRow = (value = ''): HTMLDivElement => {
  const row = document.createElement('div');
  row.className = 'card-picker__row';
  row.setAttribute('data-card-picker-row', '');

  const inputShell = document.createElement('div');
  inputShell.className = 'autocomplete-anchor card-picker__input-shell';

  const input = document.createElement('input');
  input.type = 'text';
  input.name = cardPickerFieldName;
  input.value = value;
  input.className = 'card-picker__input';
  input.autocomplete = 'off';
  input.setAttribute('data-card-picker-input', '');
  inputShell.appendChild(input);

  const addButton = document.createElement('button');
  addButton.type = 'button';
  addButton.className = 'card-picker__add';
  addButton.setAttribute('data-card-picker-add', '');
  addButton.setAttribute('aria-label', 'Add another card');
  addButton.appendChild(
    createCardPickerIcon([
      ['12', '5', '12', '19'],
      ['5', '12', '19', '12']
    ])
  );

  const removeButton = document.createElement('button');
  removeButton.type = 'button';
  removeButton.className = 'card-picker__remove hidden';
  removeButton.setAttribute('data-card-picker-remove', '');
  removeButton.setAttribute('aria-label', 'Remove this card');
  removeButton.hidden = true;
  removeButton.appendChild(createCardPickerIcon([['5', '12', '19', '12']]));

  row.append(inputShell, addButton, removeButton);
  return row;
};

const attachCardPickerRow = (container: HTMLElement, row: HTMLElement): void => {
  const rowsContainer = getCardPickerRowsContainer(container);
  const form = container.closest('form');
  const addButton = row.querySelector<HTMLButtonElement>('[data-card-picker-add]');
  const input = row.querySelector<HTMLInputElement>('[data-card-picker-input]');
  const inputShell = row.querySelector<HTMLElement>('.card-picker__input-shell');
  const removeButton = row.querySelector<HTMLButtonElement>('[data-card-picker-remove]');

  if (rowsContainer) {
    const isFirstRow = row === rowsContainer.firstElementChild;
    if (isFirstRow) {
      removeButton?.classList.add('hidden');
      if (removeButton) {
        removeButton.hidden = true;
      }
    } else {
      removeButton?.classList.remove('hidden');
      if (removeButton) {
        removeButton.hidden = false;
      }
    }
  }

  if (row.dataset.cardPickerAttached === 'true') {
    return;
  }

  row.dataset.cardPickerAttached = 'true';

  if (input && inputShell instanceof HTMLElement) {
    let suggestionPanel = inputShell.querySelector<HTMLDivElement>('.autocomplete-panel');
    if (!suggestionPanel) {
      suggestionPanel = deckFlowWindow.DeckFlow?.createTypeaheadPanel?.(inputShell) ?? null;
    }

    if (suggestionPanel) {
      deckFlowWindow.DeckFlow?.attachTypeahead?.(input, suggestionPanel, 2, pickedName => {
        input.value = pickedName;
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });
    }
  }

  addButton?.addEventListener('click', () => {
    const currentRowsContainer = getCardPickerRowsContainer(container);
    if (!currentRowsContainer) {
      return;
    }

    const newRow = createCardPickerRow();
    currentRowsContainer.appendChild(newRow);
    attachCardPickerRow(container, newRow);
    syncCardPickerRemoveButtons(container);
    form && persistFormState(form);
    newRow.querySelector<HTMLInputElement>('[data-card-picker-input]')?.focus();
  });

  removeButton?.addEventListener('click', () => {
    const currentRowsContainer = getCardPickerRowsContainer(container);
    if (!currentRowsContainer || row === currentRowsContainer.firstElementChild) {
      syncCardPickerRemoveButtons(container);
      return;
    }

    row.remove();

    if (currentRowsContainer.querySelectorAll('[data-card-picker-row]').length === 0) {
      const replacementRow = createCardPickerRow();
      currentRowsContainer.appendChild(replacementRow);
      attachCardPickerRow(container, replacementRow);
    }

    syncCardPickerRemoveButtons(container);
    form && persistFormState(form);
  });
};

const attachCardPicker = (form: HTMLFormElement): void => {
  form.querySelectorAll<HTMLElement>('[data-card-picker]').forEach(container => {
    const rowsContainer = getCardPickerRowsContainer(container);
    if (!rowsContainer) {
      return;
    }

    if (rowsContainer.querySelectorAll('[data-card-picker-row]').length === 0) {
      rowsContainer.appendChild(createCardPickerRow());
    }

    getCardPickerRows(container).forEach(row => attachCardPickerRow(container, row));
    syncCardPickerRemoveButtons(container);
  });
};

const restoreCardPickerFields = (form: HTMLFormElement, data: Record<string, string[]>): void => {
  const container = form.querySelector<HTMLElement>('[data-card-picker]');
  if (!container) {
    return;
  }

  const rowsContainer = getCardPickerRowsContainer(container);
  if (!rowsContainer) {
    return;
  }

  const values = data[cardPickerFieldName];
  if (!values || values.length === 0) {
    return;
  }

  rowsContainer.replaceChildren();
  values.forEach(value => {
    rowsContainer.appendChild(createCardPickerRow(value));
  });
};

const restoreFormFields = (form: HTMLFormElement, data: Record<string, string[]>) => {
  restoreCardPickerFields(form, data);

  form.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>('[name]').forEach(element => {
    if (nonPersistedFieldNames.has(element.name)) {
      return;
    }

    if (element.name === cardPickerFieldName) {
      return;
    }

    const values = data[element.name];
    if (!values || values.length === 0) {
      return;
    }

    if (element instanceof HTMLInputElement) {
      if (element.type === 'checkbox' || element.type === 'radio') {
        element.checked = values.includes(element.value);
        return;
      }

      element.value = values[0];
      return;
    }

    if (element instanceof HTMLSelectElement && element.multiple) {
      Array.from(element.options).forEach(option => {
        option.selected = values.includes(option.value);
      });
      return;
    }

    element.value = values[0];
  });
};

const persistFormState = (form: HTMLFormElement): void => {
  if (form.dataset.skipPersistence === 'true') {
    return;
  }

  const key = form.getAttribute('data-cache-key');
  if (!key || !storageAvailable) {
    return;
  }

  const state = serializePersistedFormFields(form);
  storageAvailable.setItem(`${formStateStoragePrefix}${key}`, JSON.stringify(state));
  storageAvailable.setItem(`${formStateStoragePrefix}${key}:savedAt`, Date.now().toString());
};

const clearPersistedFormState = (form: HTMLFormElement): void => {
  const key = form.getAttribute('data-cache-key');
  if (!key || !storageAvailable) {
    return;
  }

  storageAvailable.removeItem(`${formStateStoragePrefix}${key}`);
  storageAvailable.removeItem(`${formStateStoragePrefix}${key}:savedAt`);
  form.querySelector<HTMLElement>('[data-cache-pill]')?.remove();
};

const clearFormToFreshSlate = (form: HTMLFormElement): void => {
  form.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>('[name], [data-question-bucket]').forEach(element => {
    if (element.name === antiForgeryFieldName) {
      return;
    }

    if (element instanceof HTMLInputElement) {
      const inputType = element.type.toLowerCase();
      if (inputType === 'checkbox' || inputType === 'radio') {
        element.checked = false;
        element.indeterminate = false;
        return;
      }

      if (inputType === 'hidden' || inputType === 'button' || inputType === 'submit' || inputType === 'reset') {
        return;
      }

      element.value = '';
      return;
    }

    if (element instanceof HTMLSelectElement) {
      Array.from(element.options).forEach(option => {
        option.selected = false;
      });

      if (!element.multiple && element.options.length > 0) {
        element.selectedIndex = 0;
      }

      deckFlowWindow.DeckFlow?.refreshDfSelect?.(element);
      return;
    }

    element.value = '';
  });
};

const formatCacheAge = (savedAtMs: number): string => {
  const elapsedMs = Date.now() - savedAtMs;
  if (elapsedMs < 60_000) return 'just now';
  const minutes = Math.floor(elapsedMs / 60_000);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hr ago`;
  return `${Math.floor(hours / 24)} day ago`;
};

const showCachePill = (form: HTMLFormElement, savedAtMs: number): void => {
  if (form.querySelector('[data-cache-pill]')) return;
  const pill = document.createElement('div');
  pill.className = 'cache-pill';
  pill.setAttribute('data-cache-pill', '');
  pill.setAttribute('role', 'status');

  const label = document.createElement('span');
  label.textContent = `Restored from cache · ${formatCacheAge(savedAtMs)}`;

  const resetButton = document.createElement('button');
  resetButton.type = 'button';
  resetButton.className = 'cache-pill__reset';
  resetButton.textContent = 'Reset';
  resetButton.addEventListener('click', () => {
    clearPersistedFormState(form);
    form.reset();
  });

  pill.appendChild(label);
  pill.appendChild(resetButton);
  form.insertBefore(pill, form.firstChild);
};

const hydrateFormState = (form: HTMLFormElement): void => {
  const key = form.getAttribute('data-cache-key');
  if (!key || !storageAvailable) {
    return;
  }

  const json = storageAvailable.getItem(`${formStateStoragePrefix}${key}`);
  if (!json) {
    return;
  }

  try {
    const state = JSON.parse(json) as Record<string, string[]>;
    restoreFormFields(form, state);
    const savedAtRaw = storageAvailable.getItem(`${formStateStoragePrefix}${key}:savedAt`);
    const savedAtMs = savedAtRaw ? parseInt(savedAtRaw, 10) : NaN;
    if (Number.isFinite(savedAtMs)) {
      showCachePill(form, savedAtMs);
    }
  } catch {
    storageAvailable.removeItem(`${formStateStoragePrefix}${key}`);
    storageAvailable.removeItem(`${formStateStoragePrefix}${key}:savedAt`);
  }
};

const attachGenericPersistedForms = (): void => {
  if (!storageAvailable) {
    return;
  }

  const forms = Array.from(document.querySelectorAll<HTMLFormElement>('form[data-cache-key]'));

  forms.forEach(form => {
    if (form.id === 'deck-sync-form') {
      return;
    }

    hydrateFormState(form);

    const persist = () => persistFormState(form);
    form.addEventListener('input', persist);
    form.addEventListener('change', persist);
    window.addEventListener('pagehide', persist);

    const clearButton = form.querySelector<HTMLElement>('[data-clear-cache]');
    clearButton?.addEventListener('click', () => {
      const clearHref = clearButton.getAttribute('data-clear-href');
      if (clearHref) {
        form.dataset.skipPersistence = 'true';
        if (form.getAttribute('data-cache-key') === 'prompt-packets') {
          clearPromptPacketsState(form);
        } else {
          clearPersistedFormState(form);
        }

        window.location.href = clearHref;
        return;
      }

      form.reset();
      clearPersistedFormState(form);
      clearGenericFormUi(form);
    });
  });

  document.querySelectorAll<HTMLAnchorElement>('.tool-nav__link, .page-brand, .hub-card').forEach(link => {
    link.addEventListener('click', () => {
      forms.forEach(form => persistFormState(form));
    });
  });
};

const clearGenericFormUi = (form: HTMLFormElement): void => {
  const key = form.getAttribute('data-cache-key');
  if (key !== 'mechanic-lookup') {
    return;
  }

  const mechanicInput = form.querySelector<HTMLInputElement>('#mechanic-lookup-input, input[name="MechanicName"]');
  if (mechanicInput) {
    mechanicInput.value = '';
  }

  const results = document.getElementById('mechanic-lookup-results');
  if (results) {
    results.classList.add('hidden');
    results.innerHTML = '';
  }
};

const clearDeckSyncUi = (): void => {
  const results = document.getElementById('deck-sync-results');
  const error = document.getElementById('deck-sync-error');

  if (results) {
    results.classList.add('hidden');
  }

  if (error) {
    error.classList.add('hidden');
    error.textContent = '';
  }
};

const setDeckSyncResultLabels = (sourceSystem: string, targetSystem: string): void => {
  document.querySelectorAll<HTMLElement>('[data-sync-result="source-system"]').forEach(node => {
    node.textContent = sourceSystem;
  });

  document.querySelectorAll<HTMLElement>('[data-sync-result="target-system"]').forEach(node => {
    node.textContent = targetSystem;
  });
};

const buildConflictCellText = (
  system: DeckSyncSystem,
  conflict: DeckSyncApiResponse['printingConflicts'][number]
): string => {
  if (system === 'Archidekt') {
    const categorySuffix = conflict.archidektCategory ? ` [${conflict.archidektCategory}]` : '';
    return `(${conflict.archidektSetCode}) ${conflict.archidektCollectorNumber}${categorySuffix}`;
  }

  const setCode = conflict.moxfieldSetCode ?? '';
  const collectorNumber = conflict.moxfieldCollectorNumber ?? '';
  return `(${setCode}) ${collectorNumber}`.trim();
};

const renderDeckSyncConflicts = (
  printingConflicts: DeckSyncApiResponse['printingConflicts'],
  sourceSystem: string,
  targetSystem: string
): void => {
  const panel = document.getElementById('deck-sync-conflicts-js');
  const body = document.getElementById('deck-sync-conflicts-body');
  if (!panel || !body) {
    return;
  }

  body.replaceChildren();

  if (printingConflicts.length === 0) {
    panel.classList.add('hidden');
    return;
  }

  printingConflicts.forEach(conflict => {
    const row = document.createElement('tr');
    const cardCell = document.createElement('td');
    cardCell.textContent = conflict.cardName;

    const targetCell = document.createElement('td');
    targetCell.textContent = buildConflictCellText(targetSystem as DeckSyncSystem, conflict);

    const sourceCell = document.createElement('td');
    sourceCell.textContent = buildConflictCellText(sourceSystem as DeckSyncSystem, conflict);

    row.appendChild(cardCell);
    row.appendChild(targetCell);
    row.appendChild(sourceCell);
    body.appendChild(row);
  });

  panel.classList.remove('hidden');
};

const renderDeckSyncResponse = (response: DeckSyncApiResponse): void => {
  const error = document.getElementById('deck-sync-error');
  const results = document.getElementById('deck-sync-results');
  const report = document.getElementById('deck-sync-report');
  const delta = document.getElementById('delta-output') as HTMLTextAreaElement | null;
  const fullImport = document.getElementById('full-import-output') as HTMLTextAreaElement | null;
  const instructions = document.getElementById('deck-sync-instructions');

  if (error) {
    error.classList.add('hidden');
    error.textContent = '';
  }

  if (report) {
    report.textContent = response.reportText;
  }

  if (delta) {
    delta.value = response.deltaText;
  }

  if (fullImport) {
    fullImport.value = response.fullImportText;
  }

  if (instructions) {
    instructions.textContent = response.instructionsText;
  }

  setDeckSyncResultLabels(response.sourceSystem, response.targetSystem);
  renderDeckSyncConflicts(response.printingConflicts, response.sourceSystem, response.targetSystem);

  results?.classList.remove('hidden');
  window.setTimeout(scrollResults, 100);
};

const submitDeckSyncApi = async (form: HTMLFormElement): Promise<void> => {
  const endpoint = form.dataset.deckSyncApi;
  if (!endpoint) {
    return;
  }

  const error = document.getElementById('deck-sync-error');

  try {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(serializeFormFields(form))
    });

    if (!response.ok) {
      let payload: { message?: string; Message?: string; title?: string; errors?: Record<string, string[]> } | null = null;
      try {
        payload = await response.json() as { message?: string; Message?: string; title?: string; errors?: Record<string, string[]> };
      } catch {
        payload = null;
      }

      if (error) {
        const validationSummary = payload?.errors
          ? Object.values(payload.errors)
              .reduce((messages, current) => messages.concat(current), [] as string[])
              .join(' ')
          : null;
        error.textContent = payload?.message ?? payload?.Message ?? validationSummary ?? payload?.title ?? 'Unable to run deck sync.';
        error.classList.remove('hidden');
      }

      document.getElementById('deck-sync-results')?.classList.add('hidden');
      window.hideBusyIndicator?.();
      return;
    }

    renderDeckSyncResponse(await response.json() as DeckSyncApiResponse);
    window.hideBusyIndicator?.();
  } catch (requestError) {
    if (error) {
      error.textContent = requestError instanceof Error ? requestError.message : 'Unable to run deck sync.';
      error.classList.remove('hidden');
    }

    document.getElementById('deck-sync-results')?.classList.add('hidden');
    window.hideBusyIndicator?.();
  }
};

const attachDeckSyncPersistence = (): void => {
  const form = document.getElementById('deck-sync-form') as HTMLFormElement | null;
  if (!form) {
    return;
  }

  const key = form.getAttribute('data-cache-key');
  if (!key || !storageAvailable) {
    updateSyncInputModeUi();
    updateSyncDirectionUi();
    return;
  }

  hydrateFormState(form);

  updateSyncInputModeUi();
  updateSyncDirectionUi();

  const handler = () => persistFormState(form);
  form.addEventListener('input', handler);
  form.addEventListener('change', handler);
  window.addEventListener('pagehide', handler);
  form.addEventListener('submit', event => {
    handler();
    event.preventDefault();
    submitDeckSyncApi(form);
  });

  const clearButton = form.querySelector<HTMLElement>('[data-clear-cache]');
  clearButton?.addEventListener('click', () => {
    const clearHref = clearButton.getAttribute('data-clear-href');
    if (clearHref) {
      form.dataset.skipPersistence = 'true';
      const cacheKey = form.getAttribute('data-cache-key');
      if (cacheKey === 'prompt-packets') {
        clearPromptPacketsState(form);
      } else {
        clearPersistedFormState(form);
      }

      window.location.replace(clearHref);
      return;
    }

    form.reset();
    clearPersistedFormState(form);
    clearDeckSyncUi();
    updateSyncInputModeUi();
    updateSyncDirectionUi();
  });

  document.querySelectorAll<HTMLAnchorElement>('.tool-nav__link, .page-brand, .hub-card').forEach(link => {
    link.addEventListener('click', () => {
      persistFormState(form);
    });
  });
};

const parsePromptStep = (value: string | undefined | null): number => {
  const parsedValue = parseInt(value ?? '1', 10);
  return Number.isNaN(parsedValue) || parsedValue < 1 || parsedValue > 5 ? 1 : parsedValue;
};

type PromptUiMode = 'guided' | 'focused' | 'expert';

const promptUiModeStorageKey = 'decksync-prompt-ui-mode';
const mobilePromptUiModeQuery = '(max-width: 600px)';

const parsePromptUiMode = (value: string | undefined | null): PromptUiMode => {
  if (value === 'focused' || value === 'expert') {
    return value;
  }

  return 'guided';
};

const getDefaultPromptUiMode = (): PromptUiMode => {
  return window.matchMedia(mobilePromptUiModeQuery).matches ? 'focused' : 'guided';
};

const setPromptValidationMessage = (message: string | null): void => {
  const errorNode = document.querySelector<HTMLElement>('[data-prompt-validation-error]');
  if (!errorNode) {
    return;
  }

  if (!message) {
    errorNode.textContent = '';
    errorNode.classList.add('hidden');
    return;
  }

  errorNode.textContent = message;
  errorNode.classList.remove('hidden');
  errorNode.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

const scrollPromptResults = (form: HTMLFormElement): void => {
  const step = parsePromptStep(form.dataset.promptCurrentStep);
  const activePanel = form.querySelector<HTMLElement>(`[data-prompt-step="${step}"]`);
  const resultAnchor = activePanel?.querySelector<HTMLElement>('[data-prompt-result-anchor]');
  if (!resultAnchor) {
    return;
  }

  window.setTimeout(() => {
    resultAnchor.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, 120);
};

const showPromptStep = (form: HTMLFormElement, step: number): void => {
  form.dataset.promptCurrentStep = step.toString();
  const workflowInput = form.querySelector<HTMLInputElement>('[data-prompt-workflow-step]');
  if (workflowInput) {
    workflowInput.value = step.toString();
  }

  form.querySelectorAll<HTMLElement>('[data-prompt-step]').forEach(panel => {
    const panelStep = parsePromptStep(panel.dataset.promptStep);
    panel.classList.toggle('hidden', panelStep !== step);
    panel.setAttribute('aria-hidden', panelStep === step ? 'false' : 'true');
  });

  form.querySelectorAll<HTMLElement>('[data-prompt-show-step]').forEach(button => {
    const buttonStep = parsePromptStep(button.dataset.promptShowStep);
    button.classList.toggle('is-active', buttonStep === step);
    button.setAttribute('aria-selected', buttonStep === step ? 'true' : 'false');
    button.setAttribute('tabindex', buttonStep === step ? '0' : '-1');
  });
};

const applyPromptUiMode = (form: HTMLFormElement, mode: PromptUiMode): void => {
  form.dataset.promptUiMode = mode;
  document.body.dataset.promptUiMode = mode;
  document.querySelectorAll<HTMLElement>('[data-prompt-ui-mode-button]').forEach(button => {
    const buttonMode = parsePromptUiMode(button.dataset.promptUiModeButton);
    button.classList.toggle('is-active', buttonMode === mode);
    button.setAttribute('aria-pressed', buttonMode === mode ? 'true' : 'false');
  });
};

const clearPromptPacketsState = (form: HTMLFormElement): void => {
  clearPersistedFormState(form);
  storageAvailable?.removeItem(promptUiModeStorageKey);
  clearFormToFreshSlate(form);
  applyPromptUiMode(form, getDefaultPromptUiMode());
  showPromptStep(form, 1);
  updateSyncInputModeUi();
  syncVersioningBracketOptions(form);
  syncCardSpecificQuestionField(form);
  syncBudgetQuestionField(form);
  syncPreferredCategoriesField(form);
  setPromptValidationMessage(null);
};

const validatePromptPacketsStep = (form: HTMLFormElement, step: number): string | null => {
  const deckInputSource = form.querySelector<HTMLSelectElement>('select[name="DeckInputSource"]')?.value ?? DeckInputSource.PasteText;
  const deckSource = deckInputSource === DeckInputSource.PublicUrl
    ? form.querySelector<HTMLInputElement>('input[name="DeckUrl"]')?.value.trim() ?? ''
    : form.querySelector<HTMLTextAreaElement>('textarea[name="DeckText"]')?.value.trim() ?? '';
  const deckProfileJson = form.querySelector<HTMLTextAreaElement>('textarea[name="DeckProfileJson"]')?.value.trim() ?? '';
  const targetCommanderBracket = form.querySelector<HTMLSelectElement>('select[name="TargetCommanderBracket"]')?.value.trim() ?? '';
  const cardSpecificQuestionCardNames = Array.from(
    form.querySelectorAll<HTMLInputElement>(`input[name="${cardPickerFieldName}"]`)
  )
    .map(input => input.value.trim())
    .filter(value => value.length > 0);
  const budgetUpgradeAmount = form.querySelector<HTMLInputElement>('input[name="BudgetUpgradeAmount"]')?.value.trim() ?? '';
  const setPacketText = form.querySelector<HTMLTextAreaElement>('textarea[name="SetPacketText"]')?.value.trim() ?? '';
  const selectedSetCodes = Array.from(
    form.querySelectorAll<HTMLOptionElement>('select[name="SelectedSetCodes"] option:checked')
  );
  const selectedCardSpecificQuestions = form.querySelectorAll<HTMLInputElement>(
    'input[name="SelectedAnalysisQuestions"][value="card-worth-it"]:checked, input[name="SelectedAnalysisQuestions"][value="better-alternatives"]:checked'
  ).length;
  const selectedBudgetQuestions = form.querySelectorAll<HTMLInputElement>(
    'input[name="SelectedAnalysisQuestions"][value="budget-upgrades"]:checked'
  ).length;
  const selectedCategoryQuestions = form.querySelectorAll<HTMLInputElement>(
    'input[name="SelectedAnalysisQuestions"][value="add-categories"]:checked, input[name="SelectedAnalysisQuestions"][value="update-categories"]:checked'
  ).length;
  const decklistExportFormat = form.querySelector<HTMLSelectElement>('select[name="DecklistExportFormat"]')?.value.trim() ?? '';

  if (step < 3 && !deckSource) {
    return 'Paste a deck URL or deck export before generating prompt packets.';
  }

  if (step === 2 && !targetCommanderBracket) {
    return 'Choose the target Commander bracket before generating the analysis packet.';
  }

  if (step === 2 && form.querySelectorAll<HTMLInputElement>('input[name="SelectedAnalysisQuestions"]:checked').length === 0) {
    return 'Select at least one analysis question before generating the analysis packet.';
  }

  if (step === 2 && selectedCardSpecificQuestions > 0 && cardSpecificQuestionCardNames.length === 0) {
    return 'Enter at least one card name for the selected card-specific analysis questions.';
  }

  if (step === 2 && selectedBudgetQuestions > 0 && !budgetUpgradeAmount) {
    return 'Enter a budget amount for the selected budget upgrade question.';
  }

  if (step === 2 && selectedCategoryQuestions > 0 && !decklistExportFormat) {
    return 'Choose Moxfield or Archidekt as the export format when assigning or updating categories — plain text does not support inline category formatting.';
  }

  if (step === 3 && !deckProfileJson) {
    return 'Paste the deck_profile JSON returned from your AI before rendering the analysis summary.';
  }

  if (step === 4) {
    if (!deckSource) {
      return 'Paste a deck in Step 1 before generating the set upgrade packet.';
    }

    if (!setPacketText && selectedSetCodes.length > 1) {
      return 'Choose only one set or paste a condensed set packet override before generating the set-upgrade packet.';
    }

    if (!setPacketText && selectedSetCodes.length === 0) {
      return 'Select at least one set or paste a condensed set packet override before generating the set-upgrade packet.';
    }
  }

  if (step === 5) {
    const setUpgradeResponseJson = form.querySelector<HTMLTextAreaElement>('textarea[name="SetUpgradeResponseJson"]')?.value.trim() ?? '';
    if (!setUpgradeResponseJson) {
      return 'Paste the set_upgrade_report JSON returned from your AI before rendering the set upgrade results.';
    }
  }

  return null;
};

const syncCardSpecificQuestionField = (form: HTMLFormElement): void => {
  const field = form.querySelector<HTMLElement>('[data-card-specific-question-field]');
  if (!field) {
    return;
  }

  const hasCardSpecificQuestion = form.querySelectorAll<HTMLInputElement>(
    'input[name="SelectedAnalysisQuestions"][value="card-worth-it"]:checked, input[name="SelectedAnalysisQuestions"][value="better-alternatives"]:checked'
  ).length > 0;

  field.classList.toggle('hidden', !hasCardSpecificQuestion);
};

const syncBudgetQuestionField = (form: HTMLFormElement): void => {
  const field = form.querySelector<HTMLElement>('[data-budget-question-field]');
  if (!field) {
    return;
  }

  const hasBudgetQuestion = form.querySelectorAll<HTMLInputElement>(
    'input[name="SelectedAnalysisQuestions"][value="budget-upgrades"]:checked'
  ).length > 0;

  field.classList.toggle('hidden', !hasBudgetQuestion);
};

const syncPreferredCategoriesField = (form: HTMLFormElement): void => {
  const field = form.querySelector<HTMLElement>('[data-preferred-categories-field]');
  if (!field) {
    return;
  }

  const hasUpdateCategories = form.querySelectorAll<HTMLInputElement>(
    'input[name="SelectedAnalysisQuestions"][value="update-categories"]:checked'
  ).length > 0;

  field.classList.toggle('hidden', !hasUpdateCategories);
};

const bracketToVersionQuestionId: Readonly<Record<string, string>> = {
  core: 'bracket-2-version',
  upgraded: 'bracket-3-version',
  optimized: 'bracket-4-version',
  cedh: 'bracket-5-version',
};

const syncVersioningBracketOptions = (form: HTMLFormElement): void => {
  const bracketSelect = form.querySelector<HTMLSelectElement>('select[name="TargetCommanderBracket"]');
  const selectedBracket = (bracketSelect?.value ?? '').toLowerCase();
  const disabledQuestionId = bracketToVersionQuestionId[selectedBracket] ?? null;

  Object.values(bracketToVersionQuestionId).forEach(questionId => {
    const checkbox = form.querySelector<HTMLInputElement>(`input[name="SelectedAnalysisQuestions"][value="${questionId}"]`);
    if (!checkbox) return;
    const shouldDisable = questionId === disabledQuestionId;
    checkbox.disabled = shouldDisable;
    if (shouldDisable && checkbox.checked) {
      checkbox.checked = false;
    }
    checkbox.closest('label')?.classList.toggle('prompt-question-option--disabled', shouldDisable);
  });

  syncQuestionBucketState(form);
};

const syncQuestionBucketState = (form: HTMLFormElement): void => {
  form.querySelectorAll<HTMLInputElement>('[data-question-bucket]').forEach(bucketCheckbox => {
    const bucketId = bucketCheckbox.dataset.questionBucket ?? '';
    const questionCheckboxes = Array.from(
      form.querySelectorAll<HTMLInputElement>(`input[data-question-option="${bucketId}"]`)
    );

    if (questionCheckboxes.length === 0) {
      bucketCheckbox.checked = false;
      bucketCheckbox.indeterminate = false;
      return;
    }

    const checkedCount = questionCheckboxes.filter(checkbox => checkbox.checked).length;
    bucketCheckbox.checked = checkedCount === questionCheckboxes.length;
    bucketCheckbox.indeterminate = checkedCount > 0 && checkedCount < questionCheckboxes.length;
  });
};

const attachBucketToggles = (form: HTMLFormElement): void => {
  form.querySelectorAll<HTMLButtonElement>('[data-bucket-toggle]').forEach(toggleBtn => {
    toggleBtn.addEventListener('click', () => {
      const bucketId = toggleBtn.dataset.bucketToggle ?? '';
      const questionsDiv = form.querySelector<HTMLElement>(`[data-bucket-questions="${bucketId}"]`);
      if (!questionsDiv) {
        return;
      }
      const nowHidden = questionsDiv.classList.toggle('hidden');
      toggleBtn.setAttribute('aria-expanded', nowHidden ? 'false' : 'true');
    });
  });
};

const attachQuestionBucketSelection = (form: HTMLFormElement): void => {
  form.querySelectorAll<HTMLInputElement>('[data-question-bucket]').forEach(bucketCheckbox => {
    bucketCheckbox.addEventListener('change', () => {
      const bucketId = bucketCheckbox.dataset.questionBucket ?? '';
      const questionsDiv = form.querySelector<HTMLElement>(`[data-bucket-questions="${bucketId}"]`);

      if (bucketId === 'deck-versioning') {
        // Checking the bucket header selects only the three-upgrade-paths question
        form.querySelectorAll<HTMLInputElement>(`input[data-question-option="${bucketId}"]`).forEach(questionCheckbox => {
          questionCheckbox.checked = bucketCheckbox.checked && questionCheckbox.value === 'three-upgrade-paths' && !questionCheckbox.disabled;
        });
      } else {
        form.querySelectorAll<HTMLInputElement>(`input[data-question-option="${bucketId}"]`).forEach(questionCheckbox => {
          questionCheckbox.checked = bucketCheckbox.checked;
        });
      }

      // Auto-expand the bucket when the select-all checkbox is checked
      if (bucketCheckbox.checked && questionsDiv?.classList.contains('hidden')) {
        questionsDiv.classList.remove('hidden');
        const toggleBtn = form.querySelector<HTMLButtonElement>(`[data-bucket-toggle="${bucketId}"]`);
        toggleBtn?.setAttribute('aria-expanded', 'true');
      }

      syncQuestionBucketState(form);
      syncCardSpecificQuestionField(form);
      syncBudgetQuestionField(form);
    });
  });

  form.querySelectorAll<HTMLInputElement>('input[data-question-option]').forEach(questionCheckbox => {
    questionCheckbox.addEventListener('change', () => {
      const bucketId = questionCheckbox.dataset.questionOption ?? '';

      // Single-select for deck-versioning: checking one unchecks all siblings
      if (bucketId === 'deck-versioning' && questionCheckbox.checked) {
        form.querySelectorAll<HTMLInputElement>(`input[data-question-option="${bucketId}"]`).forEach(sibling => {
          if (sibling !== questionCheckbox) {
            sibling.checked = false;
          }
        });
      }

      syncQuestionBucketState(form);
      syncCardSpecificQuestionField(form);
      syncBudgetQuestionField(form);
      syncPreferredCategoriesField(form);
    });
  });

  syncQuestionBucketState(form);
  syncCardSpecificQuestionField(form);
  syncBudgetQuestionField(form);
  syncPreferredCategoriesField(form);
};

const loadSetOptionsAsync = (): void => {
  const form = document.querySelector<HTMLFormElement>('[data-prompt-packets-form]');
  const select = form?.querySelector<HTMLSelectElement>('[data-set-options-select]');
  if (!form || !select) {
    return;
  }

  const setOptionsUrl = form.dataset.setOptionsUrl?.trim();
  if (!setOptionsUrl) {
    return;
  }

  const selectedCodes = new Set(
    (select.dataset.selectedCodes ?? '').split(',').map(c => c.trim().toLowerCase()).filter(Boolean)
  );
  type SetOptionResponse = {
    code: string;
    displayLabel: string;
    setType?: string | null;
  };

  fetch(setOptionsUrl)
    .then(response => {
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      return response.json() as Promise<SetOptionResponse[]>;
    })
    .then(sets => {
      select.innerHTML = '';
      for (const set of sets) {
        const option = document.createElement('option');
        option.value = set.code;
        option.textContent = set.displayLabel;
        if (selectedCodes.has(set.code.toLowerCase())) {
          option.selected = true;
        }
        select.appendChild(option);
      }

      deckFlowWindow.DeckFlow?.refreshDfSelect?.(select);
    })
    .catch(() => {
      const errorHint = document.querySelector<HTMLElement>('[data-set-options-error]');
      errorHint?.classList.remove('hidden');
    });
};

const attachPromptPacketsWorkflow = (): void => {
  const form = document.querySelector<HTMLFormElement>('[data-prompt-packets-form]');
  if (!form) {
    return;
  }

  const currentStep = parsePromptStep(form.dataset.promptCurrentStep);
  const persistedUiMode = storageAvailable?.getItem(promptUiModeStorageKey);
  const initialUiMode = persistedUiMode
    ? parsePromptUiMode(persistedUiMode)
    : getDefaultPromptUiMode();
  attachQuestionBucketSelection(form);
  attachBucketToggles(form);
  attachCardPicker(form);

  const bracketSelect = form.querySelector<HTMLSelectElement>('select[name="TargetCommanderBracket"]');
  bracketSelect?.addEventListener('change', () => syncVersioningBracketOptions(form));
  syncVersioningBracketOptions(form);

  applyPromptUiMode(form, initialUiMode);
  showPromptStep(form, currentStep);
  setPromptValidationMessage(null);
  scrollPromptResults(form);

  document.querySelectorAll<HTMLElement>('[data-prompt-ui-mode-button]').forEach(button => {
    button.addEventListener('click', () => {
      const mode = parsePromptUiMode(button.dataset.promptUiModeButton);
      applyPromptUiMode(form, mode);
      storageAvailable?.setItem(promptUiModeStorageKey, mode);
    });
  });

  form.querySelectorAll<HTMLElement>('[data-prompt-show-step]').forEach(button => {
    button.addEventListener('click', () => {
      const step = parsePromptStep(button.dataset.promptShowStep);
      showPromptStep(form, step);
      setPromptValidationMessage(null);
    });
  });

  form.querySelectorAll<HTMLElement>('[data-prompt-next-step]').forEach(button => {
    button.addEventListener('click', () => {
      const step = parsePromptStep(button.dataset.promptNextStep);
      showPromptStep(form, step);
      setPromptValidationMessage(null);
      form.querySelector<HTMLElement>(`[data-prompt-step="${step}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  });

  form.addEventListener('submit', event => {
    const submitter = (event as SubmitEvent).submitter as HTMLElement | null;
    if (submitter?.hasAttribute('data-prompt-upload-submit') ||
        submitter?.hasAttribute('data-prompt-download-submit')) {
      setPromptValidationMessage(null);
      return;
    }

    const step = parsePromptStep(submitter?.dataset.promptSubmitStep ?? form.dataset.promptCurrentStep);
    const validationMessage = validatePromptPacketsStep(form, step);
    if (!validationMessage) {
      setPromptValidationMessage(null);
      showPromptStep(form, step);
      return;
    }

    event.preventDefault();
    window.hideBusyIndicator?.();
    showPromptStep(form, step);
    setPromptValidationMessage(validationMessage);
  });
};

const parsePromptComparisonStep = (value: string | undefined | null): number => {
  const parsedValue = parseInt(value ?? '1', 10);
  return Number.isNaN(parsedValue) || parsedValue < 1 || parsedValue > 3 ? 1 : parsedValue;
};

const setPromptComparisonValidationMessage = (message: string | null): void => {
  const errorNode = document.querySelector<HTMLElement>('[data-prompt-comparison-validation-error]');
  if (!errorNode) {
    return;
  }

  if (!message) {
    errorNode.textContent = '';
    errorNode.classList.add('hidden');
    return;
  }

  errorNode.textContent = message;
  errorNode.classList.remove('hidden');
  errorNode.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

const showPromptComparisonStep = (form: HTMLFormElement, step: number): void => {
  form.dataset.promptComparisonCurrentStep = step.toString();
  const workflowInput = form.querySelector<HTMLInputElement>('[data-prompt-comparison-workflow-step]');
  if (workflowInput) {
    workflowInput.value = step.toString();
  }

  form.querySelectorAll<HTMLElement>('[data-prompt-comparison-step]').forEach(panel => {
    const panelStep = parsePromptComparisonStep(panel.dataset.promptComparisonStep);
    panel.classList.toggle('hidden', panelStep !== step);
    panel.setAttribute('aria-hidden', panelStep === step ? 'false' : 'true');
  });

  form.querySelectorAll<HTMLElement>('[data-prompt-comparison-show-step]').forEach(button => {
    const buttonStep = parsePromptComparisonStep(button.dataset.promptComparisonShowStep);
    button.classList.toggle('is-active', buttonStep === step);
    button.setAttribute('aria-selected', buttonStep === step ? 'true' : 'false');
    button.setAttribute('tabindex', buttonStep === step ? '0' : '-1');
  });
};

const scrollPromptComparisonResults = (form: HTMLFormElement): void => {
  const step = parsePromptComparisonStep(form.dataset.promptComparisonCurrentStep);
  const activePanel = form.querySelector<HTMLElement>(`[data-prompt-comparison-step="${step}"]`);
  const resultAnchor = activePanel?.querySelector<HTMLElement>('[data-prompt-comparison-result-anchor]');
  if (!resultAnchor) {
    return;
  }

  window.setTimeout(() => {
    resultAnchor.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, 120);
};

const validatePromptComparisonStep = (form: HTMLFormElement, step: number): string | null => {
  const deckASource = resolveSplitDeckValue(form, 'DeckA');
  const deckBSource = resolveSplitDeckValue(form, 'DeckB');
  const deckABracket = form.querySelector<HTMLSelectElement>('select[name="DeckABracket"]')?.value.trim() ?? '';
  const deckBBracket = form.querySelector<HTMLSelectElement>('select[name="DeckBBracket"]')?.value.trim() ?? '';
  const comparisonResponseJson = form.querySelector<HTMLTextAreaElement>('textarea[name="ComparisonResponseJson"]')?.value.trim() ?? '';

  if (!deckASource) {
    return 'Enter Deck A URL or deck text before generating the comparison packet.';
  }

  if (!deckBSource) {
    return 'Enter Deck B URL or deck text before generating the comparison packet.';
  }

  if (!deckABracket) {
    return 'Choose a Commander bracket for Deck A before generating the comparison packet.';
  }

  if (!deckBBracket) {
    return 'Choose a Commander bracket for Deck B before generating the comparison packet.';
  }

  if (step >= 3 && !comparisonResponseJson) {
    return 'Paste the deck_comparison JSON returned from your AI into Step 3 before rendering the summary.';
  }

  return null;
};

const attachPromptComparisonWorkflow = (): void => {
  const form = document.querySelector<HTMLFormElement>('[data-prompt-comparison-form]');
  if (!form) {
    return;
  }

  const currentStep = parsePromptComparisonStep(form.dataset.promptComparisonCurrentStep);
  showPromptComparisonStep(form, currentStep);
  setPromptComparisonValidationMessage(null);
  scrollPromptComparisonResults(form);

  form.querySelectorAll<HTMLElement>('[data-prompt-comparison-show-step]').forEach(button => {
    button.addEventListener('click', () => {
      const step = parsePromptComparisonStep(button.dataset.promptComparisonShowStep);
      showPromptComparisonStep(form, step);
      setPromptComparisonValidationMessage(null);
    });
  });

  form.querySelectorAll<HTMLElement>('[data-prompt-comparison-next-step]').forEach(button => {
    button.addEventListener('click', () => {
      const step = parsePromptComparisonStep(button.dataset.promptComparisonNextStep);
      const validationMessage = validatePromptComparisonStep(form, Math.min(step, 2));
      if (validationMessage) {
        setPromptComparisonValidationMessage(validationMessage);
        return;
      }

      showPromptComparisonStep(form, step);
      setPromptComparisonValidationMessage(null);
      form.querySelector<HTMLElement>(`[data-prompt-comparison-step="${step}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  });

  form.addEventListener('submit', event => {
    const submitter = (event as SubmitEvent).submitter as HTMLElement | null;
    if (submitter?.hasAttribute('data-prompt-upload-submit')) {
      setPromptComparisonValidationMessage(null);
      return;
    }

    const step = parsePromptComparisonStep(submitter?.dataset.promptComparisonSubmitStep ?? form.dataset.promptComparisonCurrentStep);
    const validationMessage = validatePromptComparisonStep(form, step);
    if (!validationMessage) {
      setPromptComparisonValidationMessage(null);
      showPromptComparisonStep(form, step);
      return;
    }

    event.preventDefault();
    window.hideBusyIndicator?.();
    showPromptComparisonStep(form, step);
    setPromptComparisonValidationMessage(validationMessage);
  });
};

const parsePromptCedhStep = (value: string | undefined | null): number => {
  const parsedValue = parseInt(value ?? '1', 10);
  return Number.isNaN(parsedValue) || parsedValue < 1 || parsedValue > 3 ? 1 : parsedValue;
};

const parsePromptCedhPage = (value: string | undefined | null): number => {
  const parsedValue = parseInt(value ?? '1', 10);
  return Number.isNaN(parsedValue) || parsedValue < 1 ? 1 : parsedValue;
};

const maxPromptCedhReferences = 3;

const setPromptCedhValidationMessage = (message: string | null): void => {
  const errorNode = document.querySelector<HTMLElement>('[data-prompt-cedh-validation-error]');
  if (!errorNode) {
    return;
  }

  if (!message) {
    errorNode.textContent = '';
    errorNode.classList.add('hidden');
    return;
  }

  errorNode.textContent = message;
  errorNode.classList.remove('hidden');
  errorNode.scrollIntoView({ behavior: 'smooth', block: 'center' });
};

const showPromptCedhStep = (form: HTMLFormElement, step: number): void => {
  form.dataset.promptCedhCurrentStep = step.toString();
  const workflowInput = form.querySelector<HTMLInputElement>('[data-prompt-cedh-workflow-step]');
  if (workflowInput) {
    workflowInput.value = step.toString();
  }

  form.querySelectorAll<HTMLElement>('[data-prompt-cedh-step]').forEach(panel => {
    const panelStep = parsePromptCedhStep(panel.dataset.promptCedhStep);
    panel.classList.toggle('hidden', panelStep !== step);
    panel.setAttribute('aria-hidden', panelStep === step ? 'false' : 'true');
  });

  form.querySelectorAll<HTMLElement>('[data-prompt-cedh-show-step]').forEach(button => {
    const buttonStep = parsePromptCedhStep(button.dataset.promptCedhShowStep);
    button.classList.toggle('is-active', buttonStep === step);
    button.setAttribute('aria-selected', buttonStep === step ? 'true' : 'false');
    button.setAttribute('tabindex', buttonStep === step ? '0' : '-1');
  });
};

const scrollPromptCedhResults = (form: HTMLFormElement): void => {
  const step = parsePromptCedhStep(form.dataset.promptCedhCurrentStep);
  const activePanel = form.querySelector<HTMLElement>(`[data-prompt-cedh-step="${step}"]`);
  const resultAnchor = activePanel?.querySelector<HTMLElement>('[data-prompt-cedh-result-anchor]');
  if (!resultAnchor) {
    return;
  }

  // The Step 2 anchor is a collapsed <details> by default. When the server has
  // returned PromptText (this function only runs on bootstrap with content
  // already restored), open it so the user can see the generated artifact
  // without a hunt-and-click.
  if (resultAnchor instanceof HTMLDetailsElement) {
    resultAnchor.open = true;
  }

  window.setTimeout(() => {
    resultAnchor.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, 120);
};

const validatePromptCedhStep = (form: HTMLFormElement, step: number): string | null => {
  if (step === 1) {
    const deckSource = resolveSplitDeckValue(form, 'Deck');
    if (!deckSource) {
      return 'Paste your deck URL or deck text before fetching EDH Top 16 reference decks.';
    }
  }

  if (step === 2) {
    const checkedReferences = form.querySelectorAll<HTMLInputElement>('[data-prompt-cedh-reference-checkbox]:checked').length;
    if (checkedReferences < 1) {
      return 'Select at least 1 EDH Top 16 reference deck before generating the prompt.';
    }

    if (checkedReferences > maxPromptCedhReferences) {
      return `Select no more than ${maxPromptCedhReferences} EDH Top 16 reference decks before generating the prompt.`;
    }
  }

  if (step === 3) {
    const responseJson = form.querySelector<HTMLTextAreaElement>('textarea[name="MetaGapResponseJson"]')?.value.trim() ?? '';
    if (!responseJson) {
      return 'Paste the meta_gap JSON returned from your AI into Step 3 before rendering the analysis.';
    }
  }

  return null;
};

const syncPromptCedhCheckboxState = (form: HTMLFormElement): void => {
  const checkboxes = Array.from(form.querySelectorAll<HTMLInputElement>('[data-prompt-cedh-reference-checkbox]'));
  const checkedCount = checkboxes.filter(checkbox => checkbox.checked).length;
  checkboxes.forEach(checkbox => {
    checkbox.disabled = !checkbox.checked && checkedCount >= maxPromptCedhReferences;
  });
};

const showPromptCedhReferencePage = (form: HTMLFormElement, page: number): void => {
  const rowsWithPages = Array.from(form.querySelectorAll<HTMLElement>('[data-prompt-cedh-reference-row]')).map(row => ({
    row,
    page: parsePromptCedhPage(row.dataset.promptCedhPage)
  }));
  if (rowsWithPages.length === 0) {
    return;
  }

  const maxPage = Math.max(...rowsWithPages.map(({ page: rowPage }) => rowPage));
  const nextPage = Math.min(Math.max(page, 1), maxPage);

  rowsWithPages.forEach(({ row, page: rowPage }) => {
    row.classList.toggle('hidden', rowPage !== nextPage);
  });

  form.dataset.promptCedhReferencePage = nextPage.toString();
  const status = form.querySelector<HTMLElement>('[data-prompt-cedh-page-status]');
  if (status) {
    status.textContent = `Page ${nextPage} of ${maxPage}`;
  }

  const prevButton = form.querySelector<HTMLButtonElement>('[data-prompt-cedh-page-nav="prev"]');
  const nextButton = form.querySelector<HTMLButtonElement>('[data-prompt-cedh-page-nav="next"]');
  if (prevButton) {
    prevButton.disabled = nextPage <= 1;
  }

  if (nextButton) {
    nextButton.disabled = nextPage >= maxPage;
  }
};

const parsePromptCedhSortValue = (cell: HTMLElement | undefined, type: string): number | string => {
  const raw = (cell?.dataset.sortValue ?? cell?.textContent ?? '').trim();
  if (type === 'num') {
    const parsed = parseFloat(raw);
    return Number.isNaN(parsed) ? Number.NEGATIVE_INFINITY : parsed;
  }
  return raw.toLowerCase();
};

// Core sort: shared by the desktop column-header buttons and the mobile sort
// control (the headers are sr-only-hidden at <=600px, so phones need a visible
// alternative that drives the same in-place sort + re-pagination).
const applyPromptCedhSort = (
  form: HTMLFormElement,
  table: HTMLTableElement,
  columnIndex: number,
  type: 'num' | 'text',
  direction: 'ascending' | 'descending'
): void => {
  const tbody = table.querySelector<HTMLTableSectionElement>('tbody');
  if (!tbody || columnIndex < 0) {
    return;
  }

  const factor = direction === 'ascending' ? 1 : -1;
  const rows = Array.from(tbody.querySelectorAll<HTMLTableRowElement>('[data-prompt-cedh-reference-row]'));
  rows.sort((left, right) => {
    const leftValue = parsePromptCedhSortValue(left.cells[columnIndex], type);
    const rightValue = parsePromptCedhSortValue(right.cells[columnIndex], type);
    let comparison = 0;
    if (typeof leftValue === 'number' && typeof rightValue === 'number') {
      comparison = leftValue - rightValue;
    } else {
      comparison = String(leftValue).localeCompare(String(rightValue));
    }
    return comparison * factor;
  });

  // Re-paginate: server assigned data-prompt-cedh-page by original index; after a
  // client re-sort the rows must be re-numbered so paging follows the new order.
  const pageSize = Math.max(1, parseInt(table.dataset.promptCedhPageSize ?? '10', 10) || 10);
  rows.forEach((row, index) => {
    tbody.appendChild(row);
    row.dataset.promptCedhPage = (Math.floor(index / pageSize) + 1).toString();
  });

  // Reflect sort state for assistive tech: only the active column carries a direction.
  Array.from(table.querySelectorAll<HTMLTableCellElement>('thead th')).forEach((cell, index) => {
    if (cell.hasAttribute('aria-sort')) {
      cell.setAttribute('aria-sort', index === columnIndex ? direction : 'none');
    }
  });

  showPromptCedhReferencePage(form, 1);
};

const sortPromptCedhFromHeader = (form: HTMLFormElement, button: HTMLButtonElement): void => {
  const table = button.closest<HTMLTableElement>('[data-prompt-cedh-reference-table]');
  const headerCell = button.closest<HTMLTableCellElement>('th');
  if (!table || !headerCell) {
    return;
  }

  const type = button.dataset.promptCedhSortType === 'num' ? 'num' : 'text';
  const direction = headerCell.getAttribute('aria-sort') === 'ascending' ? 'descending' : 'ascending';
  applyPromptCedhSort(form, table, headerCell.cellIndex, type, direction);
};

const sortPromptCedhFromMobileControl = (form: HTMLFormElement): void => {
  const select = form.querySelector<HTMLSelectElement>('[data-prompt-cedh-mobile-sort-select]');
  const dirButton = form.querySelector<HTMLButtonElement>('[data-prompt-cedh-mobile-sort-dir]');
  const table = form.querySelector<HTMLTableElement>('[data-prompt-cedh-reference-table]');
  if (!select || !table) {
    return;
  }

  const columnIndex = parseInt(select.value, 10);
  if (Number.isNaN(columnIndex)) {
    return;
  }

  const type = select.selectedOptions[0]?.dataset.sortType === 'num' ? 'num' : 'text';
  const direction = dirButton?.dataset.direction === 'descending' ? 'descending' : 'ascending';
  applyPromptCedhSort(form, table, columnIndex, type, direction);
};

const attachPromptCedhWorkflow = (): void => {
  const form = document.querySelector<HTMLFormElement>('[data-prompt-cedh-form]');
  if (!form) {
    return;
  }

  const currentStep = parsePromptCedhStep(form.dataset.promptCedhCurrentStep);
  showPromptCedhStep(form, currentStep);
  setPromptCedhValidationMessage(null);
  syncPromptCedhCheckboxState(form);
  showPromptCedhReferencePage(form, parsePromptCedhPage(form.dataset.promptCedhReferencePage));
  scrollPromptCedhResults(form);

  form.querySelectorAll<HTMLInputElement>('[data-prompt-cedh-reference-checkbox]').forEach(checkbox => {
    checkbox.addEventListener('change', () => {
      syncPromptCedhCheckboxState(form);
      setPromptCedhValidationMessage(null);
    });
  });

  form.querySelectorAll<HTMLElement>('[data-prompt-cedh-show-step]').forEach(button => {
    button.addEventListener('click', () => {
      const step = parsePromptCedhStep(button.dataset.promptCedhShowStep);
      showPromptCedhStep(form, step);
      setPromptCedhValidationMessage(null);
    });
  });

  form.querySelectorAll<HTMLElement>('[data-prompt-cedh-next-step]').forEach(button => {
    button.addEventListener('click', () => {
      const step = parsePromptCedhStep(button.dataset.promptCedhNextStep);
      showPromptCedhStep(form, step);
      setPromptCedhValidationMessage(null);
      form.querySelector<HTMLElement>(`[data-prompt-cedh-step="${step}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  });

  form.querySelectorAll<HTMLButtonElement>('[data-prompt-cedh-page-nav]').forEach(button => {
    button.addEventListener('click', () => {
      const currentPage = parsePromptCedhPage(form.dataset.promptCedhReferencePage);
      const delta = button.dataset.promptCedhPageNav === 'next' ? 1 : -1;
      showPromptCedhReferencePage(form, currentPage + delta);
    });
  });

  form.querySelectorAll<HTMLButtonElement>('[data-prompt-cedh-sort]').forEach(button => {
    button.addEventListener('click', () => {
      sortPromptCedhFromHeader(form, button);
    });
  });

  const mobileSortSelect = form.querySelector<HTMLSelectElement>('[data-prompt-cedh-mobile-sort-select]');
  mobileSortSelect?.addEventListener('change', () => {
    sortPromptCedhFromMobileControl(form);
  });

  const mobileSortDir = form.querySelector<HTMLButtonElement>('[data-prompt-cedh-mobile-sort-dir]');
  mobileSortDir?.addEventListener('click', () => {
    const next = mobileSortDir.dataset.direction === 'ascending' ? 'descending' : 'ascending';
    mobileSortDir.dataset.direction = next;
    mobileSortDir.textContent = next === 'ascending' ? 'Asc ▲' : 'Desc ▼';
    mobileSortDir.setAttribute('aria-label', next === 'ascending' ? 'Sort ascending' : 'Sort descending');
    if (mobileSortSelect?.value) {
      sortPromptCedhFromMobileControl(form);
    }
  });

  form.addEventListener('submit', event => {
    const submitter = (event as SubmitEvent).submitter as HTMLElement | null;
    if (submitter?.hasAttribute('data-prompt-upload-submit')) {
      setPromptCedhValidationMessage(null);
      return;
    }

    const step = parsePromptCedhStep(submitter?.dataset.promptCedhSubmitStep ?? form.dataset.promptCedhCurrentStep);
    const validationMessage = validatePromptCedhStep(form, step);
    if (!validationMessage) {
      setPromptCedhValidationMessage(null);
      showPromptCedhStep(form, step);
      return;
    }

    event.preventDefault();
    window.hideBusyIndicator?.();
    showPromptCedhStep(form, step);
    setPromptCedhValidationMessage(validationMessage);
  });
};

const wirePromptZipUpload = (): void => {
  document.querySelectorAll<HTMLInputElement>('[data-prompt-zip-upload]').forEach(input => {
    input.addEventListener('change', () => {
      const file = input.files?.[0];
      if (!file) { return; }
      // The file-picker change event bubbled to the form and already triggered persistFormState
      // with pre-upload (mostly empty) values. After the upload POST navigates back, the
      // upload-rendered server values would be overwritten by hydrateFormState reading that
      // stale state. Clear it here, and disable further persistence on this page until navigation.
      const form = input.closest<HTMLFormElement>('form[data-cache-key]');
      if (form) {
        clearPersistedFormState(form);
        form.dataset.skipPersistence = 'true';
        // Phase 10 (D-15): if the upload POST errors before navigation,
        // skipPersistence would otherwise stay true for the rest of the
        // page lifetime, silently disabling form-state persistence. Auto-
        // clear after 30s - by then the upload either navigated us away
        // (this handler is gone) or definitively failed (clear so subsequent
        // user input is persisted normally).
        const priorTimer = skipPersistenceTimers.get(form);
        if (priorTimer !== undefined) {
          window.clearTimeout(priorTimer);
        }
        const timerId = window.setTimeout(() => {
          if (form.dataset.skipPersistence === 'true') {
            delete form.dataset.skipPersistence;
          }
          skipPersistenceTimers.delete(form);
        }, 30000);
        skipPersistenceTimers.set(form, timerId);
      }

      const wrapper = input.closest('details');
      const submit = wrapper?.querySelector<HTMLButtonElement>('button[formaction$="/upload"]');
      submit?.click();
    });
  });
};

interface Window {
  setAllPrintingChoices?: (value: string) => void;
  // Why (Phase 82 SRP split): busy-indicator.ts and moxfield-extension-bridge.ts were extracted
  // into their own files. Under tsconfig's `module: "none"` these compile as global scripts that
  // share the browser's global scope with deck-sync.ts (proven by a clean tsc build), but Vitest's
  // per-file ESM import graph does NOT share bare top-level identifiers across dynamically-imported
  // modules — so cross-file calls go through `window.*` instead, mirroring this file's existing
  // `deckFlowWindow.DeckFlow?.attachTypeahead?.(...)` bridge pattern for the same reason.
  hideBusyIndicator?: () => void;
  registerBusyIndicator?: () => void;
  attachMoxfieldExtensionImport?: () => void;
  DeckInputSource?: typeof DeckInputSource;
}

window.setAllPrintingChoices = setAllPrintingChoices;
window.DeckInputSource = DeckInputSource;

// After a full-page POST (e.g. the Mana Base Load / Analyze submit), the browser lands at the top of
// the fresh page. When the response rendered a section worth seeing — marked [data-scroll-on-load] —
// bring it into view so the user lands on the loaded costs / result instead of an empty top of page.
const scrollToOnLoadTarget = (): void => {
  const target = document.querySelector<HTMLElement>('[data-scroll-on-load]');
  if (!target) {
    return;
  }

  // Wait two frames so layout has settled (a tall result table shifts the target's position after
  // first paint) and use an INSTANT scroll — a smooth scroll fired this early is dropped by the
  // browser's post-navigation scroll restoration, so the page would stay near the top.
  window.requestAnimationFrame(() => {
    window.requestAnimationFrame(() => {
      target.scrollIntoView({ behavior: 'auto', block: 'start' });
    });
  });
};

const hasRenderedResultOnLoad = (): boolean => {
  if (document.querySelector('.result-panel[data-scroll-on-load]')) {
    return true;
  }

  return Array.from(document.querySelectorAll<HTMLHeadingElement>('.result-panel h2'))
    .some(heading => (heading.textContent ?? '').includes('Result'));
};

let deckSyncBootstrapped = false;

const bootstrapDeckSync = (): void => {
  if (deckSyncBootstrapped) {
    return;
  }

  deckSyncBootstrapped = true;
  initializeSyncInputModeUi();
  window.registerBusyIndicator?.();
  if (hasRenderedResultOnLoad()) {
    window.hideBusyIndicator?.();
  }
  scrollToOnLoadTarget();
  registerPromptDownloadHandler();
  registerPromptPrintHandler();
  attachActionButtons();
  attachGenericPersistedForms();
  attachDeckSyncPersistence();
  attachPromptPacketsWorkflow();
  attachPromptComparisonWorkflow();
  attachPromptCedhWorkflow();
  wirePromptZipUpload();
  window.attachMoxfieldExtensionImport?.();
  loadSetOptionsAsync();
  attachConvertForm();
  attachCommanderSearchInputs();
};

deckFlowWindow.DeckFlow = deckFlowWindow.DeckFlow ?? {};
deckFlowWindow.DeckFlow.attachActionButtons = attachActionButtons;

const attachConvertForm = (): void => {
  const form = document.querySelector<HTMLFormElement>('form[data-cache-key="deck-convert"]');
  if (!form) return;

  const inputSourceSelect = form.querySelector<HTMLSelectElement>('select[name="InputSource"]');
  const sourceFormatSelect = form.querySelector<HTMLSelectElement>('[data-convert-source]');
  const urlPanel = form.querySelector<HTMLElement>('[data-convert-panel="url"]');
  const textPanel = form.querySelector<HTMLElement>('[data-convert-panel="text"]');
  const commanderPanel = form.querySelector<HTMLElement>('[data-convert-panel="commander"]');

  const syncConvertPanels = (): void => {
    const isUrl = inputSourceSelect?.value === 'PublicUrl';
    urlPanel?.classList.toggle('hidden', !isUrl);
    textPanel?.classList.toggle('hidden', isUrl);

    const isMoxfield = sourceFormatSelect?.value === 'Moxfield';
    commanderPanel?.classList.toggle('hidden', !isMoxfield);
  };

  inputSourceSelect?.addEventListener('change', syncConvertPanels);
  sourceFormatSelect?.addEventListener('change', syncConvertPanels);
  syncConvertPanels();
};

// Wires every commander-name datalist typeahead on the page. Used by the deck
// convert form and the cEDH meta-gap commander override. Each input resolves its
// own <datalist> via its `list` attribute so the helper is form-agnostic — the
// meta-gap commander field needs the exact EDH Top 16 card name (e.g. "Stella Lee,
// Wild Card"), so the suggestion picker prevents partial-name lookup misses.
const attachCommanderSearchInputs = (): void => {
  document.querySelectorAll<HTMLInputElement>('input[data-commander-search]').forEach(commanderInput => {
    const endpoint = commanderInput.dataset.commanderSearch;
    if (!endpoint) return;
    const targetSelector = commanderInput.dataset.commanderTarget;
    const targetElement = targetSelector
      ? document.querySelector<HTMLInputElement | HTMLSelectElement>(targetSelector)
      : null;

    const listId = commanderInput.getAttribute('list');
    const datalist = listId ? (document.getElementById(listId) as HTMLDataListElement | null) : null;
    let debounceTimer: number | undefined;
    let inFlight: AbortController | undefined;
    let generatedOption: HTMLOptionElement | undefined;

    const syncCommanderTarget = (): void => {
      if (!targetElement) return;

      const value = commanderInput.value.trim();
      if (targetElement instanceof HTMLSelectElement) {
        if (generatedOption) {
          generatedOption.remove();
          generatedOption = undefined;
        }

        const existingOption = Array.from(targetElement.options).find(option => option.value === value);
        if (existingOption) {
          targetElement.value = existingOption.value;
          return;
        }

        if (value.length === 0) {
          targetElement.value = '';
          return;
        }

        if (commanderInput.dataset.commanderCreateOption === 'true') {
          generatedOption = document.createElement('option');
          generatedOption.value = value;
          generatedOption.text = value;
          targetElement.appendChild(generatedOption);
          targetElement.value = value;
        }

        return;
      }

      targetElement.value = value;
    };

    if (targetElement instanceof HTMLSelectElement) {
      targetElement.addEventListener('change', () => {
        commanderInput.value = targetElement.value;
      });
    }

    commanderInput.addEventListener('input', () => {
      syncCommanderTarget();
      window.clearTimeout(debounceTimer);
      const query = commanderInput.value.trim();
      if (query.length < 2) {
        // Cancel any in-flight request too, so a late response for a longer query
        // can't repopulate the list the user just cleared.
        inFlight?.abort();
        if (datalist) datalist.innerHTML = '';
        return;
      }

      debounceTimer = window.setTimeout(async () => {
        // Abort any request the previous keystroke left in flight so a slow stale
        // response can't overwrite newer suggestions or waste a Scryfall throttle slot.
        inFlight?.abort();
        inFlight = new AbortController();
        try {
          const response = await fetch(`${endpoint}?q=${encodeURIComponent(query)}`, { signal: inFlight.signal });
          if (!response.ok || !datalist) return;
          const names = await response.json() as string[];
          // Drop the result if the input moved on while we were fetching.
          if (commanderInput.value.trim() !== query) return;
          datalist.innerHTML = '';
          names.forEach(name => {
            const option = document.createElement('option');
            option.value = name;
            datalist.appendChild(option);
          });
        } catch {
          // ignore — typeahead is best-effort
        }
      }, 300);
    });

    syncCommanderTarget();
  });
};

document.addEventListener('DOMContentLoaded', bootstrapDeckSync);
if (document.readyState !== 'loading') {
  bootstrapDeckSync();
}

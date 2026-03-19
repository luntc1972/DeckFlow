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

type DeckInputSourceValue = (typeof DeckInputSource)[keyof typeof DeckInputSource];

type PanelConfig = {
  selectName: string;
  urlSelector: string;
  textSelector: string;
};

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

let syncInputModeInitialized = false;

const initializeSyncInputModeUi = (): void => {
  if (syncInputModeInitialized) {
    return;
  }

  syncInputModeInitialized = true;
  const inputSelectors = document.querySelectorAll<HTMLSelectElement>('select[name="MoxfieldInputSource"], select[name="ArchidektInputSource"]');
  inputSelectors.forEach(element => {
    element.addEventListener('change', updateSyncInputModeUi);
  });

  updateSyncInputModeUi();
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

const toggleSyncDirection = (): void => {
  const directionSelect = document.querySelector<HTMLSelectElement>('select[name="Direction"]');
  const form = document.querySelector<HTMLFormElement>('form.deck-form');
  if (!directionSelect || !form) {
    return;
  }

  directionSelect.value = directionSelect.value === 'DeckSyncWorkbench'
    ? 'ArchidektToMoxfield'
    : 'DeckSyncWorkbench';

  if (typeof form.requestSubmit === 'function') {
    form.requestSubmit();
  } else {
    form.submit();
  }
};

let busyProgressTimer: number | undefined;
let busyHideTimer: number | undefined;

const formatProgressText = (steps: string[], index: number) => `Step ${index + 1}/${steps.length}: ${steps[index]}`;

const clearBusyProgress = (): void => {
  if (busyProgressTimer !== undefined) {
    window.clearInterval(busyProgressTimer);
    busyProgressTimer = undefined;
  }
};

const hideBusyIndicator = (): void => {
  const container = document.getElementById('busy-indicator');
  const progressNode = document.getElementById('busy-indicator-progress');
  if (!container) {
    return;
  }

  container.classList.add('hidden');
  if (progressNode) {
    progressNode.textContent = '';
    delete progressNode.dataset.currentIndex;
  }

  clearBusyProgress();
  if (busyHideTimer !== undefined) {
    window.clearTimeout(busyHideTimer);
    busyHideTimer = undefined;
  }
};

const scheduleBusyHide = (durationMs: number): void => {
  if (!durationMs || durationMs <= 0) {
    return;
  }

  if (busyHideTimer !== undefined) {
    window.clearTimeout(busyHideTimer);
  }

  busyHideTimer = window.setTimeout(() => {
    hideBusyIndicator();
  }, durationMs);
};

const showBusyIndicator = (
  title?: string,
  message?: string,
  progressSteps?: string[],
  durationMs?: number,
  holdFinalStep = false
): void => {
  const container = document.getElementById('busy-indicator');
  const titleNode = document.getElementById('busy-indicator-title');
  const messageNode = document.getElementById('busy-indicator-message');
  const progressNode = document.getElementById('busy-indicator-progress');
  if (!container || !titleNode || !messageNode) {
    return;
  }

  titleNode.textContent = title || 'Working';
  messageNode.textContent = message || 'Request in progress.';
  container.classList.remove('hidden');

  clearBusyProgress();
  if (progressNode) {
    if (progressSteps && progressSteps.length > 0) {
      const finalIndex = progressSteps.length - 1;
      let currentIndex = 0;
      progressNode.textContent = formatProgressText(progressSteps, currentIndex);
      progressNode.dataset.currentIndex = currentIndex.toString();

      busyProgressTimer = window.setInterval(() => {
        currentIndex++;

        if (currentIndex > finalIndex) {
          currentIndex = holdFinalStep ? finalIndex : 0;
        }

        progressNode.dataset.currentIndex = currentIndex.toString();
        progressNode.textContent = formatProgressText(progressSteps, currentIndex);

        if (holdFinalStep && currentIndex === finalIndex) {
          clearBusyProgress();
        }
      }, 4000);
    } else {
      progressNode.textContent = '';
    }
  }
  if (durationMs && durationMs > 0) {
    scheduleBusyHide(durationMs);
  }
};

const registerBusyIndicator = (): void => {
  document.querySelectorAll<HTMLFormElement>('form[data-busy-title]').forEach(form => {
    form.addEventListener('submit', () => {
      const title = form.getAttribute('data-busy-title');
      const message = form.getAttribute('data-busy-message');
      const stepsAttr = form.getAttribute('data-busy-progress');
      const steps = stepsAttr
        ? stepsAttr
            .split('|')
            .map(step => step.trim())
            .filter(step => step.length > 0)
        : [];
      const durationAttr = form.getAttribute('data-busy-duration');
      const duration = durationAttr ? parseInt(durationAttr, 10) : undefined;
      const holdFinalAttr = form.getAttribute('data-busy-hold-final-step');
      const holdFinalStep = holdFinalAttr !== null && holdFinalAttr.toLowerCase() === 'true';
      showBusyIndicator(
        title ?? undefined,
        message ?? undefined,
        steps.length > 0 ? steps : undefined,
        duration,
        holdFinalStep
      );
    });
  });
};

const formStateStoragePrefix = 'decksync-form-state-';
const storageAvailable = (() => {
  try {
    const testKey = '__decksync_test_key__';
    window.localStorage.setItem(testKey, '1');
    window.localStorage.removeItem(testKey);
    return window.localStorage;
  } catch {
    return null;
  }
})();

const serializeFormFields = (form: HTMLFormElement) => {
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

const restoreFormFields = (form: HTMLFormElement, data: Record<string, string>) => {
  form.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>('[name]').forEach(element => {
    const value = data[element.name];
    if (value === undefined) {
      return;
    }

    if (element instanceof HTMLInputElement && (element.type === 'checkbox' || element.type === 'radio')) {
      element.checked = element.value === value;
      return;
    }

    element.value = value;
  });
};

const persistFormState = (form: HTMLFormElement): void => {
  const key = form.getAttribute('data-cache-key');
  if (!key || !storageAvailable) {
    return;
  }

  const state = serializeFormFields(form);
  storageAvailable.setItem(`${formStateStoragePrefix}${key}`, JSON.stringify(state));
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
    const state = JSON.parse(json) as Record<string, string>;
    restoreFormFields(form, state);
  } catch {
    storageAvailable.removeItem(`${formStateStoragePrefix}${key}`);
  }
};

const attachFormStatePersistence = (): void => {
  document.querySelectorAll<HTMLFormElement>('form[data-cache-key]').forEach(form => {
    hydrateFormState(form);
    const handler = () => persistFormState(form);
    form.addEventListener('input', handler);
    form.addEventListener('change', handler);
    form.addEventListener('submit', handler);
  });
};

const initializeScrollHandler = (): void => {
  const deckForm = document.querySelector<HTMLFormElement>('form.deck-form');
  if (deckForm) {
    deckForm.addEventListener('submit', () => {
      window.setTimeout(scrollResults, 2500);
    });
  }
};

interface Window {
  toggleSyncDirection?: () => void;
  setAllPrintingChoices?: (value: string) => void;
  showCommanderHarvestBusy?: () => void;
  hideBusyIndicator?: () => void;
}

window.toggleSyncDirection = toggleSyncDirection;
window.setAllPrintingChoices = setAllPrintingChoices;
window.showCommanderHarvestBusy = (): void => {
  showBusyIndicator(
    'Growing commander cache',
    'Scanning Archidekt decks for commander categories.',
    ['Refreshing cached store', 'Scanning recent Archidekt decks', 'Compiling commander categories'],
    30000,
    true
  );
};
window.hideBusyIndicator = hideBusyIndicator;

const bootstrapDeckSync = (): void => {
  initializeSyncInputModeUi();
  registerBusyIndicator();
  initializeScrollHandler();
  attachFormStatePersistence();
};

document.addEventListener('DOMContentLoaded', bootstrapDeckSync);
if (document.readyState !== 'loading') {
  bootstrapDeckSync();
}

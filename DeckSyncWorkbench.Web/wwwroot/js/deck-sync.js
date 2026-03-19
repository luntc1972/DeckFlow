(() => {
    "use strict";
const togglePanel = (selector, shouldHide) => {
    document.querySelectorAll(selector).forEach(element => {
        element.classList.toggle('hidden', shouldHide);
        element.style.display = shouldHide ? 'none' : '';
    });
};
const DeckInputSource = {
    PasteText: 'PasteText',
    PublicUrl: 'PublicUrl',
};
const panelConfigs = [
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
const updateSyncInputModeUi = () => {
    panelConfigs.forEach(config => {
        const select = document.querySelector(`select[name="${config.selectName}"]`);
        if (!select) {
            return;
        }
        const selectedValue = select.value;
        const showUrl = selectedValue === DeckInputSource.PublicUrl;
        const showText = selectedValue === DeckInputSource.PasteText;
        togglePanel(config.urlSelector, !showUrl);
        togglePanel(config.textSelector, !showText);
    });
};
let syncInputModeInitialized = false;
const initializeSyncInputModeUi = () => {
    if (syncInputModeInitialized) {
        return;
    }
    syncInputModeInitialized = true;
    const inputSelectors = document.querySelectorAll('select[name="MoxfieldInputSource"], select[name="ArchidektInputSource"]');
    inputSelectors.forEach(element => {
        element.addEventListener('change', updateSyncInputModeUi);
    });
    updateSyncInputModeUi();
};
const scrollResults = () => {
    const anchor = document.getElementById('results-anchor');
    if (anchor) {
        anchor.scrollIntoView({ behavior: 'smooth' });
    }
};
const setAllPrintingChoices = (value) => {
    const selector = `input[type="radio"][name^="Resolutions["][value="${value}"]`;
    document.querySelectorAll(selector).forEach(input => {
        input.checked = true;
    });
};
const toggleSyncDirection = () => {
    const directionSelect = document.querySelector('select[name="Direction"]');
    const form = document.querySelector('form.deck-form');
    if (!directionSelect || !form) {
        return;
    }
    directionSelect.value = directionSelect.value === 'DeckSyncWorkbench'
        ? 'ArchidektToMoxfield'
        : 'DeckSyncWorkbench';
    if (typeof form.requestSubmit === 'function') {
        form.requestSubmit();
    }
    else {
        form.submit();
    }
};
let busyProgressTimer;
let busyHideTimer;
const formatProgressText = (steps, index) => `Step ${index + 1}/${steps.length}: ${steps[index]}`;
const clearBusyProgress = () => {
    if (busyProgressTimer !== undefined) {
        window.clearInterval(busyProgressTimer);
        busyProgressTimer = undefined;
    }
};
const hideBusyIndicator = () => {
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
const scheduleBusyHide = (durationMs) => {
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
const showBusyIndicator = (title, message, progressSteps, durationMs, holdFinalStep = false) => {
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
        }
        else {
            progressNode.textContent = '';
        }
    }
    if (durationMs && durationMs > 0) {
        scheduleBusyHide(durationMs);
    }
};
const registerBusyIndicator = () => {
    document.querySelectorAll('form[data-busy-title]').forEach(form => {
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
            showBusyIndicator(title !== null && title !== void 0 ? title : undefined, message !== null && message !== void 0 ? message : undefined, steps.length > 0 ? steps : undefined, duration, holdFinalStep);
        });
    });
};
const formStateStoragePrefix = 'decksync-form-state-';
const cacheInitializedKey = 'decksync-cache-initialized';
const readinessStartTime = performance.now();
let readinessMarked = false;

const disableReadinessButtons = () => {
    document.querySelectorAll('[data-enable-on-ready]').forEach(button => {
        button.disabled = true;
        button.classList.add('disabled');
    });
};

const markReadiness = () => {
    if (readinessMarked) {
        return;
    }

    readinessMarked = true;
    const duration = performance.now() - readinessStartTime;
    console.info(`Deck Sync page ready in ${Math.round(duration)}ms.`);
    document.querySelectorAll('[data-enable-on-ready]').forEach(button => {
        button.disabled = false;
        button.classList.remove('disabled');
    });
};
const storageAvailable = (() => {
    try {
        const testKey = '__decksync_test_key__';
        const storage = window.sessionStorage;
        storage.setItem(testKey, '1');
        storage.removeItem(testKey);
        return storage;
    }
    catch (_a) {
        return null;
    }
})();
const serializeFormFields = (form) => {
    const state = {};
    form.querySelectorAll('[name]').forEach(element => {
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
const restoreFormFields = (form, data) => {
    form.querySelectorAll('[name]').forEach(element => {
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
const persistFormState = (form) => {
    const key = form.getAttribute('data-cache-key');
    if (!key || !storageAvailable) {
        return;
    }
    const state = serializeFormFields(form);
    storageAvailable.setItem(`${formStateStoragePrefix}${key}`, JSON.stringify(state));
};
const hydrateFormState = (form) => {
    const key = form.getAttribute('data-cache-key');
    if (!key || !storageAvailable) {
        return;
    }
    const json = storageAvailable.getItem(`${formStateStoragePrefix}${key}`);
    if (!json) {
        return;
    }
    try {
        const state = JSON.parse(json);
        restoreFormFields(form, state);
    }
    catch (_a) {
        storageAvailable.removeItem(`${formStateStoragePrefix}${key}`);
    }
};
const attachFormStatePersistence = () => {
    document.querySelectorAll('form[data-cache-key]').forEach(form => {
        hydrateFormState(form);
        const handler = () => persistFormState(form);
        form.addEventListener('input', handler);
        form.addEventListener('change', handler);
        form.addEventListener('submit', handler);
    });
};

const resetFormFields = (form) => {
    form.querySelectorAll('input, textarea, select').forEach(element => {
        if (element instanceof HTMLInputElement) {
            if (element.type === 'checkbox' || element.type === 'radio') {
                element.checked = false;
                return;
            }
            if (element.type === 'submit' || element.type === 'button') {
                return;
            }
        }

        if (element instanceof HTMLSelectElement) {
            const defaultValue = element.getAttribute('data-default-value');
            element.value = defaultValue ?? (element.options[0]?.value ?? '');
            return;
        }

        element.value = element.getAttribute('data-default-value') ?? '';
    });
};

const clearFormCache = (form) => {
    if (!form) {
        return;
    }

    const key = form.getAttribute('data-cache-key');
    if (key && storageAvailable) {
        storageAvailable.removeItem(`${formStateStoragePrefix}${key}`);
    }

    resetFormFields(form);
    updateSyncInputModeUi();
};

const clearAllFormCache = () => {
    document.querySelectorAll('form[data-cache-key]').forEach(form => {
        clearFormCache(form);
    });
};

const ensureCacheInitialized = () => {
    if (!storageAvailable) {
        return;
    }

    if (!storageAvailable.getItem(cacheInitializedKey)) {
        clearAllFormCache();
        storageAvailable.setItem(cacheInitializedKey, 'true');
    }
};

const attachCacheClearButtons = () => {
    document.querySelectorAll('[data-clear-cache]').forEach(button => {
        button.addEventListener('click', event => {
            event.preventDefault();
            const form = button.closest('form[data-cache-key]');
            if (form) {
                clearFormCache(form);
            }
        });
    });
};

const validateLookupForm = (form) => {
    const lookupField = form.querySelector('input[name="CardName"], input[name="CommanderName"]');
    if (!lookupField || !lookupField.value.trim()) {
        lookupField?.setCustomValidity('Please enter a card or commander name.');
        lookupField?.reportValidity();
        return false;
    }

    const modeSelect = form.querySelector('select[name="Mode"]');
    if (modeSelect && !modeSelect.value) {
        modeSelect.setCustomValidity('Please choose a lookup mode.');
        modeSelect.reportValidity();
        return false;
    }

    lookupField?.setCustomValidity('');
    modeSelect?.setCustomValidity('');

    return true;
};

const attachLookupValidation = () => {
    document.querySelectorAll('form[data-validate-lookup]').forEach(form => {
        form.addEventListener('submit', event => {
            if (!validateLookupForm(form)) {
                event.preventDefault();
            }
        });
    });
};
const initializeScrollHandler = () => {
    const deckForm = document.querySelector('form.deck-form');
    if (deckForm) {
        deckForm.addEventListener('submit', () => {
            window.setTimeout(scrollResults, 2500);
        });
    }
};
window.toggleSyncDirection = toggleSyncDirection;
window.setAllPrintingChoices = setAllPrintingChoices;
window.showCommanderHarvestBusy = () => {
    showBusyIndicator('Growing commander cache', 'Scanning Archidekt decks for commander categories.', ['Refreshing cached store', 'Scanning recent Archidekt decks', 'Compiling commander categories'], 30000, true);
};
window.hideBusyIndicator = hideBusyIndicator;
const bootstrapDeckSync = () => {
    initializeSyncInputModeUi();
    registerBusyIndicator();
    initializeScrollHandler();
    ensureCacheInitialized();
    attachFormStatePersistence();
    attachCacheClearButtons();
    attachLookupValidation();
    markReadiness();
};
document.addEventListener('DOMContentLoaded', bootstrapDeckSync);
if (document.readyState !== 'loading') {
    bootstrapDeckSync();
}
disableReadinessButtons();
})();

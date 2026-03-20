"use strict";

const togglePanel = (selector, visible) => {
    const element = document.querySelector(`[data-api-panel="${selector}"]`);
    if (!element) {
        return;
    }
    element.classList.toggle("hidden", !visible);
};

const setFieldText = (field, value) => {
    const element = document.querySelector(`[data-api-field="${field}"]`);
    if (!element) {
        return;
    }
    if (element instanceof HTMLTextAreaElement || element instanceof HTMLInputElement) {
        element.value = value ?? "";
    }
    else {
        element.textContent = value ?? "";
    }
};

const handleError = (panel, message) => {
    if (!message) {
        togglePanel(panel, false);
        return;
    }
    setFieldText(panel === "suggest-error" ? "suggest-error-text" : "commander-error-text", message);
    togglePanel(panel, true);
};

const resetCardUi = () => {
    togglePanel("suggest-error", false);
    togglePanel("cache-info", false);
    togglePanel("additional-decks", false);
    togglePanel("source-summary", false);
    togglePanel("exact", false);
    togglePanel("inferred", false);
    togglePanel("edhrec", false);
    togglePanel("no-suggestions", false);
    togglePanel("lookup-hint", false);
    togglePanel("commander-results", false);
};

const resetCommanderUi = () => {
    togglePanel("commander-error", false);
    togglePanel("commander-results", false);
    togglePanel("commander-no-results", false);
    togglePanel("commander-additional", false);
    togglePanel("commander-hint", false);
};

const handleCardResponse = (form, response) => {
    resetCardUi();
    handleError("suggest-error", null);
    const hintText = response.noSuggestionsFound && response.noSuggestionsMessage
        ? response.noSuggestionsMessage
        : `The cached store tracks Archidekt categories that appear on decks containing ${response.cardName}. Click Suggest to keep scanning public decks for another 30 seconds so those categories can populate.`;
    setFieldText("lookup-hint-text", hintText);
    togglePanel("lookup-hint", true);

    if (response.cardDeckTotals.totalDeckCount > 0) {
        setFieldText("cache-info-count", response.cardDeckTotals.totalDeckCount.toString());
        setFieldText("cache-info-text", `The cached store currently contains ${response.cardDeckTotals.totalDeckCount} deck(s) featuring ${response.cardName}.`);
        togglePanel("cache-info", true);
    }

    if (response.additionalDecksFound > 0) {
        setFieldText("additional-deck-count", response.additionalDecksFound.toString());
        togglePanel("additional-decks", true);
    }

    if (response.suggestionSourceSummary) {
        setFieldText("source-summary-text", response.suggestionSourceSummary);
        togglePanel("source-summary", true);
    }

    const isReferenceMode = form.querySelector('select[name="Mode"]').value === 'ReferenceDeck';
    togglePanel("exact", response.hasExactCategories && isReferenceMode);
    setFieldText("exact-context", response.exactSuggestionContextText);
    setFieldText("exact-text", response.exactCategoriesText);

    togglePanel("inferred", true);
    setFieldText("inferred-context", response.inferredSuggestionContextText);
    setFieldText("inferred-text", response.inferredCategoriesText);
    setFieldText("cache-info-detail", response.cardDeckTotals.totalDeckCount > 0
        ? `${response.cardDeckTotals.totalDeckCount} deck(s) in the cache include ${response.cardName}.`
        : "");

    togglePanel("edhrec", response.hasEdhrecCategories);
    setFieldText("edhrec-context", response.edhrecSuggestionContextText);
    setFieldText("edhrec-text", response.edhrecCategoriesText);

    togglePanel("no-suggestions", response.noSuggestionsFound);
    if (response.noSuggestionsFound) {
        setFieldText("no-suggestions-text", response.noSuggestionsMessage ?? `No category suggestions were found for ${response.cardName}.`);
    }
};

const handleCommanderResponse = (response) => {
    resetCommanderUi();
    const hasResults = response.summaries.length > 0;
    const hintText = hasResults
        ? `Commander categories for ${response.commanderName} were sourced from the cached store.`
        : `No commander categories for ${response.commanderName} have been observed in the cached data yet. Run Show Categories again to refresh the cache.`;
    setFieldText("commander-hint-text", hintText);
    togglePanel("commander-hint", true);

    if (response.additionalDecksFound > 0) {
        setFieldText("commander-additional-count", response.additionalDecksFound.toString());
        togglePanel("commander-additional", true);
    }

    togglePanel("commander-results", hasResults);
    togglePanel("commander-no-results", !hasResults);
    if (!hasResults) {
        setFieldText("commander-no-results-text", response.noResultsMessage ?? hintText);
        return;
    }

    setFieldText("commander-cards-count", `${response.cardRowCount} cards contributed to this summary.`);
    setFieldText("commander-deck-count", `Derived from ${response.harvestedDeckCount} cached decks with ${response.categoryCount} distinct categories.`);
    setFieldText("commander-card-deck-count", `${response.commanderName} appears in ${response.cardDeckTotals.totalDeckCount} cached commander deck(s).`);
    setFieldText("commander-card-count", response.cardDeckTotals.totalDeckCount.toString());

    const body = document.querySelector('[data-api-field="commander-summary-body"]');
    if (!body) {
        return;
    }
    body.innerHTML = '';
    response.summaries.forEach(summary => {
        const row = document.createElement('tr');
        row.innerHTML = `<td>${summary.category}</td><td>${summary.count}</td><td>${summary.deckCount}</td>`;
        body.appendChild(row);
    });
};

const readRequestData = (form) => {
    const data = {};
    form.querySelectorAll('[name]').forEach(element => {
        if (!element.name) {
            return;
        }
        if (element instanceof HTMLInputElement && (element.type === 'checkbox' || element.type === 'radio')) {
            if (!element.checked) {
                return;
            }
        }
        data[element.name] = element.value;
    });
    return data;
};

const restoreFormState = (form) => {
    const key = form.dataset.cacheKey;
    if (!key) {
        return;
    }

    const payload = sessionStorage.getItem(key);
    if (!payload) {
        return;
    }

    try {
        const values = JSON.parse(payload);
        Object.entries(values).forEach(([name, value]) => {
            const element = form.querySelector(`[name="${name}"]`);
            if (!element) {
                return;
            }

            if (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement || element instanceof HTMLSelectElement) {
                element.value = value;
            }
        });
    }
    catch (error) {
        console.warn('Unable to restore cached form state', error);
        sessionStorage.removeItem(key);
    }
};

const persistFormState = (form) => {
    const key = form.dataset.cacheKey;
    if (!key) {
        return;
    }

    const state = readRequestData(form);
    sessionStorage.setItem(key, JSON.stringify(state));
};

const submitSuggestion = async (form) => {
    const endpoint = form.dataset.suggestionApi;
    if (!endpoint) {
        return;
    }
    const type = form.dataset.suggestionsType;
    try {
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(readRequestData(form))
        });
        if (!response.ok) {
            let payload;
            try {
                payload = await response.json();
            }
            catch {
                payload = null;
            }
            handleError(type === 'commander' ? 'commander-error' : 'suggest-error', payload?.message ?? payload?.Message ?? 'Unable to fetch suggestions.');
            return;
        }
        const data = await response.json();
        if (type === 'card') {
            handleCardResponse(form, data);
        }
        else if (type === 'commander') {
            handleCommanderResponse(data);
        }
    }
    catch (error) {
        handleError(type === 'commander' ? 'commander-error' : 'suggest-error', error?.message ?? error?.Message ?? 'Unable to fetch suggestions.');
    }
};

const attachSuggestionHandlers = () => {
    document.querySelectorAll('form[data-suggestions-type]').forEach(form => {
        restoreFormState(form);
        form.addEventListener('submit', event => {
            event.preventDefault();
            persistFormState(form);
            submitSuggestion(form);
        });
        form.addEventListener('input', () => persistFormState(form));
        const clearButton = form.querySelector('[data-clear-cache]');
        if (clearButton) {
            clearButton.addEventListener('click', () => {
                form.reset();
                const key = form.dataset.cacheKey;
                if (key) {
                    sessionStorage.removeItem(key);
                }
                if (form.dataset.suggestionsType === 'card') {
                    resetCardUi();
                }
                else {
                    resetCommanderUi();
                }
            });
        }
        const retryButton = form.querySelector('[data-api-action="retry"]');
        if (retryButton) {
            retryButton.addEventListener('click', () => {
                persistFormState(form);
                form.requestSubmit();
            });
        }
    });
};

document.addEventListener('DOMContentLoaded', attachSuggestionHandlers);

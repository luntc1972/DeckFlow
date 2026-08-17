((): void => {
  'use strict';

  type SuggestionForm = HTMLFormElement & {
    dataset: DOMStringMap & {
      cacheKey?: string;
      suggestionApi?: string;
      suggestionsType?: string;
    };
  };

  type SuggestionResponseEnvelope =
    | { type: 'card'; payload: CardSuggestionResponse }
    | { type: 'commander'; payload: CommanderSuggestionResponse };

  interface Window {
    hideBusyIndicator?: () => void;
  }

  const formStateStoragePrefix = 'decksync-form-state-';
  const formResultStoragePrefix = 'decksync-form-result-';
  const tabNavigationKey = 'decksync-tab-navigation';

  type CardSuggestionResponse = {
    cardName: string;
    mergedCategoriesText?: string | null;
    exactCategoriesText: string;
    exactSuggestionContextText: string;
    inferredCategoriesText: string;
    inferredSuggestionContextText: string;
    edhrecCategoriesText: string;
    edhrecSuggestionContextText: string;
    hasExactCategories: boolean;
    hasInferredCategories: boolean;
    hasEdhrecCategories: boolean;
    taggerCategoriesText: string;
    taggerSuggestionContextText: string;
    hasTaggerCategories: boolean;
    suggestionSourceSummary?: string | null;
    weightedCategories?: Array<{
      category: string;
      deckCount: number | null;
      percent: number | null;
      sourceCount: number;
      sourceTotal: number;
    }>;
    noSuggestionsFound: boolean;
    noSuggestionsMessage?: string | null;
    cardDeckTotals: {
      totalDeckCount: number;
    };
  };

  type CommanderSuggestionResponse = {
    commanderName: string;
    cardRowCount: number;
    categoryCount: number;
    harvestedDeckCount: number;
    noResultsMessage?: string | null;
    cardDeckTotals: {
      totalDeckCount: number;
    };
    summaries: Array<{
      category: string;
      count: number;
      deckCount: number;
      deckShare: number;
    }>;
  };

  const toggleSuggestionPanel = (selector: string, visible: boolean): void => {
    const element = document.querySelector<HTMLElement>(`[data-api-panel="${selector}"]`);
    if (!element) {
      return;
    }

    element.classList.toggle('hidden', !visible);
  };

  const setFieldText = (field: string, value?: string | null): void => {
    const element = document.querySelector<HTMLElement>(`[data-api-field="${field}"]`);
    if (!element) {
      return;
    }

    if (element instanceof HTMLTextAreaElement || element instanceof HTMLInputElement) {
      element.value = value ?? '';
      return;
    }

    element.textContent = value ?? '';
  };

  const createTextCell = (value: string | number): HTMLTableCellElement => {
    const cell = document.createElement('td');
    cell.textContent = value.toString();
    return cell;
  };

  const createUnavailableCell = (): HTMLTableCellElement => {
    const cell = document.createElement('td');
    const dash = document.createElement('span');
    dash.setAttribute('aria-hidden', 'true');
    dash.textContent = '—';
    const unavailableText = document.createElement('span');
    unavailableText.className = 'sr-only';
    unavailableText.textContent = 'Not available';
    cell.append(dash, unavailableText);
    return cell;
  };

  const createCommanderSummaryRow = (summary: CommanderSuggestionResponse['summaries'][number]): HTMLTableRowElement => {
    const row = document.createElement('tr');
    row.appendChild(createTextCell(summary.category));
    row.appendChild(createTextCell(`${Math.round(summary.deckShare * 100)}%`));
    row.appendChild(createTextCell(summary.deckCount));
    return row;
  };

  const renderWeightedCategories = (rows?: CardSuggestionResponse['weightedCategories']): void => {
    const body = document.querySelector<HTMLTableSectionElement>('[data-api-field="weighted-body"]');
    body?.replaceChildren();

    const hasRows = Boolean(body && rows && rows.length > 0);
    if (hasRows && body && rows) {
      rows.forEach(row => {
        const tableRow = document.createElement('tr');
        tableRow.appendChild(createTextCell(row.category));
        tableRow.appendChild(row.deckCount === null ? createUnavailableCell() : createTextCell(row.deckCount));
        tableRow.appendChild(
          row.percent === null
            ? createUnavailableCell()
            : createTextCell(row.percent === 0 && row.deckCount !== null && row.deckCount > 0 ? '<1%' : `${row.percent}%`));
        tableRow.appendChild(createTextCell(`${row.sourceCount}/${row.sourceTotal}`));
        body.appendChild(tableRow);
      });
    }

    toggleSuggestionPanel('weighted', hasRows);
  };

  const handleError = (panel: 'suggest-error' | 'commander-error', message?: string | null): void => {
    if (!message) {
      toggleSuggestionPanel(panel, false);
      return;
    }

    setFieldText(panel === 'suggest-error' ? 'suggest-error-text' : 'commander-error-text', message);
    toggleSuggestionPanel(panel, true);
  };

  const resetCardUi = (): void => {
    toggleSuggestionPanel('suggest-error', false);
    toggleSuggestionPanel('cache-info', false);
    toggleSuggestionPanel('source-summary', false);
    toggleSuggestionPanel('merged', false);
    toggleSuggestionPanel('weighted', false);
    toggleSuggestionPanel('exact', false);
    toggleSuggestionPanel('inferred', false);
    toggleSuggestionPanel('edhrec', false);
    toggleSuggestionPanel('tagger', false);
    toggleSuggestionPanel('no-suggestions', false);
    toggleSuggestionPanel('lookup-hint', false);
    toggleSuggestionPanel('commander-results', false);
  };

  const resetCommanderUi = (): void => {
    toggleSuggestionPanel('commander-error', false);
    toggleSuggestionPanel('commander-results', false);
    toggleSuggestionPanel('commander-no-results', false);
    toggleSuggestionPanel('commander-hint', false);
  };

  const scrollPanelIntoCenter = (selector: string): void => {
    const element = document.querySelector<HTMLElement>(selector);
    if (!element) {
      return;
    }

    window.setTimeout(() => {
      element.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 100);
  };

  const busyConfigByMode: Record<string, { title: string; message: string; progress: string }> = {
    CachedData: {
      title: 'Finding Categories',
      message: 'Checking cached category data.',
      progress: 'Checking cached store|Loading category matches|Finalizing results',
    },
    ReferenceDeck: {
      title: 'Finding Categories',
      message: 'Loading the reference deck and matching categories.',
      progress: 'Loading reference deck|Matching card categories|Finalizing results',
    },
    ScryfallTagger: {
      title: 'Looking Up Tags',
      message: 'Fetching functional tags from Scryfall Tagger.',
      progress: 'Resolving card|Querying Scryfall Tagger|Finalizing tags',
    },
    All: {
      title: 'Finding Categories',
      message: 'Checking all sources: cached store and Scryfall Tagger.',
      progress: 'Checking cached store|Querying Scryfall Tagger|Finalizing results',
    },
  };

  const updateBusyAttributes = (form: HTMLFormElement, mode: string): void => {
    const config = busyConfigByMode[mode] ?? busyConfigByMode['CachedData'];
    form.setAttribute('data-busy-title', config.title);
    form.setAttribute('data-busy-message', config.message);
    form.setAttribute('data-busy-progress', config.progress);
  };

  const updateSuggestionInputModeUi = (): void => {
    const modeSelect = document.querySelector<HTMLSelectElement>('select[name="Mode"]');
    const archidektModeSelect = document.querySelector<HTMLSelectElement>('select[name="ArchidektInputSource"]');
    if (!modeSelect || !archidektModeSelect) {
      return;
    }

    const mode = modeSelect.value;
    const showReference = mode === 'ReferenceDeck';
    const showUrl = archidektModeSelect.value === 'PublicUrl';
    const showText = archidektModeSelect.value === 'PasteText';

    document.querySelectorAll<HTMLElement>('.reference-controls').forEach(element => {
      element.classList.toggle('hidden', !showReference);
    });

    document.querySelectorAll<HTMLElement>('[data-suggest-panel="url"]').forEach(element => {
      element.classList.toggle('hidden', !showReference || !showUrl);
    });

    document.querySelectorAll<HTMLElement>('[data-suggest-panel="text"]').forEach(element => {
      element.classList.toggle('hidden', !showReference || !showText);
    });

    const form = modeSelect.closest<HTMLFormElement>('form');
    if (form) {
      updateBusyAttributes(form, mode);
    }
  };

  const handleCardResponse = (form: SuggestionForm, response: CardSuggestionResponse): void => {
    resetCardUi();
    handleError('suggest-error', null);

    const modeSelect = form.querySelector<HTMLSelectElement>('select[name="Mode"]');
    const mode = modeSelect?.value ?? 'CachedData';
    const showAll = mode === 'All';

    const hintText = response.noSuggestionsFound && response.noSuggestionsMessage
      ? response.noSuggestionsMessage
      : `The cached store tracks Archidekt categories that appear on decks containing ${response.cardName} when they have been observed by a completed harvest.`;

    setFieldText('lookup-hint-text', hintText);
    toggleSuggestionPanel('lookup-hint', true);

    const showCached = mode === 'CachedData' || showAll;
    if (showCached && response.cardDeckTotals.totalDeckCount > 0) {
      setFieldText('cache-info-count', response.cardDeckTotals.totalDeckCount.toString());
      setFieldText('cache-info-text', `The cached store currently contains ${response.cardDeckTotals.totalDeckCount} deck(s) featuring ${response.cardName}.`);
      toggleSuggestionPanel('cache-info', true);
    }

    if (response.suggestionSourceSummary) {
      setFieldText('source-summary-text', response.suggestionSourceSummary);
      toggleSuggestionPanel('source-summary', true);
    }

    const showMerged = Boolean(response.mergedCategoriesText && response.mergedCategoriesText.trim().length > 0);
    setFieldText('merged-text', response.mergedCategoriesText);
    toggleSuggestionPanel('merged', showMerged);
    renderWeightedCategories(response.weightedCategories);

    toggleSuggestionPanel('no-suggestions', response.noSuggestionsFound);
    if (response.noSuggestionsFound) {
      setFieldText('no-suggestions-text', response.noSuggestionsMessage ?? `No category suggestions were found for ${response.cardName}.`);
      scrollPanelIntoCenter('[data-api-panel="no-suggestions"]');
      return;
    }

    if (showMerged) {
      scrollPanelIntoCenter('[data-api-panel="merged"]');
    }
  };

  const handleCommanderResponse = (response: CommanderSuggestionResponse): void => {
    resetCommanderUi();

    const hasResults = response.summaries.length > 0;
    const hintText = hasResults
      ? `Commander categories for ${response.commanderName} were sourced from the cached store.`
      : `No commander categories for ${response.commanderName} have been observed in the cached data yet.`;

    setFieldText('commander-hint-text', hintText);
    toggleSuggestionPanel('commander-hint', true);

    toggleSuggestionPanel('commander-results', hasResults);
    toggleSuggestionPanel('commander-no-results', !hasResults);
    if (!hasResults) {
      setFieldText('commander-no-results-text', response.noResultsMessage ?? hintText);
      scrollPanelIntoCenter('[data-api-panel="commander-no-results"]');
      return;
    }

    setFieldText('commander-cards-count', `${response.cardRowCount} cards contributed to this summary.`);
    setFieldText('commander-deck-count', `Derived from ${response.harvestedDeckCount} cached decks with ${response.categoryCount} filtered categories.`);
    setFieldText('commander-card-deck-count', `${response.commanderName} appears in ${response.cardDeckTotals.totalDeckCount} cached commander deck(s).`);
    setFieldText('commander-card-count', response.cardDeckTotals.totalDeckCount.toString());

    const body = document.querySelector<HTMLElement>('[data-api-field="commander-summary-body"]');
    const overflow = document.querySelector<HTMLDetailsElement>('[data-api-field="commander-summary-overflow"]');
    const overflowLabel = document.querySelector<HTMLElement>('[data-api-field="commander-summary-overflow-label"]');
    const overflowBody = document.querySelector<HTMLElement>('[data-api-field="commander-summary-overflow-body"]');
    if (!body || !overflow || !overflowLabel || !overflowBody) {
      return;
    }

    body.innerHTML = '';
    overflowBody.innerHTML = '';
    overflow.open = false;

    const visibleSummaries = response.summaries.slice(0, 25);
    const overflowSummaries = response.summaries.slice(25);

    visibleSummaries.forEach(summary => {
      body.appendChild(createCommanderSummaryRow(summary));
    });

    overflowSummaries.forEach(summary => {
      overflowBody.appendChild(createCommanderSummaryRow(summary));
    });

    overflowLabel.textContent = `Show ${overflowSummaries.length} more categories`;
    overflow.classList.toggle('hidden', overflowSummaries.length === 0);

    scrollPanelIntoCenter('#commander-results-anchor');
  };

  const readRequestData = (form: HTMLFormElement): Record<string, string> => {
    const data: Record<string, string> = {};
    form.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>('[name]').forEach(element => {
      if (!element.name) {
        return;
      }

      if (element instanceof HTMLInputElement && (element.type === 'checkbox' || element.type === 'radio') && !element.checked) {
        return;
      }

      data[element.name] = element.value;
    });

    return data;
  };

  const restoreFormState = (form: SuggestionForm): void => {
    const key = form.dataset.cacheKey;
    if (!key) {
      return;
    }

    const payload = sessionStorage.getItem(`${formStateStoragePrefix}${key}`);
    if (!payload) {
      return;
    }

    try {
      const values = JSON.parse(payload) as Record<string, string>;
      Object.entries(values).forEach(([name, value]) => {
        const element = form.querySelector<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>(`[name="${name}"]`);
        if (!element) {
          return;
        }

        element.value = value;
      });
    } catch (error) {
      console.warn('Unable to restore cached form state', error);
      sessionStorage.removeItem(`${formStateStoragePrefix}${key}`);
    }
  };

  const persistFormState = (form: SuggestionForm): void => {
    const key = form.dataset.cacheKey;
    if (!key) {
      return;
    }

    sessionStorage.setItem(`${formStateStoragePrefix}${key}`, JSON.stringify(readRequestData(form)));
  };

  const persistResultState = (form: SuggestionForm, envelope: SuggestionResponseEnvelope): void => {
    const key = form.dataset.cacheKey;
    if (!key) {
      return;
    }

    sessionStorage.setItem(`${formResultStoragePrefix}${key}`, JSON.stringify(envelope));
  };

  const restoreResultState = (form: SuggestionForm): void => {
    const key = form.dataset.cacheKey;
    if (!key) {
      return;
    }

    const payload = sessionStorage.getItem(`${formResultStoragePrefix}${key}`);
    if (!payload) {
      return;
    }

    try {
      const envelope = JSON.parse(payload) as SuggestionResponseEnvelope;
      if (envelope.type === 'card') {
        handleCardResponse(form, envelope.payload);
        return;
      }

      handleCommanderResponse(envelope.payload);
    } catch (error) {
      console.warn('Unable to restore cached suggestion result', error);
      sessionStorage.removeItem(`${formResultStoragePrefix}${key}`);
    }
  };

  const clearStoredState = (form: SuggestionForm): void => {
    const key = form.dataset.cacheKey;
    if (!key) {
      return;
    }

    sessionStorage.removeItem(`${formStateStoragePrefix}${key}`);
    sessionStorage.removeItem(`${formResultStoragePrefix}${key}`);
  };

  const submitSuggestion = async (form: SuggestionForm): Promise<void> => {
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
        let payload: { message?: string; Message?: string } | null = null;
        try {
          payload = await response.json() as { message?: string; Message?: string };
        } catch {
          payload = null;
        }

        handleError(type === 'commander' ? 'commander-error' : 'suggest-error', payload?.message ?? payload?.Message ?? 'Unable to fetch suggestions.');
        window.hideBusyIndicator?.();
        return;
      }

      if (type === 'card') {
        const payload = await response.json() as CardSuggestionResponse;
        persistResultState(form, { type: 'card', payload });
        handleCardResponse(form, payload);
        window.hideBusyIndicator?.();
        return;
      }

      if (type === 'commander') {
        const payload = await response.json() as CommanderSuggestionResponse;
        persistResultState(form, { type: 'commander', payload });
        handleCommanderResponse(payload);
        window.hideBusyIndicator?.();
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unable to fetch suggestions.';
      handleError(type === 'commander' ? 'commander-error' : 'suggest-error', message);
      window.hideBusyIndicator?.();
    }
  };

  const attachSuggestionHandlers = (): void => {
    document.querySelectorAll<SuggestionForm>('form[data-suggestions-type]').forEach(form => {
      const restoredFromTabs = sessionStorage.getItem(tabNavigationKey) === '1';
      if (restoredFromTabs) {
        restoreFormState(form);
        restoreResultState(form);
      } else {
        clearStoredState(form);
      }

      sessionStorage.removeItem(tabNavigationKey);
      updateSuggestionInputModeUi();

      form.addEventListener('submit', event => {
        event.preventDefault();
        persistFormState(form);
        submitSuggestion(form);
      });

      form.addEventListener('input', () => persistFormState(form));
      form.addEventListener('change', event => {
        persistFormState(form);

        const target = event.target;
        if (target instanceof HTMLSelectElement && (target.name === 'Mode' || target.name === 'ArchidektInputSource')) {
          updateSuggestionInputModeUi();
        }
      });

      const clearButton = form.querySelector<HTMLElement>('[data-clear-cache]');
      if (clearButton) {
        clearButton.addEventListener('click', () => {
          form.reset();
          clearStoredState(form);

          if (form.dataset.suggestionsType === 'card') {
            resetCardUi();
          } else {
            resetCommanderUi();
          }

          updateSuggestionInputModeUi();
        });
      }

      const retryButton = form.querySelector<HTMLElement>('[data-api-action="retry"]');
      if (retryButton) {
        retryButton.addEventListener('click', () => {
          persistFormState(form);
          form.requestSubmit();
        });
      }

      document.querySelectorAll<HTMLAnchorElement>('.tool-nav__link').forEach(link => {
        link.addEventListener('click', () => {
          persistFormState(form);
          sessionStorage.setItem(tabNavigationKey, '1');
        });
      });
    });
  };

  Object.assign(globalThis, {
    DeckFlowCategorySuggestions: { renderWeightedCategories }
  });

  document.addEventListener('DOMContentLoaded', attachSuggestionHandlers);
})();

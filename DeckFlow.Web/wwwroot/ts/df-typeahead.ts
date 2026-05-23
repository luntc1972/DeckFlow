((): void => {
  'use strict';

  type DeckFlowNamespace = {
    attachTypeahead?: (
      input: HTMLInputElement,
      panel: HTMLDivElement,
      minChars: number,
      onPick: (name: string) => void,
      options?: {
        endpoint?: string;
        debounceMs?: number;
        onError?: (message?: string) => void;
      }
    ) => void;
    createTypeaheadPanel?: (anchor: HTMLElement) => HTMLDivElement;
  };

  type TypeaheadWindow = Window & {
    DeckFlow?: DeckFlowNamespace;
  };

  const typeaheadWindow = window as TypeaheadWindow;

  // Module-scoped counters ensure stable, unique ids across all panels and options
  // even when multiple typeahead inputs are attached on the same page.
  // `aria-activedescendant` on the input references these ids.
  let panelIdCounter = 0;

  const debounceCardLookupSearch = (fn: () => void, delay: number) => {
    let timer: number | undefined;
    return () => {
      if (timer !== undefined) {
        window.clearTimeout(timer);
      }

      timer = window.setTimeout(fn, delay);
    };
  };

  const createTypeaheadPanel = (anchor: HTMLElement): HTMLDivElement => {
    const panel = document.createElement('div');
    panel.className = 'autocomplete-panel hidden';
    panel.setAttribute('role', 'listbox');
    // Ensure the panel carries a stable id so the input's `aria-controls`
    // and `aria-activedescendant` can reference it and its options.
    if (!panel.id) {
      panelIdCounter += 1;
      panel.id = `df-typeahead-panel-${panelIdCounter}`;
    }
    anchor.appendChild(panel);
    return panel;
  };

  const getErrorMessage = async (response: Response): Promise<string> => {
    try {
      const payload = await response.json() as { message?: string; Message?: string };
      return payload.message ?? payload.Message ?? 'Scryfall could not be reached right now. Try again shortly.';
    } catch {
      return 'Scryfall could not be reached right now. Try again shortly.';
    }
  };

  const attachTypeahead = (
    input: HTMLInputElement,
    panel: HTMLDivElement,
    minChars: number,
    onPick: (name: string) => void,
    options?: {
      endpoint?: string;
      debounceMs?: number;
      onError?: (message?: string) => void;
    }
  ): void => {
    const endpoint = options?.endpoint ?? '/suggest-categories/card-search';
    const debounceMs = options?.debounceMs ?? 250;
    const onError = options?.onError;

    // Panel must have an id for `aria-controls` / `aria-activedescendant` wiring.
    // `createTypeaheadPanel` assigns one, but consumer-built panels may not — generate
    // a fallback id here so the ARIA wiring is robust regardless of panel origin.
    if (!panel.id) {
      panelIdCounter += 1;
      panel.id = `df-typeahead-panel-${panelIdCounter}`;
    }

    // ARIA combobox pattern (WAI-ARIA 1.2 "combobox with list autocomplete").
    // Attributes that don't change after attach are applied once here; the dynamic
    // attributes (aria-expanded, aria-activedescendant) flip during open/close/navigate.
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-expanded', 'false');
    input.setAttribute('aria-controls', panel.id);

    // Keyboard navigation state. `highlightedIndex` is -1 when no option is active.
    // The list mirrors the live order of option elements rendered into the panel.
    let optionElements: HTMLButtonElement[] = [];
    let highlightedIndex = -1;

    const isPanelOpen = (): boolean => !panel.classList.contains('hidden');

    const setActiveDescendant = (index: number): void => {
      if (index < 0 || index >= optionElements.length) {
        input.removeAttribute('aria-activedescendant');
        return;
      }
      const target = optionElements[index];
      if (target !== undefined) {
        input.setAttribute('aria-activedescendant', target.id);
      }
    };

    const clearHighlight = (): void => {
      optionElements.forEach(opt => {
        opt.setAttribute('aria-selected', 'false');
        opt.classList.remove('is-highlighted');
      });
    };

    const setHighlight = (index: number): void => {
      clearHighlight();
      if (index < 0 || index >= optionElements.length) {
        highlightedIndex = -1;
        setActiveDescendant(-1);
        return;
      }
      highlightedIndex = index;
      const target = optionElements[index];
      if (target !== undefined) {
        target.setAttribute('aria-selected', 'true');
        target.classList.add('is-highlighted');
        // Keep the active option visible if the panel is scrollable.
        if (typeof target.scrollIntoView === 'function') {
          target.scrollIntoView({ block: 'nearest' });
        }
      }
      setActiveDescendant(index);
    };

    const hideLookupSuggestionPanel = (): void => {
      panel.classList.add('hidden');
      panel.replaceChildren();
      optionElements = [];
      highlightedIndex = -1;
      input.setAttribute('aria-expanded', 'false');
      input.removeAttribute('aria-activedescendant');
    };

    const commitSelection = (name: string): void => {
      input.value = name;
      hideLookupSuggestionPanel();
      onPick(name);
    };

    const fetchSuggestions = async (): Promise<void> => {
      const query = input.value.trim();
      if (query.length < minChars) {
        hideLookupSuggestionPanel();
        onError?.(undefined);
        return;
      }

      try {
        const response = await fetch(`${endpoint}?query=${encodeURIComponent(query)}`);
        if (!response.ok) {
          hideLookupSuggestionPanel();
          onError?.(await getErrorMessage(response));
          return;
        }

        const names: string[] = await response.json();
        onError?.(undefined);
        panel.replaceChildren();
        optionElements = [];
        highlightedIndex = -1;
        if (names.length === 0) {
          hideLookupSuggestionPanel();
          return;
        }

        names.forEach((name, index) => {
          const option = document.createElement('button');
          option.type = 'button';
          option.className = 'autocomplete-option';
          option.textContent = name;
          option.id = `${panel.id}-option-${index}`;
          option.setAttribute('role', 'option');
          option.setAttribute('aria-selected', 'false');
          // tabindex=-1 keeps focus on the input; navigation happens via
          // aria-activedescendant per the combobox pattern, not by moving DOM focus.
          option.tabIndex = -1;
          option.addEventListener('mousedown', event => {
            event.preventDefault();
            commitSelection(name);
          });
          option.addEventListener('mouseenter', () => {
            setHighlight(index);
          });
          panel.appendChild(option);
          optionElements.push(option);
        });
        panel.classList.remove('hidden');
        input.setAttribute('aria-expanded', 'true');
        // Open without any option highlighted; ArrowDown moves to the first.
        input.removeAttribute('aria-activedescendant');
      } catch {
        hideLookupSuggestionPanel();
        onError?.('Scryfall could not be reached right now. Try again shortly.');
      }
    };

    const debounced = debounceCardLookupSearch(fetchSuggestions, debounceMs);
    input.addEventListener('input', debounced);
    input.addEventListener('focus', debounced);

    input.addEventListener('keydown', event => {
      const key = event.key;

      if (key === "ArrowDown") {
        if (!isPanelOpen() || optionElements.length === 0) {
          // Trigger a fetch on ArrowDown when nothing is open yet — matches
          // common combobox UX where ArrowDown opens the suggestion list.
          event.preventDefault();
          debounced();
          return;
        }
        event.preventDefault();
        // No-wrap: stop at the last item.
        const next = Math.min(highlightedIndex + 1, optionElements.length - 1);
        setHighlight(next);
        return;
      }

      if (key === "ArrowUp") {
        if (!isPanelOpen() || optionElements.length === 0) {
          return;
        }
        event.preventDefault();
        // No-wrap: stop at the first item. -1 means "no selection".
        const prev = highlightedIndex <= 0 ? 0 : highlightedIndex - 1;
        setHighlight(prev);
        return;
      }

      if (key === "Enter") {
        if (isPanelOpen() && highlightedIndex >= 0 && highlightedIndex < optionElements.length) {
          event.preventDefault();
          const target = optionElements[highlightedIndex];
          if (target !== undefined) {
            commitSelection(target.textContent ?? '');
          }
        }
        // If nothing is highlighted, fall through to native form submission.
        return;
      }

      if (key === "Escape") {
        if (isPanelOpen()) {
          event.preventDefault();
          hideLookupSuggestionPanel();
        }
        // If the panel was already closed, let Escape do whatever the page wants.
        return;
      }
    });

    document.addEventListener('click', event => {
      if (!(event.target instanceof Node) || panel.contains(event.target) || input.contains(event.target)) {
        return;
      }

      hideLookupSuggestionPanel();
    });
  };

  typeaheadWindow.DeckFlow = typeaheadWindow.DeckFlow ?? {};
  typeaheadWindow.DeckFlow.attachTypeahead = attachTypeahead;
  typeaheadWindow.DeckFlow.createTypeaheadPanel = createTypeaheadPanel;
})();

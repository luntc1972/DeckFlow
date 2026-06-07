((): void => {
  'use strict';

  interface DeckFlowNamespace {
    initKbSelection?: () => void;
  }

  type DeckFlowWindow = Window & {
    DeckFlow?: DeckFlowNamespace;
  };

  type PinnedSelection = {
    id: string;
    title: string;
  };

  type FollowedSelection = {
    source: string;
  };

  type SuggestionOption =
    | { kind: 'entry'; id: string; title: string }
    | { kind: 'creator'; source: string };

  type SelectionState = {
    pinned: PinnedSelection[];
    followed: FollowedSelection[];
  };

  type BrowseElements = {
    tray: HTMLElement;
    pinCount: HTMLElement;
    pinList: HTMLElement;
    followList: HTMLElement;
    pinButtons: HTMLButtonElement[];
    followButtons: HTMLButtonElement[];
  };

  type AnalysisElements = {
    chipContainer: HTMLElement;
    typeaheadAnchor: HTMLElement | null;
    typeaheadInput: HTMLInputElement | null;
    typeaheadPanel: HTMLDivElement | null;
    emptyHint: HTMLElement | null;
    form: HTMLFormElement | null;
    shouldClearPinsOnLoad: boolean;
  };

  const win = window as DeckFlowWindow;
  const PINNED_KEY = 'deckflow.kb.pinned';
  const FOLLOWED_KEY = 'deckflow.kb.followed';
  const MAX_PINS = 3;
  const MIN_QUERY_LENGTH = 2;
  const DEBOUNCE_MS = 250;

  let panelIdCounter = 0;

  const dedupePinned = (items: PinnedSelection[]): PinnedSelection[] => {
    const seen = new Set<string>();
    const deduped: PinnedSelection[] = [];
    items.forEach(item => {
      const id = item.id.trim();
      if (id.length === 0 || seen.has(id)) {
        return;
      }

      seen.add(id);
      deduped.push({
        id,
        title: item.title.trim().length > 0 ? item.title.trim() : id,
      });
    });
    return deduped.slice(0, MAX_PINS);
  };

  const dedupeFollowed = (items: FollowedSelection[]): FollowedSelection[] => {
    const seen = new Set<string>();
    const deduped: FollowedSelection[] = [];
    items.forEach(item => {
      const source = item.source.trim();
      const key = source.toLowerCase();
      if (source.length === 0 || seen.has(key)) {
        return;
      }

      seen.add(key);
      deduped.push({ source });
    });
    return deduped;
  };

  const loadPinned = (): PinnedSelection[] => {
    try {
      const raw = window.localStorage.getItem(PINNED_KEY);
      if (!raw) {
        return [];
      }

      const parsed = JSON.parse(raw) as unknown;
      if (!Array.isArray(parsed)) {
        return [];
      }

      const items: PinnedSelection[] = [];
      parsed.forEach(item => {
        if (typeof item !== 'object' || item === null) {
          return;
        }

        const candidate = item as { id?: unknown; title?: unknown };
        if (typeof candidate.id !== 'string' || typeof candidate.title !== 'string') {
          return;
        }

        items.push({ id: candidate.id, title: candidate.title });
      });

      return dedupePinned(items);
    } catch {
      return [];
    }
  };

  const savePinned = (items: PinnedSelection[]): void => {
    try {
      if (items.length === 0) {
        window.localStorage.removeItem(PINNED_KEY);
        return;
      }

      window.localStorage.setItem(PINNED_KEY, JSON.stringify(dedupePinned(items)));
    } catch {
      return;
    }
  };

  const clearPinnedStorage = (): void => {
    try {
      window.localStorage.removeItem(PINNED_KEY);
    } catch {
      return;
    }
  };

  const loadFollowed = (): FollowedSelection[] => {
    try {
      const raw = window.localStorage.getItem(FOLLOWED_KEY);
      if (!raw) {
        return [];
      }

      const parsed = JSON.parse(raw) as unknown;
      if (!Array.isArray(parsed)) {
        return [];
      }

      const items: FollowedSelection[] = [];
      parsed.forEach(item => {
        if (typeof item !== 'object' || item === null) {
          return;
        }

        const candidate = item as { source?: unknown };
        if (typeof candidate.source !== 'string') {
          return;
        }

        items.push({ source: candidate.source });
      });

      return dedupeFollowed(items);
    } catch {
      return [];
    }
  };

  const saveFollowed = (items: FollowedSelection[]): void => {
    try {
      if (items.length === 0) {
        window.localStorage.removeItem(FOLLOWED_KEY);
        return;
      }

      window.localStorage.setItem(FOLLOWED_KEY, JSON.stringify(dedupeFollowed(items)));
    } catch {
      return;
    }
  };

  const createSelectionState = (): SelectionState => ({
    pinned: loadPinned(),
    followed: loadFollowed(),
  });

  const hasPinned = (state: SelectionState, id: string): boolean =>
    state.pinned.some(item => item.id === id);

  const hasFollowed = (state: SelectionState, source: string): boolean =>
    state.followed.some(item => item.source.localeCompare(source, undefined, { sensitivity: 'accent' }) === 0);

  const syncStorage = (state: SelectionState): void => {
    savePinned(state.pinned);
    saveFollowed(state.followed);
  };

  const updatePinButtonState = (button: HTMLButtonElement, state: SelectionState): void => {
    const id = button.dataset.videoId ?? '';
    const pinned = hasPinned(state, id);
    const pinCapReached = state.pinned.length >= MAX_PINS;
    const shouldDisable = !pinned && pinCapReached;

    button.setAttribute('aria-pressed', pinned ? 'true' : 'false');
    button.disabled = shouldDisable;
    button.setAttribute('aria-disabled', shouldDisable ? 'true' : 'false');
    button.textContent = pinned ? '📌 Pinned' : '📌 Pin';
    if (shouldDisable) {
      button.title = 'Pin cap reached (3 max). Remove a pin to add another.';
    } else {
      button.removeAttribute('title');
    }
  };

  const updateFollowButtonState = (button: HTMLButtonElement, state: SelectionState): void => {
    const creator = button.dataset.creator ?? '';
    const followed = hasFollowed(state, creator);
    button.setAttribute('aria-pressed', followed ? 'true' : 'false');
    button.textContent = followed ? '★ Following' : '★ Follow';
  };

  const createTrayItem = (
    label: string,
    removeLabel: string,
    onRemove: () => void,
  ): HTMLLIElement => {
    const item = document.createElement('li');
    item.className = 'kb-selection-tray__item';

    const itemLabel = document.createElement('span');
    itemLabel.className = 'kb-selection-tray__item-label';
    itemLabel.title = label;
    itemLabel.textContent = label;

    const removeButton = document.createElement('button');
    removeButton.type = 'button';
    removeButton.className = 'kb-selection-tray__remove';
    removeButton.setAttribute('aria-label', removeLabel);
    removeButton.textContent = '×';
    removeButton.addEventListener('click', onRemove);

    item.append(itemLabel, removeButton);
    return item;
  };

  const renderBrowseTray = (elements: BrowseElements, state: SelectionState): void => {
    elements.pinCount.textContent = state.pinned.length.toString();
    elements.pinList.replaceChildren();
    elements.followList.replaceChildren();

    state.pinned.forEach(item => {
      elements.pinList.appendChild(createTrayItem(
        item.title,
        `Remove ${item.title} from selection`,
        () => {
          state.pinned = state.pinned.filter(candidate => candidate.id !== item.id);
          syncStorage(state);
          renderBrowse(elements, state);
        }));
    });

    state.followed.forEach(item => {
      elements.followList.appendChild(createTrayItem(
        item.source,
        `Remove ${item.source} from selection`,
        () => {
          state.followed = state.followed.filter(candidate =>
            candidate.source.localeCompare(item.source, undefined, { sensitivity: 'accent' }) !== 0);
          syncStorage(state);
          renderBrowse(elements, state);
        }));
    });

    elements.tray.hidden = state.pinned.length === 0 && state.followed.length === 0;
  };

  const renderBrowse = (elements: BrowseElements, state: SelectionState): void => {
    elements.pinButtons.forEach(button => updatePinButtonState(button, state));
    elements.followButtons.forEach(button => updateFollowButtonState(button, state));
    renderBrowseTray(elements, state);
  };

  const attachBrowse = (state: SelectionState): void => {
    const pinButtons = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-kb-pin]'));
    const followButtons = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-kb-follow]'));
    const tray = document.querySelector<HTMLElement>('.kb-selection-tray');
    const pinCount = document.querySelector<HTMLElement>('[data-tray-pin-count]');
    const pinList = document.querySelector<HTMLElement>('[data-tray-pins]');
    const followList = document.querySelector<HTMLElement>('[data-tray-follows]');

    if (pinButtons.length === 0 || followButtons.length === 0 || !tray || !pinCount || !pinList || !followList) {
      return;
    }

    const elements: BrowseElements = {
      tray,
      pinCount,
      pinList,
      followList,
      pinButtons,
      followButtons,
    };

    pinButtons.forEach(button => {
      button.addEventListener('click', () => {
        const id = button.dataset.videoId ?? '';
        const title = button.dataset.videoTitle ?? id;
        if (id.length === 0) {
          return;
        }

        if (hasPinned(state, id)) {
          state.pinned = state.pinned.filter(item => item.id !== id);
        } else if (state.pinned.length < MAX_PINS) {
          state.pinned = dedupePinned([...state.pinned, { id, title }]);
        }

        syncStorage(state);
        renderBrowse(elements, state);
      });
    });

    followButtons.forEach(button => {
      button.addEventListener('click', () => {
        const source = button.dataset.creator ?? '';
        if (source.length === 0) {
          return;
        }

        if (hasFollowed(state, source)) {
          state.followed = state.followed.filter(item =>
            item.source.localeCompare(source, undefined, { sensitivity: 'accent' }) !== 0);
        } else {
          state.followed = dedupeFollowed([...state.followed, { source }]);
        }

        syncStorage(state);
        renderBrowse(elements, state);
      });
    });

    renderBrowse(elements, state);
  };

  const readServerPinnedChips = (chipContainer: HTMLElement): PinnedSelection[] => {
    const items: PinnedSelection[] = [];
    chipContainer.querySelectorAll<HTMLElement>('[data-chip-type="video"]').forEach(chip => {
      const id = chip.dataset.chipId ?? '';
      const label = chip.querySelector<HTMLElement>('.kb-chip__label')?.textContent?.trim() ?? id;
      if (id.length > 0) {
        items.push({ id, title: label });
      }
    });
    return dedupePinned(items);
  };

  const readServerFollowedChips = (chipContainer: HTMLElement): FollowedSelection[] => {
    const items: FollowedSelection[] = [];
    chipContainer.querySelectorAll<HTMLElement>('[data-chip-type="creator"]').forEach(chip => {
      const source = chip.dataset.chipCreator ?? '';
      if (source.length > 0) {
        items.push({ source });
      }
    });
    return dedupeFollowed(items);
  };

  const removeHiddenInputs = (form: HTMLFormElement, name: string): void => {
    form.querySelectorAll<HTMLInputElement>(`input[type="hidden"][name="${name}"]`).forEach(input => {
      input.remove();
    });
  };

  const injectHiddenInputs = (form: HTMLFormElement, state: SelectionState): void => {
    removeHiddenInputs(form, 'PinnedVideoIds');
    removeHiddenInputs(form, 'FollowedCreators');

    state.pinned.forEach(item => {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = 'PinnedVideoIds';
      input.value = item.id;
      form.appendChild(input);
    });

    state.followed.forEach(item => {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = 'FollowedCreators';
      input.value = item.source;
      form.appendChild(input);
    });
  };

  const createChip = (
    kind: 'video' | 'creator',
    label: string,
    onRemove: () => void,
    idOrSource: string,
  ): HTMLSpanElement => {
    const chip = document.createElement('span');
    chip.className = kind === 'video' ? 'kb-chip kb-chip--pinned' : 'kb-chip kb-chip--followed';
    chip.dataset.chipType = kind;
    if (kind === 'video') {
      chip.dataset.chipId = idOrSource;
    } else {
      chip.dataset.chipCreator = idOrSource;
    }

    const prefix = document.createTextNode(kind === 'video' ? '📌 ' : '★ ');
    const chipLabel = document.createElement('span');
    chipLabel.className = 'kb-chip__label';
    chipLabel.textContent = label;

    const removeButton = document.createElement('button');
    removeButton.type = 'button';
    removeButton.className = 'kb-chip__remove';
    removeButton.setAttribute('aria-label', kind === 'video' ? 'Remove pinned video' : `Remove followed creator ${label}`);
    removeButton.textContent = '×';
    removeButton.addEventListener('click', onRemove);

    chip.append(prefix, chipLabel, removeButton);
    return chip;
  };

  const renderAnalysis = (elements: AnalysisElements, state: SelectionState): void => {
    const { chipContainer, emptyHint, form } = elements;
    chipContainer.replaceChildren();

    state.pinned.forEach(item => {
      chipContainer.appendChild(createChip('video', item.title, () => {
        state.pinned = state.pinned.filter(candidate => candidate.id !== item.id);
        syncStorage(state);
        if (form) {
          injectHiddenInputs(form, state);
        }
        renderAnalysis(elements, state);
      }, item.id));
    });

    state.followed.forEach(item => {
      chipContainer.appendChild(createChip('creator', item.source, () => {
        state.followed = state.followed.filter(candidate =>
          candidate.source.localeCompare(item.source, undefined, { sensitivity: 'accent' }) !== 0);
        syncStorage(state);
        if (form) {
          injectHiddenInputs(form, state);
        }
        renderAnalysis(elements, state);
      }, item.source));
    });

    if (emptyHint) {
      emptyHint.hidden = state.pinned.length > 0 || state.followed.length > 0;
    }

    if (form) {
      injectHiddenInputs(form, state);
    }
  };

  const ensurePanelId = (panel: HTMLDivElement): void => {
    if (panel.id.length > 0) {
      return;
    }

    panelIdCounter += 1;
    panel.id = `kb-selection-panel-${panelIdCounter}`;
  };

  const hidePanel = (input: HTMLInputElement, panel: HTMLDivElement): void => {
    panel.hidden = true;
    panel.replaceChildren();
    input.setAttribute('aria-expanded', 'false');
    input.removeAttribute('aria-activedescendant');
  };

  const readSuggestions = async (query: string): Promise<SuggestionOption[]> => {
    const [entryResponse, creatorResponse] = await Promise.all([
      fetch(`/api/content-kb/entries?query=${encodeURIComponent(query)}`),
      fetch(`/api/content-kb/creators?query=${encodeURIComponent(query)}`),
    ]);

    if (!entryResponse.ok || !creatorResponse.ok) {
      return [];
    }

    const entryPayload = await entryResponse.json() as unknown;
    const creatorPayload = await creatorResponse.json() as unknown;
    const suggestions: SuggestionOption[] = [];

    if (Array.isArray(entryPayload)) {
      entryPayload.forEach(item => {
        if (typeof item !== 'object' || item === null) {
          return;
        }

        const candidate = item as { id?: unknown; title?: unknown };
        if (typeof candidate.id === 'string' && typeof candidate.title === 'string') {
          suggestions.push({ kind: 'entry', id: candidate.id, title: candidate.title });
        }
      });
    }

    if (Array.isArray(creatorPayload)) {
      creatorPayload.forEach(item => {
        if (typeof item === 'string' && item.trim().length > 0) {
          suggestions.push({ kind: 'creator', source: item });
        }
      });
    }

    return suggestions;
  };

  const attachAnalysis = (state: SelectionState): void => {
    const chipContainer = document.querySelector<HTMLElement>('[data-kb-chips]');
    if (!chipContainer) {
      return;
    }

    const form = chipContainer.closest('form');
    const elements: AnalysisElements = {
      chipContainer,
      typeaheadAnchor: document.querySelector<HTMLElement>('[data-kb-chip-typeahead]'),
      typeaheadInput: document.querySelector<HTMLInputElement>('[data-kb-typeahead-input]'),
      typeaheadPanel: document.querySelector<HTMLDivElement>('[data-kb-typeahead-panel]'),
      emptyHint: document.querySelector<HTMLElement>('.kb-chip-area__empty-hint'),
      form: form instanceof HTMLFormElement ? form : null,
      shouldClearPinsOnLoad: document.querySelector<HTMLElement>('[data-kb-clear-pins-on-load]') !== null,
    };

    const serverPinned = readServerPinnedChips(chipContainer);
    const serverFollowed = readServerFollowedChips(chipContainer);
    state.pinned = dedupePinned([...serverPinned, ...state.pinned]);
    state.followed = dedupeFollowed([...serverFollowed, ...state.followed]);
    syncStorage(state);

    if (elements.shouldClearPinsOnLoad) {
      clearPinnedStorage();
    }

    renderAnalysis(elements, state);

    const input = elements.typeaheadInput;
    const panel = elements.typeaheadPanel;
    if (!elements.typeaheadAnchor || !input || !panel) {
      return;
    }

    ensurePanelId(panel);
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-expanded', 'false');
    input.setAttribute('aria-controls', panel.id);

    let highlightedIndex = -1;
    let optionButtons: HTMLButtonElement[] = [];
    let suggestionOptions: SuggestionOption[] = [];
    let timer: number | null = null;

    const clearHighlight = (): void => {
      optionButtons.forEach(button => {
        button.classList.remove('is-highlighted');
        button.setAttribute('aria-selected', 'false');
      });
    };

    const setHighlight = (index: number): void => {
      clearHighlight();
      if (index < 0 || index >= optionButtons.length) {
        highlightedIndex = -1;
        input.removeAttribute('aria-activedescendant');
        return;
      }

      highlightedIndex = index;
      const button = optionButtons[index];
      button.classList.add('is-highlighted');
      button.setAttribute('aria-selected', 'true');
      input.setAttribute('aria-activedescendant', button.id);
      button.scrollIntoView({ block: 'nearest' });
    };

    const commitSelection = (option: SuggestionOption): void => {
      if (option.kind === 'entry') {
        if (!hasPinned(state, option.id) && state.pinned.length < MAX_PINS) {
          state.pinned = dedupePinned([...state.pinned, { id: option.id, title: option.title }]);
        }
      } else if (!hasFollowed(state, option.source)) {
        state.followed = dedupeFollowed([...state.followed, { source: option.source }]);
      }

      syncStorage(state);
      renderAnalysis(elements, state);
      input.value = '';
      hidePanel(input, panel);
    };

    const renderSuggestions = (options: SuggestionOption[]): void => {
      panel.replaceChildren();
      optionButtons = [];
      suggestionOptions = options;
      highlightedIndex = -1;

      if (options.length === 0) {
        hidePanel(input, panel);
        return;
      }

      options.forEach((option, index) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'autocomplete-option';
        button.id = `${panel.id}-option-${index}`;
        button.setAttribute('role', 'option');
        button.setAttribute('aria-selected', 'false');
        button.tabIndex = -1;
        button.textContent = option.kind === 'entry' ? `📌 ${option.title}` : `★ ${option.source}`;
        button.addEventListener('mousedown', event => {
          event.preventDefault();
          commitSelection(option);
        });
        button.addEventListener('mouseenter', () => setHighlight(index));
        panel.appendChild(button);
        optionButtons.push(button);
      });

      panel.hidden = false;
      input.setAttribute('aria-expanded', 'true');
      input.removeAttribute('aria-activedescendant');
    };

    const fetchAndRenderSuggestions = async (): Promise<void> => {
      const query = input.value.trim();
      if (query.length < MIN_QUERY_LENGTH) {
        hidePanel(input, panel);
        return;
      }

      const options = await readSuggestions(query);
      renderSuggestions(options);
    };

    input.addEventListener('input', () => {
      if (timer !== null) {
        window.clearTimeout(timer);
      }

      timer = window.setTimeout(() => {
        void fetchAndRenderSuggestions();
      }, DEBOUNCE_MS);
    });

    input.addEventListener('focus', () => {
      if (input.value.trim().length >= MIN_QUERY_LENGTH) {
        void fetchAndRenderSuggestions();
      }
    });

    input.addEventListener('keydown', event => {
      if (event.key === 'ArrowDown') {
        if (panel.hidden || optionButtons.length === 0) {
          event.preventDefault();
          void fetchAndRenderSuggestions();
          return;
        }

        event.preventDefault();
        setHighlight(Math.min(highlightedIndex + 1, optionButtons.length - 1));
        return;
      }

      if (event.key === 'ArrowUp') {
        if (panel.hidden || optionButtons.length === 0) {
          return;
        }

        event.preventDefault();
        setHighlight(highlightedIndex <= 0 ? 0 : highlightedIndex - 1);
        return;
      }

      if (event.key === 'Enter') {
        if (!panel.hidden && highlightedIndex >= 0) {
          event.preventDefault();
          const option = suggestionOptions[highlightedIndex];
          if (option) {
            commitSelection(option);
          }
        }
        return;
      }

      if (event.key === 'Escape') {
        if (!panel.hidden) {
          event.preventDefault();
          hidePanel(input, panel);
        }
      }
    });

    document.addEventListener('click', event => {
      const target = event.target;
      if (!(target instanceof Node) || !elements.typeaheadAnchor?.contains(target)) {
        hidePanel(input, panel);
      }
    });
  };

  const initKbSelection = (): void => {
    const state = createSelectionState();
    attachBrowse(state);
    attachAnalysis(state);
  };

  win.DeckFlow = win.DeckFlow ?? {};
  win.DeckFlow.initKbSelection = initKbSelection;
  document.addEventListener('DOMContentLoaded', initKbSelection);
})();

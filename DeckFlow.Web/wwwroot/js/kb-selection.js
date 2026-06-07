"use strict";
(() => {
    'use strict';
    var _a;
    const win = window;
    const PINNED_KEY = 'deckflow.kb.pinned';
    const FOLLOWED_KEY = 'deckflow.kb.followed';
    const MAX_PINS = 3;
    const MIN_QUERY_LENGTH = 2;
    const DEBOUNCE_MS = 250;
    let panelIdCounter = 0;
    const dedupePinned = (items) => {
        const seen = new Set();
        const deduped = [];
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
    const dedupeFollowed = (items) => {
        const seen = new Set();
        const deduped = [];
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
    const loadPinned = () => {
        try {
            const raw = window.localStorage.getItem(PINNED_KEY);
            if (!raw) {
                return [];
            }
            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) {
                return [];
            }
            const items = [];
            parsed.forEach(item => {
                if (typeof item !== 'object' || item === null) {
                    return;
                }
                const candidate = item;
                if (typeof candidate.id !== 'string' || typeof candidate.title !== 'string') {
                    return;
                }
                items.push({ id: candidate.id, title: candidate.title });
            });
            return dedupePinned(items);
        }
        catch (_a) {
            return [];
        }
    };
    const savePinned = (items) => {
        try {
            if (items.length === 0) {
                window.localStorage.removeItem(PINNED_KEY);
                return;
            }
            window.localStorage.setItem(PINNED_KEY, JSON.stringify(dedupePinned(items)));
        }
        catch (_a) {
            return;
        }
    };
    const clearPinnedStorage = () => {
        try {
            window.localStorage.removeItem(PINNED_KEY);
        }
        catch (_a) {
            return;
        }
    };
    const loadFollowed = () => {
        try {
            const raw = window.localStorage.getItem(FOLLOWED_KEY);
            if (!raw) {
                return [];
            }
            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) {
                return [];
            }
            const items = [];
            parsed.forEach(item => {
                if (typeof item !== 'object' || item === null) {
                    return;
                }
                const candidate = item;
                if (typeof candidate.source !== 'string') {
                    return;
                }
                items.push({ source: candidate.source });
            });
            return dedupeFollowed(items);
        }
        catch (_a) {
            return [];
        }
    };
    const saveFollowed = (items) => {
        try {
            if (items.length === 0) {
                window.localStorage.removeItem(FOLLOWED_KEY);
                return;
            }
            window.localStorage.setItem(FOLLOWED_KEY, JSON.stringify(dedupeFollowed(items)));
        }
        catch (_a) {
            return;
        }
    };
    const createSelectionState = () => ({
        pinned: loadPinned(),
        followed: loadFollowed(),
    });
    const hasPinned = (state, id) => state.pinned.some(item => item.id === id);
    const hasFollowed = (state, source) => state.followed.some(item => item.source.localeCompare(source, undefined, { sensitivity: 'accent' }) === 0);
    const syncStorage = (state) => {
        savePinned(state.pinned);
        saveFollowed(state.followed);
    };
    const updatePinButtonState = (button, state) => {
        var _a;
        const id = (_a = button.dataset.videoId) !== null && _a !== void 0 ? _a : '';
        const pinned = hasPinned(state, id);
        const pinCapReached = state.pinned.length >= MAX_PINS;
        const shouldDisable = !pinned && pinCapReached;
        button.setAttribute('aria-pressed', pinned ? 'true' : 'false');
        button.disabled = shouldDisable;
        button.setAttribute('aria-disabled', shouldDisable ? 'true' : 'false');
        button.textContent = pinned ? '📌 Pinned' : '📌 Pin';
        if (shouldDisable) {
            button.title = 'Pin cap reached (3 max). Remove a pin to add another.';
        }
        else {
            button.removeAttribute('title');
        }
    };
    const updateFollowButtonState = (button, state) => {
        var _a;
        const creator = (_a = button.dataset.creator) !== null && _a !== void 0 ? _a : '';
        const followed = hasFollowed(state, creator);
        button.setAttribute('aria-pressed', followed ? 'true' : 'false');
        button.textContent = followed ? '★ Following' : '★ Follow';
    };
    const createTrayItem = (label, removeLabel, onRemove) => {
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
    const renderBrowseTray = (elements, state) => {
        elements.pinCount.textContent = state.pinned.length.toString();
        elements.pinList.replaceChildren();
        elements.followList.replaceChildren();
        state.pinned.forEach(item => {
            elements.pinList.appendChild(createTrayItem(item.title, `Remove ${item.title} from selection`, () => {
                state.pinned = state.pinned.filter(candidate => candidate.id !== item.id);
                syncStorage(state);
                renderBrowse(elements, state);
            }));
        });
        state.followed.forEach(item => {
            elements.followList.appendChild(createTrayItem(item.source, `Remove ${item.source} from selection`, () => {
                state.followed = state.followed.filter(candidate => candidate.source.localeCompare(item.source, undefined, { sensitivity: 'accent' }) !== 0);
                syncStorage(state);
                renderBrowse(elements, state);
            }));
        });
        elements.tray.hidden = state.pinned.length === 0 && state.followed.length === 0;
    };
    const renderBrowse = (elements, state) => {
        elements.pinButtons.forEach(button => updatePinButtonState(button, state));
        elements.followButtons.forEach(button => updateFollowButtonState(button, state));
        renderBrowseTray(elements, state);
    };
    const attachBrowse = (state) => {
        const pinButtons = Array.from(document.querySelectorAll('[data-kb-pin]'));
        const followButtons = Array.from(document.querySelectorAll('[data-kb-follow]'));
        const tray = document.querySelector('.kb-selection-tray');
        const pinCount = document.querySelector('[data-tray-pin-count]');
        const pinList = document.querySelector('[data-tray-pins]');
        const followList = document.querySelector('[data-tray-follows]');
        if (pinButtons.length === 0 || followButtons.length === 0 || !tray || !pinCount || !pinList || !followList) {
            return;
        }
        const elements = {
            tray,
            pinCount,
            pinList,
            followList,
            pinButtons,
            followButtons,
        };
        pinButtons.forEach(button => {
            button.addEventListener('click', () => {
                var _a, _b;
                const id = (_a = button.dataset.videoId) !== null && _a !== void 0 ? _a : '';
                const title = (_b = button.dataset.videoTitle) !== null && _b !== void 0 ? _b : id;
                if (id.length === 0) {
                    return;
                }
                if (hasPinned(state, id)) {
                    state.pinned = state.pinned.filter(item => item.id !== id);
                }
                else if (state.pinned.length < MAX_PINS) {
                    state.pinned = dedupePinned([...state.pinned, { id, title }]);
                }
                syncStorage(state);
                renderBrowse(elements, state);
            });
        });
        followButtons.forEach(button => {
            button.addEventListener('click', () => {
                var _a;
                const source = (_a = button.dataset.creator) !== null && _a !== void 0 ? _a : '';
                if (source.length === 0) {
                    return;
                }
                if (hasFollowed(state, source)) {
                    state.followed = state.followed.filter(item => item.source.localeCompare(source, undefined, { sensitivity: 'accent' }) !== 0);
                }
                else {
                    state.followed = dedupeFollowed([...state.followed, { source }]);
                }
                syncStorage(state);
                renderBrowse(elements, state);
            });
        });
        renderBrowse(elements, state);
    };
    const readServerPinnedChips = (chipContainer) => {
        const items = [];
        chipContainer.querySelectorAll('[data-chip-type="video"]').forEach(chip => {
            var _a, _b, _c, _d;
            const id = (_a = chip.dataset.chipId) !== null && _a !== void 0 ? _a : '';
            const label = (_d = (_c = (_b = chip.querySelector('.kb-chip__label')) === null || _b === void 0 ? void 0 : _b.textContent) === null || _c === void 0 ? void 0 : _c.trim()) !== null && _d !== void 0 ? _d : id;
            if (id.length > 0) {
                items.push({ id, title: label });
            }
        });
        return dedupePinned(items);
    };
    const readServerFollowedChips = (chipContainer) => {
        const items = [];
        chipContainer.querySelectorAll('[data-chip-type="creator"]').forEach(chip => {
            var _a;
            const source = (_a = chip.dataset.chipCreator) !== null && _a !== void 0 ? _a : '';
            if (source.length > 0) {
                items.push({ source });
            }
        });
        return dedupeFollowed(items);
    };
    const removeHiddenInputs = (form, name) => {
        form.querySelectorAll(`input[type="hidden"][name="${name}"]`).forEach(input => {
            input.remove();
        });
    };
    const injectHiddenInputs = (form, state) => {
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
    const createChip = (kind, label, onRemove, idOrSource) => {
        const chip = document.createElement('span');
        chip.className = kind === 'video' ? 'kb-chip kb-chip--pinned' : 'kb-chip kb-chip--followed';
        chip.dataset.chipType = kind;
        if (kind === 'video') {
            chip.dataset.chipId = idOrSource;
        }
        else {
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
    const renderAnalysis = (elements, state) => {
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
                state.followed = state.followed.filter(candidate => candidate.source.localeCompare(item.source, undefined, { sensitivity: 'accent' }) !== 0);
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
    const ensurePanelId = (panel) => {
        if (panel.id.length > 0) {
            return;
        }
        panelIdCounter += 1;
        panel.id = `kb-selection-panel-${panelIdCounter}`;
    };
    const hidePanel = (input, panel) => {
        panel.hidden = true;
        panel.replaceChildren();
        input.setAttribute('aria-expanded', 'false');
        input.removeAttribute('aria-activedescendant');
    };
    const readSuggestions = async (query) => {
        const [entryResponse, creatorResponse] = await Promise.all([
            fetch(`/api/content-kb/entries?query=${encodeURIComponent(query)}`),
            fetch(`/api/content-kb/creators?query=${encodeURIComponent(query)}`),
        ]);
        if (!entryResponse.ok || !creatorResponse.ok) {
            return [];
        }
        const entryPayload = await entryResponse.json();
        const creatorPayload = await creatorResponse.json();
        const suggestions = [];
        if (Array.isArray(entryPayload)) {
            entryPayload.forEach(item => {
                if (typeof item !== 'object' || item === null) {
                    return;
                }
                const candidate = item;
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
    const attachAnalysis = (state) => {
        const chipContainer = document.querySelector('[data-kb-chips]');
        if (!chipContainer) {
            return;
        }
        const form = chipContainer.closest('form');
        const elements = {
            chipContainer,
            typeaheadAnchor: document.querySelector('[data-kb-chip-typeahead]'),
            typeaheadInput: document.querySelector('[data-kb-typeahead-input]'),
            typeaheadPanel: document.querySelector('[data-kb-typeahead-panel]'),
            emptyHint: document.querySelector('.kb-chip-area__empty-hint'),
            form: form instanceof HTMLFormElement ? form : null,
            shouldClearPinsOnLoad: document.querySelector('[data-kb-clear-pins-on-load]') !== null,
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
        let optionButtons = [];
        let suggestionOptions = [];
        let timer = null;
        const clearHighlight = () => {
            optionButtons.forEach(button => {
                button.classList.remove('is-highlighted');
                button.setAttribute('aria-selected', 'false');
            });
        };
        const setHighlight = (index) => {
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
        const commitSelection = (option) => {
            if (option.kind === 'entry') {
                if (!hasPinned(state, option.id) && state.pinned.length < MAX_PINS) {
                    state.pinned = dedupePinned([...state.pinned, { id: option.id, title: option.title }]);
                }
            }
            else if (!hasFollowed(state, option.source)) {
                state.followed = dedupeFollowed([...state.followed, { source: option.source }]);
            }
            syncStorage(state);
            renderAnalysis(elements, state);
            input.value = '';
            hidePanel(input, panel);
        };
        const renderSuggestions = (options) => {
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
        const fetchAndRenderSuggestions = async () => {
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
            var _a;
            const target = event.target;
            if (!(target instanceof Node) || !((_a = elements.typeaheadAnchor) === null || _a === void 0 ? void 0 : _a.contains(target))) {
                hidePanel(input, panel);
            }
        });
    };
    const initKbSelection = () => {
        const state = createSelectionState();
        attachBrowse(state);
        attachAnalysis(state);
    };
    win.DeckFlow = (_a = win.DeckFlow) !== null && _a !== void 0 ? _a : {};
    win.DeckFlow.initKbSelection = initKbSelection;
    document.addEventListener('DOMContentLoaded', initKbSelection);
})();

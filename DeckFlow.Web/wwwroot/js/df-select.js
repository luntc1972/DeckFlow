"use strict";
(() => {
    'use strict';
    var _a;
    const controllers = new Set();
    const controllerBySelect = new WeakMap();
    const enhancedSelects = new WeakSet();
    let dfSelectListenersAttached = false;
    let dfSelectInstanceSequence = 0;
    let currentOpenController = null;
    const typeaheadTimeoutMs = 700;
    const normalizeText = (value) => value.replace(/\s+/g, ' ').trim();
    const normalizeSearchQuery = (value) => normalizeText(value).toLowerCase();
    const getLabelText = (select) => {
        var _a;
        const labeledText = Array.from((_a = select.labels) !== null && _a !== void 0 ? _a : [])
            .map(label => { var _a; return normalizeText((_a = label.textContent) !== null && _a !== void 0 ? _a : ''); })
            .filter(label => label.length > 0)
            .join(' ');
        if (labeledText.length > 0) {
            return labeledText;
        }
        const ariaLabel = select.getAttribute('aria-label');
        if (ariaLabel) {
            return normalizeText(ariaLabel);
        }
        return normalizeText(select.name || select.id || 'Select');
    };
    const getOptionText = (option) => normalizeText(option.label || option.textContent || option.value);
    const getSelectedEntry = (controller) => {
        var _a;
        if (controller.select.selectedIndex < 0) {
            return null;
        }
        return (_a = controller.entries.find(entry => entry.optionIndex === controller.select.selectedIndex)) !== null && _a !== void 0 ? _a : null;
    };
    const getSelectableEntry = (controller, selectableIndex) => {
        var _a;
        if (selectableIndex < 0 || selectableIndex >= controller.selectableEntries.length) {
            return null;
        }
        return (_a = controller.selectableEntries[selectableIndex]) !== null && _a !== void 0 ? _a : null;
    };
    const getSelectableIndexForEntry = (controller, entry) => {
        if (!entry || entry.selectableIndex === null) {
            return controller.selectableEntries.length > 0 ? 0 : -1;
        }
        return entry.selectableIndex;
    };
    const getActiveSelectableEntries = (controller) => {
        if (controller.mode === 'search') {
            return controller.selectableEntries.filter(entry => !entry.item.hidden);
        }
        return controller.selectableEntries;
    };
    const getHighlightedEntry = (controller) => {
        var _a;
        const activeEntries = getActiveSelectableEntries(controller);
        if (controller.highlightedSelectableIndex < 0 || controller.highlightedSelectableIndex >= activeEntries.length) {
            return null;
        }
        return (_a = activeEntries[controller.highlightedSelectableIndex]) !== null && _a !== void 0 ? _a : null;
    };
    const getFirstVisibleSelectableIndex = (controller) => {
        const activeEntries = getActiveSelectableEntries(controller);
        return activeEntries.length > 0 ? 0 : -1;
    };
    const getLastVisibleSelectableIndex = (controller) => {
        const activeEntries = getActiveSelectableEntries(controller);
        return activeEntries.length > 0 ? activeEntries.length - 1 : -1;
    };
    const syncSearchFilter = (controller) => {
        if (controller.mode !== 'search') {
            controller.groups.forEach(group => {
                group.heading.hidden = false;
            });
            controller.entries.forEach(entry => {
                entry.item.hidden = false;
            });
            return;
        }
        const hasQuery = controller.searchQuery.length > 0;
        const groupVisibility = new Map();
        controller.entries.forEach(entry => {
            const isVisible = !hasQuery || entry.searchText.includes(controller.searchQuery);
            entry.item.hidden = !isVisible;
            if (isVisible && entry.groupIndex !== null) {
                groupVisibility.set(entry.groupIndex, true);
            }
        });
        controller.groups.forEach((group, groupIndex) => {
            group.heading.hidden = !groupVisibility.get(groupIndex);
        });
    };
    const ensureVisibleHighlight = (controller, preferredEntry = null) => {
        const activeEntries = getActiveSelectableEntries(controller);
        if (activeEntries.length === 0) {
            controller.highlightedSelectableIndex = -1;
            return;
        }
        if (preferredEntry) {
            const preferredIndex = activeEntries.findIndex(entry => entry === preferredEntry);
            if (preferredIndex >= 0) {
                controller.highlightedSelectableIndex = preferredIndex;
                return;
            }
        }
        const selectedEntry = getSelectedEntry(controller);
        if (selectedEntry) {
            const selectedIndex = activeEntries.findIndex(entry => entry === selectedEntry);
            if (selectedIndex >= 0) {
                controller.highlightedSelectableIndex = selectedIndex;
                return;
            }
        }
        controller.highlightedSelectableIndex = 0;
    };
    const setSearchQuery = (controller, value) => {
        if (controller.mode !== 'search') {
            return;
        }
        const previousHighlightedEntry = getHighlightedEntry(controller);
        controller.searchQuery = normalizeSearchQuery(value);
        if (controller.searchInput && controller.searchInput.value !== value) {
            controller.searchInput.value = value;
        }
        syncSearchFilter(controller);
        ensureVisibleHighlight(controller, previousHighlightedEntry);
        updateTriggerAndListboxState(controller);
    };
    const clearTypeaheadBuffer = (controller) => {
        if (controller.typeaheadTimer !== null) {
            window.clearTimeout(controller.typeaheadTimer);
            controller.typeaheadTimer = null;
        }
        controller.typeaheadBuffer = '';
    };
    const scheduleTypeaheadClear = (controller) => {
        if (controller.typeaheadTimer !== null) {
            window.clearTimeout(controller.typeaheadTimer);
        }
        controller.typeaheadTimer = window.setTimeout(() => {
            controller.typeaheadTimer = null;
            controller.typeaheadBuffer = '';
        }, typeaheadTimeoutMs);
    };
    const updateTriggerAndListboxState = (controller) => {
        const selectedEntry = getSelectedEntry(controller);
        const highlightedEntry = controller.isOpen
            ? getHighlightedEntry(controller)
            : null;
        controller.trigger.textContent = selectedEntry ? getOptionText(selectedEntry.option) : '';
        controller.trigger.disabled = controller.select.disabled;
        controller.trigger.setAttribute('aria-expanded', String(controller.isOpen));
        controller.trigger.setAttribute('aria-label', getLabelText(controller.select));
        if (controller.isOpen && highlightedEntry) {
            controller.trigger.setAttribute('aria-activedescendant', highlightedEntry.item.id);
        }
        else {
            controller.trigger.removeAttribute('aria-activedescendant');
        }
        controller.root.classList.toggle('is-open', controller.isOpen);
        controller.root.classList.toggle('is-disabled', controller.select.disabled);
        if (controller.panel) {
            controller.panel.hidden = !controller.isOpen;
        }
        controller.listbox.hidden = !controller.isOpen;
        controller.listbox.setAttribute('aria-disabled', String(controller.select.disabled));
        controller.entries.forEach(entry => {
            const isSelected = entry.optionIndex === controller.select.selectedIndex;
            const isHighlighted = controller.isOpen && highlightedEntry === entry;
            entry.item.hidden = controller.mode === 'search' ? entry.item.hidden : false;
            entry.item.setAttribute('aria-selected', String(isSelected));
            entry.item.classList.toggle('is-selected', isSelected);
            entry.item.classList.toggle('is-highlighted', isHighlighted);
            entry.item.classList.toggle('is-disabled', entry.option.disabled);
        });
    };
    const closeController = (controller, restoreFocus, resetSearch = true) => {
        if (!controller.isOpen) {
            if (resetSearch && controller.mode === 'search') {
                controller.searchQuery = '';
                if (controller.searchInput) {
                    controller.searchInput.value = '';
                }
                syncSearchFilter(controller);
                ensureVisibleHighlight(controller);
            }
            if (restoreFocus) {
                controller.trigger.focus();
            }
            return;
        }
        controller.isOpen = false;
        clearTypeaheadBuffer(controller);
        controller.highlightedSelectableIndex = -1;
        if (resetSearch && controller.mode === 'search') {
            controller.searchQuery = '';
            if (controller.searchInput) {
                controller.searchInput.value = '';
            }
            syncSearchFilter(controller);
        }
        updateTriggerAndListboxState(controller);
        if (currentOpenController === controller) {
            currentOpenController = null;
        }
        if (restoreFocus) {
            controller.trigger.focus();
        }
    };
    const openController = (controller, selectableIndex) => {
        var _a, _b;
        if (controller.select.disabled || controller.selectableEntries.length === 0) {
            return;
        }
        if (currentOpenController && currentOpenController !== controller) {
            closeController(currentOpenController, false);
        }
        controller.isOpen = true;
        currentOpenController = controller;
        if (controller.mode === 'search') {
            controller.searchQuery = '';
            if (controller.searchInput) {
                controller.searchInput.value = '';
            }
            syncSearchFilter(controller);
            controller.highlightedSelectableIndex = selectableIndex !== null && selectableIndex !== void 0 ? selectableIndex : -1;
        }
        else {
            controller.highlightedSelectableIndex = selectableIndex !== null && selectableIndex !== void 0 ? selectableIndex : getSelectableIndexForEntry(controller, getSelectedEntry(controller));
            if (controller.highlightedSelectableIndex < 0) {
                controller.highlightedSelectableIndex = 0;
            }
        }
        updateTriggerAndListboxState(controller);
        (_a = getHighlightedEntry(controller)) === null || _a === void 0 ? void 0 : _a.item.scrollIntoView({
            block: 'nearest'
        });
        if (controller.mode === 'search') {
            (_b = controller.searchInput) === null || _b === void 0 ? void 0 : _b.focus();
        }
    };
    const moveHighlight = (controller, delta) => {
        var _a;
        if (controller.select.disabled) {
            return;
        }
        const activeEntries = getActiveSelectableEntries(controller);
        if (activeEntries.length === 0) {
            return;
        }
        if (!controller.isOpen) {
            openController(controller);
        }
        const nextIndex = controller.highlightedSelectableIndex < 0
            ? (delta > 0 ? 0 : activeEntries.length - 1)
            : controller.highlightedSelectableIndex + delta;
        const clampedIndex = Math.max(0, Math.min(activeEntries.length - 1, nextIndex));
        controller.highlightedSelectableIndex = clampedIndex;
        updateTriggerAndListboxState(controller);
        (_a = getHighlightedEntry(controller)) === null || _a === void 0 ? void 0 : _a.item.scrollIntoView({
            block: 'nearest'
        });
    };
    const jumpHighlight = (controller, toStart) => {
        var _a;
        if (controller.select.disabled) {
            return;
        }
        const activeEntries = getActiveSelectableEntries(controller);
        if (activeEntries.length === 0) {
            return;
        }
        if (!controller.isOpen) {
            openController(controller);
        }
        controller.highlightedSelectableIndex = toStart ? 0 : activeEntries.length - 1;
        updateTriggerAndListboxState(controller);
        (_a = getHighlightedEntry(controller)) === null || _a === void 0 ? void 0 : _a.item.scrollIntoView({
            block: 'nearest'
        });
    };
    const selectEntry = (controller, entry, closeAfterSelect) => {
        if (controller.select.disabled || entry.option.disabled) {
            return;
        }
        if (controller.select.selectedIndex !== entry.optionIndex) {
            controller.select.selectedIndex = entry.optionIndex;
        }
        if (closeAfterSelect) {
            closeController(controller, true);
        }
        else {
            updateTriggerAndListboxState(controller);
        }
        controller.select.dispatchEvent(new Event('change', { bubbles: true }));
    };
    const findMatchingEntry = (controller, query) => {
        const normalizedQuery = query.toLowerCase();
        if (!normalizedQuery) {
            return null;
        }
        const startIndex = controller.highlightedSelectableIndex >= 0
            ? controller.highlightedSelectableIndex + 1
            : 0;
        for (let offset = 0; offset < controller.selectableEntries.length; offset += 1) {
            const candidate = controller.selectableEntries[(startIndex + offset) % controller.selectableEntries.length];
            if (candidate.searchText.startsWith(normalizedQuery)) {
                return candidate;
            }
        }
        return null;
    };
    const handleTypeahead = (controller, key) => {
        var _a;
        controller.typeaheadBuffer += key.toLowerCase();
        scheduleTypeaheadClear(controller);
        const entry = (_a = findMatchingEntry(controller, controller.typeaheadBuffer)) !== null && _a !== void 0 ? _a : (controller.typeaheadBuffer.length > 1
            ? findMatchingEntry(controller, key)
            : null);
        if (!entry) {
            return;
        }
        selectEntry(controller, entry, true);
    };
    const handleTriggerKeydown = (controller, event) => {
        if (event.altKey || event.ctrlKey || event.metaKey) {
            return;
        }
        switch (event.key) {
            case 'ArrowDown':
                event.preventDefault();
                moveHighlight(controller, 1);
                return;
            case 'ArrowUp':
                event.preventDefault();
                moveHighlight(controller, -1);
                return;
            case 'Home':
                event.preventDefault();
                jumpHighlight(controller, true);
                return;
            case 'End':
                event.preventDefault();
                jumpHighlight(controller, false);
                return;
            case 'Enter':
            case ' ':
                event.preventDefault();
                if (controller.isOpen) {
                    const highlightedEntry = getSelectableEntry(controller, controller.highlightedSelectableIndex);
                    if (highlightedEntry) {
                        selectEntry(controller, highlightedEntry, true);
                    }
                }
                else {
                    openController(controller);
                }
                return;
            case 'Escape':
                event.preventDefault();
                closeController(controller, true);
                return;
            case 'Tab':
                closeController(controller, false);
                return;
            default:
                if (event.key.length === 1 && !event.repeat) {
                    event.preventDefault();
                    handleTypeahead(controller, event.key);
                }
        }
    };
    const moveSearchHighlight = (controller, delta) => {
        var _a;
        const activeEntries = getActiveSelectableEntries(controller);
        if (activeEntries.length === 0) {
            controller.highlightedSelectableIndex = -1;
            updateTriggerAndListboxState(controller);
            return;
        }
        if (controller.highlightedSelectableIndex < 0) {
            controller.highlightedSelectableIndex = delta > 0 ? 0 : activeEntries.length - 1;
        }
        else {
            const nextIndex = controller.highlightedSelectableIndex + delta;
            controller.highlightedSelectableIndex = Math.max(0, Math.min(activeEntries.length - 1, nextIndex));
        }
        updateTriggerAndListboxState(controller);
        (_a = getHighlightedEntry(controller)) === null || _a === void 0 ? void 0 : _a.item.scrollIntoView({
            block: 'nearest'
        });
    };
    const handleSearchInputKeydown = (controller, event) => {
        var _a, _b;
        if (event.altKey || event.ctrlKey || event.metaKey) {
            return;
        }
        switch (event.key) {
            case 'ArrowDown':
                event.preventDefault();
                moveSearchHighlight(controller, 1);
                return;
            case 'ArrowUp':
                event.preventDefault();
                moveSearchHighlight(controller, -1);
                return;
            case 'Home':
                event.preventDefault();
                controller.highlightedSelectableIndex = 0;
                updateTriggerAndListboxState(controller);
                (_a = getHighlightedEntry(controller)) === null || _a === void 0 ? void 0 : _a.item.scrollIntoView({
                    block: 'nearest'
                });
                return;
            case 'End':
                event.preventDefault();
                controller.highlightedSelectableIndex = getLastVisibleSelectableIndex(controller);
                updateTriggerAndListboxState(controller);
                (_b = getHighlightedEntry(controller)) === null || _b === void 0 ? void 0 : _b.item.scrollIntoView({
                    block: 'nearest'
                });
                return;
            case 'Enter': {
                event.preventDefault();
                const highlightedEntry = getHighlightedEntry(controller);
                if (highlightedEntry) {
                    selectEntry(controller, highlightedEntry, true);
                }
                return;
            }
            case 'Escape':
                event.preventDefault();
                closeController(controller, true, true);
                return;
            case 'Tab':
                closeController(controller, false, true);
                return;
            default:
                return;
        }
    };
    const syncControllerOptions = (controller) => {
        controller.listbox.replaceChildren();
        controller.entries = [];
        controller.selectableEntries = [];
        controller.groups = [];
        let optionIndex = 0;
        Array.from(controller.select.children).forEach(child => {
            if (child instanceof HTMLOptGroupElement) {
                const heading = document.createElement('li');
                heading.className = 'df-select__group';
                heading.setAttribute('role', 'presentation');
                heading.textContent = child.label;
                controller.listbox.appendChild(heading);
                const groupIndex = controller.groups.length;
                const group = {
                    heading,
                    entries: []
                };
                controller.groups.push(group);
                Array.from(child.children).forEach(optionChild => {
                    if (!(optionChild instanceof HTMLOptionElement)) {
                        return;
                    }
                    const entry = {
                        optionIndex,
                        selectableIndex: optionChild.disabled ? null : controller.selectableEntries.length,
                        option: optionChild,
                        item: document.createElement('li'),
                        searchText: getOptionText(optionChild).toLowerCase(),
                        groupIndex
                    };
                    entry.item.className = 'df-select__option';
                    entry.item.id = `${controller.trigger.id.replace(/-trigger$/, '')}-option-${optionIndex}`;
                    entry.item.setAttribute('role', 'option');
                    entry.item.textContent = getOptionText(optionChild);
                    entry.item.setAttribute('aria-selected', String(optionChild.selected));
                    if (optionChild.disabled) {
                        entry.item.setAttribute('aria-disabled', 'true');
                    }
                    controller.listbox.appendChild(entry.item);
                    controller.entries.push(entry);
                    group.entries.push(entry);
                    if (!optionChild.disabled) {
                        controller.selectableEntries.push(entry);
                    }
                    optionIndex += 1;
                });
                return;
            }
            if (!(child instanceof HTMLOptionElement)) {
                return;
            }
            const entry = {
                optionIndex,
                selectableIndex: child.disabled ? null : controller.selectableEntries.length,
                option: child,
                item: document.createElement('li'),
                searchText: getOptionText(child).toLowerCase(),
                groupIndex: null
            };
            entry.item.className = 'df-select__option';
            entry.item.id = `${controller.trigger.id.replace(/-trigger$/, '')}-option-${optionIndex}`;
            entry.item.setAttribute('role', 'option');
            entry.item.textContent = getOptionText(child);
            entry.item.setAttribute('aria-selected', String(child.selected));
            if (child.disabled) {
                entry.item.setAttribute('aria-disabled', 'true');
            }
            controller.listbox.appendChild(entry.item);
            controller.entries.push(entry);
            if (!child.disabled) {
                controller.selectableEntries.push(entry);
            }
            optionIndex += 1;
        });
        syncSearchFilter(controller);
        ensureVisibleHighlight(controller);
        updateTriggerAndListboxState(controller);
    };
    const renderSelect = (select) => {
        var _a, _b;
        const controllerId = `df-select-${++dfSelectInstanceSequence}`;
        const root = document.createElement('div');
        root.className = 'df-select';
        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'df-select__trigger';
        trigger.id = `${controllerId}-trigger`;
        trigger.setAttribute('role', 'combobox');
        trigger.setAttribute('aria-haspopup', 'listbox');
        trigger.setAttribute('aria-expanded', 'false');
        const listbox = document.createElement('ul');
        listbox.className = 'df-select__listbox';
        listbox.id = `${controllerId}-listbox`;
        listbox.setAttribute('role', 'listbox');
        listbox.hidden = true;
        trigger.setAttribute('aria-controls', listbox.id);
        trigger.setAttribute('aria-label', getLabelText(select));
        const mode = ((_a = select.dataset.dfSelect) !== null && _a !== void 0 ? _a : '').trim().toLowerCase() === 'search' ? 'search' : 'default';
        let panel = null;
        let searchInput = null;
        if (mode === 'search') {
            panel = document.createElement('div');
            panel.className = 'df-select__panel';
            panel.hidden = true;
            searchInput = document.createElement('input');
            searchInput.type = 'search';
            searchInput.className = 'df-select__search';
            searchInput.placeholder = 'Search sets';
            searchInput.autocomplete = 'off';
            searchInput.spellcheck = false;
            searchInput.setAttribute('role', 'searchbox');
            searchInput.setAttribute('aria-controls', listbox.id);
            listbox.classList.add('df-select__listbox--search');
            panel.append(searchInput, listbox);
            root.append(trigger, panel);
        }
        else {
            root.append(trigger, listbox);
        }
        select.insertAdjacentElement('beforebegin', root);
        select.classList.add('df-select__native');
        select.tabIndex = -1;
        const controller = {
            select,
            root,
            trigger,
            panel,
            searchInput,
            listbox,
            entries: [],
            selectableEntries: [],
            groups: [],
            isOpen: false,
            highlightedSelectableIndex: -1,
            typeaheadBuffer: '',
            typeaheadTimer: null,
            mode,
            searchQuery: ''
        };
        syncControllerOptions(controller);
        trigger.addEventListener('click', () => {
            if (controller.isOpen) {
                closeController(controller, true);
            }
            else {
                openController(controller);
            }
        });
        trigger.addEventListener('keydown', event => {
            handleTriggerKeydown(controller, event);
        });
        searchInput === null || searchInput === void 0 ? void 0 : searchInput.addEventListener('input', event => {
            const target = event.target;
            if (!(target instanceof HTMLInputElement)) {
                return;
            }
            setSearchQuery(controller, target.value);
        });
        searchInput === null || searchInput === void 0 ? void 0 : searchInput.addEventListener('keydown', event => {
            handleSearchInputKeydown(controller, event);
        });
        listbox.addEventListener('mousedown', event => {
            var _a;
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }
            const optionItem = target.closest('li[role="option"]');
            if (!(optionItem instanceof HTMLLIElement) || optionItem.getAttribute('aria-disabled') === 'true') {
                return;
            }
            event.preventDefault();
            const entry = (_a = controller.entries.find(candidate => candidate.item === optionItem)) !== null && _a !== void 0 ? _a : null;
            if (!entry) {
                return;
            }
            selectEntry(controller, entry, true);
        });
        Array.from((_b = select.labels) !== null && _b !== void 0 ? _b : []).forEach(label => {
            label.addEventListener('click', event => {
                event.preventDefault();
                trigger.focus();
            });
        });
        select.addEventListener('change', () => {
            if (controller.typeaheadTimer !== null) {
                clearTypeaheadBuffer(controller);
            }
            if (controller.mode === 'search' && controller.isOpen) {
                setSearchQuery(controller, controller.searchQuery);
            }
            updateTriggerAndListboxState(controller);
        });
        controllers.add(controller);
        enhancedSelects.add(select);
        controllerBySelect.set(select, controller);
        return controller;
    };
    const ensureGlobalListeners = () => {
        if (dfSelectListenersAttached) {
            return;
        }
        dfSelectListenersAttached = true;
        document.addEventListener('click', event => {
            const target = event.target;
            if (!(target instanceof Node)) {
                return;
            }
            controllers.forEach(controller => {
                if (controller.isOpen && !controller.root.contains(target)) {
                    closeController(controller, false);
                }
            });
        });
        document.addEventListener('keydown', event => {
            if (event.key !== 'Escape') {
                return;
            }
            controllers.forEach(controller => {
                if (controller.isOpen) {
                    closeController(controller, true);
                }
            });
        });
    };
    const attachDfSelect = () => {
        ensureGlobalListeners();
        document.querySelectorAll('select[data-df-select]').forEach(select => {
            if (enhancedSelects.has(select)) {
                return;
            }
            renderSelect(select);
        });
    };
    const refreshDfSelect = (select) => {
        const controller = controllerBySelect.get(select);
        if (!controller) {
            if (select.matches('select[data-df-select]') && !enhancedSelects.has(select)) {
                renderSelect(select);
            }
            return;
        }
        syncControllerOptions(controller);
    };
    window.DeckFlow = (_a = window.DeckFlow) !== null && _a !== void 0 ? _a : {};
    window.DeckFlow.attachDfSelect = attachDfSelect;
    window.DeckFlow.refreshDfSelect = refreshDfSelect;
    document.addEventListener('DOMContentLoaded', attachDfSelect);
    if (document.readyState !== 'loading') {
        attachDfSelect();
    }
})();

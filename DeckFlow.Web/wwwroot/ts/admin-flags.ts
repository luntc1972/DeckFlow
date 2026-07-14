declare const DeckFlowFlagFilter: {
  keyMatches(key: string, query: string): boolean;
  statusMatches(enabled: boolean, status: string): boolean;
  formatCount(matched: number, total: number): string;
  emptyRowHidden(matched: number, total: number): boolean;
};

((): void => {
  'use strict';

  const searchFilterKey = 'deckflowAdminFlagSearch';
  const prefixFilterKey = 'deckflowAdminFlagPrefix';
  const statusFilterKey = 'deckflowAdminFlagStatus';

  const wireFlagFilter = (): void => {
    const input = document.querySelector<HTMLInputElement>('#flag-filter-search');
    if (input === null) {
      return;
    }

    const count = document.getElementById('flag-filter-count');
    const emptyRow = document.getElementById('flag-filter-empty');
    const prefixChips = Array.from(document.querySelectorAll<HTMLButtonElement>('button[data-flag-prefix]'));
    const statusChips = Array.from(document.querySelectorAll<HTMLButtonElement>('button[data-flag-status]'));
    const rows = Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-flag-key]'));
    const total = rows.length;
    const chipPrefixes = new Set(prefixChips.map((chip) => chip.dataset.flagPrefix ?? ''));
    const chipStatuses = new Set(statusChips.map((chip) => chip.dataset.flagStatus ?? ''));

    let activePrefix = '';
    let activeStatus = '';

    const syncActiveChip = (
      chips: HTMLButtonElement[],
      getValue: (chip: HTMLButtonElement) => string,
      activeValue: string,
    ): void => {
      chips.forEach((chip) => {
        const isActive = getValue(chip) === activeValue;
        chip.classList.toggle('is-active', isActive);
        chip.setAttribute('aria-pressed', isActive ? 'true' : 'false');
      });
    };

    const applyFilter = (): void => {
      const searchText = input.value.trim();
      let matched = 0;

      rows.forEach((row) => {
        const key = row.dataset.flagKey ?? '';
        const enabled = row.dataset.flagEnabled === 'true';
        const matchesPrefix = DeckFlowFlagFilter.keyMatches(key, activePrefix);
        const matchesSearch = DeckFlowFlagFilter.keyMatches(key, searchText);
        const matchesStatus = DeckFlowFlagFilter.statusMatches(enabled, activeStatus);
        const isMatch = matchesPrefix && matchesSearch && matchesStatus;

        row.classList.toggle('hidden', !isMatch);
        if (isMatch) {
          matched += 1;
        }
      });

      if (count !== null) {
        count.textContent = DeckFlowFlagFilter.formatCount(matched, total);
      }

      if (emptyRow !== null) {
        emptyRow.classList.toggle('hidden', DeckFlowFlagFilter.emptyRowHidden(matched, total));
      }
    };

    const persistFilter = (): void => {
      window.sessionStorage.setItem(searchFilterKey, input.value);
      window.sessionStorage.setItem(prefixFilterKey, activePrefix);
      window.sessionStorage.setItem(statusFilterKey, activeStatus);
    };

    const setActivePrefix = (prefix: string): void => {
      activePrefix = chipPrefixes.has(prefix) ? prefix : '';
      syncActiveChip(prefixChips, (chip) => chip.dataset.flagPrefix ?? '', activePrefix);
      persistFilter();
      applyFilter();
    };

    const setActiveStatus = (status: string): void => {
      activeStatus = chipStatuses.has(status) ? status : '';
      syncActiveChip(statusChips, (chip) => chip.dataset.flagStatus ?? '', activeStatus);
      persistFilter();
      applyFilter();
    };

    const savedSearch = window.sessionStorage.getItem(searchFilterKey);
    if (savedSearch !== null) {
      input.value = savedSearch;
    }

    const savedPrefix = window.sessionStorage.getItem(prefixFilterKey);
    if (savedPrefix !== null && chipPrefixes.has(savedPrefix)) {
      activePrefix = savedPrefix;
    }

    const savedStatus = window.sessionStorage.getItem(statusFilterKey);
    if (savedStatus !== null && chipStatuses.has(savedStatus)) {
      activeStatus = savedStatus;
    }

    syncActiveChip(prefixChips, (chip) => chip.dataset.flagPrefix ?? '', activePrefix);
    syncActiveChip(statusChips, (chip) => chip.dataset.flagStatus ?? '', activeStatus);

    input.addEventListener('input', () => {
      persistFilter();
      applyFilter();
    });

    prefixChips.forEach((chip) => {
      chip.addEventListener('click', () => {
        setActivePrefix(chip.dataset.flagPrefix ?? '');
      });
    });

    statusChips.forEach((chip) => {
      chip.addEventListener('click', () => {
        setActiveStatus(chip.dataset.flagStatus ?? '');
      });
    });

    applyFilter();
  };

  document.addEventListener('DOMContentLoaded', () => {
    wireFlagFilter();
  });
})();

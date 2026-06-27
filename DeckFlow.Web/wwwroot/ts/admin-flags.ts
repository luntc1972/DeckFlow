declare const DeckFlowFlagFilter: {
  keyMatches(key: string, query: string): boolean;
  formatCount(matched: number, total: number): string;
  emptyRowHidden(matched: number, total: number): boolean;
};

((): void => {
  'use strict';

  const searchFilterKey = 'deckflowAdminFlagSearch';
  const prefixFilterKey = 'deckflowAdminFlagPrefix';

  const wireFlagFilter = (): void => {
    const input = document.querySelector<HTMLInputElement>('#flag-filter-search');
    if (input === null) {
      return;
    }

    const count = document.getElementById('flag-filter-count');
    const emptyRow = document.getElementById('flag-filter-empty');
    const chips = Array.from(document.querySelectorAll<HTMLButtonElement>('button[data-flag-prefix]'));
    const rows = Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-flag-key]'));
    const total = rows.length;
    const chipPrefixes = new Set(chips.map((chip) => chip.dataset.flagPrefix ?? ''));

    let activePrefix = '';

    const syncActiveChip = (): void => {
      chips.forEach((chip) => {
        const isActive = (chip.dataset.flagPrefix ?? '') === activePrefix;
        chip.classList.toggle('is-active', isActive);
        chip.setAttribute('aria-pressed', isActive ? 'true' : 'false');
      });
    };

    const applyFilter = (): void => {
      const searchText = input.value.trim();
      let matched = 0;

      rows.forEach((row) => {
        const key = row.dataset.flagKey ?? '';
        const matchesPrefix = DeckFlowFlagFilter.keyMatches(key, activePrefix);
        const matchesSearch = DeckFlowFlagFilter.keyMatches(key, searchText);
        const isMatch = matchesPrefix && matchesSearch;

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
    };

    const setActivePrefix = (prefix: string): void => {
      activePrefix = chipPrefixes.has(prefix) ? prefix : '';
      syncActiveChip();
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

    syncActiveChip();

    input.addEventListener('input', () => {
      persistFilter();
      applyFilter();
    });

    chips.forEach((chip) => {
      chip.addEventListener('click', () => {
        setActivePrefix(chip.dataset.flagPrefix ?? '');
      });
    });

    applyFilter();
  };

  document.addEventListener('DOMContentLoaded', () => {
    wireFlagFilter();
  });
})();

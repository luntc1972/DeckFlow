// Wires the /Admin/Tools filter. The matching rule is deliberately borrowed from
// flag-filter.ts (D-01); the browser loads flag-filter.js before this script.

document.addEventListener('DOMContentLoaded', () => {
  const input = document.querySelector<HTMLInputElement>('#tools-filter-search');
  if (input === null) {
    return;
  }

  // Why: module:none shares a global scope, so declaring this global again collides with admin-flags.ts.
  const filter = (globalThis as typeof globalThis & { DeckFlowFlagFilter?: FlagFilterApi }).DeckFlowFlagFilter;
  if (filter === undefined) {
    return;
  }

  const count = document.querySelector<HTMLElement>('#tools-filter-count');
  const empty = document.querySelector<HTMLElement>('#tools-filter-empty');
  const rows = Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-tool-label]'));
  const total = rows.length;
  const sections = Array.from(document.querySelectorAll<HTMLElement>('.admin-tools__section'));
  const chips = Array.from(document.querySelectorAll<HTMLButtonElement>('button[data-tools-status]'));
  const searchKey = 'deckflowAdminToolsSearch';
  const statusKey = 'deckflowAdminToolsStatus';
  const statuses = new Set(chips.map((chip) => chip.dataset.toolsStatus ?? ''));
  let activeStatus = '';

  // Why: the shared formatter hard-codes the word "flags".
  const formatCount = (matched: number, total: number): string => `${matched} of ${total} tools shown`;

  const syncActiveChip = (): void => {
    chips.forEach((chip) => {
      const isActive = (chip.dataset.toolsStatus ?? '') === activeStatus;
      chip.classList.toggle('is-active', isActive);
      chip.setAttribute('aria-pressed', isActive ? 'true' : 'false');
    });
  };

  const applyFilter = (): void => {
    const query = input.value.trim();
    let matched = 0;
    rows.forEach((row) => {
      const isMatch = (filter.keyMatches(row.dataset.toolLabel ?? '', query) || filter.keyMatches(row.dataset.toolFlagKey ?? '', query)) && filter.statusMatches(row.dataset.toolEnabled === 'true', activeStatus);
      row.classList.toggle('hidden', !isMatch);
      if (isMatch) {
        matched += 1;
      }
    });
    sections.forEach((section) => {
      const sectionRows = section.querySelectorAll<HTMLTableRowElement>('tr[data-tool-label]');
      // Why: a heading over an empty table falsely implies this section has no tools.
      if (sectionRows.length > 0) {
        section.classList.toggle('hidden', Array.from(sectionRows).every((row) => row.classList.contains('hidden')));
      } else {
        section.classList.toggle('hidden', query !== '' || activeStatus !== '');
      }
    });
    if (count !== null) {
      count.textContent = formatCount(matched, total);
    }
    if (empty !== null) {
      empty.classList.toggle('hidden', filter.emptyRowHidden(matched, total));
    }
  };

  const persist = (): void => {
    window.sessionStorage.setItem(searchKey, input.value);
    window.sessionStorage.setItem(statusKey, activeStatus);
  };

  input.value = window.sessionStorage.getItem(searchKey) ?? '';
  const savedStatus = window.sessionStorage.getItem(statusKey) ?? '';
  activeStatus = statuses.has(savedStatus) ? savedStatus : '';
  syncActiveChip();
  applyFilter();

  input.addEventListener('input', () => { persist(); applyFilter(); });
  chips.forEach((chip) => chip.addEventListener('click', () => {
    activeStatus = chip.dataset.toolsStatus ?? '';
    syncActiveChip();
    persist();
    applyFilter();
  }));
});

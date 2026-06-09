((): void => {
  'use strict';

  interface DeckFlowNamespace {
    initPrimerSelection?: () => void;
  }

  type DeckFlowWindow = Window & {
    DeckFlow?: DeckFlowNamespace;
  };

  type PrimerElements = {
    form: HTMLFormElement;
    bracketSelect: HTMLSelectElement;
    sectionCheckboxes: HTMLInputElement[];
    sectionRows: HTMLElement[];
    groups: HTMLElement[];
  };

  const win = window as DeckFlowWindow;
  const PRIMER_SECTIONS_KEY_PREFIX = 'deckflow.primer.sections.';
  const CEDH_BRACKET = 'cEDH';

  const parseJsonStringArray = (value: string | undefined): string[] => {
    if (!value) {
      return [];
    }

    try {
      const parsed = JSON.parse(value) as unknown;
      if (!Array.isArray(parsed)) {
        return [];
      }

      const items: string[] = [];
      parsed.forEach(item => {
        if (typeof item === 'string' && item.trim().length > 0) {
          items.push(item.trim());
        }
      });
      return items;
    } catch {
      return [];
    }
  };

  const dedupeIds = (ids: string[]): string[] => {
    const seen = new Set<string>();
    const ordered: string[] = [];

    ids.forEach(id => {
      const normalized = id.trim();
      const key = normalized.toLowerCase();
      if (normalized.length === 0 || seen.has(key)) {
        return;
      }

      seen.add(key);
      ordered.push(normalized);
    });

    return ordered;
  };

  const loadSectionsForBracket = (bracket: string): string[] | null => {
    try {
      const raw = window.localStorage.getItem(`${PRIMER_SECTIONS_KEY_PREFIX}${bracket}`);
      if (raw === null) {
        return null;
      }

      const parsed = JSON.parse(raw) as unknown;
      if (!Array.isArray(parsed)) {
        return [];
      }

      const ids: string[] = [];
      parsed.forEach(item => {
        if (typeof item === 'string' && item.trim().length > 0) {
          ids.push(item.trim());
        }
      });

      return dedupeIds(ids);
    } catch {
      return null;
    }
  };

  const saveSectionsForBracket = (bracket: string, ids: string[]): void => {
    try {
      window.localStorage.setItem(`${PRIMER_SECTIONS_KEY_PREFIX}${bracket}`, JSON.stringify(dedupeIds(ids)));
    } catch {
      return;
    }
  };

  const isCedhBracket = (bracket: string): boolean =>
    bracket.localeCompare(CEDH_BRACKET, undefined, { sensitivity: 'accent' }) === 0;

  const presetForBracket = (bracketSelect: HTMLSelectElement, bracket: string): string[] => {
    const option = Array.from(bracketSelect.options).find(candidate =>
      candidate.value.localeCompare(bracket, undefined, { sensitivity: 'accent' }) === 0);
    return parseJsonStringArray(option?.dataset.presetIds);
  };

  const orderedSelectedIds = (elements: PrimerElements, selectedIds: Set<string>): string[] => {
    const ordered: string[] = [];
    elements.sectionCheckboxes.forEach(checkbox => {
      if (!checkbox.disabled && selectedIds.has(checkbox.value)) {
        ordered.push(checkbox.value);
      }
    });
    return ordered;
  };

  const currentSelectedIds = (elements: PrimerElements): string[] =>
    elements.sectionCheckboxes
      .filter(checkbox => checkbox.checked)
      .map(checkbox => checkbox.value);

  const updateGroupBadge = (group: HTMLElement): void => {
    const badge = group.querySelector<HTMLElement>('.primer-group__badge');
    if (!badge) {
      return;
    }

    const visibleCheckboxes = Array.from(group.querySelectorAll<HTMLInputElement>('[data-primer-section-checkbox]'))
      .filter(checkbox => !checkbox.disabled && !checkbox.closest<HTMLElement>('[data-primer-section-row]')?.hidden);
    const selectedCount = visibleCheckboxes.filter(checkbox => checkbox.checked).length;
    badge.textContent = `${selectedCount}/${visibleCheckboxes.length} sections selected`;
  };

  const updateAllGroupBadges = (elements: PrimerElements): void => {
    elements.groups.forEach(group => updateGroupBadge(group));
  };

  const enforceBracketGating = (elements: PrimerElements, bracket: string, selectedIds: Set<string>): Set<string> => {
    const cedhOnlyIds = new Set(parseJsonStringArray(elements.bracketSelect.dataset.cedhOnlyIds).map(id => id.toLowerCase()));
    const casualOnlyIds = new Set(parseJsonStringArray(elements.bracketSelect.dataset.casualOnlyIds).map(id => id.toLowerCase()));
    const cedh = isCedhBracket(bracket);

    elements.sectionCheckboxes.forEach(checkbox => {
      const row = checkbox.closest<HTMLElement>('[data-primer-section-row]');
      const key = checkbox.value.toLowerCase();
      const isCedhOnly = cedhOnlyIds.has(key);
      const isCasualOnly = casualOnlyIds.has(key);
      const allowed = (!isCedhOnly && !isCasualOnly) || (isCedhOnly && cedh) || (isCasualOnly && !cedh);

      if (!allowed) {
        selectedIds.delete(checkbox.value);
        checkbox.checked = false;
      }

      checkbox.disabled = !allowed;
      checkbox.checked = allowed && selectedIds.has(checkbox.value);

      if (row) {
        row.hidden = !allowed;
        row.setAttribute('aria-hidden', allowed ? 'false' : 'true');
      }
    });

    return selectedIds;
  };

  const syncSelection = (elements: PrimerElements, bracket: string, ids: string[]): string[] => {
    const selectedIds = enforceBracketGating(elements, bracket, new Set(dedupeIds(ids)));
    const orderedIds = orderedSelectedIds(elements, selectedIds);
    saveSectionsForBracket(bracket, orderedIds);
    updateAllGroupBadges(elements);
    return orderedIds;
  };

  const restoreSectionsForBracket = (elements: PrimerElements, bracket: string): void => {
    const savedIds = loadSectionsForBracket(bracket);
    const ids = savedIds !== null ? savedIds : presetForBracket(elements.bracketSelect, bracket);
    syncSelection(elements, bracket, ids);
  };

  const removeHiddenSectionInputs = (form: HTMLFormElement): void => {
    form.querySelectorAll<HTMLInputElement>('input[type="hidden"][name="SelectedSectionIds"]').forEach(input => {
      input.remove();
    });
  };

  const injectHiddenSectionInputs = (form: HTMLFormElement, ids: string[]): void => {
    removeHiddenSectionInputs(form);

    ids.forEach(id => {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = 'SelectedSectionIds';
      input.value = id;
      form.appendChild(input);
    });
  };

  const stripCheckboxNames = (elements: PrimerElements): void => {
    elements.sectionCheckboxes.forEach(checkbox => {
      checkbox.removeAttribute('name');
    });
  };

  const initPrimerSelection = (): void => {
    const form = document.querySelector<HTMLFormElement>('[data-primer-form]');
    const bracketSelect = document.querySelector<HTMLSelectElement>('[data-primer-bracket]');
    if (!form || !bracketSelect) {
      return;
    }

    const elements: PrimerElements = {
      form,
      bracketSelect,
      sectionCheckboxes: Array.from(form.querySelectorAll<HTMLInputElement>('[data-primer-section-checkbox]')),
      sectionRows: Array.from(form.querySelectorAll<HTMLElement>('[data-primer-section-row]')),
      groups: Array.from(form.querySelectorAll<HTMLElement>('[data-primer-group]')),
    };

    if (elements.sectionCheckboxes.length === 0) {
      return;
    }

    let currentBracket = bracketSelect.value.trim();
    restoreSectionsForBracket(elements, currentBracket);

    bracketSelect.addEventListener('change', () => {
      syncSelection(elements, currentBracket, currentSelectedIds(elements));
      currentBracket = bracketSelect.value.trim();
      restoreSectionsForBracket(elements, currentBracket);
    });

    elements.sectionCheckboxes.forEach(checkbox => {
      checkbox.addEventListener('change', () => {
        syncSelection(elements, currentBracket, currentSelectedIds(elements));
      });
    });

    form.addEventListener('submit', event => {
      const submitter = (event as SubmitEvent).submitter;
      const skipValidation = submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement
        ? submitter.formNoValidate
        : false;

      if (!skipValidation && !form.checkValidity()) {
        return;
      }

      const selectedIds = syncSelection(elements, currentBracket, currentSelectedIds(elements));
      stripCheckboxNames(elements);
      injectHiddenSectionInputs(form, selectedIds);
    });
  };

  win.DeckFlow = win.DeckFlow ?? {};
  win.DeckFlow.initPrimerSelection = initPrimerSelection;
  document.addEventListener('DOMContentLoaded', initPrimerSelection);
})();

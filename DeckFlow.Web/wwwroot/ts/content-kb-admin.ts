// Admin Content KB curation page behaviors (Phase 22, Surface 3).
// CSP is `script-src 'self'` — no inline scripts — so the reload-confirm modal and the
// two-click "Hide All" confirm live in this compiled module, mirroring admin-feedback.ts.

((): void => {
  'use strict';

  const SCROLL_KEY = 'deckflowAdminKbScrollY';

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

  // Reload-from-seed: intercept submit, confirm via the shared admin modal, then submit.
  const wireReloadConfirm = (): void => {
    const forms = document.querySelectorAll<HTMLFormElement>('[data-admin-confirm-reload]');
    forms.forEach((form) => {
      form.addEventListener('submit', async (event: SubmitEvent) => {
        if (form.dataset.confirmed === 'true') {
          return;
        }
        event.preventDefault();
        const showConfirm = window.DeckFlowAdminModal?.showConfirm;
        if (showConfirm === undefined) {
          form.dataset.confirmed = 'true';
          form.submit();
          return;
        }
        const confirmed = await showConfirm({
          title: 'Reload index from seed?',
          message: 'This will upsert all rows from the committed seed file. Visibility settings for previously-curated entries are preserved.',
          confirmLabel: 'Reload',
          cancelLabel: 'Cancel',
        });
        if (confirmed) {
          form.dataset.confirmed = 'true';
          form.requestSubmit();
        }
      });
    });
  };

  // Bulk "Hide All": first click arms the button (label → data-confirm-label), second click submits.
  const wireTwoClickConfirm = (): void => {
    const forms = document.querySelectorAll<HTMLFormElement>('[data-admin-confirm-twoclick]');
    forms.forEach((form) => {
      const button = form.querySelector<HTMLButtonElement>('button[data-confirm-label]');
      if (button === null) {
        return;
      }
      const original = button.textContent ?? '';
      const armed = button.dataset.confirmLabel ?? 'Confirm';
      let resetTimer = 0;

      form.addEventListener('submit', (event: SubmitEvent) => {
        if (form.dataset.armed === 'true') {
          return;
        }
        event.preventDefault();
        form.dataset.armed = 'true';
        button.textContent = armed;
        button.classList.add('is-armed');
        window.clearTimeout(resetTimer);
        resetTimer = window.setTimeout(() => {
          form.dataset.armed = 'false';
          button.textContent = original;
          button.classList.remove('is-armed');
        }, 4000);
      });
    });
  };

  const wireScrollRestore = (): void => {
    const scrollY = window.sessionStorage.getItem(SCROLL_KEY);
    if (scrollY !== null) {
      const parsedScrollY = Number(scrollY);
      if (Number.isFinite(parsedScrollY)) {
        window.scrollTo(0, parsedScrollY);
      }
      window.sessionStorage.removeItem(SCROLL_KEY);
    }

    const forms = document.querySelectorAll<HTMLFormElement>('form.admin-action-form');
    forms.forEach((form) => {
      form.addEventListener('submit', (event: SubmitEvent) => {
        if (event.defaultPrevented) {
          return;
        }

        window.sessionStorage.setItem(SCROLL_KEY, String(window.scrollY));
      });
    });
  };

  const wireToast = (): void => {
    const toast = document.querySelector<HTMLElement>('[data-admin-toast]');
    if (toast === null) {
      return;
    }

    toast.addEventListener('transitionend', () => {
      toast.remove();
    }, { once: true });

    window.setTimeout(() => {
      toast.classList.add('is-dismissing');
    }, 4000);
  };

  const setCommanderSearchError = (message?: string): void => {
    const panel = document.querySelector<HTMLElement>('[data-api-panel="commander-search-error"]');
    const text = document.querySelector<HTMLElement>('[data-api-field="commander-search-error-text"]');
    if (!panel || !text) {
      return;
    }

    text.textContent = message ?? '';
    panel.classList.toggle('hidden', !message);
  };

  const ensureAutocompleteAnchor = (input: HTMLInputElement): HTMLDivElement => {
    const parent = input.parentElement;
    if (!parent) {
      throw new Error('Admin Content KB preview commander input is missing a parent element.');
    }

    if (parent.classList.contains('autocomplete-anchor')) {
      return parent as HTMLDivElement;
    }

    const anchor = document.createElement('div');
    anchor.className = 'autocomplete-anchor';
    input.insertAdjacentElement('beforebegin', anchor);
    anchor.appendChild(input);
    return anchor;
  };

  const wireCommanderPreviewTypeahead = (): void => {
    const input = document.getElementById('kb-preview-commander-input') as HTMLInputElement | null;
    if (!input) {
      return;
    }

    const anchor = ensureAutocompleteAnchor(input);
    const deckFlowWindow = window as TypeaheadWindow;
    const panel = deckFlowWindow.DeckFlow?.createTypeaheadPanel?.(anchor);
    if (!panel) {
      return;
    }

    deckFlowWindow.DeckFlow?.attachTypeahead?.(input, panel, 2, () => {
      input.dispatchEvent(new Event('change', { bubbles: true }));
    }, {
      endpoint: '/commander-categories/search',
      debounceMs: 350,
      onError: setCommanderSearchError,
    });
  };

  const wireEntryFilter = (): void => {
    const input = document.getElementById('kb-filter-search') as HTMLInputElement | null;
    if (input === null) {
      return;
    }

    const count = document.getElementById('kb-filter-count');
    const emptyRow = document.getElementById('kb-filter-empty') as HTMLTableRowElement | null;
    const rows = Array.from(document.querySelectorAll<HTMLTableRowElement>('#kb-entries-table tbody tr'))
      .filter((row) => row.id !== 'kb-filter-empty');
    const total = rows.length;

    const applyFilter = (): void => {
      const query = input.value.trim().toLowerCase();
      let matched = 0;

      rows.forEach((row) => {
        const searchText = row.dataset.kbSearch ?? '';
        const isMatch = query === '' || searchText.includes(query);
        row.hidden = !isMatch;
        if (isMatch) {
          matched += 1;
        }
      });

      if (count !== null) {
        count.textContent = `${matched} of ${total} entries shown`;
      }

      if (emptyRow !== null) {
        emptyRow.classList.toggle('hidden', matched !== 0 || total === 0);
      }
    };

    input.addEventListener('input', applyFilter);
    applyFilter();
  };

  document.addEventListener('DOMContentLoaded', () => {
    wireReloadConfirm();
    wireTwoClickConfirm();
    wireScrollRestore();
    wireToast();
    wireCommanderPreviewTypeahead();
    wireEntryFilter();
  });
})();

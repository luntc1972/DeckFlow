interface Window {
  DeckFlowAdminModal?: {
    showConfirm?: (opts: ConfirmOptions) => Promise<boolean>;
  };
}

interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

declare const DeckFlowKbFilter: {
  rowMatches(searchText: string, query: string): boolean;
  formatCount(matched: number, total: number): string;
  emptyRowHidden(matched: number, total: number): boolean;
};

((): void => {
  'use strict';

  const scrollKey = 'deckflowAdminKbScrollY';
  const creatorFilterKey = 'deckflowAdminKbCreator';
  const searchFilterKey = 'deckflowAdminKbSearch';

  const wireReloadConfirm = (): void => {
    const forms = document.querySelectorAll<HTMLFormElement>('form[data-admin-confirm-reload]');
    forms.forEach((form) => {
      form.addEventListener('submit', async (event) => {
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

  const wireTwoClickConfirm = (): void => {
    const forms = document.querySelectorAll<HTMLFormElement>('form[data-admin-confirm-twoclick]');
    forms.forEach((form) => {
      const button = form.querySelector<HTMLButtonElement>('button[data-confirm-label]');
      if (button === null) {
        return;
      }

      const originalLabel = button.textContent ?? '';
      const confirmLabel = button.dataset.confirmLabel ?? 'Confirm';
      let resetTimer = 0;

      form.addEventListener('submit', (event) => {
        if (form.dataset.armed === 'true') {
          return;
        }

        event.preventDefault();
        form.dataset.armed = 'true';
        button.textContent = confirmLabel;
        button.classList.add('is-armed');

        window.clearTimeout(resetTimer);
        resetTimer = window.setTimeout(() => {
          form.dataset.armed = 'false';
          button.textContent = originalLabel;
          button.classList.remove('is-armed');
        }, 4000);
      });
    });
  };

  const wireScrollRestore = (): void => {
    const savedScrollY = window.sessionStorage.getItem(scrollKey);
    if (savedScrollY !== null) {
      const parsedScrollY = Number(savedScrollY);
      if (Number.isFinite(parsedScrollY)) {
        window.scrollTo(0, parsedScrollY);
      }

      window.sessionStorage.removeItem(scrollKey);
    }

    const forms = document.querySelectorAll<HTMLFormElement>('form.admin-action-form');
    forms.forEach((form) => {
      form.addEventListener('submit', (event) => {
        if (event.defaultPrevented) {
          return;
        }

        window.sessionStorage.setItem(scrollKey, String(window.scrollY));
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

  const wireEntryFilter = (): void => {
    const input = document.querySelector<HTMLInputElement>('#kb-filter-search');
    if (input === null) {
      return;
    }

    const select = document.querySelector<HTMLSelectElement>('#kb-creator-filter');
    const count = document.getElementById('kb-filter-count');
    const emptyRow = document.getElementById('kb-filter-empty');
    const rows = Array.from(document.querySelectorAll<HTMLTableRowElement>('#kb-entries-table tbody tr'))
      .filter((row) => row.id !== 'kb-filter-empty');
    const total = rows.length;

    const applyFilter = (): void => {
      const query = input.value.trim().toLowerCase();
      const creator = select?.value ?? '';
      let matched = 0;

      rows.forEach((row) => {
        const searchText = row.dataset.kbSearch ?? '';
        const matchesText = DeckFlowKbFilter.rowMatches(searchText, query);
        const matchesCreator = creator === '' || (row.dataset.kbSource ?? '') === creator;
        const isMatch = matchesText && matchesCreator;
        row.hidden = !isMatch;

        if (isMatch) {
          matched += 1;
        }
      });

      if (count !== null) {
        count.textContent = DeckFlowKbFilter.formatCount(matched, total);
      }

      if (emptyRow !== null) {
        emptyRow.classList.toggle('hidden', DeckFlowKbFilter.emptyRowHidden(matched, total));
      }
    };

    if (select !== null) {
      const savedCreator = window.sessionStorage.getItem(creatorFilterKey);
      if (savedCreator !== null) {
        select.value = savedCreator;
        window.sessionStorage.removeItem(creatorFilterKey);
      }
    }

    const savedSearch = window.sessionStorage.getItem(searchFilterKey);
    if (savedSearch !== null) {
      input.value = savedSearch;
      window.sessionStorage.removeItem(searchFilterKey);
    }

    input.addEventListener('input', applyFilter);
    select?.addEventListener('change', applyFilter);
    document.querySelectorAll<HTMLFormElement>('form.admin-action-form').forEach((form) => {
      form.addEventListener('submit', (event) => {
        if (event.defaultPrevented) {
          return;
        }

        if (select !== null) {
          window.sessionStorage.setItem(creatorFilterKey, select.value);
        }

        window.sessionStorage.setItem(searchFilterKey, input.value);
      });
    });
    applyFilter();
  };

  document.addEventListener('DOMContentLoaded', () => {
    wireReloadConfirm();
    wireTwoClickConfirm();
    wireScrollRestore();
    wireToast();
    wireEntryFilter();
  });
})();

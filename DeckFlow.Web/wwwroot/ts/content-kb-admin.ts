// Admin Content KB curation page behaviors (Phase 22, Surface 3).
// CSP is `script-src 'self'` — no inline scripts — so the reload-confirm modal and the
// two-click "Hide All" confirm live in this compiled module, mirroring admin-feedback.ts.

((): void => {
  'use strict';

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

  document.addEventListener('DOMContentLoaded', () => {
    wireReloadConfirm();
    wireTwoClickConfirm();
  });
})();

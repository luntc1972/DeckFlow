((): void => {
  'use strict';

  // WDG-04 / Phase 11 Sweep 4 — replaces the inline onchange="this.form.submit()"
  // attribute that previously lived on the type-filter <select> in
  // Views/AdminFeedback/Index.cshtml. Listening for change on any element
  // tagged with data-admin-feedback-submit-on-change and submitting the
  // enclosing <form> keeps the same UX while letting the app move toward
  // strict CSP (script-src 'self').
  // Source: .planning/quick/260513-wdg-web-design-guidelines-audit-findings/260513-wdg-FINDINGS.md
  // finding D.
  document.addEventListener('DOMContentLoaded', () => {
    const triggers = document.querySelectorAll<HTMLElement>('[data-admin-feedback-submit-on-change]');
    triggers.forEach((trigger) => {
      trigger.addEventListener('change', (event: Event) => {
        const target = event.currentTarget as HTMLElement | null;
        const form = target?.closest('form') ?? null;
        if (form === null) {
          return;
        }

        form.submit();
      });
    });
  });

  document.addEventListener('DOMContentLoaded', () => {
    const deleteForms = document.querySelectorAll<HTMLFormElement>('[data-admin-confirm-delete]');
    deleteForms.forEach((form) => {
      form.addEventListener('submit', async (event: SubmitEvent) => {
        event.preventDefault();
        const showConfirm = window.DeckFlowAdminModal?.showConfirm;
        if (showConfirm === undefined) {
          // admin-modal.js not loaded - fail closed (do NOT submit silently).
          return;
        }

        const id = form.dataset.adminFeedbackId ?? '?';
        const confirmed = await showConfirm({
          title: 'Delete Feedback',
          message: `Delete feedback #${id} permanently?`,
          confirmLabel: 'Delete',
          danger: true,
        });
        if (confirmed) {
          form.submit();
        }
      });
    });
  });
})();

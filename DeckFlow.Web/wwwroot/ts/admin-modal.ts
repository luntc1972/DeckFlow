// Reusable admin confirm-dialog primitive built on native HTML <dialog>.
// Consumers: AdminFeedback Detail v1.4 Phase 1; future Phase 7 ContentSources delete.

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

((): void => {
  'use strict';

  const showConfirm = (opts: ConfirmOptions): Promise<boolean> => new Promise<boolean>((resolve) => {
    const dialog = document.querySelector<HTMLDialogElement>('#admin-confirm-modal');
    if (dialog === null) {
      resolve(false);
      return;
    }

    if (typeof dialog.showModal !== 'function') {
      resolve(false);
      return;
    }

    if (dialog.open === true) {
      resolve(false);
      return;
    }

    const titleEl = dialog.querySelector<HTMLElement>('#admin-modal-title');
    const messageEl = dialog.querySelector<HTMLElement>('#admin-modal-message');
    const confirmBtn = dialog.querySelector<HTMLButtonElement>('[data-admin-modal-confirm]');
    const cancelBtn = dialog.querySelector<HTMLButtonElement>('[data-admin-modal-cancel]');
    if (titleEl === null || messageEl === null || confirmBtn === null || cancelBtn === null) {
      resolve(false);
      return;
    }

    titleEl.textContent = opts.title;
    messageEl.textContent = opts.message;
    confirmBtn.textContent = opts.confirmLabel ?? 'Confirm';
    cancelBtn.textContent = opts.cancelLabel ?? 'Cancel';
    confirmBtn.classList.toggle('admin-modal__button--danger', opts.danger === true);

    const previouslyFocused = document.activeElement as HTMLElement | null;

    const onConfirm = (): void => {
      dialog.returnValue = 'confirm';
      dialog.close();
    };

    const onCancel = (): void => {
      dialog.returnValue = 'cancel';
      dialog.close();
    };

    const onBackdropClick = (event: MouseEvent): void => {
      if (event.target === dialog) {
        onCancel();
      }
    };

    const cleanup = (): void => {
      confirmBtn.removeEventListener('click', onConfirm);
      cancelBtn.removeEventListener('click', onCancel);
      dialog.removeEventListener('click', onBackdropClick);
      dialog.removeEventListener('close', onClose);
    };

    const onClose = (): void => {
      cleanup();
      previouslyFocused?.focus();
      resolve(dialog.returnValue === 'confirm');
    };

    confirmBtn.addEventListener('click', onConfirm);
    cancelBtn.addEventListener('click', onCancel);
    dialog.addEventListener('click', onBackdropClick);
    dialog.addEventListener('close', onClose);

    dialog.returnValue = '';
    try {
      dialog.showModal();
    } catch {
      cleanup();
      resolve(false);
      return;
    }

    if (opts.danger === true) {
      cancelBtn.focus();
    }
  });

  window.DeckFlowAdminModal = window.DeckFlowAdminModal ?? {};
  window.DeckFlowAdminModal.showConfirm = showConfirm;
})();

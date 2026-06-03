((): void => {
  'use strict';

  // File-download POSTs give no page feedback, so swap the submit button into a busy
  // state. The browser cannot observe download completion from JS without a cookie
  // handshake, so re-enable on a timer matching the server's 5-minute export timeout.
  const attach = (): void => {
    const form = document.querySelector<HTMLFormElement>('[data-yt-export-form]');
    const button = form?.querySelector<HTMLButtonElement>('button[type="submit"]');
    if (!form || !button) {
      return;
    }

    const original = button.textContent ?? 'Download list';
    form.addEventListener('submit', () => {
      if (!form.reportValidity()) {
        return;
      }

      button.disabled = true;
      button.textContent = 'Fetching from YouTube… this can take a minute';
      window.setTimeout(() => {
        button.disabled = false;
        button.textContent = original;
      }, 5 * 60 * 1000);
    });
  };

  document.addEventListener('DOMContentLoaded', attach);
})();

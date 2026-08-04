((): void => {
  'use strict';

  const cookieName = 'yt-export-done';
  const cookiePath = '/';

  // File-download POSTs give no page feedback, so swap the submit button into a busy
  // state. The browser cannot observe a file download directly; the form sends a random
  // download token and the server echoes it back as a cookie on the file response, which
  // this module polls for to restore the button. A 5-minute timer (matching the server's
  // export timeout) remains as the fallback for error responses that replace the page.
  const attach = (): void => {
    const form = document.querySelector<HTMLFormElement>('[data-yt-export-form]');
    const button = form?.querySelector<HTMLButtonElement>('button[type="submit"]');
    const tokenInput = form?.querySelector<HTMLInputElement>('input[name="downloadToken"]');
    if (!form || !button || !tokenInput) {
      return;
    }

    const original = button.textContent ?? 'Download list';
    let pollTimer: number | null = null;
    let fallbackTimer: number | null = null;

    const restore = (): void => {
      if (pollTimer !== null) {
        window.clearInterval(pollTimer);
        pollTimer = null;
      }
      if (fallbackTimer !== null) {
        window.clearTimeout(fallbackTimer);
        fallbackTimer = null;
      }
      document.cookie = `${cookieName}=; Max-Age=0; path=${cookiePath}`;
      button.disabled = false;
      button.textContent = original;
    };

    form.addEventListener('submit', () => {
      if (!form.reportValidity()) {
        return;
      }

      const bytes = new Uint8Array(16);
      window.crypto.getRandomValues(bytes);
      const token = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
      tokenInput.value = token;

      button.disabled = true;
      button.textContent = 'Fetching from YouTube… this can take a minute';
      pollTimer = window.setInterval(() => {
        if (document.cookie.includes(`${cookieName}=${token}`)) {
          restore();
        }
      }, 500);
      fallbackTimer = window.setTimeout(restore, 5 * 60 * 1000);
    });
  };

  document.addEventListener('DOMContentLoaded', attach);
})();

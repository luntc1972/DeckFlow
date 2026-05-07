((): void => {
  'use strict';

  const POLL_INTERVAL_MS = 15000;
  const FETCH_TIMEOUT_MS = 10000;

  type AnalyticsStatusPayload = {
    metricsRevision: string;
  };

  const fetchStatus = async (): Promise<AnalyticsStatusPayload | null> => {
    const abortController = new AbortController();
    const timeoutId = window.setTimeout(() => abortController.abort(), FETCH_TIMEOUT_MS);

    try {
      const response = await fetch('/Admin/Analytics/status', {
        credentials: 'same-origin',
        headers: { Accept: 'application/json' },
        signal: abortController.signal
      });

      if (!response.ok) {
        return null;
      }

      return await response.json() as AnalyticsStatusPayload;
    } finally {
      window.clearTimeout(timeoutId);
    }
  };

  document.addEventListener('DOMContentLoaded', () => {
    const root = document.querySelector<HTMLElement>('.admin-analytics');
    if (!root) {
      return;
    }

    let stopped = false;
    let reloaded = false;
    let timerId: number | null = null;
    let lastRevision: string | null = null;

    const stopPolling = (): void => {
      stopped = true;
      if (timerId !== null) {
        window.clearTimeout(timerId);
        timerId = null;
      }
    };

    const schedulePoll = (): void => {
      if (stopped) {
        return;
      }

      timerId = window.setTimeout(() => {
        void poll();
      }, POLL_INTERVAL_MS);
    };

    const poll = async (): Promise<void> => {
      try {
        const payload = await fetchStatus();
        if (payload === null) {
          schedulePoll();
          return;
        }

        if (lastRevision === null) {
          lastRevision = payload.metricsRevision;
        } else if (payload.metricsRevision !== lastRevision) {
          stopPolling();
          if (!reloaded) {
            reloaded = true;
            window.location.reload();
          }

          return;
        }

        schedulePoll();
      } catch {
        stopPolling();
      }
    };

    schedulePoll();
  });
})();

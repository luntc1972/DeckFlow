((): void => {
  'use strict';

  const POLL_INTERVAL_MS = 3000;
  const FETCH_TIMEOUT_MS = 10000;
  const ACTIVE_STATES = new Set<string>(['Queued', 'Running', 'Stopping']);
  const TERMINAL_STATES = new Set<string>(['Succeeded', 'Failed', 'Cancelled']);

  type HarvestStatusPayload = {
    state: string;
    jobId: string | null;
    kind: string | null;
    decksProcessed: number;
    startedUtc: string | null;
    completedUtc: string | null;
    errorMessage: string | null;
  };

  const setText = (root: HTMLElement, selector: string, value: string): boolean => {
    const element = root.querySelector<HTMLElement>(selector);
    if (!element) {
      return false;
    }

    element.textContent = value;
    return true;
  };

  const formatUtc = (value: string | null): string => {
    if (!value) {
      return '—';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return date.toISOString().replace('T', ' ').replace('.000Z', ' UTC');
  };

  const formatElapsed = (startedUtc: string | null, completedUtc: string | null): string => {
    if (!startedUtc) {
      return '—';
    }

    const started = new Date(startedUtc);
    if (Number.isNaN(started.getTime())) {
      return '—';
    }

    const ended = completedUtc ? new Date(completedUtc) : new Date();
    if (Number.isNaN(ended.getTime())) {
      return '—';
    }

    const totalSeconds = Math.max(0, Math.floor((ended.getTime() - started.getTime()) / 1000));
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    if (hours > 0) {
      return `${hours}h ${minutes}m ${seconds}s`;
    }

    if (minutes > 0) {
      return `${minutes}m ${seconds}s`;
    }

    return `${seconds}s`;
  };

  const renderFallback = (root: HTMLElement, payload: HarvestStatusPayload): void => {
    root.textContent = `Status: ${payload.state} | Decks: ${payload.decksProcessed} | Started: ${formatUtc(payload.startedUtc)} | Elapsed: ${formatElapsed(payload.startedUtc, payload.completedUtc)}`;
  };

  const render = (root: HTMLElement, payload: HarvestStatusPayload): void => {
    root.dataset.state = payload.state;

    const stateSet = setText(root, '.admin-harvest__state', payload.state);
    const decksSet = setText(root, '.admin-harvest__decks', payload.decksProcessed.toString());
    const startedSet = setText(root, '.admin-harvest__started', formatUtc(payload.startedUtc));
    const elapsedSet = setText(root, '.admin-harvest__elapsed', formatElapsed(payload.startedUtc, payload.completedUtc));

    if (!stateSet || !decksSet || !startedSet || !elapsedSet) {
      renderFallback(root, payload);
    }
  };

  const fetchStatus = async (): Promise<HarvestStatusPayload | null> => {
    const abortController = new AbortController();
    const timeoutId = window.setTimeout(() => abortController.abort(), FETCH_TIMEOUT_MS);

    try {
      const response = await fetch('/Admin/Harvest/status', {
        credentials: 'same-origin',
        headers: { Accept: 'application/json' },
        signal: abortController.signal
      });

      if (!response.ok) {
        return null;
      }

      return await response.json() as HarvestStatusPayload;
    } finally {
      window.clearTimeout(timeoutId);
    }
  };

  document.addEventListener('DOMContentLoaded', () => {
    const root = document.querySelector<HTMLElement>('#harvest-status-live')
      ?? document.querySelector<HTMLElement>('[data-harvest-status]');
    if (!root) {
      return;
    }

    const initialState = root.dataset.state ?? '';
    if (!ACTIVE_STATES.has(initialState)) {
      return;
    }

    let stopped = false;
    let reloaded = false;
    let timerId: number | null = null;

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
          stopPolling();
          return;
        }

        render(root, payload);

        if (TERMINAL_STATES.has(payload.state)) {
          stopPolling();
          if (!reloaded) {
            reloaded = true;
            window.location.reload();
          }

          return;
        }

        if (!ACTIVE_STATES.has(payload.state)) {
          stopPolling();
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

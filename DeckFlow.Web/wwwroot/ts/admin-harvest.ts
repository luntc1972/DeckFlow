((): void => {
  'use strict';

  const ACTIVE_POLL_INTERVAL_MS = 3000;
  const IDLE_POLL_INTERVAL_MS = 10000;
  const FETCH_TIMEOUT_MS = 10000;
  const COMMANDERS_FETCH_TIMEOUT_MS = 10000;
  const ACTIVE_STATES = new Set<string>(['Queued', 'Running', 'Stopping']);
  const TERMINAL_STATES = new Set<string>(['Succeeded', 'Failed', 'Cancelled']);
  const COMMANDERS_LOADING_HTML = '<p class="admin-harvest__grid-loading">Loading commanders…</p>';
  const COMMANDERS_ERROR_HTML = '<p class="admin-harvest__grid-error">Could not load commanders. <a href="#" id="commanders-retry">Retry</a></p>';

  type HarvestStatusPayload = {
    state: string;
    jobId: string | null;
    kind: string | null;
    decksProcessed: number;
    startedUtc: string | null;
    completedUtc: string | null;
    errorMessage: string | null;
    recentRunsRevision: string;
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

  const fetchCommandersGrid = async (page: number): Promise<string | null> => {
    const abortController = new AbortController();
    const timeoutId = window.setTimeout(() => abortController.abort(), COMMANDERS_FETCH_TIMEOUT_MS);

    try {
      const response = await fetch(`/Admin/Harvest/commanders?page=${page}`, {
        credentials: 'same-origin',
        headers: { Accept: 'text/html' },
        signal: abortController.signal
      });

      if (!response.ok) {
        return null;
      }

      return await response.text();
    } finally {
      window.clearTimeout(timeoutId);
    }
  };

  const bindCommandersRetry = (container: HTMLElement, page: number): void => {
    const retryLink = container.querySelector<HTMLAnchorElement>('#commanders-retry');
    if (!retryLink) {
      return;
    }

    retryLink.addEventListener('click', (event) => {
      event.preventDefault();
      void loadCommandersGrid(container, page, { scrollIntoView: true });
    });
  };

  const loadCommandersGrid = async (
    container: HTMLElement,
    page: number,
    options?: { scrollIntoView?: boolean }
  ): Promise<void> => {
    const shouldScroll = options?.scrollIntoView ?? false;
    container.setAttribute('aria-busy', 'true');
    container.innerHTML = COMMANDERS_LOADING_HTML;

    try {
      const html = await fetchCommandersGrid(page);
      if (html === null) {
        container.innerHTML = COMMANDERS_ERROR_HTML;
        container.setAttribute('aria-busy', 'false');
        bindCommandersRetry(container, page);
        return;
      }

      container.innerHTML = html;
      container.setAttribute('aria-busy', 'false');

      if (shouldScroll) {
        const section = document.getElementById('harvested-commanders');
        if (section) {
          const prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
          section.scrollIntoView({ behavior: prefersReduced ? 'auto' : 'smooth', block: 'start' });
        }
      }
    } catch {
      container.innerHTML = COMMANDERS_ERROR_HTML;
      container.setAttribute('aria-busy', 'false');
      bindCommandersRetry(container, page);
    }
  };

  document.addEventListener('DOMContentLoaded', () => {
    const root = document.querySelector<HTMLElement>('#harvest-status-live')
      ?? document.querySelector<HTMLElement>('[data-harvest-status]');
    if (root) {
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

      const schedulePoll = (intervalMs: number): void => {
        if (stopped) {
          return;
        }

        timerId = window.setTimeout(() => {
          void poll();
        }, intervalMs);
      };

      const poll = async (): Promise<void> => {
        try {
          const payload = await fetchStatus();
          if (payload === null) {
            schedulePoll(IDLE_POLL_INTERVAL_MS);
            return;
          }

          render(root, payload);

          if (lastRevision === null) {
            lastRevision = payload.recentRunsRevision;
          } else if (payload.recentRunsRevision !== lastRevision) {
            stopPolling();
            if (!reloaded) {
              reloaded = true;
              window.location.reload();
            }

            return;
          }

          if (TERMINAL_STATES.has(payload.state)) {
            stopPolling();
            if (!reloaded) {
              reloaded = true;
              window.location.reload();
            }

            return;
          }

          schedulePoll(ACTIVE_STATES.has(payload.state) ? ACTIVE_POLL_INTERVAL_MS : IDLE_POLL_INTERVAL_MS);
        } catch {
          stopPolling();
        }
      };

      schedulePoll(ACTIVE_STATES.has(root.dataset.state ?? '') ? ACTIVE_POLL_INTERVAL_MS : IDLE_POLL_INTERVAL_MS);
    }

    const commandersGridContainer = document.getElementById('commanders-grid-container');
    if (commandersGridContainer) {
      commandersGridContainer.addEventListener('click', (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
          return;
        }

        const pageLink = target.closest<HTMLElement>('[data-page]');
        if (!pageLink) {
          return;
        }

        event.preventDefault();

        const page = Number.parseInt(pageLink.dataset.page ?? '', 10);
        if (Number.isNaN(page) || page < 1) {
          return;
        }

        void loadCommandersGrid(commandersGridContainer, page, { scrollIntoView: true });
      });

      void loadCommandersGrid(commandersGridContainer, 1, { scrollIntoView: false });
    }
  });
})();

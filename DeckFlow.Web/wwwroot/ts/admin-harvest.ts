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

  const commandersGridState = { search: '', sortBy: 'deck_count', sortDir: 'desc' };
  let commandersRequestId = 0;
  let commandersAbortController: AbortController | null = null;

  const fetchCommandersGrid = async (page: number, abortController: AbortController): Promise<string | null> => {
    const timeoutId = window.setTimeout(() => abortController.abort(), COMMANDERS_FETCH_TIMEOUT_MS);

    try {
      const parameters = new URLSearchParams({ page: page.toString() });
      if (commandersGridState.search !== '') {
        parameters.set('search', commandersGridState.search);
      }
      if (commandersGridState.sortBy !== 'deck_count') {
        parameters.set('sortBy', commandersGridState.sortBy);
      }
      if (commandersGridState.sortBy !== 'deck_count' || commandersGridState.sortDir !== 'desc') {
        parameters.set('sortDir', commandersGridState.sortDir);
      }
      const response = await fetch(`/Admin/Harvest/commanders?${parameters.toString()}`, {
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
    options?: { scrollIntoView?: boolean; sortColumn?: string }
  ): Promise<void> => {
    commandersAbortController?.abort();
    const requestId = ++commandersRequestId;
    const abortController = new AbortController();
    commandersAbortController = abortController;
    const shouldScroll = options?.scrollIntoView ?? false;
    const exportSearch = document.querySelector<HTMLInputElement>('[data-export-search]');
    const exportSortBy = document.querySelector<HTMLInputElement>('[data-export-sort-by]');
    const exportSortDir = document.querySelector<HTMLInputElement>('[data-export-sort-dir]');
    if (exportSearch) {
      exportSearch.value = commandersGridState.search;
    }
    if (exportSortBy) {
      exportSortBy.value = commandersGridState.sortBy;
    }
    if (exportSortDir) {
      exportSortDir.value = commandersGridState.sortDir;
    }
    container.setAttribute('aria-busy', 'true');
    container.innerHTML = COMMANDERS_LOADING_HTML;

    try {
      const html = await fetchCommandersGrid(page, abortController);
      if (requestId !== commandersRequestId) {
        return;
      }
      if (html === null) {
        container.innerHTML = COMMANDERS_ERROR_HTML;
        container.setAttribute('aria-busy', 'false');
        bindCommandersRetry(container, page);
        return;
      }

      container.innerHTML = html;
      container.setAttribute('aria-busy', 'false');

      if (options?.sortColumn) {
        container.querySelector<HTMLElement>(`[data-sort-column="${options.sortColumn}"]`)?.focus();
      }

      if (shouldScroll) {
        const section = document.getElementById('harvested-commanders');
        if (section) {
          const prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
          section.scrollIntoView({ behavior: prefersReduced ? 'auto' : 'smooth', block: 'start' });
        }
      }
    } catch {
      if (requestId !== commandersRequestId) {
        return;
      }
      container.innerHTML = COMMANDERS_ERROR_HTML;
      container.setAttribute('aria-busy', 'false');
      bindCommandersRetry(container, page);
    } finally {
      if (requestId === commandersRequestId) {
        commandersAbortController = null;
      }
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
      const loadCommanderBreakdown = async (details: HTMLDetailsElement): Promise<void> => {
        if (details.hasAttribute('data-commander-loaded') || details.hasAttribute('data-commander-loading')) {
          return;
        }

        const panel = details.querySelector<HTMLElement>('[data-commander-panel]');
        const commanderName = details.dataset.commanderDetails;
        if (!panel || !commanderName) {
          return;
        }

        details.removeAttribute('data-commander-failed');

        details.setAttribute('data-commander-loading', 'true');
        panel.setAttribute('aria-busy', 'true');
        const abortController = new AbortController();
        const timeoutId = window.setTimeout(() => abortController.abort(), COMMANDERS_FETCH_TIMEOUT_MS);
        try {
          const parameters = new URLSearchParams({ name: commanderName });
          const response = await fetch(`/Admin/Harvest/commander-categories?${parameters.toString()}`, {
            credentials: 'same-origin',
            headers: { Accept: 'text/html' },
            signal: abortController.signal
          });
          if (!response.ok) {
            throw new Error('Could not load commander categories.');
          }

          panel.innerHTML = await response.text();
          details.setAttribute('data-commander-loaded', 'true');
        } catch {
          details.setAttribute('data-commander-failed', 'true');
          panel.innerHTML = '<p class="admin-harvest__grid-error">Could not load category breakdown. <a href="#" data-commander-retry>Retry</a></p>';
          panel.querySelector<HTMLAnchorElement>('[data-commander-retry]')?.addEventListener('click', (event) => {
            event.preventDefault();
            void loadCommanderBreakdown(details);
          });
        } finally {
          window.clearTimeout(timeoutId);
          details.removeAttribute('data-commander-loading');
          panel.removeAttribute('aria-busy');
        }
      };

      commandersGridContainer.addEventListener('toggle', (event) => {
        const details = event.target;
        if (!(details instanceof HTMLDetailsElement) || !details.hasAttribute('data-commander-details')) {
          return;
        }

        if (!details.open) {
          return;
        }

        void loadCommanderBreakdown(details);
      }, true);

      commandersGridContainer.addEventListener('click', (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
          return;
        }

        const sortHeader = target.closest<HTMLElement>('[data-sort-column]');
        if (sortHeader) {
          event.preventDefault();
          const sortColumn = sortHeader.dataset.sortColumn;
          const sortDirection = sortHeader.dataset.sortNextDir;
          if (!sortColumn || !sortDirection) {
            return;
          }

          commandersGridState.sortBy = sortColumn;
          commandersGridState.sortDir = sortDirection;
          void loadCommandersGrid(commandersGridContainer, 1, { scrollIntoView: true, sortColumn });
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

        commandersGridState.search = pageLink.dataset.search ?? '';
        commandersGridState.sortBy = pageLink.dataset.sortBy ?? 'deck_count';
        commandersGridState.sortDir = pageLink.dataset.sortDir ?? 'desc';
        void loadCommandersGrid(commandersGridContainer, page, { scrollIntoView: true });
      });
    }

    const commandersSearchForm = document.getElementById('commanders-search-form');
    const commandersSearchInput = document.getElementById('commanders-search') as HTMLInputElement | null;
    const commandersSearchClear = document.getElementById('commanders-search-clear');
    if (commandersGridContainer && commandersSearchForm && commandersSearchInput) {
      commandersSearchForm.addEventListener('submit', (event) => {
        event.preventDefault();
        commandersGridState.search = commandersSearchInput.value;
        void loadCommandersGrid(commandersGridContainer, 1, { scrollIntoView: true });
      });

      commandersSearchClear?.addEventListener('click', () => {
        commandersSearchInput.value = '';
        commandersGridState.search = '';
        void loadCommandersGrid(commandersGridContainer, 1, { scrollIntoView: true });
      });
    }

    const tabs = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-harvest-tab]'));
    const panels = Array.from(document.querySelectorAll<HTMLElement>('[data-harvest-panel]'));
    if (tabs.length === 0) {
      return;
    }

    const activateTab = (name: string, focusTab: boolean): void => {
      for (const tab of tabs) {
        const isSelected = tab.dataset.harvestTab === name;
        tab.setAttribute('aria-selected', String(isSelected));
        tab.setAttribute('tabindex', isSelected ? '0' : '-1');
        if (isSelected && focusTab) {
          tab.focus();
        }
      }

      for (const panel of panels) {
        panel.toggleAttribute('hidden', panel.dataset.harvestPanel !== name);
      }

      if (name === 'commanders' && commandersGridContainer && !commandersGridContainer.hasAttribute('data-loaded')) {
        commandersGridContainer.dataset.loaded = 'true';
        void loadCommandersGrid(commandersGridContainer, 1, { scrollIntoView: false });
      }
    };

    for (const tab of tabs) {
      tab.addEventListener('click', () => {
        activateTab(tab.dataset.harvestTab ?? 'overview', false);
      });

      tab.addEventListener('keydown', (event) => {
        const currentIndex = tabs.indexOf(tab);
        let nextIndex: number;
        switch (event.key) {
          case 'ArrowLeft':
            nextIndex = (currentIndex - 1 + tabs.length) % tabs.length;
            break;
          case 'ArrowRight':
            nextIndex = (currentIndex + 1) % tabs.length;
            break;
          case 'Home':
            nextIndex = 0;
            break;
          case 'End':
            nextIndex = tabs.length - 1;
            break;
          default:
            return;
        }

        event.preventDefault();
        const next = tabs[nextIndex];
        activateTab(next.dataset.harvestTab ?? 'overview', true);
      });
    }
  });
})();

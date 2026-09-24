import { afterEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/admin-harvest';

const categoryMarkup = '<table class="admin-harvest__category-breakdown"><tbody><tr><td>Ramp</td></tr></tbody></table>';

const gridMarkup = (): string => `
  <table><tbody>
    <tr><td>Krenko, Mob Boss</td></tr><tr><td colspan="4"><details data-commander-details="Krenko, Mob Boss"><summary>Category breakdown</summary><div data-commander-panel></div></details></td></tr>
    <tr><td>Esika, God of the Tree // The Prismatic Bridge</td></tr><tr><td colspan="4"><details data-commander-details="Esika, God of the Tree // The Prismatic Bridge"><summary>Category breakdown</summary><div data-commander-panel></div></details></td></tr>
  </tbody></table>`;

const renderFixture = (): void => {
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: false }));
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
  document.body.innerHTML = `<div class="admin-harvest__tabs" role="tablist"><button type="button" id="harvest-tab-overview" data-harvest-tab="overview" role="tab" aria-controls="harvest-panel-overview" aria-selected="true" tabindex="0">Overview</button><button type="button" id="harvest-tab-commanders" data-harvest-tab="commanders" role="tab" aria-controls="harvest-panel-commanders" aria-selected="false" tabindex="-1">Commanders</button></div><section id="harvest-panel-overview" data-harvest-panel="overview" role="tabpanel"></section><section id="harvest-panel-commanders" data-harvest-panel="commanders" role="tabpanel"><section id="harvested-commanders"><form id="commanders-search-form"><input id="commanders-search" maxlength="100" /><button id="commanders-search-clear" type="button">Clear</button></form><div id="commanders-grid-container">${gridMarkup()}</div></section></section>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const details = (index = 0): HTMLDetailsElement => document.querySelectorAll<HTMLDetailsElement>('[data-commander-details]')[index];
const open = (element: HTMLDetailsElement): void => { element.open = true; };
const close = (element: HTMLDetailsElement): void => { element.open = false; };
const flushToggle = async (): Promise<void> => { await new Promise((resolve) => setTimeout(resolve, 0)); };
const categoryUrls = (fetchMock: ReturnType<typeof vi.fn>): string[] => fetchMock.mock.calls.map(([url]) => String(url)).filter((url) => url.startsWith('/Admin/Harvest/commander-categories'));

describe('admin harvest commander category breakdown', () => {
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ''; });

  it('fetches the category endpoint exactly once when opening a disclosure', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); open(details());
    await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(1)); expect(new URL(categoryUrls(fetchMock)[0], 'http://localhost').pathname).toBe('/Admin/Harvest/commander-categories');
  });

  it('sends ordinary and slash-containing commander names intact as query name only', async () => {
    for (const [index, name] of ['Krenko, Mob Boss', 'Esika, God of the Tree // The Prismatic Bridge'].entries()) {
      const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); open(details(index));
      await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(1)); const url = new URL(categoryUrls(fetchMock)[0], 'http://localhost'); expect(url.searchParams.get('name')).toBe(name); expect(url.pathname).not.toContain(name);
      vi.unstubAllGlobals(); document.body.innerHTML = '';
    }
  });

  it('injects fetched HTML into the opened panel', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); open(details());
    await vi.waitFor(() => expect(details().querySelector('[data-commander-panel]')!.innerHTML).toContain('Ramp'));
  });

  it('does not refetch after a successful close and reopen', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); const element = details(); open(element);
    await vi.waitFor(() => expect(element.hasAttribute('data-commander-loaded')).toBe(true)); close(element); open(element); expect(categoryUrls(fetchMock)).toHaveLength(1);
  });

  it('does not refetch while the first request is pending', async () => {
    let resolve!: (value: { ok: boolean; text: () => Promise<string> }) => void; const response = new Promise<{ ok: boolean; text: () => Promise<string> }>((done) => { resolve = done; });
    const fetchMock = vi.fn().mockReturnValue(response); vi.stubGlobal('fetch', fetchMock); renderFixture(); const element = details(); open(element); await flushToggle(); expect(categoryUrls(fetchMock)).toHaveLength(1); close(element); await flushToggle(); open(element); await flushToggle(); expect(element.hasAttribute('data-commander-loading')).toBe(true); expect(categoryUrls(fetchMock)).toHaveLength(1); resolve({ ok: true, text: async () => categoryMarkup });
    await vi.waitFor(() => expect(element.hasAttribute('data-commander-loaded')).toBe(true)); expect(element.querySelector('[data-commander-panel]')!.innerHTML).toContain('Ramp');
  });

  it('fetches the selected second commander', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); open(details(1)); await flushToggle();
    await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(1)); expect(new URL(categoryUrls(fetchMock)[0], 'http://localhost').searchParams.get('name')).toBe('Esika, God of the Tree // The Prismatic Bridge');
  });

  it('shows retry after non-OK response and retries', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce({ ok: false }).mockResolvedValueOnce({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); const element = details(); open(element);
    await vi.waitFor(() => expect(element.hasAttribute('data-commander-failed')).toBe(true)); element.querySelector<HTMLElement>('[data-commander-retry]')!.click(); await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(2));
  });

  it('shows retry after a thrown fetch', async () => {
    const fetchMock = vi.fn().mockRejectedValue(new Error('offline')); vi.stubGlobal('fetch', fetchMock); renderFixture(); open(details());
    await vi.waitFor(() => expect(details().querySelector('[data-commander-retry]')).not.toBeNull());
  });

  it('refetches on reopen when a request fails after the row was closed', async () => {
    let rejectRequest!: (reason?: unknown) => void;
    const pending = new Promise<never>((_, reject) => { rejectRequest = reject; });
    const fetchMock = vi.fn().mockReturnValueOnce(pending).mockResolvedValue({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); const element = details(); open(element);
    await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(1)); close(element); rejectRequest(new Error('offline'));
    await vi.waitFor(() => expect(element.hasAttribute('data-commander-failed')).toBe(true)); open(element); await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(2));
  });

  it('refetches after a failed close and reopen', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: false }); vi.stubGlobal('fetch', fetchMock); renderFixture(); const element = details(); open(element);
    await vi.waitFor(() => expect(element.querySelector('[data-commander-retry]')).not.toBeNull()); close(element); open(element); await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(2));
  });

  it('unloads details after grid reload and refetches when opened again', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce({ ok: true, text: async () => categoryMarkup }).mockResolvedValueOnce({ ok: true, text: async () => gridMarkup() }).mockResolvedValueOnce({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); open(details());
    await vi.waitFor(() => expect(details().hasAttribute('data-commander-loaded')).toBe(true)); document.querySelector<HTMLFormElement>('#commanders-search-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); await vi.waitFor(() => expect(document.querySelector('#commanders-grid-container')!.innerHTML).toContain('data-commander-details')); open(details()); await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(2));
  });

  it('does not trigger sort or pagination when expanding', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => categoryMarkup }); vi.stubGlobal('fetch', fetchMock); renderFixture(); open(details());
    await vi.waitFor(() => expect(categoryUrls(fetchMock)).toHaveLength(1)); expect(fetchMock.mock.calls.map(([url]) => String(url))).not.toContain('/Admin/Harvest/commanders?');
  });
});

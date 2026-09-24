import { afterEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/admin-harvest';

const gridMarkup = (nextDirection = 'asc'): string => `
  <table><thead><tr>
    <th><button type="button" data-sort-column="commander_name" data-sort-next-dir="${nextDirection}">Commander</button></th>
    <th><button type="button" data-sort-column="deck_count" data-sort-next-dir="asc">Decks</button></th>
  </tr></thead></table>
  <a href="#" data-page="2" data-search="tef" data-sort-by="commander_name" data-sort-dir="asc">Next</a>`;

const renderFixture = (): void => {
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: false }));
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
  document.body.innerHTML = `
    <div class="admin-harvest__tabs" role="tablist">
      <button type="button" id="harvest-tab-overview" data-harvest-tab="overview" role="tab" aria-controls="harvest-panel-overview" aria-selected="true" tabindex="0">Overview</button>
      <button type="button" id="harvest-tab-commanders" data-harvest-tab="commanders" role="tab" aria-controls="harvest-panel-commanders" aria-selected="false" tabindex="-1">Commanders</button>
    </div>
    <section id="harvest-panel-overview" data-harvest-panel="overview" role="tabpanel"></section>
    <section id="harvest-panel-commanders" data-harvest-panel="commanders" role="tabpanel">
      <section id="harvested-commanders"><form id="commanders-search-form"><input id="commanders-search" maxlength="100" /><button id="commanders-search-clear" type="button">Clear</button></form><div id="commanders-grid-container">${gridMarkup()}</div></section>
    </section>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const commanderUrls = (fetchMock: ReturnType<typeof vi.fn>): string[] => fetchMock.mock.calls
  .map(([url]) => String(url))
  .filter((url) => url.startsWith('/Admin/Harvest/commanders?'));

describe('admin harvest commanders', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = '';
  });

  it('submits search with page one', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    document.querySelector<HTMLInputElement>('#commanders-search')!.value = 'tef';
    document.querySelector<HTMLFormElement>('#commanders-search-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(1));
    expect(commanderUrls(fetchMock)[0]).toContain('search=tef');
    expect(commanderUrls(fetchMock)[0]).toContain('page=1');
  });

  it('preserves active sort when submitting search', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    document.querySelector<HTMLElement>('[data-sort-column="commander_name"]')!.click();
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(1));
    document.querySelector<HTMLInputElement>('#commanders-search')!.value = 'tef';
    document.querySelector<HTMLFormElement>('#commanders-search-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(2));
    expect(commanderUrls(fetchMock)[1]).toContain('sortBy=commander_name');
    expect(commanderUrls(fetchMock)[1]).toContain('sortDir=asc');
  });

  it('loads a selected sort at page one', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    document.querySelector<HTMLElement>('[data-sort-column="commander_name"]')!.click();
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(1));
    expect(commanderUrls(fetchMock)[0]).toContain('sortBy=commander_name');
    expect(commanderUrls(fetchMock)[0]).toContain('sortDir=asc');
    expect(commanderUrls(fetchMock)[0]).toContain('page=1');
  });

  it('uses the re-rendered sort direction on a second click', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce({ ok: true, text: async () => gridMarkup('desc') }).mockResolvedValueOnce({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    document.querySelector<HTMLElement>('[data-sort-column="commander_name"]')!.click();
    await vi.waitFor(() => expect(document.querySelector('[data-sort-next-dir="desc"]')).not.toBeNull());
    document.querySelector<HTMLElement>('[data-sort-column="commander_name"]')!.click();
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(2));
    expect(commanderUrls(fetchMock)[1]).toContain('sortDir=desc');
  });

  it('restores focus to the sorted header after reload', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    document.querySelector<HTMLElement>('[data-sort-column="commander_name"]')!.click();
    await vi.waitFor(() => expect(document.activeElement).toBe(document.querySelector('[data-sort-column="commander_name"]')));
    expect(document.activeElement).not.toBe(document.body);
  });

  it('preserves search and sort from pagination links', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    document.querySelector<HTMLElement>('[data-page="2"]')!.click();
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(1));
    expect(commanderUrls(fetchMock)[0]).toContain('page=2');
    expect(commanderUrls(fetchMock)[0]).toContain('search=tef');
    expect(commanderUrls(fetchMock)[0]).toContain('sortBy=commander_name');
    expect(commanderUrls(fetchMock)[0]).toContain('sortDir=asc');
  });

  it('clears search and reloads page one', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    const input = document.querySelector<HTMLInputElement>('#commanders-search')!;
    input.value = 'tef';
    document.querySelector<HTMLElement>('#commanders-search-clear')!.click();
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(1));
    expect(commanderUrls(fetchMock)[0]).toContain('page=1');
    expect(commanderUrls(fetchMock)[0]).not.toContain('search=');
    expect(input.value).toBe('');
  });

  it('retains typed search text across grid reloads', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    const input = document.querySelector<HTMLInputElement>('#commanders-search')!;
    input.value = 'tef';
    document.querySelector<HTMLFormElement>('#commanders-search-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(1));
    expect(input.value).toBe('tef');
  });

  it('retries the same filtered request after a grid fetch failure', async () => {
    const fetchMock = vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce({ ok: true, text: async () => gridMarkup() });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    document.querySelector<HTMLInputElement>('#commanders-search')!.value = 'tef';
    document.querySelector<HTMLFormElement>('#commanders-search-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(document.querySelector('#commanders-retry')).not.toBeNull());
    document.querySelector<HTMLElement>('#commanders-retry')!.click();
    await vi.waitFor(() => expect(commanderUrls(fetchMock)).toHaveLength(2));
    expect(commanderUrls(fetchMock)[1]).toBe(commanderUrls(fetchMock)[0]);
  });
});

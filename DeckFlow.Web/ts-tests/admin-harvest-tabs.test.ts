import { afterEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/admin-harvest';

const commandersUrl = '/Admin/Harvest/commanders?page=1';

const renderFixture = (): void => {
  document.body.innerHTML = `
    <div class="admin-harvest__tabs" role="tablist" aria-label="Harvest sections">
      <button type="button" id="harvest-tab-overview" data-harvest-tab="overview" role="tab" aria-controls="harvest-panel-overview" aria-selected="true" tabindex="0">Overview</button>
      <button type="button" id="harvest-tab-commanders" data-harvest-tab="commanders" role="tab" aria-controls="harvest-panel-commanders" aria-selected="false" tabindex="-1">Commanders</button>
    </div>
    <section id="harvest-panel-overview" data-harvest-panel="overview" role="tabpanel"></section>
    <section id="harvest-panel-commanders" data-harvest-panel="commanders" role="tabpanel" hidden><div id="commanders-grid-container" aria-live="polite" aria-busy="false" aria-label="Harvested commanders grid"></div></section>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const getTabs = (): { overview: HTMLButtonElement; commanders: HTMLButtonElement; container: HTMLElement } => ({
  overview: document.querySelector<HTMLButtonElement>('#harvest-tab-overview')!,
  commanders: document.querySelector<HTMLButtonElement>('#harvest-tab-commanders')!,
  container: document.querySelector<HTMLElement>('#commanders-grid-container')!,
});

const getCommanderCalls = (fetchMock: ReturnType<typeof vi.fn>): unknown[][] => fetchMock.mock.calls.filter(
  ([url]) => url === commandersUrl
);

describe('admin harvest tabs', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = '';
  });

  it('does not fetch commanders after DOMContentLoaded', () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();

    expect(getCommanderCalls(fetchMock)).toHaveLength(0);
  });

  it('loads the commanders grid on first activation', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => '<table><tbody></tbody></table>' });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    const { commanders, container } = getTabs();

    commanders.click();
    await vi.waitFor(() => expect(container.innerHTML).toContain('<table>'));

    expect(getCommanderCalls(fetchMock)).toHaveLength(1);
  });

  it('does not refetch commanders after returning to its tab', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => '<table></table>' });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    const { overview, commanders, container } = getTabs();

    commanders.click();
    await vi.waitFor(() => expect(container.dataset.loaded).toBe('true'));
    overview.click();
    commanders.click();

    expect(getCommanderCalls(fetchMock)).toHaveLength(1);
  });

  it('updates selected tabs and visible panels on commanders activation', () => {
    vi.stubGlobal('fetch', vi.fn());
    renderFixture();
    const { overview, commanders } = getTabs();

    commanders.click();

    expect(commanders.getAttribute('aria-selected')).toBe('true');
    expect(overview.getAttribute('aria-selected')).toBe('false');
    expect(document.querySelector<HTMLElement>('#harvest-panel-commanders')!.hidden).toBe(false);
    expect(document.querySelector<HTMLElement>('#harvest-panel-overview')!.hidden).toBe(true);
  });

  it('moves selection and focus with ArrowLeft and ArrowRight', () => {
    vi.stubGlobal('fetch', vi.fn());
    renderFixture();
    const { overview, commanders } = getTabs();

    commanders.focus();
    commanders.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    expect(document.activeElement).toBe(overview);
    overview.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(document.activeElement).toBe(commanders);
  });

  it('selects endpoint tabs with Home and End', () => {
    vi.stubGlobal('fetch', vi.fn());
    renderFixture();
    const { overview, commanders } = getTabs();

    commanders.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }));
    expect(document.activeElement).toBe(overview);
    overview.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    expect(document.activeElement).toBe(commanders);
  });

  it('loads commanders once through keyboard activation', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => '<table></table>' });
    vi.stubGlobal('fetch', fetchMock);
    renderFixture();
    const { overview, commanders, container } = getTabs();

    overview.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    await vi.waitFor(() => expect(container.dataset.loaded).toBe('true'));
    commanders.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));

    expect(getCommanderCalls(fetchMock)).toHaveLength(1);
  });
});

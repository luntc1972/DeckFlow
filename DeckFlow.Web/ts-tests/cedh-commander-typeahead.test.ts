import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

// The commander input + datalist must exist before deck-sync is imported so the
// bootstrap's attachCommanderSearchInputs() wires the input on load.
document.body.innerHTML = `
  <input id="cmd" name="CommanderName" autocomplete="off"
         list="cedh-commander-suggestions"
         data-commander-search="/cedh-meta-gap/commander-search" />
  <datalist id="cedh-commander-suggestions"></datalist>
`;

await import('../wwwroot/ts/deck-sync');

beforeAll(() => {
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

afterEach(() => {
  vi.unstubAllGlobals();
});

// Let the 300ms input debounce fire and any resulting fetch/microtasks settle.
const flush = (): Promise<void> => new Promise(resolve => setTimeout(resolve, 400));

const input = (): HTMLInputElement => document.getElementById('cmd') as HTMLInputElement;
const datalist = (): HTMLDataListElement =>
  document.getElementById('cedh-commander-suggestions') as HTMLDataListElement;

describe('cEDH meta-gap commander typeahead', () => {
  it('fetches suggestions and populates the datalist', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ['Stella Lee, Wild Card'],
    });
    vi.stubGlobal('fetch', fetchMock);

    input().value = 'stella';
    input().dispatchEvent(new Event('input'));
    await flush();

    expect(fetchMock).toHaveBeenCalledWith(
      '/cedh-meta-gap/commander-search?q=stella',
      expect.objectContaining({ signal: expect.anything() }),
    );
    expect(Array.from(datalist().options).map(option => option.value)).toEqual([
      'Stella Lee, Wild Card',
    ]);
  });

  it('does not fetch for queries shorter than 2 chars and clears the list', async () => {
    datalist().innerHTML = '<option value="stale"></option>';
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    input().value = 's';
    input().dispatchEvent(new Event('input'));
    await flush();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(datalist().options.length).toBe(0);
  });

  it('drops a stale response when the input moved on during the fetch', async () => {
    datalist().innerHTML = '';
    let resolveJson: (value: string[]) => void = () => {};
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => new Promise<string[]>(resolve => { resolveJson = resolve; }),
    });
    vi.stubGlobal('fetch', fetchMock);

    input().value = 'stella';
    input().dispatchEvent(new Event('input'));
    await flush();

    // User keeps typing while the response is still pending.
    input().value = 'stellar wind';
    resolveJson(['Stella Lee, Wild Card']);
    await flush();

    expect(datalist().options.length).toBe(0);
  });
});

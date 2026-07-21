import { afterEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

type ScenarioSummary = {
  id: string;
  name: string;
  savedAt: string;
};

type SaveScenarioResult = 'ok' | 'invalid' | 'cap-reached' | 'quota-exceeded' | 'disabled';

type CutLabScenarioApi = {
  saveScenario: (name: string, stateJson: string) => SaveScenarioResult;
  listScenarios: () => ScenarioSummary[];
  loadScenario: (id: string) => string | null;
  deleteScenario: (id: string) => boolean;
};

type CutLabWindow = Window & {
  DeckFlowCutLab?: Partial<CutLabScenarioApi>;
};

const originalLocalStorageDescriptor = Object.getOwnPropertyDescriptor(window, 'localStorage');

const scenarioApi = (): CutLabScenarioApi => {
  const api = (window as CutLabWindow).DeckFlowCutLab;
  expect(api?.saveScenario).toBeTypeOf('function');
  expect(api?.listScenarios).toBeTypeOf('function');
  expect(api?.loadScenario).toBeTypeOf('function');
  expect(api?.deleteScenario).toBeTypeOf('function');

  return api as CutLabScenarioApi;
};

const disableLocalStorage = (): void => {
  Object.defineProperty(window, 'localStorage', {
    configurable: true,
    get(): never {
      throw new DOMException('localStorage disabled', 'SecurityError');
    },
  });
};

const quotaExceeded = (): DOMException => {
  const error = new DOMException('quota exceeded', 'QuotaExceededError');
  Object.defineProperty(error, 'code', {
    configurable: true,
    value: 22,
  });
  return error;
};

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
  if (originalLocalStorageDescriptor) {
    Object.defineProperty(window, 'localStorage', originalLocalStorageDescriptor);
  }

  window.localStorage.clear();
});

describe('cut-lab scenario storage', () => {
  it('saves, lists, loads, and deletes a scenario using the exact state JSON', () => {
    const api = scenarioApi();
    const stateJson = '{"goals":{"commanderByTurn":3},"pool":["Sol Ring"]}';

    expect(api.saveScenario('Goldfish opener', stateJson)).toBe('ok');

    const scenarios = api.listScenarios();
    expect(scenarios).toHaveLength(1);
    expect(scenarios[0].name).toBe('Goldfish opener');
    expect(scenarios[0].savedAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    expect(api.loadScenario(scenarios[0].id)).toBe(stateJson);

    expect(api.deleteScenario(scenarios[0].id)).toBe(true);
    expect(api.listScenarios()).toEqual([]);
    expect(api.loadScenario(scenarios[0].id)).toBeNull();
  });

  it('rejects blank names and caps storage at 20 scenarios', () => {
    const api = scenarioApi();

    expect(api.saveScenario('   ', '{"state":1}')).toBe('invalid');

    for (let index = 0; index < 20; index += 1) {
      expect(api.saveScenario(`Scenario ${index + 1}`, `{"scenario":${index + 1}}`)).toBe('ok');
    }

    expect(api.listScenarios()).toHaveLength(20);
    expect(api.saveScenario('Scenario 21', '{"scenario":21}')).toBe('cap-reached');
    expect(api.listScenarios()).toHaveLength(20);
  });

  it('returns quota-exceeded when localStorage writes throw a quota error', () => {
    const api = scenarioApi();
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw quotaExceeded();
    });

    expect(api.saveScenario('Quota case', '{"state":1}')).toBe('quota-exceeded');
    expect(api.listScenarios()).toEqual([]);
  });

  it('gracefully degrades when localStorage access is disabled', () => {
    disableLocalStorage();
    const api = scenarioApi();

    expect(api.saveScenario('Disabled', '{"state":1}')).toBe('disabled');
    expect(api.listScenarios()).toEqual([]);
    expect(api.loadScenario('missing')).toBeNull();
    expect(api.deleteScenario('missing')).toBe(false);
  });

  it('falls back when crypto.randomUUID is unavailable', () => {
    const api = scenarioApi();
    vi.stubGlobal('crypto', {
      getRandomValues: crypto.getRandomValues.bind(crypto),
    } satisfies Partial<Crypto>);

    expect(api.saveScenario('Fallback id', '{"state":1}')).toBe('ok');

    const scenarios = api.listScenarios();
    expect(scenarios).toHaveLength(1);
    expect(scenarios[0].id).toMatch(/^s-/);
  });

  it('clears deck inputs before submitting a loaded scenario so the server rehydrates from state', () => {
    const api = scenarioApi();
    const savedStateJson = '{"pool":[{"name":"Sol Ring","quantity":1}],"goals":{"commanderByTurn":5}}';
    expect(api.saveScenario('Rehydrate me', savedStateJson)).toBe('ok');
    const [savedScenario] = api.listScenarios();

    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <select id="cut-lab-input-source" name="DeckInputSource">
          <option value="PublicUrl" selected>URL</option>
          <option value="PasteText">Paste</option>
        </select>
        <input id="cut-lab-deck-url" name="DeckUrl" value="https://www.moxfield.com/decks/current" />
        <textarea id="cut-lab-deck-text" name="DeckText">1 Current Card</textarea>
        <input type="hidden" name="CutLabStateJson" value="" />
      </form>
      <div data-cut-lab-scenario-list>
        <button type="button" data-cut-lab-scenario-load="${savedScenario.id}">Load</button>
      </div>
      <p data-cut-lab-scenario-status></p>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]');
    const requestSubmitSpy = vi.fn();
    Object.defineProperty(form!, 'requestSubmit', {
      configurable: true,
      value: requestSubmitSpy,
    });

    document.querySelector<HTMLElement>('[data-cut-lab-scenario-load]')?.click();

    expect(document.querySelector<HTMLSelectElement>('#cut-lab-input-source')?.value).toBe('PasteText');
    expect(document.querySelector<HTMLInputElement>('#cut-lab-deck-url')?.value).toBe('');
    expect(document.querySelector<HTMLTextAreaElement>('#cut-lab-deck-text')?.value).toBe('');
    expect(document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')?.value).toBe(savedStateJson);
    expect(requestSubmitSpy).toHaveBeenCalledOnce();
  });
});

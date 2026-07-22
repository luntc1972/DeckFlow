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
  it('preserves quantity adjustments when scenario save rebuilds hidden state from the DOM', () => {
    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value='${JSON.stringify({
          commander: 'Zur the Enchanter',
          pool: [
            {
              name: 'Zur the Enchanter',
              quantity: 1,
              typeLine: 'Legendary Creature',
              isCommander: true,
              isLocked: true,
              packageId: null,
            },
            {
              name: 'Island',
              quantity: 36,
              typeLine: 'Basic Land - Island',
              isCommander: false,
              isLocked: false,
              packageId: null,
            },
          ],
          packages: [],
          decisions: [],
          quantityAdjustments: [{ name: 'Island', delta: -2, isAddedBasic: false }],
          baselineSnapshot: { source: 'seeded' },
          intent: {
            primaryPlan: 'Trim to exactly 100.',
            secondaryPlan: null,
            bracket: 4,
            playExperience: 'Focused',
            includeSideboard: false,
            includeMaybeboard: false,
          },
          roleFloors: [],
          goals: {
            commanderByTurn: 3,
            engineByTurn: 2,
            representativeLineByTurn: 4,
          },
        })}' />
        <textarea name="PrimaryPlan">Trim to exactly 100.</textarea>
        <textarea name="SecondaryPlan"></textarea>
        <input type="radio" name="Bracket" value="4" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <input data-cut-lab-goal="commander" value="3" />
        <input data-cut-lab-goal="engine" value="2" />
        <input data-cut-lab-goal="representative-line" value="4" />
      </form>
      <table>
        <tbody>
          <tr
            data-cut-lab-card="Zur the Enchanter"
            data-cut-lab-quantity="1"
            data-cut-lab-type-line="Legendary Creature"
            data-cut-lab-commander="true">
            <td data-label="Card"><strong>1 × Zur the Enchanter</strong></td>
            <td><input type="checkbox" data-cut-lab-lock-card checked /></td>
            <td><select data-cut-lab-package-card><option value="" selected>Unassigned</option></select></td>
          </tr>
          <tr
            data-cut-lab-card="Island"
            data-cut-lab-quantity="36"
            data-cut-lab-type-line="Basic Land - Island"
            data-cut-lab-role="Lands">
            <td data-label="Card"><strong>36 × Island</strong></td>
            <td><input type="checkbox" data-cut-lab-lock-card /></td>
            <td><select data-cut-lab-package-card><option value="" selected>Unassigned</option></select></td>
          </tr>
        </tbody>
      </table>
      <input data-cut-lab-scenario-name value="Exact 100" />
      <button type="button" data-cut-lab-scenario-save>Save</button>
      <div data-cut-lab-scenario-list></div>
      <p data-cut-lab-scenario-status></p>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));
    document.querySelector<HTMLElement>('[data-cut-lab-scenario-save]')?.click();

    const [scenario] = scenarioApi().listScenarios();
    expect(scenario).toBeDefined();

    const savedState = scenarioApi().loadScenario(scenario.id);
    expect(savedState).toContain('"quantityAdjustments":[{"name":"Island","delta":-2,"isAddedBasic":false}]');
  });

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
    const removeItemSpy = vi.spyOn(Storage.prototype, 'removeItem');
    const clearLastDeckSpy = vi.fn();
    Object.assign(window, {
      DeckFlow: {
        clearLastDeck: clearLastDeckSpy,
      },
    });

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
    expect(removeItemSpy).toHaveBeenCalledWith('decksync-form-state-cut-lab');
    expect(removeItemSpy).toHaveBeenCalledWith('decksync-form-state-cut-lab:savedAt');
    expect(clearLastDeckSpy).toHaveBeenCalledOnce();
    expect(requestSubmitSpy).toHaveBeenCalledOnce();
  });
});

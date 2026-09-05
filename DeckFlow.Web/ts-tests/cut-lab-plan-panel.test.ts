import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import '../wwwroot/ts/cut-lab';

let fetchMock: any;

beforeAll(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  document.body.innerHTML = '';
  fetchMock.mockReset();
});

const stateJson = JSON.stringify({ intent: { planProfile: { genericStrategies: [], commanderThemes: [] } } });
const patch = {
  cutLabStateJson: stateJson,
  currentCount: 100,
  cardsRemaining: 0,
  canBuildExport: true,
  nextProposal: null,
  cutsMade: [],
  structuralFindings: [],
  whatifCardOutOptions: [],
  whatifCardInOptions: [],
  quantityTuners: [],
  addableBasics: [],
};

const response = (strategies: string[], themes: string[]) => ({
  ok: true,
  json: async () => ({ patch, appliedStrategies: strategies, appliedThemes: themes }),
});

const flush = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

const buildFixture = (): HTMLInputElement => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab"><input name="CutLabStateJson" value='${stateJson}' /><input name="__RequestVerificationToken" value="token" /><button data-cut-lab-plan-apply-submit>Apply plan</button></form>
    <div data-cut-lab-plan-panel>
      <label><input type="checkbox" name="PlanStrategies" value="kept" />Kept</label>
      <label><input type="checkbox" name="PlanStrategies" value="dropped" checked />Dropped</label>
      <label><input type="checkbox" name="PlanThemes" value="theme-a" />Theme A</label>
      <label><input type="checkbox" name="PlanThemes" value="theme-b" checked />Theme B</label>
    </div>
    <div class="cutlab-proposal"></div>
    <table><tbody><tr data-cut-lab-floor-row="ramp" data-cut-lab-floor-count="2" data-cut-lab-floor-default="1" data-cut-lab-floor-user-set="false"><td data-label="In pool"><span data-cut-lab-floor-count-label>2 in pool</span></td><td data-label="Floor"><input data-cut-lab-floor="ramp" value="1" /></td><td data-label="Source"><button data-cut-lab-floor-reset></button></td></tr></tbody></table>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
  return document.querySelector<HTMLInputElement>('input[value="dropped"]')!;
};

describe('cut-lab plan panel', () => {
  it('reverts optimistic checkbox changes when apply responds not ok', async () => {
    buildFixture();
    fetchMock.mockResolvedValueOnce({ ok: false, text: async () => 'Profile failed' });

    document.querySelector<HTMLInputElement>('input[value="kept"]')!.click();
    document.querySelector<HTMLInputElement>('input[value="theme-a"]')!.click();
    await flush();

    expect(document.querySelector<HTMLInputElement>('input[value="kept"]')!.checked).toBe(false);
    expect(document.querySelector<HTMLInputElement>('input[value="theme-a"]')!.checked).toBe(false);
  });

  it('reverts optimistic checkbox changes when apply rejects', async () => {
    buildFixture();
    fetchMock.mockRejectedValueOnce(new Error('Network failed'));

    document.querySelector<HTMLInputElement>('input[value="kept"]')!.click();
    document.querySelector<HTMLInputElement>('input[value="theme-a"]')!.click();
    await flush();

    expect(document.querySelector<HTMLInputElement>('input[value="kept"]')!.checked).toBe(false);
    expect(document.querySelector<HTMLInputElement>('input[value="theme-a"]')!.checked).toBe(false);
  });

  it('reverts optimistic checkbox changes when apply returns no patch state', async () => {
    buildFixture();
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => ({ patch: null }) });

    document.querySelector<HTMLInputElement>('input[value="kept"]')!.click();
    document.querySelector<HTMLInputElement>('input[value="theme-a"]')!.click();
    await flush();

    expect(document.querySelector<HTMLInputElement>('input[value="kept"]')!.checked).toBe(false);
    expect(document.querySelector<HTMLInputElement>('input[value="theme-a"]')!.checked).toBe(false);
  });

  it('disables the hidden plan apply submit button', () => {
    buildFixture();

    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-plan-apply-submit]')!;
    expect(button.hidden).toBe(true);
    expect(button.disabled).toBe(true);
  });

  it('refreshes resolved floors from a decision patch', async () => {
    buildFixture();
    document.body.insertAdjacentHTML('beforeend', `<form><input name="CutLabStateJson" value='${stateJson}' /><input name="CardName" value="Card" /><input name="Decision" value="accept" /><input name="__RequestVerificationToken" value="token" /><button type="submit">Accept</button></form>`);
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => ({ patch: { ...patch, resolvedFloors: [{ roleKey: 'ramp', inPoolCount: 7, bracketValue: 2, commanderDisplay: 'None', floor: 2, defaultValue: 1, planDelta: 1, isUserSet: false, sourceLabel: 'Default', sourceDetail: 'Default for B2: 1' }] } }) });

    document.querySelector<HTMLFormElement>('form:not([data-cache-key])')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await flush();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-floor-count-label]')!.textContent).toBe('7 in pool');
  });

  it('preserves persisted commander themes when the outage omits theme checkboxes', async () => {
    buildFixture();
    const persistedState = {
      intent: {
        planProfile: {
          genericStrategies: [],
          commanderThemes: [{ slug: 'theme-a' }],
          commanderThemesUnavailable: true,
        },
      },
    };
    document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')!.value = JSON.stringify(persistedState);
    document.querySelectorAll<HTMLInputElement>('input[name="PlanThemes"]').forEach(input => { input.closest('label')!.remove(); });
    fetchMock.mockResolvedValueOnce(response(['kept'], []));

    document.querySelector<HTMLInputElement>('input[value="dropped"]')!.dispatchEvent(new Event('change', { bubbles: true }));
    await flush();

    const request = JSON.parse(fetchMock.mock.calls[0][1].body);
    const postedState = JSON.parse(request.cutLabStateJson);
    expect(postedState.intent.planProfile.commanderThemes).toEqual([{ slug: 'theme-a' }]);
    expect(postedState.intent.planProfile.commanderThemesUnavailable).toBe(true);
  });

  it('clears commander themes when rendered theme checkboxes are all unchecked', async () => {
    buildFixture();
    const persistedState = {
      intent: {
        planProfile: {
          genericStrategies: [],
          commanderThemes: [{ slug: 'theme-a' }],
          commanderThemesUnavailable: true,
        },
      },
    };
    document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')!.value = JSON.stringify(persistedState);
    document.querySelectorAll<HTMLInputElement>('input[name="PlanThemes"]').forEach(input => { input.checked = false; });
    fetchMock.mockResolvedValueOnce(response(['kept'], []));

    document.querySelector<HTMLInputElement>('input[value="dropped"]')!.dispatchEvent(new Event('change', { bubbles: true }));
    await flush();

    const request = JSON.parse(fetchMock.mock.calls[0][1].body);
    const postedState = JSON.parse(request.cutLabStateJson);
    expect(postedState.intent.planProfile.commanderThemes).toEqual([]);
  });

  it('applies changes after the plan-panel root is replaced', async () => {
    buildFixture();
    const panel = document.querySelector('[data-cut-lab-plan-panel]')!;
    panel.outerHTML = '<div data-cut-lab-plan-panel><label><input type="checkbox" name="PlanStrategies" value="replacement" checked />Replacement</label></div>';
    fetchMock.mockResolvedValueOnce(response(['replacement'], []));

    document.querySelector<HTMLInputElement>('input[value="replacement"]')!.dispatchEvent(new Event('change', { bubbles: true }));
    await flush();

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('reconciles server state, coalesces changes, and localizes apply errors', async () => {
    const checkbox = buildFixture();
    let resolveInFlight: ((value: unknown) => void) | undefined;
    const inFlight = new Promise(resolve => { resolveInFlight = resolve; });
    fetchMock
      .mockResolvedValueOnce(response(['kept'], ['theme-a']))
      .mockImplementationOnce(() => inFlight)
      .mockResolvedValueOnce(response(['kept'], ['theme-a']))
      .mockResolvedValueOnce({ ok: false, text: async () => 'Profile failed' });

    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
    await flush();
    expect(document.querySelector<HTMLInputElement>('input[value="kept"]')!.checked).toBe(true);
    expect(checkbox.checked).toBe(false);
    expect(document.querySelector<HTMLInputElement>('input[value="theme-a"]')!.checked).toBe(true);
    expect(document.querySelector<HTMLInputElement>('input[value="theme-b"]')!.checked).toBe(false);

    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
    await flush();
    checkbox.checked = false;
    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
    expect(fetchMock).toHaveBeenCalledTimes(2);
    resolveInFlight!(response(['kept'], ['theme-a']));
    await flush();
    expect(fetchMock).toHaveBeenCalledTimes(3);

    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
    await flush();
    expect(document.querySelector('[data-cut-lab-plan-panel] [data-cut-lab-decision-error]')?.textContent).toBe("Couldn't recalculate this cut — nothing changed. Try again.");
    expect(document.querySelector('.cutlab-proposal [data-cut-lab-decision-error]')).toBeNull();
  });
});

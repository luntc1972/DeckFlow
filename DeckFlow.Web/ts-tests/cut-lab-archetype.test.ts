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

const stateJson = JSON.stringify({ intent: { planProfile: { archetype: 'stax', genericStrategies: [], commanderThemes: [] } } });
const patch = { cutLabStateJson: stateJson, currentCount: 100, cardsRemaining: 0, canBuildExport: true, nextProposal: null, cutsMade: [], structuralFindings: [], whatifCardOutOptions: [], whatifCardInOptions: [], quantityTuners: [], addableBasics: [] };
const response = (archetype: string | null, outcome: 'Replaced' | 'Kept' | 'Unchanged' = 'Unchanged', goals: any = null) => ({ ok: true, json: async () => ({ patch, appliedStrategies: [], appliedThemes: [], appliedArchetype: archetype, appliedGoals: goals, goalOutcome: outcome }) });
const flush = async (): Promise<void> => { await Promise.resolve(); await Promise.resolve(); await new Promise(resolve => window.setTimeout(resolve, 0)); };

const buildFixture = (): HTMLInputElement => {
  document.body.innerHTML = `<form data-cache-key="cut-lab"><input name="CutLabStateJson" value='${stateJson}' /><input name="__RequestVerificationToken" value="token" /><button data-cut-lab-plan-apply-submit>Apply plan</button></form><div data-cut-lab-plan-panel><label><input type="radio" name="PlanArchetype" value="stax" checked data-cut-lab-plan-archetype /><span class="cut-lab-plan-panel__row-name">Stax</span></label><label><input type="radio" name="PlanArchetype" value="turbo-combo" data-cut-lab-plan-archetype /><span class="cut-lab-plan-panel__row-name">Turbo</span></label><p data-cut-lab-plan-zero-notice></p><p class="hidden" data-cut-lab-archetype-notice></p></div><form data-cut-lab-goals-form><input data-cut-lab-goal="commanderByTurn" value="4" /><input data-cut-lab-goal="engineByTurn" value="5" /><input data-cut-lab-goal="representativeLineByTurn" value="6" /></form><div class="cutlab-proposal"></div>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
  return document.querySelector<HTMLInputElement>('input[value="turbo-combo"]')!;
};

describe('cut-lab archetype picker', () => {
  it('posts planProfile.archetype and priorArchetype for a radio pick', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce(response('turbo-combo'));
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    const request = JSON.parse(fetchMock.mock.calls[0][1].body);
    expect(JSON.parse(request.cutLabStateJson).intent.planProfile.archetype).toBe('turbo-combo');
    expect(request.priorArchetype).toBe('stax');
  });

  it('writes replaced goals and shows the replacement notice', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce(response('turbo-combo', 'Replaced', { commanderByTurn: 2, engineByTurn: 3, representativeLineByTurn: 6 }));
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-goal="commanderByTurn"]')!.value).toBe('2');
    expect(document.querySelector('[data-cut-lab-archetype-notice]')!.textContent).toBe('Goals set to Turbo defaults: commander by T2, engine by T3, line by T6. Change them in Step 4.');
  });

  it('keeps custom goals and shows the kept notice', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce(response('turbo-combo', 'Kept', { commanderByTurn: 2, engineByTurn: 3, representativeLineByTurn: 6 }));
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-goal="commanderByTurn"]')!.value).toBe('4');
    expect(document.querySelector('[data-cut-lab-archetype-notice]')!.textContent).toBe('Your custom goals were kept. Turbo defaults would be T2 / T3 / T6 — change them in Step 4.');
  });

  it('reverts the radio and shows an error after a failed response', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce({ ok: false, text: async () => 'Profile failed' });
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(document.querySelector<HTMLInputElement>('input[value="stax"]')!.checked).toBe(true);
    expect(document.querySelector('[data-cut-lab-decision-error]')!.textContent).toBe("Couldn't recalculate this cut — nothing changed. Try again.");
  });

  it('applies the second of two quick picks', async () => {
    const radio = buildFixture(); let resolveFirst: ((value: unknown) => void) | undefined;
    fetchMock.mockImplementationOnce(() => new Promise(resolve => { resolveFirst = resolve; })).mockResolvedValueOnce(response('stax'));
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); document.querySelector<HTMLInputElement>('input[value="stax"]')!.checked = true; document.querySelector<HTMLInputElement>('input[value="stax"]')!.dispatchEvent(new Event('change', { bubbles: true })); resolveFirst!(response('turbo-combo')); await flush(); await flush();
    expect(document.querySelector<HTMLInputElement>('input[value="stax"]')!.checked).toBe(true);
  });

  it('posts and applies a queued second pick when the first fails', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce({ ok: false, text: async () => 'Profile failed' }).mockResolvedValueOnce(response('stax'));
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); document.querySelector<HTMLInputElement>('input[value="stax"]')!.checked = true; document.querySelector<HTMLInputElement>('input[value="stax"]')!.dispatchEvent(new Event('change', { bubbles: true })); await flush(); await flush();
    expect(fetchMock).toHaveBeenCalledTimes(2); expect(document.querySelector<HTMLInputElement>('input[value="stax"]')!.checked).toBe(true);
  });
});

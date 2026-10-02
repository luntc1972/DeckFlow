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
const response = (archetype: string | null, outcome: 'Replaced' | 'Kept' | 'Unchanged' = 'Unchanged', goals: any = null, archetypeDefaultGoals: any = null) => ({ ok: true, json: async () => ({ patch, appliedStrategies: [], appliedThemes: [], appliedArchetype: archetype, appliedGoals: goals, archetypeDefaultGoals, goalOutcome: outcome }) });
const flush = async (): Promise<void> => { await Promise.resolve(); await Promise.resolve(); await new Promise(resolve => window.setTimeout(resolve, 0)); };

const buildFixture = (): HTMLInputElement => {
  document.body.innerHTML = `<form data-cache-key="cut-lab"><input name="CutLabStateJson" value='${stateJson}' /><input name="__RequestVerificationToken" value="token" /><button data-cut-lab-plan-apply-submit>Apply plan</button></form><div data-cut-lab-plan-panel><label><input type="radio" name="PlanArchetype" value="" data-cut-lab-plan-archetype data-cut-lab-archetype-presets="" /><span class="cut-lab-plan-panel__row-name">None</span><span class="hidden" data-cut-lab-archetype-badge="choice">Your choice</span></label><label><input type="radio" name="PlanArchetype" value="stax" checked data-cut-lab-plan-archetype data-cut-lab-archetype-presets="stax" /><span class="cut-lab-plan-panel__row-name">Stax</span><span class="hidden" data-cut-lab-archetype-badge="choice">Your choice</span></label><label><input type="radio" name="PlanArchetype" value="turbo-combo" data-cut-lab-plan-archetype data-cut-lab-archetype-presets="combo" /><span class="cut-lab-plan-panel__row-name">Turbo</span><span data-cut-lab-archetype-badge="suggested">Suggested</span><span class="hidden" data-cut-lab-archetype-badge="choice">Your choice</span><span data-cut-lab-archetype-reason>Why suggested: Fast mana.</span></label><label class="cut-lab-plan-panel__row"><input type="checkbox" name="PlanStrategies" value="stax" /><span class="cut-lab-plan-panel__row-name">Stax strategy</span></label><p data-cut-lab-plan-zero-notice></p><p class="hidden" data-cut-lab-archetype-notice></p></div><form data-cut-lab-goals-form><input data-cut-lab-goal="commander" value="4" /><input data-cut-lab-goal="engine" value="5" /><input data-cut-lab-goal="representative-line" value="6" /></form><div class="cutlab-proposal"></div>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
  return document.querySelector<HTMLInputElement>('input[value="turbo-combo"]')!;
};

describe('cut-lab archetype picker', () => {
  it('Archetype rapid re-pick restores chain-start goals and shows applying state', async () => {
    buildFixture();
    const stateInput = document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')!;
    const initialGoals = { commanderByTurn: 4, engineByTurn: 5, representativeLineByTurn: 6 };
    stateInput.value = JSON.stringify({ goals: initialGoals, intent: { planProfile: { archetype: null, genericStrategies: [], commanderThemes: [] } } });
    const stax = document.querySelector<HTMLInputElement>('input[value="stax"]')!;
    const none = document.querySelector<HTMLInputElement>('input[name="PlanArchetype"][value=""]')!;
    let resolveFirst: (value: unknown) => void;
    const staxGoals = { commanderByTurn: 2, engineByTurn: 3, representativeLineByTurn: 6 };
    const staxPatch = { ...patch, cutLabStateJson: JSON.stringify({ goals: staxGoals, intent: { planProfile: { archetype: 'stax', genericStrategies: [], commanderThemes: [] } } }) };
    const nonePatch = { ...patch, cutLabStateJson: JSON.stringify({ goals: initialGoals, intent: { planProfile: { archetype: null, genericStrategies: [], commanderThemes: [] } } }) };
    fetchMock.mockImplementationOnce(() => new Promise(resolve => { resolveFirst = resolve; }))
      .mockResolvedValueOnce({ ok: true, json: async () => ({ patch: nonePatch, appliedStrategies: [], appliedThemes: [], appliedArchetype: null, appliedGoals: initialGoals, goalOutcome: 'Unchanged' }) });

    stax.checked = true; stax.dispatchEvent(new Event('change', { bubbles: true }));
    await Promise.resolve();
    const wasBusy = stax.disabled && stax.closest('label')!.textContent!.includes('Applying…');
    none.checked = true; none.dispatchEvent(new Event('change', { bubbles: true }));
    resolveFirst!({ ok: true, json: async () => ({ patch: staxPatch, appliedStrategies: [], appliedThemes: [], appliedArchetype: 'stax', appliedGoals: staxGoals, goalOutcome: 'Replaced' }) });
    await flush(); await flush();

    expect(wasBusy).toBe(true);
    expect(JSON.parse(stateInput.value).goals).toEqual(initialGoals);
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-goal="commander"]')!.value).toBe('4');
    expect(document.querySelector('[data-cut-lab-archetype-notice]')!.textContent).toBe('Archetype cleared. Manual strategies and goals are unchanged.');
    expect(stax.closest('label')!.textContent).not.toContain('Applying…');
    expect(JSON.parse(fetchMock.mock.calls[1][1].body).chainStartGoals).toEqual(initialGoals);
  });

  it('Archetype timeout uses archetype failure copy', async () => {
    buildFixture();
    const turbo = document.querySelector<HTMLInputElement>('input[value="turbo-combo"]')!;
    fetchMock.mockRejectedValueOnce(new DOMException('Aborted', 'AbortError'));
    turbo.checked = true; turbo.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(document.querySelector('[data-cut-lab-decision-error]')!.textContent).toBe("Couldn't apply the archetype. Your previous choice is restored — try again.");
  });
  it('Archetype pick sets implied chip on preset strategy rows and None clears it', async () => {
    buildFixture();
    const stax = document.querySelector<HTMLInputElement>('input[value="stax"]')!;
    const none = document.querySelector<HTMLInputElement>('input[name="PlanArchetype"][value=""]')!;
    const strategy = document.querySelector<HTMLInputElement>('input[name="PlanStrategies"][value="stax"]')!;
    fetchMock.mockResolvedValueOnce(response('stax')).mockResolvedValueOnce(response(null));

    stax.checked = true; stax.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(strategy.closest('label')!.classList.contains('cut-lab-plan-panel__row--implied')).toBe(true);
    expect(strategy.closest('label')!.querySelector('.cut-lab-plan-panel__badge--implied')!.textContent).toBe('Included by Stax');
    expect(strategy.checked).toBe(false);

    none.checked = true; none.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(strategy.closest('label')!.classList.contains('cut-lab-plan-panel__row--implied')).toBe(false);
    expect(strategy.closest('label')!.querySelector('.cut-lab-plan-panel__badge--implied')).toBeNull();
  });

  it('does not mark a manually checked preset strategy as implied', async () => {
    buildFixture();
    const stax = document.querySelector<HTMLInputElement>('input[value="stax"]')!;
    const strategy = document.querySelector<HTMLInputElement>('input[name="PlanStrategies"][value="stax"]')!;
    strategy.checked = true;
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => ({ ...await response('stax', 'Unchanged').json(), appliedStrategies: ['stax'] }) });
    stax.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(strategy.closest('label')!.classList.contains('cut-lab-plan-panel__row--implied')).toBe(false);
  });

  it('hides Suggested for the selected suggestion and restores it after another pick', async () => {
    const turbo = buildFixture();
    const stax = document.querySelector<HTMLInputElement>('input[value="stax"]')!;
    const suggested = turbo.closest('label')!.querySelector<HTMLElement>('[data-cut-lab-archetype-badge="suggested"]')!;
    const choice = turbo.closest('label')!.querySelector<HTMLElement>('[data-cut-lab-archetype-badge="choice"]')!;
    fetchMock.mockResolvedValueOnce(response('turbo-combo')).mockResolvedValueOnce(response('stax'));
    turbo.checked = true; turbo.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(suggested.classList.contains('hidden')).toBe(true); expect(choice.classList.contains('hidden')).toBe(false);
    expect(turbo.closest('label')!.querySelector('[data-cut-lab-archetype-reason]')!.classList.contains('hidden')).toBe(true);
    stax.checked = true; stax.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(suggested.classList.contains('hidden')).toBe(false);
  });

  it('keeps the archetype notice hidden for a strategy apply with no archetype before or after', async () => {
    buildFixture();
    const stateInput = document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')!;
    stateInput.value = JSON.stringify({ intent: { planProfile: { archetype: null, genericStrategies: [], commanderThemes: [] } } });
    const none = document.querySelector<HTMLInputElement>('input[name="PlanArchetype"][value=""]')!;
    const strategy = document.querySelector<HTMLInputElement>('input[name="PlanStrategies"]')!;
    none.checked = true; strategy.checked = true;
    fetchMock.mockResolvedValueOnce(response(null));
    strategy.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    const notice = document.querySelector<HTMLElement>('[data-cut-lab-archetype-notice]')!;
    expect(notice.classList.contains('hidden')).toBe(true); expect(notice.textContent).not.toContain('Archetype cleared');
  });

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
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-goal="commander"]')!.value).toBe('2');
    expect(document.querySelector('[data-cut-lab-archetype-notice]')!.textContent).toBe('Goals set to Turbo defaults: commander by T2, engine by T3, line by T6. Change them in Step 4.');
  });

  it('keeps custom goals and shows archetype defaults in the kept notice', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce(response('turbo-combo', 'Kept', { commanderByTurn: 9, engineByTurn: 9, representativeLineByTurn: 9 }, { commanderByTurn: 2, engineByTurn: 3, representativeLineByTurn: 6 }));
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-goal="commander"]')!.value).toBe('9');
    expect(document.querySelector('[data-cut-lab-archetype-notice]')!.textContent).toBe('Your custom goals were kept. Turbo defaults would be T2 / T3 / T6 — change them in Step 4.');
  });

  it('archetype Kept without archetype defaults shows the no-numbers notice', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce(response('turbo-combo', 'Kept', { commanderByTurn: 9, engineByTurn: 9, representativeLineByTurn: 9 }));

    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); await flush();

    expect(document.querySelector('[data-cut-lab-archetype-notice]')!.textContent).toBe('Your custom goals were kept. Change them in Step 4.');
  });

  it('archetype Replaced with a missing goal input still checks the radio and renders the notice', async () => {
    const radio = buildFixture();
    document.querySelector('[data-cut-lab-goal="engine"]')!.remove();
    fetchMock.mockResolvedValueOnce(response('turbo-combo', 'Replaced', { commanderByTurn: 2, engineByTurn: 3, representativeLineByTurn: 6 }));

    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); await flush();

    expect(radio.checked).toBe(true);
    expect(document.querySelector('[data-cut-lab-archetype-notice]')!.textContent).toBe('Goals set to Turbo defaults: commander by T2, engine by T3, line by T6. Change them in Step 4.');
  });

  it('reverts the radio and shows an error after a failed response', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce({ ok: false, text: async () => 'Profile failed' });
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); await flush();
    expect(document.querySelector<HTMLInputElement>('input[value="stax"]')!.checked).toBe(true);
    expect(document.querySelector('[data-cut-lab-decision-error]')!.textContent).toBe("Couldn't apply the archetype. Your previous choice is restored — try again.");
  });

  it('applies the second of two quick picks', async () => {
    const radio = buildFixture(); let resolveFirst: ((value: unknown) => void) | undefined;
    const stax = document.querySelector<HTMLInputElement>('input[value="stax"]')!;
    fetchMock.mockImplementationOnce(() => new Promise(resolve => { resolveFirst = resolve; })).mockResolvedValueOnce(response('turbo-combo', 'Replaced', { commanderByTurn: 2, engineByTurn: 3, representativeLineByTurn: 6 }));
    stax.checked = true; stax.dispatchEvent(new Event('change', { bubbles: true })); radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); resolveFirst!(response('stax')); await flush(); await flush();
    expect(radio.checked).toBe(true); expect(radio.value).not.toBe('stax'); expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(JSON.parse(fetchMock.mock.calls[1][1].body).cutLabStateJson).toContain('"archetype":"turbo-combo"');
    expect(document.querySelector('[data-cut-lab-archetype-notice]')!.textContent).toBe('Goals set to Turbo defaults: commander by T2, engine by T3, line by T6. Change them in Step 4.');
  });

  it('posts and applies a queued second pick when the first fails', async () => {
    const radio = buildFixture(); fetchMock.mockResolvedValueOnce({ ok: false, text: async () => 'Profile failed' }).mockResolvedValueOnce(response('stax'));
    radio.checked = true; radio.dispatchEvent(new Event('change', { bubbles: true })); document.querySelector<HTMLInputElement>('input[value="stax"]')!.checked = true; document.querySelector<HTMLInputElement>('input[value="stax"]')!.dispatchEvent(new Event('change', { bubbles: true })); await flush(); await flush();
    expect(fetchMock).toHaveBeenCalledTimes(2); expect(document.querySelector<HTMLInputElement>('input[value="stax"]')!.checked).toBe(true);
  });
});

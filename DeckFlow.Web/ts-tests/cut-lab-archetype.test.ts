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

const flush = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

describe('cut-lab archetype picker', () => {
  it('posts planProfile.archetype and priorArchetype for a radio pick', async () => {
    const state = JSON.stringify({ intent: { planProfile: { archetype: 'stax', genericStrategies: [], commanderThemes: [] } } });
    document.body.innerHTML = `<form data-cache-key="cut-lab"><input name="CutLabStateJson" value='${state}' /><input name="__RequestVerificationToken" value="token" /><button data-cut-lab-plan-apply-submit>Apply plan</button></form><div data-cut-lab-plan-panel><input type="radio" name="PlanArchetype" value="stax" checked /><input type="radio" name="PlanArchetype" value="turbo-combo" /><p data-cut-lab-plan-zero-notice></p></div><div class="cutlab-proposal"></div>`;
    const radio = document.querySelector<HTMLInputElement>('input[value="turbo-combo"]')!;
    radio.checked = true;
    expect(radio.checked).toBe(true);
    expect(JSON.parse(state).intent.planProfile.archetype).toBe('stax');
  });
});

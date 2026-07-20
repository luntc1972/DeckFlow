import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabDecideMetricDeltaDto {
  label: string;
  delta: number;
  direction: 'Up' | 'Down' | 'None';
  isMeaningful: boolean;
  kind: string;
  unit: 'Percent' | 'Cards';
}

interface CutLabDecideResponse {
  cutLabStateJson: string;
  nextProposal: {
    isTerminal: boolean;
    isAtTarget: boolean;
    isNothingToCut: boolean;
    cardName: string;
    roundKey: string;
    roundLabel: string;
    roundBannerBody: string;
    findingCount: number;
    findingChips: string[];
  };
  proposalDeltas: {
    cardName: string;
    changedFamilyCount: number;
    deltas: CutLabDecideMetricDeltaDto[];
  } | null;
  floorWarnings: Array<{ message: string }>;
  cardsRemaining: number;
  cutsMade: Array<{ cardName: string; roundKey: string; roundLabel: string; ordinal: number }>;
}

let fetchMock: ReturnType<typeof vi.fn>;

beforeAll(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  document.body.innerHTML = '';
  fetchMock.mockReset();
});

const flushDecisionSubmit = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

const deferred = <T,>(): {
  promise: Promise<T>;
  resolve: (value: T) => void;
} => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(pendingResolve => {
    resolve = pendingResolve;
  });

  return { promise, resolve };
};

const buildDecisionFixture = (): void => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
      <textarea name="PrimaryPlan">Keep the fast mana density honest.</textarea>
      <textarea name="SecondaryPlan"></textarea>
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <table>
        <tbody>
          <tr data-cut-lab-card="Tymna the Weaver" data-cut-lab-type-line="Legendary Creature" data-cut-lab-role="draw" data-cut-lab-commander="true">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Tymna the Weaver" checked disabled /></td>
            <td data-label="Card"><strong>1 × Tymna the Weaver</strong></td>
            <td data-label="Package assignment">
              <select data-cut-lab-package-card="Tymna the Weaver">
                <option value="">Unlocked pool</option>
                <option value="__new__">+ New package…</option>
              </select>
            </td>
          </tr>
        </tbody>
      </table>
      <section class="result-panel nested-panel">
        <div class="card-picker__rows">
          <div class="card-picker__row hidden" data-cut-lab-new-package-row>
            <div class="card-picker__input-shell">
              <input class="card-picker__input" type="text" data-cut-lab-new-package-input />
            </div>
            <button type="button" data-cut-lab-new-package-save>+</button>
            <button type="button" data-cut-lab-new-package-cancel>x</button>
          </div>
        </div>
      </section>
    </form>
    <section class="result-panel">
      <div class="cutlab-sticky-bar">
        <span class="cutlab-sticky-bar__round" data-cut-lab-sticky-round>Round 1 · Obvious cuts</span>
        <span class="cutlab-sticky-bar__count" data-cut-lab-sticky-remaining>12 to cut</span>
        <span class="cutlab-sticky-bar__accepted" data-cut-lab-sticky-accepted>0 cut so far</span>
      </div>
      <div class="cutlab-round-banner">
        <p class="cutlab-finding__heading">Round 1 · Obvious cuts</p>
        <p>Cards flagged by 2 or more structural findings from the section above.</p>
      </div>
      <div class="cutlab-proposal" data-cut-lab-card="Sol Ring" data-cut-lab-round="round-1">
        <p class="cutlab-proposal__heading">Proposed cut: Sol Ring</p>
        <div class="cutlab-proposal__evidence">
          <p>Flagged by 2 findings:</p>
          <div class="kb-chip-area__chips">
            <span class="kb-chip">Curve congestion</span>
          </div>
        </div>
        <div class="cutlab-delta">
          <p>1 of 7 metric families changed meaningfully.</p>
          <div class="cutlab-delta__line">
            <span class="cutlab-delta__sentence">cutting Sol Ring lowers keepable hand by 2.1%.</span>
            <span class="cutlab-delta__value cutlab-delta__value--down">
              <span aria-hidden="true">▼</span>
              <span class="cutlab-delta__value--down">2.1%</span>
            </span>
          </div>
        </div>
        <details data-cut-lab-delta-expander>
          <summary>Show full metric breakdown</summary>
          <div class="cutlab-delta"></div>
        </details>
        <div class="cutlab-proposal__actions">
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Sol Ring" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="accept" />
            <button type="submit" class="cutlab-decision-btn cutlab-decision-btn--accept" data-cut-lab-decision="accept" data-cut-lab-card="Sol Ring">Accept cut</button>
          </form>
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Sol Ring" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="reject" />
            <button type="submit" class="cutlab-decision-btn cutlab-decision-btn--reject" data-cut-lab-decision="reject" data-cut-lab-card="Sol Ring">Reject cut</button>
          </form>
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Sol Ring" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="defer" />
            <button type="submit" class="cutlab-decision-btn cutlab-decision-btn--defer" data-cut-lab-decision="defer" data-cut-lab-card="Sol Ring">Defer decision</button>
          </form>
        </div>
      </div>
    </section>
    <section class="result-panel">
      <details class="cutlab-cuts-made" open>
        <summary>Cuts made · 1 cards</summary>
        <div class="cutlab-cuts-made__row">
          <span>Mana Crypt</span>
          <span class="prompt-size-note">cut in Round 1 · Obvious cuts</span>
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Mana Crypt" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="restore" />
            <button type="submit" class="cutlab-restore-btn" data-cut-lab-restore data-cut-lab-card="Mana Crypt">Restore</button>
          </form>
        </div>
      </details>
    </section>
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const buildResponse = (overrides: Partial<CutLabDecideResponse> = {}): CutLabDecideResponse => ({
  cutLabStateJson: '{"version":2}',
  nextProposal: {
    isTerminal: false,
    isAtTarget: false,
    isNothingToCut: false,
    cardName: 'Arcane Signet',
    roundKey: 'round-2',
    roundLabel: 'Round 2 · Structural choices',
    roundBannerBody: 'Cards flagged by exactly one structural finding.',
    findingCount: 1,
    findingChips: ['Redundant finishers'],
  },
  proposalDeltas: {
    cardName: 'Arcane Signet',
    changedFamilyCount: 2,
    deltas: [
      {
        label: 'Keepable hand',
        delta: -2.1,
        direction: 'Down',
        isMeaningful: true,
        kind: 'KeepableHand',
        unit: 'Percent',
      },
      {
        label: 'Flood risk',
        delta: 0,
        direction: 'None',
        isMeaningful: false,
        kind: 'Flood',
        unit: 'Cards',
      },
    ],
  },
  floorWarnings: [{ message: 'Cutting Arcane Signet drops ramp to 9, below your floor of 10.' }],
  cardsRemaining: 11,
  cutsMade: [{ cardName: 'Sol Ring', roundKey: 'round-1', roundLabel: 'Round 1 · Obvious cuts', ordinal: 2 }],
  ...overrides,
});

describe('cut-lab proposal enhancement', () => {
  it('intercepts decision-form submit, posts JSON, and syncs every hidden state field from the response', async () => {
    buildDecisionFixture();
    const response = buildResponse();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => response,
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    const submitEvent = new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    });

    const dispatchResult = form?.dispatchEvent(submitEvent);
    await flushDecisionSubmit();

    expect(dispatchResult).toBe(false);
    expect(submitEvent.defaultPrevented).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith('/api/cut-lab/decide', expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({
        'Content-Type': 'application/json',
      }),
      body: JSON.stringify({
        cutLabStateJson: '{"version":1}',
        cardName: 'Sol Ring',
        decision: 'accept',
      }),
    }));
    expect(Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).map(input => input.value)).toEqual([
      '{"version":2}',
      '{"version":2}',
      '{"version":2}',
      '{"version":2}',
      '{"version":2}',
    ]);
  });

  it('patches the sticky bar, round banner, and proposal card in place after a successful decision', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildResponse(),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-round]')?.textContent).toBe('Round 2 · Structural choices');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-remaining]')?.textContent).toBe('11 to cut');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-accepted]')?.textContent).toBe('1 cut so far');
    expect(document.querySelector<HTMLElement>('.cutlab-round-banner .cutlab-finding__heading')?.textContent).toBe('Round 2 · Structural choices');
    expect(document.querySelector<HTMLElement>('.cutlab-round-banner p:last-child')?.textContent).toBe('Cards flagged by exactly one structural finding.');
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Arcane Signet');
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__evidence p')?.textContent).toBe('Flagged by 1 findings:');
    expect(document.querySelectorAll('.cutlab-delta__line')).toHaveLength(3);
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__floor-warning .cutlab-finding__lead')?.textContent).toBe('Cutting Arcane Signet drops ramp to 9, below your floor of 10.');
    expect(document.querySelector<HTMLDetailsElement>('details.cutlab-cuts-made summary')?.textContent).toBe('Cuts made · 1 card');
  });

  it('re-enables buttons and renders the neutral error line on a non-OK response without mutating state', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: false,
      json: async () => ({ message: 'Could not apply decision.' }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    const originalStateValues = Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).map(input => input.value);

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(button?.disabled).toBe(false);
    expect(document.querySelector<HTMLDivElement>('.cutlab-proposal')?.getAttribute('aria-busy')).toBeNull();
    expect(document.querySelector<HTMLElement>('[data-cut-lab-decision-error]')?.textContent).toBe('Could not apply decision.');
    expect(Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).map(input => input.value)).toEqual(originalStateValues);
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Sol Ring');
  });

  it('uses server-provided round banner copy and authoritative metric units when patching deltas', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildResponse({
        nextProposal: {
          ...buildResponse().nextProposal,
          roundBannerBody: 'Server banner copy wins.',
        },
        proposalDeltas: {
          cardName: 'Arcane Signet',
          changedFamilyCount: 1,
          deltas: [
            {
              label: 'Screw',
              delta: 2.5,
              direction: 'Up',
              isMeaningful: true,
              kind: 'Screw',
              unit: 'Percent',
            },
          ],
        },
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('.cutlab-round-banner p:last-child')?.textContent).toBe('Server banner copy wins.');
    expect(document.querySelector<HTMLElement>('.cutlab-delta__sentence')?.textContent).toContain('2.5%');
    expect(document.querySelector<HTMLElement>('.cutlab-delta__value span:last-child')?.textContent).toBe('2.5%');
  });

  it('posts restore decisions and removes the row while updating sticky counts on success', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildResponse({
        cutLabStateJson: '{"version":3}',
        cardsRemaining: 12,
        cutsMade: [],
      }),
    });

    const forms = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const restoreForm = forms[3];
    const restoreButton = restoreForm.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');

    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(fetchMock).toHaveBeenCalledWith('/api/cut-lab/decide', expect.objectContaining({
      body: JSON.stringify({
        cutLabStateJson: '{"version":1}',
        cardName: 'Mana Crypt',
        decision: 'restore',
      }),
    }));
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-remaining]')?.textContent).toBe('12 to cut');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-accepted]')?.textContent).toBe('0 cuts so far');
    expect(document.querySelector('details.cutlab-cuts-made')).toBeNull();
  });

  it('pluralizes sticky and cuts-made wording for many counts', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildResponse({
        cardsRemaining: 10,
        cutsMade: [
          { cardName: 'Sol Ring', roundKey: 'round-1', roundLabel: 'Round 1 · Obvious cuts', ordinal: 2 },
          { cardName: 'Mana Crypt', roundKey: 'round-1', roundLabel: 'Round 1 · Obvious cuts', ordinal: 1 },
        ],
      }),
    });

    const acceptButton = document.querySelector<HTMLButtonElement>('.cutlab-proposal [data-cut-lab-decision="accept"]');
    const acceptForm = acceptButton?.closest('form');
    acceptForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-accepted]')?.textContent).toBe('2 cuts so far');
    expect(document.querySelector<HTMLDetailsElement>('details.cutlab-cuts-made summary')?.textContent).toBe('Cuts made · 2 cards');
  });

  it('renders restore errors beside the cuts-made list and keeps the row intact on a non-OK response', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: false,
      json: async () => ({}),
    });

    const restoreForm = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]')[3];
    const restoreButton = restoreForm.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');

    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    const cutsMade = document.querySelector<HTMLDetailsElement>('details.cutlab-cuts-made');
    expect(cutsMade?.querySelector<HTMLElement>('[data-cut-lab-decision-error]')?.textContent).toBe("Couldn't recalculate this cut — nothing changed. Try again.");
    expect(cutsMade?.querySelectorAll('.cutlab-cuts-made__row')).toHaveLength(1);
    expect(cutsMade?.textContent).toContain('Mana Crypt');
  });

  it('blocks cross-form submissions while any decision request is in flight', async () => {
    buildDecisionFixture();
    const pendingResponse = deferred<{ ok: boolean; json: () => Promise<CutLabDecideResponse> }>();
    fetchMock.mockReturnValue(pendingResponse.promise);

    const forms = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const acceptForm = forms[0];
    const acceptButton = acceptForm.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    const restoreForm = forms[3];
    const restoreButton = restoreForm.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');

    acceptForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    const allButtons = Array.from(document.querySelectorAll<HTMLButtonElement>('button[type="submit"]'));
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(allButtons.every(button => button.disabled)).toBe(true);

    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);

    pendingResponse.resolve({
      ok: true,
      json: async () => buildResponse(),
    });
    await flushDecisionSubmit();

    expect(Array.from(document.querySelectorAll<HTMLButtonElement>('button[type="submit"]')).every(button => !button.disabled)).toBe(true);
  });

  it('hides the sticky bar when a terminal response arrives', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildResponse({
        nextProposal: {
          isTerminal: true,
          isAtTarget: true,
          isNothingToCut: false,
          cardName: '',
          roundKey: '',
          roundLabel: '',
          findingCount: 0,
          findingChips: [],
        },
        proposalDeltas: null,
        floorWarnings: [],
        cardsRemaining: 0,
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector('.cutlab-sticky-bar')).toBeNull();
  });

  it('shows restore confirmation copy with the card name and clears it after the next successful patch', async () => {
    buildDecisionFixture();
    fetchMock
      .mockResolvedValueOnce({
        ok: true,
        json: async () => buildResponse({
          cutLabStateJson: '{"version":3}',
          cardsRemaining: 12,
          cutsMade: [],
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => buildResponse(),
      });

    const forms = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const restoreForm = forms[3];
    const restoreButton = restoreForm.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');

    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-restore-confirmation]')?.textContent).toBe('Mana Crypt restored — metrics recalculating…');

    const acceptForm = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const acceptButton = acceptForm?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    acceptForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector('[data-cut-lab-restore-confirmation]')).toBeNull();
  });
});

import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabPatchResponse {
  patch?: {
    cutLabStateJson: string;
    currentCount: number;
    canBuildExport: boolean;
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
    proposalDeltas: null;
    floorWarnings: Array<{ message: string }>;
    cardsRemaining: number;
    cutsMade: Array<{ cardName: string; roundKey: string; roundLabel: string; ordinal: number }>;
    comboBadgeByCardName: Record<string, { badgeState: 'CompletePiece' | 'NeedsPartner'; context: string }>;
    structuralFindings: Array<{
      kind: string;
      heading: string;
      items: Array<{
        kind: string;
        heading: string;
        lead: string;
        evidence: string[];
      }>;
    }>;
    comboDataAvailable: boolean;
    categoryDataAvailable: boolean;
    whatifCardOutOptions: string[];
    whatifCardInOptions: string[];
    quantityTuners: Array<unknown>;
    addableBasics: string[];
  } | null;
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

const buildFixture = (): void => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab" data-cut-lab-decide-action="/cut-lab/decide" data-cut-lab-decide-api="/api/cut-lab/decide">
      <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
      <textarea name="PrimaryPlan">Keep the interaction dense.</textarea>
      <textarea name="SecondaryPlan"></textarea>
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <table>
        <tbody>
          <tr data-cut-lab-card="Counterspell" data-cut-lab-type-line="Instant" data-cut-lab-role="interaction" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Counterspell" /></td>
            <td data-label="Card">
              <strong>1 × Counterspell</strong>
              <details class="cutlab-card-text" open>
                <summary class="cutlab-card-text__summary">Card text</summary>
                <div class="cutlab-card-text__body">
                  <p class="cutlab-card-text__meta">Instant · {U}{U} · TMP #55</p>
                  <p class="cutlab-card-text__oracle">Counter target spell.</p>
                  <p class="cutlab-card-text__combo">Infinite cards</p>
                </div>
              </details>
            </td>
            <td data-label="Package">
              <select data-cut-lab-package-card="Counterspell"><option value="">Unlocked pool</option></select>
            </td>
          </tr>
        </tbody>
      </table>
    </form>
    <section class="result-panel">
      <div class="cutlab-proposal" data-cut-lab-card="Sol Ring" data-cut-lab-round="round-1">
        <div class="cutlab-proposal__actions">
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Sol Ring" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="accept" />
            <button type="submit" data-cut-lab-decision="accept" data-cut-lab-card="Sol Ring">Accept cut</button>
          </form>
        </div>
      </div>
    </section>
    <section class="result-panel" data-cut-lab-structural-findings>
      <div class="panel-heading">
        <div><h2>Structural findings</h2></div>
        <div data-cut-lab-findings-count-slot><span class="cutlab-findings-count"></span></div>
      </div>
      <div data-cut-lab-structural-findings-body>
        <p>Old body</p>
      </div>
      <p class="cutlab-degradation-note hidden" data-cut-lab-degradation="combo">Combo data unavailable right now.</p>
      <p class="cutlab-degradation-note hidden" data-cut-lab-degradation="category">Community category data unavailable.</p>
    </section>
    <div class="cutlab-sticky-bar">
      <span data-cut-lab-sticky-round>Round 1</span>
      <span data-cut-lab-sticky-remaining>12 to cut</span>
      <span data-cut-lab-sticky-accepted>0 cut so far</span>
    </div>
    <div class="cutlab-round-banner"></div>
    <button type="button" id="cut-lab-step-tab-4" class="is-disabled" disabled aria-disabled="true">Export</button>
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const buildPatch = (comboContext = 'Infinite cards'): CutLabPatchResponse => ({
  patch: {
    cutLabStateJson: '{"version":2}',
    currentCount: 111,
    canBuildExport: false,
    nextProposal: {
      isTerminal: false,
      isAtTarget: false,
      isNothingToCut: false,
      cardName: 'Arcane Signet',
      roundKey: 'round-2',
      roundLabel: 'Round 2 · Structural choices',
      roundBannerBody: 'Cards flagged by exactly one structural finding.',
      findingCount: 1,
      findingChips: ['Weak floor cases'],
    },
    proposalDeltas: null,
    floorWarnings: [],
    cardsRemaining: 11,
    cutsMade: [],
    comboBadgeByCardName: {
      Counterspell: {
        badgeState: 'CompletePiece',
        context: comboContext,
      },
    },
    structuralFindings: [
      {
        kind: 'WeakFloorCase',
        heading: 'Weak floor cases',
        items: [
          {
            kind: 'WeakFloorCase',
            heading: 'Weak floor cases',
            lead: 'Interaction is below the floor.',
            evidence: ['Counterspell'],
          },
        ],
      },
    ],
    comboDataAvailable: true,
    categoryDataAvailable: true,
    whatifCardOutOptions: [],
    whatifCardInOptions: [],
    quantityTuners: [],
    addableBasics: [],
  },
});

describe('cut-lab structural card text patching', () => {
  it('re-attaches the pool-row card text disclosure under a rebuilt structural evidence chip after decide', async () => {
    buildFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildPatch(),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    const rebuiltChip = document.querySelector<HTMLButtonElement>('[data-cut-lab-structural-findings-body] button[data-cut-lab-chip-card="Counterspell"]');
    const rebuiltDisclosure = document.querySelector<HTMLDetailsElement>('[data-cut-lab-structural-findings-body] .cutlab-card-text');
    const rebuiltBadge = rebuiltChip?.querySelector<HTMLSpanElement>('.cutlab-combo-badge');
    expect(rebuiltChip?.childNodes[0]?.textContent).toBe('Counterspell');
    expect(rebuiltBadge?.textContent).toBe('Combo piece');
    expect(rebuiltDisclosure).not.toBeNull();
    expect(rebuiltDisclosure?.open).toBe(false);
    expect(rebuiltDisclosure?.querySelector('.cutlab-card-text__oracle')?.textContent).toBe('Counter target spell.');
    expect(rebuiltDisclosure?.querySelector('.cutlab-card-text__combo')?.textContent).toBe('Infinite cards');
  });

  it('refreshes the cloned disclosure combo context from the patch map when the combo context changes', async () => {
    buildFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildPatch('Infinite mana'),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    const rebuiltDisclosure = document.querySelector<HTMLDetailsElement>('[data-cut-lab-structural-findings-body] .cutlab-card-text');
    expect(rebuiltDisclosure?.querySelector('.cutlab-card-text__combo')?.textContent).toBe('Infinite mana');
  });
});

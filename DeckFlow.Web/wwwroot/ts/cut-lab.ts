interface CutLabPoolSnapshotCard {
  name: string;
  quantity: number;
  typeLine: string;
  isCommander: boolean;
  isLocked: boolean;
  packageId: string | null;
}

interface CutLabPackageSnapshot {
  id: string;
  name: string;
  locked: boolean;
}

interface CutLabIntentSnapshot {
  primaryPlan: string;
  secondaryPlan: string | null;
  bracket: number | null;
  playExperience: string;
  includeSideboard: boolean;
  includeMaybeboard: boolean;
}

interface CutLabRoleFloorSnapshot {
  role: string;
  floor: number;
  isUserSet: boolean;
}

interface CutLabQuantityAdjustmentSnapshot {
  name: string;
  delta: number;
  isAddedBasic?: boolean;
}

interface CutLabStateSnapshot {
  commander: string;
  pool: CutLabPoolSnapshotCard[];
  packages: CutLabPackageSnapshot[];
  decisions?: CutLabStateDecision[];
  quantityAdjustments?: CutLabQuantityAdjustmentSnapshot[];
  baselineSnapshot?: unknown;
  intent: CutLabIntentSnapshot;
  roleFloors: CutLabRoleFloorSnapshot[];
  goals: {
    commanderByTurn: number;
    engineByTurn: number;
    representativeLineByTurn: number;
  };
}

interface CutLabStateDecision {
  cardName: string;
  kind: 'Accepted' | 'Rejected' | 'Deferred' | 0 | 1 | 2;
  round: string;
  ordinal: number;
}

interface CutLabCardTextEntry {
  typeLine?: string;
  manaCost?: string;
  setCode?: string;
  collectorNumber?: string;
  oracleText?: string;
  comboContext?: string;
}

interface Window {
  DeckFlow?: {
    attachDfSelect?: () => void;
    refreshDfSelect?: (select: HTMLSelectElement) => void;
  };
}

type CutLabDecisionAction = 'accept' | 'reject' | 'defer' | 'restore';
type CutLabMetricDirection = 'Up' | 'Down' | 'None';
type CutLabMetricUnit = 'Percent' | 'Cards';
type CutLabMetricKind =
  | 'CommanderOnTime'
  | 'KeepableHand'
  | 'ManaColorReliability'
  | 'EarlyInteraction'
  | 'PlanPresence'
  | 'CommanderByTurn'
  | 'EngineByTurn'
  | 'RepresentativeLineByTurn'
  | 'Flood'
  | 'Screw'
  | 'Curve';

interface CutLabDecisionNextProposal {
  isTerminal: boolean;
  isAtTarget: boolean;
  isNothingToCut: boolean;
  cardName: string;
  roundKey: string;
  roundLabel: string;
  roundBannerBody: string;
  findingCount: number;
  findingChips: string[];
}

interface CutLabDecisionMetricDelta {
  kind: CutLabMetricKind;
  label: string;
  before: number;
  after: number;
  delta: number;
  unit: CutLabMetricUnit;
  direction: CutLabMetricDirection;
  isMeaningful: boolean;
}

interface CutLabDecisionProposalDeltas {
  cardName: string;
  changedFamilyCount: number;
  deltas: CutLabDecisionMetricDelta[];
}

interface CutLabDecisionFloorWarning {
  role: string;
  newCount: number;
  floor: number;
  message: string;
}

interface CutLabDecisionCutRecord {
  cardName: string;
  roundKey: string;
  roundLabel: string;
  ordinal: number;
}

interface CutLabDecisionFinding {
  kind: string;
  heading: string;
  lead: string;
  evidence: string[];
}

interface CutLabDecisionFindingGroup {
  kind: string;
  heading: string;
  items: CutLabDecisionFinding[];
}

interface CutLabQuantityTunerRow {
  cardName: string;
  currentQuantity: number;
  legalMax: number;
  removeDisabled: boolean;
  addDisabled: boolean;
  isLockedOrCommander: boolean;
  isVisible: boolean;
  roleLabel: string;
  isLegalMultiple: boolean;
  isAddedBasic: boolean;
}

interface CutLabUiPatch {
  cutLabStateJson: string;
  currentCount: number;
  cardsRemaining: number;
  canBuildExport: boolean;
  nextProposal: CutLabDecisionNextProposal;
  proposalDeltas: CutLabDecisionProposalDeltas | null;
  floorWarnings: CutLabDecisionFloorWarning[];
  cutsMade: CutLabDecisionCutRecord[];
  structuralFindings: CutLabDecisionFindingGroup[];
  comboBadgeByCardName: Record<string, { badgeState: 'CompletePiece' | 'NeedsPartner'; context: string }>;
  comboDataAvailable: boolean;
  categoryDataAvailable: boolean;
  whatifCardOutOptions: string[];
  whatifCardInOptions: string[];
  quantityTuners: CutLabQuantityTunerRow[];
  addableBasics: string[];
}

interface CutLabPatchResponse {
  patch?: CutLabUiPatch | null;
}

interface ScenarioIndexEntry {
  id: string;
  name: string;
  savedAt: string;
}

type SaveScenarioResult = 'ok' | 'invalid' | 'cap-reached' | 'quota-exceeded' | 'disabled';

interface CutLabWhatifResponse extends CutLabPatchResponse {
  cutLabStateJson: string | null;
  cardOut: string;
  cardIn: string;
  deltas: CutLabDecisionMetricDelta[];
  changedFamilyCount: number;
}

type PackageCheckboxState = 'checked' | 'unchecked' | 'indeterminate';

interface CutLabFloorDomRow {
  row: HTMLTableRowElement;
  input: HTMLInputElement;
}

interface CutLabSubtypeEntry {
  name: string;
  typeLine: string;
  isLocked: boolean;
  isCommander: boolean;
}

interface CutLabApi {
  computePackageCheckboxState(memberLocked: boolean[]): PackageCheckboxState;
  hasRoleToken(roleList: string | null | undefined, role: string): boolean;
  isLandRole(roleList: string | null | undefined): boolean;
  filterPoolBySubtype(entries: CutLabSubtypeEntry[], query: string): CutLabSubtypeEntry[];
  buildCutLabStateJson(snapshot: CutLabStateSnapshot): string;
  syncDecisionState(serializedState: string): void;
  saveScenario(name: string, stateJson: string): SaveScenarioResult;
  listScenarios(): ScenarioIndexEntry[];
  loadScenario(id: string): string | null;
  deleteScenario(id: string): boolean;
}

interface CutLabRoot {
  DeckFlowCutLab?: CutLabApi;
  DeckFlow?: {
    clearLastDeck?: () => void;
  };
}

interface PendingNewPackageTarget {
  select: HTMLSelectElement;
}

const newPackageOptionValue = '__new__';
const unlockedPoolOptionValue = '';
const cutLabAntiForgeryFieldName = '__RequestVerificationToken';
const cutLabAdjustApiEndpoint = '/api/cut-lab/adjust';
const cutLabRestartRoundsApiEndpoint = '/api/cut-lab/restart-rounds';
const cutLabWhatifApiEndpoint = '/api/cut-lab/whatif';
const cutLabWhatifCommitApiEndpoint = '/api/cut-lab/whatif/commit';
const cutLabDecisionTimeoutMs = 20000;
const cutLabDecisionBusyCopy = 'Recalculating…';
const cutLabDecisionErrorCopy = "Couldn't recalculate this cut — nothing changed. Try again.";
const cutLabDecisionTimeoutCopy = 'This is taking longer than expected. Try again in a moment.';
const cutLabWhatifPreviewErrorCopy = "Couldn't preview this swap — nothing changed. Try again.";
const cutLabWhatifKeepErrorCopy = "Couldn't keep this swap — nothing changed. Try again.";
const cutLabWhatifPreviewSummaryCopy = 'metric families changed meaningfully.';
const SCENARIO_INDEX_KEY = 'deckflow.cutlab.scenario-index';
const SCENARIO_SLOT_PREFIX = 'deckflow.cutlab.scenario.';
const CUT_LAB_SECTION_STORAGE_KEY = 'deckflow.cutlab.sections';
const MAX_SCENARIO_SLOTS = 20;

const formatCountLabel = (count: number, singular: string, plural: string): string =>
  count === 1 ? `1 ${singular}` : `${count} ${plural}`;

const formatCutsMadeCount = (count: number): string => formatCountLabel(count, 'card', 'cards');

const formatCutsAcceptedSoFar = (count: number): string => `${formatCountLabel(count, 'cut', 'cuts')} so far`;

const cutLabExportCountReadyCopy = '✅ Card count = 100';

const cutLabExportCountLockedHelperCopy = 'Reach 100 cards to unlock the finished-list export.';

const formatCutLabExportCount = (currentCount: number): string =>
  currentCount === 100 ? cutLabExportCountReadyCopy : `❌ Card count = ${currentCount}`;

const formatStructuralFindingsCount = (count: number): string => formatCountLabel(count, 'structural finding', 'structural findings');

(function (root: CutLabRoot): void {
  const normalizeAsciiCase = (value: string): string =>
    value.replace(/[A-Z]/g, character => String.fromCharCode(character.charCodeAt(0) + 32));

  const frontFaceTypeLine = (typeLine: string): string =>
    typeLine.split('//')[0]?.trim() ?? '';

  const getScenarioSlotKey = (id: string): string => `${SCENARIO_SLOT_PREFIX}${id}`;
  const defaultMobileCollapsedSectionIds = new Set<string>([
    'cut-lab-section-packages',
    'cut-lab-section-scenarios',
    'cut-lab-section-whatif',
  ]);

  const collapseMobileCollapsiblesOnLoad = (): void => {
    if (typeof window.matchMedia !== 'function' || !window.matchMedia('(max-width: 767px)').matches) {
      return;
    }

    document
      .querySelectorAll<HTMLDetailsElement>('details[data-cutlab-mobile-collapse]')
      .forEach(details => {
        if (!defaultMobileCollapsedSectionIds.has(details.id)) {
          return;
        }

        details.removeAttribute('open');
      });
  };

  const getLocalStorage = (): Storage | null => {
    try {
      return window.localStorage;
    } catch {
      return null;
    }
  };

  const isQuotaExceededError = (error: unknown): boolean =>
    error instanceof DOMException && (error.name === 'QuotaExceededError' || error.code === 22);

  const parseJsonStringArray = (value: string): string[] | null => {
    try {
      const parsed = JSON.parse(value) as unknown;
      if (!Array.isArray(parsed)) {
        return null;
      }

      // Type-guard to strings only; dedupeIds owns trimming/empty-filtering/dedupe.
      const ids: string[] = [];
      parsed.forEach(item => {
        if (typeof item === 'string') {
          ids.push(item);
        }
      });
      return ids;
    } catch {
      return null;
    }
  };

  const dedupeIds = (ids: string[]): string[] => {
    const seen = new Set<string>();
    const ordered: string[] = [];

    ids.forEach(id => {
      const normalized = id.trim();
      if (normalized === '' || seen.has(normalized)) {
        return;
      }

      seen.add(normalized);
      ordered.push(normalized);
    });

    return ordered;
  };

  const getSectionCollapsibles = (): HTMLDetailsElement[] =>
    Array.from(document.querySelectorAll<HTMLDetailsElement>('details[data-cutlab-mobile-collapse]'))
      .filter(details => details.id.trim().length > 0);

  const readCollapsedSectionIds = (): string[] | null => {
    try {
      const storage = getLocalStorage();
      if (!storage) {
        return null;
      }

      const raw = storage.getItem(CUT_LAB_SECTION_STORAGE_KEY);
      if (raw === null) {
        return null;
      }

      const parsedIds = parseJsonStringArray(raw);
      if (parsedIds === null) {
        return null;
      }

      return dedupeIds(parsedIds);
    } catch {
      // Any storage/parse failure falls open to defaults.
      return null;
    }
  };

  const writeCollapsedSectionIds = (collapsedIds: string[]): void => {
    const storage = getLocalStorage();
    if (!storage) {
      return;
    }

    try {
      storage.setItem(CUT_LAB_SECTION_STORAGE_KEY, JSON.stringify(dedupeIds(collapsedIds)));
    } catch {
      // localStorage unavailable/quota-exceeded — persistence is best-effort, non-fatal.
    }
  };

  const restoreSectionCollapseState = (): void => {
    const collapsedIds = readCollapsedSectionIds();
    if (collapsedIds === null) {
      return;
    }

    const collapsedIdSet = new Set(collapsedIds);
    getSectionCollapsibles().forEach(details => {
      if (collapsedIdSet.has(details.id)) {
        details.removeAttribute('open');
        return;
      }

      details.setAttribute('open', 'open');
    });
  };

  const persistSectionCollapseState = (): void => {
    const collapsedIds = getSectionCollapsibles()
      .filter(details => !details.open)
      .map(details => details.id);
    writeCollapsedSectionIds(collapsedIds);
  };

  const attachSectionCollapsePersistence = (): void => {
    getSectionCollapsibles().forEach(details => {
      details.addEventListener('toggle', persistSectionCollapseState);
    });
  };

  const expandJumpTarget = (target: HTMLElement): void => {
    if (!(target instanceof HTMLDetailsElement)) {
      return;
    }

    if (!target.matches('details[data-cutlab-mobile-collapse]') || target.open) {
      return;
    }

    target.open = true;
    // Persist explicitly: the details 'toggle' event is not reliably dispatched
    // synchronously across environments, so do not rely on the toggle listener
    // to capture a jump-driven expand.
    persistSectionCollapseState();
  };

  const focusJumpTarget = (target: HTMLElement): void => {
    target.setAttribute('tabindex', '-1');
    target.focus({ preventScroll: true });
  };

  const scrollJumpTargetIntoView = (target: HTMLElement): void => {
    if (typeof target.scrollIntoView !== 'function') {
      return;
    }

    const prefersReduced = typeof window.matchMedia === 'function'
      && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    target.scrollIntoView({ behavior: prefersReduced ? 'auto' : 'smooth', block: 'start' });
  };

  const attachAnchorNavHandler = (): void => {
    const anchorNav = document.querySelector<HTMLElement>('.cutlab-anchor-nav');
    if (!anchorNav) {
      return;
    }

    anchorNav.querySelectorAll<HTMLAnchorElement>('a[href^="#"]').forEach(link => {
      link.addEventListener('click', event => {
        const hash = link.getAttribute('href');
        if (!hash || hash === '#') {
          return;
        }

        const target = document.getElementById(hash.slice(1));
        if (!(target instanceof HTMLElement)) {
          return;
        }

        event.preventDefault();
        expandJumpTarget(target);
        scrollJumpTargetIntoView(target);
        focusJumpTarget(target);
        if (window.location.hash !== hash) {
          history.pushState(null, '', hash);
        }
      });
    });
  };

  const readScenarioIndex = (): ScenarioIndexEntry[] => {
    try {
      const storage = getLocalStorage();
      if (!storage) {
        return [];
      }

      const raw = storage.getItem(SCENARIO_INDEX_KEY);
      if (!raw) {
        return [];
      }

      const parsed = JSON.parse(raw) as unknown;
      if (!Array.isArray(parsed)) {
        return [];
      }

      return parsed
        .filter((entry): entry is Partial<ScenarioIndexEntry> => !!entry && typeof entry === 'object')
        .filter((entry): entry is ScenarioIndexEntry =>
          typeof entry.id === 'string'
          && typeof entry.name === 'string'
          && typeof entry.savedAt === 'string')
        .map(entry => ({
          id: entry.id,
          name: entry.name,
          savedAt: entry.savedAt,
        }));
    } catch {
      return [];
    }
  };

  const newScenarioId = (): string =>
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : `s-${Date.now()}-${Math.random().toString(36).slice(2)}`;

  const api: CutLabApi = {
    computePackageCheckboxState(memberLocked: boolean[]): PackageCheckboxState {
      if (memberLocked.length === 0) {
        return 'unchecked';
      }

      const allLocked = memberLocked.every(value => value);
      if (allLocked) {
        return 'checked';
      }

      const allUnlocked = memberLocked.every(value => !value);
      return allUnlocked ? 'unchecked' : 'indeterminate';
    },

    hasRoleToken(roleList: string | null | undefined, role: string): boolean {
      const normalizedRole = role.trim().toLowerCase();
      if (normalizedRole === '') {
        return false;
      }

      return (roleList ?? '')
        .split(/\s+/)
        .map(token => token.trim().toLowerCase())
        .filter(token => token !== '')
        .includes(normalizedRole);
    },

    isLandRole(roleList: string | null | undefined): boolean {
      return api.hasRoleToken(roleList, 'lands');
    },

    filterPoolBySubtype(entries: CutLabSubtypeEntry[], query: string): CutLabSubtypeEntry[] {
      const normalizedQuery = normalizeAsciiCase(query.trim());
      if (normalizedQuery === '') {
        return [];
      }

      return entries.filter(entry =>
        normalizeAsciiCase(frontFaceTypeLine(entry.typeLine)).includes(normalizedQuery));
    },

    buildCutLabStateJson(snapshot: CutLabStateSnapshot): string {
      const normalizedSnapshot: CutLabStateSnapshot = {
        commander: snapshot.commander,
        pool: snapshot.pool.map(card => ({
          name: card.name,
          quantity: card.quantity,
          typeLine: card.typeLine,
          isCommander: card.isCommander,
          isLocked: card.isCommander ? true : card.isLocked,
          packageId: card.packageId,
        })),
        packages: snapshot.packages.map(pkg => ({
          id: pkg.id,
          name: pkg.name,
          locked: pkg.locked,
        })),
        decisions: snapshot.decisions ?? [],
        baselineSnapshot: snapshot.baselineSnapshot,
        intent: {
          primaryPlan: snapshot.intent.primaryPlan,
          secondaryPlan: snapshot.intent.secondaryPlan,
          bracket: snapshot.intent.bracket,
          playExperience: snapshot.intent.playExperience,
          includeSideboard: snapshot.intent.includeSideboard,
          includeMaybeboard: snapshot.intent.includeMaybeboard,
        },
        roleFloors: snapshot.roleFloors
          .filter(row => row.isUserSet)
          .map(row => ({
            role: row.role,
            floor: Math.trunc(row.floor),
            isUserSet: row.isUserSet,
          })),
        goals: {
          commanderByTurn: Math.trunc(snapshot.goals.commanderByTurn),
          engineByTurn: Math.trunc(snapshot.goals.engineByTurn),
          representativeLineByTurn: Math.trunc(snapshot.goals.representativeLineByTurn),
        },
      };

      if ((snapshot.quantityAdjustments ?? []).length > 0) {
        normalizedSnapshot.quantityAdjustments = snapshot.quantityAdjustments ?? [];
      }

      return JSON.stringify(normalizedSnapshot);
    },

    syncDecisionState(serializedState: string): void {
      writeDecisionStateToHiddenInputs(serializedState);
    },

    saveScenario(name: string, stateJson: string): SaveScenarioResult {
      const trimmedName = name.trim();
      if (trimmedName === '') {
        return 'invalid';
      }

      const storage = getLocalStorage();
      if (!storage) {
        return 'disabled';
      }

      const index = readScenarioIndex();
      if (index.length >= MAX_SCENARIO_SLOTS) {
        return 'cap-reached';
      }

      const entry: ScenarioIndexEntry = {
        id: newScenarioId(),
        name: trimmedName,
        savedAt: new Date().toISOString(),
      };

      try {
        storage.setItem(getScenarioSlotKey(entry.id), stateJson);
        storage.setItem(SCENARIO_INDEX_KEY, JSON.stringify([...index, entry]));
        return 'ok';
      } catch (error) {
        try {
          storage.removeItem(getScenarioSlotKey(entry.id));
        } catch {
          // localStorage may be disabled or quota-limited; skip persistence silently.
        }

        return isQuotaExceededError(error) ? 'quota-exceeded' : 'disabled';
      }
    },

    listScenarios(): ScenarioIndexEntry[] {
      return readScenarioIndex();
    },

    loadScenario(id: string): string | null {
      try {
        const storage = getLocalStorage();
        if (!storage) {
          return null;
        }

        return storage.getItem(getScenarioSlotKey(id));
      } catch {
        return null;
      }
    },

    deleteScenario(id: string): boolean {
      try {
        const storage = getLocalStorage();
        if (!storage) {
          return false;
        }

        const index = readScenarioIndex();
        const nextIndex = index.filter(entry => entry.id !== id);
        if (nextIndex.length === index.length) {
          return false;
        }

        storage.removeItem(getScenarioSlotKey(id));
        storage.setItem(SCENARIO_INDEX_KEY, JSON.stringify(nextIndex));
        return true;
      } catch {
        return false;
      }
    },
  };

  root.DeckFlowCutLab = api;

  let pendingNewPackageTarget: PendingNewPackageTarget | null = null;
  let generatedPackageCounter = 0;
  let packageHandlersAttached = false;
  let decisionHandlersAttached = false;
  let restartRoundsHandlersAttached = false;
  let scenarioHandlersAttached = false;
  let whatifHandlersAttached = false;
  let decisionSubmitInFlight = false;
  let adjustSubmitInFlight = false;
  let whatifSubmitInFlight = false;
  let copyHandlersAttached = false;
  let cardModalHandlersAttached = false;
  let cardTextByCardNameCache: Record<string, CutLabCardTextEntry> | null = null;
  let activeModalCardName: string | null = null;

  const getForm = (): HTMLFormElement | null =>
    document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]');

  const getCutLabDecideAction = (): string =>
    getForm()?.dataset.cutLabDecideAction?.trim() || '/cut-lab/decide';

  const getCutLabDecideApi = (): string =>
    getForm()?.dataset.cutLabDecideApi?.trim() || '/api/cut-lab/decide';

  const getRestartRoundsForm = (): HTMLFormElement | null =>
    document.querySelector<HTMLFormElement>('form[data-cut-lab-restart-rounds-form]');

  const getCutLabRestartRoundsApi = (): string =>
    getRestartRoundsForm()?.dataset.cutLabRestartRoundsApi?.trim() || cutLabRestartRoundsApiEndpoint;

  const getStateInput = (form: HTMLFormElement): HTMLInputElement | null =>
    form.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]');

  const getDecisionStateInputs = (): HTMLInputElement[] =>
    Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]'));

  const getPoolRows = (): HTMLTableRowElement[] =>
    Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-cut-lab-card]'));

  const getSubtypeSearchInput = (): HTMLInputElement | null =>
    document.querySelector<HTMLInputElement>('input[data-cut-lab-subtype-search]');

  const getSubtypeResults = (): HTMLDivElement | null =>
    document.querySelector<HTMLDivElement>('[data-cut-lab-subtype-results]');

  const getCutRoundsSection = (): HTMLElement | null =>
    Array.from(document.querySelectorAll<HTMLElement>('section.result-panel'))
      .find(section => section.querySelector('.cutlab-proposal, .cutlab-sticky-bar, .cutlab-round-banner') !== null)
      ?? null;

  const getStickyRound = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-sticky-round]');

  const getStickyLocked = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-sticky-locked]');

  const getStickyCurrent = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-sticky-current]');

  const getStickyRemaining = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-sticky-remaining]');

  const getStickyAccepted = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-sticky-accepted]');

  const getExportStepTab = (): HTMLButtonElement | null =>
    document.getElementById('cut-lab-step-tab-4') as HTMLButtonElement | null;

  const getBuildExportSubmit = (): HTMLButtonElement | null =>
    document.querySelector<HTMLButtonElement>('#cut-lab-export-form button[type="submit"]');

  const getExportCountStatus = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-export-count]');

  const getRoundBanner = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('.cutlab-round-banner');

  const getProposalCard = (): HTMLDivElement | null =>
    document.querySelector<HTMLDivElement>('.cutlab-proposal');

  const getCutsMadeDetails = (): HTMLDetailsElement | null =>
    document.querySelector<HTMLDetailsElement>('details.cutlab-cuts-made');

  const getCutsMadeSection = (): HTMLElement | null => {
    const markedSection = document.querySelector<HTMLElement>('[data-cut-lab-cuts-made-section]');
    if (markedSection) {
      return markedSection;
    }

    const section = getCutsMadeDetails()?.closest<HTMLElement>('section.result-panel') ?? null;
    section?.setAttribute('data-cut-lab-cuts-made-section', 'true');
    return section;
  };

  const getStructuralFindingsSection = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-structural-findings]');

  const getStructuralFindingsBody = (): HTMLDivElement | null =>
    document.querySelector<HTMLDivElement>('[data-cut-lab-structural-findings-body]');

  const getErrorBanner = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('.error-banner');

  const getPackageContainers = (): HTMLDivElement[] =>
    Array.from(document.querySelectorAll<HTMLDivElement>('[data-cut-lab-package-id]'));

  const getRoleLockButtons = (): HTMLButtonElement[] =>
    Array.from(document.querySelectorAll<HTMLButtonElement>('[data-cut-lab-lock-role]'));

  const getRoleGroupLockedCount = (roleKey: string): HTMLElement | null =>
    document.querySelector<HTMLElement>(`[data-cut-lab-group-locked="${cssEscape(roleKey)}"]`);

  const getRoleGroupChips = (cardName: string): HTMLElement[] =>
    Array.from(document.querySelectorAll<HTMLElement>(`[data-cut-lab-chip-card="${cssEscape(cardName)}"]`));

  const getCardModal = (): HTMLDialogElement | null =>
    document.getElementById('cutlab-card-modal') as HTMLDialogElement | null;

  const getCardModalTitle = (): HTMLElement | null =>
    document.getElementById('cutlab-card-modal-title');

  const getCardModalMeta = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cutlab-modal-meta]');

  const getCardModalOracle = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cutlab-modal-oracle]');

  const getCardModalCombo = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cutlab-modal-combo]');

  const getCardModalLockButton = (): HTMLButtonElement | null =>
    document.querySelector<HTMLButtonElement>('[data-cutlab-modal-lock]');

  const getFloorRows = (): CutLabFloorDomRow[] =>
    Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-cut-lab-floor-row]'))
      .map(row => {
        const input = row.querySelector<HTMLInputElement>('input[data-cut-lab-floor]');
        return input ? { row, input } : null;
      })
      .filter((entry): entry is CutLabFloorDomRow => entry !== null);

  const getGoalInput = (goalKey: string): HTMLInputElement | null =>
    document.querySelector<HTMLInputElement>(`input[data-cut-lab-goal="${cssEscape(goalKey)}"]`);

  const getScenarioNameInput = (): HTMLInputElement | null =>
    document.querySelector<HTMLInputElement>('input[data-cut-lab-scenario-name]');

  const getScenarioList = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-scenario-list]');

  const getScenarioStatus = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-scenario-status]');

  const getSessionFileInput = (): HTMLInputElement | null =>
    document.querySelector<HTMLInputElement>('input[data-cut-lab-session-file]');

  const getWhatifForm = (): HTMLFormElement | null =>
    document.querySelector<HTMLFormElement>('form[data-cut-lab-whatif-form]');

  const getWhatifCardOutSelect = (): HTMLSelectElement | null =>
    document.querySelector<HTMLSelectElement>('select[data-cut-lab-whatif-card-out]');

  const getWhatifCardInSelect = (): HTMLSelectElement | null =>
    document.querySelector<HTMLSelectElement>('select[data-cut-lab-whatif-card-in]');

  const getWhatifPreviewContainer = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-whatif-preview]');

  const getWhatifSelection = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[data-cut-lab-whatif-selection]');

  const getWhatifDeltaBody = (): HTMLTableSectionElement | null =>
    document.querySelector<HTMLTableSectionElement>('[data-cut-lab-whatif-delta-body]');

  const getQuantityTunerSection = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('section.cutlab-tuner');

  const getQuantityTunerBody = (): HTMLTableSectionElement | null =>
    getQuantityTunerSection()?.querySelector<HTMLTableSectionElement>('tbody') ?? null;

  const getAddBasicForm = (): HTMLFormElement | null =>
    getQuantityTunerSection()?.querySelector<HTMLFormElement>('form.cutlab-tuner__add-basic[data-cut-lab-adjust-form]') ?? null;

  const getAddBasicSelect = (): HTMLSelectElement | null =>
    document.querySelector<HTMLSelectElement>('[data-cut-lab-add-basic-select]');

  const getWhatifPreviewButton = (): HTMLButtonElement | null =>
    document.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-preview-submit]');

  const getWhatifKeepButton = (): HTMLButtonElement | null =>
    document.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-keep-submit]');

  const getWhatifDiscardButton = (): HTMLButtonElement | null =>
    document.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-discard]');

  const getDeckInputSourceSelect = (): HTMLSelectElement | null =>
    document.querySelector<HTMLSelectElement>('#cut-lab-input-source');

  const getDeckUrlInput = (): HTMLInputElement | null =>
    document.querySelector<HTMLInputElement>('#cut-lab-deck-url');

  const getDeckTextInput = (): HTMLTextAreaElement | null =>
    document.querySelector<HTMLTextAreaElement>('#cut-lab-deck-text');

  const getLockCheckbox = (row: HTMLTableRowElement): HTMLInputElement | null =>
    row.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card]');

  const getPackageSelect = (row: HTMLTableRowElement): HTMLSelectElement | null =>
    row.querySelector<HTMLSelectElement>('select[data-cut-lab-package-card]');

  const getPoolFilterContainer = (): HTMLDivElement | null =>
    document.querySelector<HTMLDivElement>('#cut-lab-section-lock-pool .cutlab-pool-filter');

  const getPoolSearchInput = (): HTMLInputElement | null =>
    getPoolFilterContainer()?.querySelector<HTMLInputElement>('input.cutlab-pool-search') ?? null;

  const getPoolMatchCount = (): HTMLElement | null =>
    getPoolFilterContainer()?.querySelector<HTMLElement>('.cutlab-pool-match-count') ?? null;

  const getPoolEmptyRow = (): HTMLTableRowElement | null =>
    document.querySelector<HTMLTableRowElement>('#cut-lab-section-lock-pool tr.cutlab-pool-empty-row');

  const getPoolFilterRows = (): HTMLTableRowElement[] =>
    Array.from(document.querySelectorAll<HTMLTableRowElement>('#cut-lab-section-lock-pool tr[data-cut-lab-card]'));

  const getPackageToggle = (container: Element): HTMLInputElement | null =>
    container.querySelector<HTMLInputElement>('input[data-cut-lab-package-toggle]');

  const findPackageContainer = (packageId: string): HTMLDivElement | null =>
    document.querySelector<HTMLDivElement>(`[data-cut-lab-package-id="${cssEscape(packageId)}"]`);

  const getPackageName = (packageId: string): string => {
    const container = findPackageContainer(packageId);
    return container?.dataset.cutLabPackageName ?? packageId;
  };

  const cssEscape = (value: string): string => {
    if (typeof CSS !== 'undefined' && typeof CSS.escape === 'function') {
      return CSS.escape(value);
    }

    return value.replace(/["\\]/g, '\\$&');
  };

  const parseRowQuantity = (row: HTMLTableRowElement): number => {
    const datasetQuantity = row.dataset.cutLabQuantity?.trim() ?? '';
    if (datasetQuantity !== '') {
      const parsedQuantity = Number.parseInt(datasetQuantity, 10);
      if (!Number.isNaN(parsedQuantity)) {
        return parsedQuantity;
      }
    }

    const cardCell = row.querySelector<HTMLTableCellElement>('td[data-label="Card"] strong');
    const text = cardCell?.textContent?.trim() ?? '';
    const match = /^(\d+)\s×/.exec(text);
    if (!match) {
      return 1;
    }

    const parsed = Number.parseInt(match[1], 10);
    return Number.isNaN(parsed) ? 1 : parsed;
  };

  const updateLockedCountChip = (): void => {
    const rows = getPoolRows();
    const poolCount = rows.reduce((total, row) => total + parseRowQuantity(row), 0);
    const lockedCount = rows.reduce((total, row) => {
      const checkbox = getLockCheckbox(row);
      return checkbox?.checked ? total + parseRowQuantity(row) : total;
    }, 0);
    const summary = document.querySelector<HTMLElement>('[data-cut-lab-lock-count]');
    const stickyLocked = getStickyLocked();

    // Why: this chip mirrors the imported protected pool (commander-inclusive) and does not
    // re-sum after adjust-path quantity tuning; the sticky bar owns that live working-list total.
    if (summary) {
      summary.textContent = `${poolCount} cards in pool · ${lockedCount} locked`;
    }

    if (stickyLocked) {
      stickyLocked.textContent = `${lockedCount} locked`;
    }
  };

  const getSelectedPoolFilter = (): 'all' | 'locked' | 'unlocked' => {
    const selected = getPoolFilterContainer()
      ?.querySelector<HTMLInputElement>('input[name="CutLabPoolFilter"]:checked')
      ?.value;
    return selected === 'locked' || selected === 'unlocked' ? selected : 'all';
  };

  const updatePoolFilterState = (): void => {
    const filterContainer = getPoolFilterContainer();
    if (!filterContainer) {
      return;
    }

    const rows = getPoolFilterRows();
    const selectedFilter = getSelectedPoolFilter();
    const searchTerm = normalizeAsciiCase(getPoolSearchInput()?.value.trim() ?? '');
    let visibleCount = 0;

    rows.forEach(row => {
      const checkbox = getLockCheckbox(row);
      const cardName = normalizeAsciiCase(row.dataset.cutLabCard ?? '');
      const matchesLockedState = selectedFilter === 'all'
        || (selectedFilter === 'locked' && (checkbox?.checked ?? false))
        || (selectedFilter === 'unlocked' && !(checkbox?.checked ?? false));
      const matchesSearch = searchTerm === '' || cardName.includes(searchTerm);
      const isVisible = matchesLockedState && matchesSearch;
      row.hidden = !isVisible;
      if (isVisible) {
        visibleCount++;
      }
    });

    const totalCount = rows.length;
    const matchCount = getPoolMatchCount();
    if (matchCount) {
      matchCount.textContent = `Showing ${visibleCount} of ${totalCount} cards`;
    }

    const emptyRow = getPoolEmptyRow();
    if (emptyRow) {
      emptyRow.hidden = visibleCount !== 0;
    }
  };

  const parseIntegerAttribute = (element: HTMLElement, name: string, fallback: number): number => {
    const rawValue = element.dataset[name] ?? '';
    const parsed = Number.parseInt(rawValue, 10);
    return Number.isNaN(parsed) ? fallback : parsed;
  };

  const clampFloorValue = (input: HTMLInputElement): number => {
    const min = input.min === '' ? 0 : Number.parseInt(input.min, 10);
    const max = input.max === '' ? Number.MAX_SAFE_INTEGER : Number.parseInt(input.max, 10);
    const parsed = Number.parseInt(input.value, 10);
    const fallback = Number.isNaN(parsed) ? min : parsed;
    const clamped = Math.min(Math.max(fallback, min), max);
    input.value = `${clamped}`;
    return clamped;
  };

  const clampGoalValue = (input: HTMLInputElement, fallback: number): number => {
    const min = input.min === '' ? 1 : Number.parseInt(input.min, 10);
    const max = input.max === '' ? Number.MAX_SAFE_INTEGER : Number.parseInt(input.max, 10);
    const parsed = Number.parseInt(input.value, 10);
    const attributeValue = input.getAttribute('value')?.trim() ?? '';
    const attributeParsed = Number.parseInt(attributeValue, 10);
    const resolvedFallback = Number.isNaN(attributeParsed) ? fallback : attributeParsed;
    const normalized = Number.isNaN(parsed) ? resolvedFallback : parsed;
    const clamped = Math.min(Math.max(normalized, min), max);
    input.value = `${clamped}`;
    return clamped;
  };

  const tryReadSerializedState = (): Partial<CutLabStateSnapshot> | null => {
    const form = getForm();
    const stateInput = form ? getStateInput(form) : null;
    const raw = stateInput?.value.trim() ?? '';
    if (raw === '') {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as unknown;
      return parsed && typeof parsed === 'object' ? parsed as Partial<CutLabStateSnapshot> : null;
    } catch {
      return null;
    }
  };

  const setFloorUserSetState = (row: HTMLTableRowElement, isUserSet: boolean): void => {
    row.dataset.cutLabFloorUserSet = isUserSet ? 'true' : 'false';

    const defaultLabel = row.querySelector<HTMLElement>('[data-cut-lab-floor-source-default]');
    const adjustedBadge = row.querySelector<HTMLElement>('[data-cut-lab-floor-adjusted-badge]');
    const resetButton = row.querySelector<HTMLElement>('[data-cut-lab-floor-reset]');

    defaultLabel?.classList.toggle('hidden', isUserSet);
    adjustedBadge?.classList.toggle('hidden', !isUserSet);
    resetButton?.classList.toggle('hidden', !isUserSet);
  };

  const updateFloorRowMarker = (row: HTMLTableRowElement, floor: number): void => {
    const inPoolCount = parseIntegerAttribute(row, 'cutLabFloorCount', 0);
    const marker = row.querySelector<HTMLElement>('[data-cut-lab-floor-at-marker]');
    if (!marker) {
      return;
    }

    const atFloor = inPoolCount <= floor + 1;
    marker.classList.toggle('hidden', !atFloor);
    marker.textContent = '· at floor';
  };

  const setRoleLockButtonState = (button: HTMLButtonElement, isPressed: boolean): void => {
    button.classList.toggle('is-selected', isPressed);
    button.setAttribute('aria-pressed', isPressed ? 'true' : 'false');
  };

  const syncRoleLockButtons = (): void => {
    getRoleLockButtons().forEach(button => {
      const roleKey = button.dataset.cutLabLockRole ?? '';
      if (roleKey === '') {
        return;
      }

      const memberRows = getPoolRows().filter(row => api.hasRoleToken(row.dataset.cutLabRole, roleKey));
      const lockableMembers = memberRows
        .map(row => getLockCheckbox(row))
        .filter((checkbox): checkbox is HTMLInputElement => checkbox !== null && !checkbox.disabled);
      const allLocked = lockableMembers.length > 0 && lockableMembers.every(checkbox => checkbox.checked);
      setRoleLockButtonState(button, allLocked);
    });
  };

  const syncRoleGroupLockState = (): void => {
    const lockedCounts = new Map<string, number>();

    getPoolRows().forEach(row => {
      const cardName = row.dataset.cutLabCard ?? '';
      const checkbox = getLockCheckbox(row);
      const isLocked = checkbox?.checked ?? false;

      getRoleGroupChips(cardName).forEach(chip => {
        chip.classList.toggle('cutlab-role-chip--locked', isLocked);
        if (chip instanceof HTMLButtonElement) {
          chip.setAttribute('aria-pressed', isLocked ? 'true' : 'false');
        }
      });

      (row.dataset.cutLabRole ?? '')
        .split(/\s+/)
        .map(token => token.trim())
        .filter(token => token !== '')
        .forEach(roleKey => {
          const previous = lockedCounts.get(roleKey) ?? 0;
          lockedCounts.set(roleKey, previous + (isLocked ? parseRowQuantity(row) : 0));
        });
    });

    getRoleLockButtons().forEach(button => {
      const roleKey = button.dataset.cutLabLockRole ?? '';
      if (roleKey === '') {
        return;
      }

      const count = getRoleGroupLockedCount(roleKey);
      if (count) {
        count.textContent = `${lockedCounts.get(roleKey) ?? 0}`;
      }
    });
  };

  const buildSnapshotFromDom = (): CutLabStateSnapshot => {
    const rows = getPoolRows();
    const persistedState = tryReadSerializedState();
    const selectedCommander = document.querySelector<HTMLSelectElement>('select[name="SelectedCommander"]');
    const commanderFromRow = rows.find(row => row.dataset.cutLabCommander === 'true')?.dataset.cutLabCard ?? '';
    const bracketInput = document.querySelector<HTMLInputElement>('input[name="Bracket"]:checked');
    const bracketValue = bracketInput?.value.trim() ?? '';
    const secondaryPlan = readNamedFieldValue('SecondaryPlan');

    return {
      commander: commanderFromRow || selectedCommander?.value.trim() || '',
      pool: rows.map(row => {
        const checkbox = getLockCheckbox(row);
        const select = getPackageSelect(row);

        return {
          name: row.dataset.cutLabCard ?? '',
          quantity: parseRowQuantity(row),
          typeLine: row.dataset.cutLabTypeLine ?? '',
          isCommander: row.dataset.cutLabCommander === 'true',
          isLocked: checkbox?.checked ?? false,
          packageId: normalizePackageId(select?.value ?? ''),
        };
      }),
      packages: getPackageContainers().map(container => {
        const toggle = getPackageToggle(container);
        return {
          id: container.dataset.cutLabPackageId ?? '',
          name: container.dataset.cutLabPackageName ?? '',
          locked: toggle?.checked ?? false,
        };
      }),
      decisions: Array.isArray(persistedState?.decisions) ? persistedState.decisions : [],
      quantityAdjustments: Array.isArray(persistedState?.quantityAdjustments) ? persistedState.quantityAdjustments : [],
      baselineSnapshot: persistedState?.baselineSnapshot,
      intent: {
        primaryPlan: readNamedFieldValue('PrimaryPlan'),
        secondaryPlan: secondaryPlan === '' ? null : secondaryPlan,
        bracket: bracketValue === '' ? null : Number.parseInt(bracketValue, 10),
        playExperience: readCheckedValue('PlayExperience'),
        includeSideboard: readCheckedBoolean('IncludeSideboard'),
        includeMaybeboard: readCheckedBoolean('IncludeMaybeboard'),
      },
      roleFloors: getFloorRows()
        .filter(({ row }) => row.dataset.cutLabFloorUserSet === 'true')
        .map(({ row, input }) => ({
          role: input.dataset.cutLabFloor ?? '',
          floor: clampFloorValue(input),
          isUserSet: true,
        })),
      goals: {
        commanderByTurn: clampGoalValue(getGoalInput('commander') ?? document.createElement('input'), 3),
        engineByTurn: clampGoalValue(getGoalInput('engine') ?? document.createElement('input'), 2),
        representativeLineByTurn: clampGoalValue(getGoalInput('representative-line') ?? document.createElement('input'), 4),
      },
    };
  };

  const readNamedFieldValue = (name: string): string => {
    const element = document.querySelector<HTMLInputElement | HTMLTextAreaElement>(`[name="${name}"]`);
    return element?.value ?? '';
  };

  const readCheckedValue = (name: string): string => {
    const element = document.querySelector<HTMLInputElement>(`input[name="${name}"]:checked`);
    return element?.value ?? '';
  };

  const readCheckedBoolean = (name: string): boolean =>
    document.querySelector<HTMLInputElement>(`input[name="${name}"]`)?.checked ?? false;

  const normalizePackageId = (packageId: string): string | null => {
    const trimmed = packageId.trim();
    if (trimmed === '' || trimmed === newPackageOptionValue) {
      return null;
    }

    return trimmed;
  };

  const writeStateToHiddenInput = (serializedState?: string): void => {
    const form = getForm();
    if (!form) {
      return;
    }
    if (form.dataset.cutLabPreserveSubmittedState === 'true') {
      return;
    }

    const stateInput = getStateInput(form);
    if (!stateInput) {
      return;
    }

    stateInput.value = serializedState ?? api.buildCutLabStateJson(buildSnapshotFromDom());
  };

  const writeDecisionStateToHiddenInputs = (serializedState: string): void => {
    const form = getForm();
    const mainStateInput = form ? getStateInput(form) : null;
    if (mainStateInput) {
      mainStateInput.value = serializedState;
    }

    getDecisionStateInputs().forEach(input => {
      input.value = serializedState;
    });
  };

  const getDecisionInput = (form: HTMLFormElement): HTMLInputElement | null =>
    form.querySelector<HTMLInputElement>('input[name="Decision"]');

  const getCardNameInput = (form: HTMLFormElement): HTMLInputElement | null =>
    form.querySelector<HTMLInputElement>('input[name="CardName"]');

  const getRoundKeyInput = (form: HTMLFormElement): HTMLInputElement | null =>
    form.querySelector<HTMLInputElement>('input[name="RoundKey"]');

  const getDeltaInput = (form: HTMLFormElement): HTMLInputElement | null =>
    form.querySelector<HTMLInputElement>('input[name="Delta"]');

  const getIsAddedBasicInput = (form: HTMLFormElement): HTMLInputElement | null =>
    form.querySelector<HTMLInputElement>('input[name="IsAddedBasic"]');

  const getAntiForgeryToken = (form: HTMLFormElement): string =>
    form.querySelector<HTMLInputElement>(`input[name="${cutLabAntiForgeryFieldName}"]`)?.value ?? '';

  const isDecisionForm = (form: HTMLFormElement): boolean => {
    const decision = getDecisionInput(form)?.value.trim().toLowerCase();
    return decision === 'accept' || decision === 'reject' || decision === 'defer' || decision === 'restore';
  };

  const isAdjustForm = (form: HTMLFormElement): boolean =>
    form.hasAttribute('data-cut-lab-adjust-form');

  const isRestartRoundsForm = (form: HTMLFormElement): boolean =>
    form.hasAttribute('data-cut-lab-restart-rounds-form');

  const deltaClassFor = (direction: CutLabMetricDirection): string => {
    switch (direction) {
      case 'Up':
        return 'cutlab-delta__value--up';
      case 'Down':
        return 'cutlab-delta__value--down';
      default:
        return 'cutlab-delta__value--none';
    }
  };

  const glyphFor = (direction: CutLabMetricDirection): string => {
    switch (direction) {
      case 'Up':
        return '▲';
      case 'Down':
        return '▼';
      default:
        return '';
    }
  };

  const formatCardValue = (value: number): string => {
    const rounded = Math.round(Math.abs(value));
    return `${rounded} card${rounded === 1 ? '' : 's'}`;
  };

  const formatMetricValue = (value: number, unit: CutLabMetricUnit): string =>
    unit === 'Cards' ? formatCardValue(value) : `${value.toFixed(1)}%`;

  const formatDeltaToken = (delta: number, unit: CutLabMetricUnit): string => {
    const magnitude = Math.abs(delta);
    return unit === 'Cards' ? formatCardValue(magnitude) : `${magnitude.toFixed(1)}%`;
  };

  const directionVerbFor = (direction: CutLabMetricDirection): string =>
    direction === 'Down' ? 'lowers' : 'raises';

  const createTextElement = <T extends keyof HTMLElementTagNameMap>(
    tagName: T,
    className: string,
    text: string,
  ): HTMLElementTagNameMap[T] => {
    const element = document.createElement(tagName);
    if (className !== '') {
      element.className = className;
    }

    element.textContent = text;
    return element;
  };

  const isAsciiDigit = (character: string): boolean =>
    character >= '0' && character <= '9';

  const isValidManaValueSuffix = (value: string): boolean => {
    let index = 0;
    while (index < value.length && isAsciiDigit(value[index])) {
      index++;
    }

    if (index === 0) {
      return false;
    }

    if (index === value.length) {
      return true;
    }

    if (value[index] !== '.') {
      return false;
    }

    const fractionalStart = ++index;
    while (index < value.length && isAsciiDigit(value[index])) {
      index++;
    }

    const fractionalDigits = index - fractionalStart;
    return index === value.length && fractionalDigits >= 1 && fractionalDigits <= 2;
  };

  const findLockablePoolCardForEvidence = (evidence: string): { cardName: string; checkbox: HTMLInputElement } | null => {
    const normalizedEvidence = normalizeAsciiCase(evidence);
    const matches = getPoolRows()
      .map(row => {
        const cardName = row.dataset.cutLabCard?.trim() ?? '';
        const checkbox = getLockCheckbox(row);
        return cardName !== '' && checkbox && !checkbox.disabled ? { cardName, checkbox } : null;
      })
      .filter((match): match is { cardName: string; checkbox: HTMLInputElement } => match !== null)
      .sort((left, right) => right.cardName.length - left.cardName.length);

    return matches.find(({ cardName }) => {
      const normalizedCardName = normalizeAsciiCase(cardName);
      if (normalizedEvidence === normalizedCardName) {
        return true;
      }

      const manaValuePrefix = `${normalizedCardName} · mv `;
      return normalizedEvidence.startsWith(manaValuePrefix)
        && isValidManaValueSuffix(normalizedEvidence.slice(manaValuePrefix.length));
    }) ?? null;
  };

  const getComboBadgeText = (comboBadge: { badgeState: 'CompletePiece' | 'NeedsPartner'; context: string }): string =>
    comboBadge.badgeState === 'CompletePiece' ? 'Combo piece' : comboBadge.context;

  const getComboBadgeClassName = (comboBadge: { badgeState: 'CompletePiece' | 'NeedsPartner'; context: string }): string =>
    comboBadge.badgeState === 'CompletePiece'
      ? 'cutlab-combo-badge cutlab-combo-badge--complete'
      : 'cutlab-combo-badge cutlab-combo-badge--near';

  const appendComboBadge = (
    button: HTMLButtonElement,
    comboBadgeByCardName: Record<string, { badgeState: 'CompletePiece' | 'NeedsPartner'; context: string }>,
    cardName: string,
  ): void => {
    const comboBadge = comboBadgeByCardName[cardName];
    if (!comboBadge) {
      return;
    }

    button.appendChild(createTextElement('span', getComboBadgeClassName(comboBadge), getComboBadgeText(comboBadge)));
  };

  const createStructuralEvidenceChip = (
    evidence: string,
    comboBadgeByCardName: Record<string, { badgeState: 'CompletePiece' | 'NeedsPartner'; context: string }>,
  ): HTMLElement => {
    const match = findLockablePoolCardForEvidence(evidence);
    if (!match) return createTextElement('span', 'kb-chip', evidence);

    const button = createTextElement('button', 'kb-chip cutlab-role-chip', evidence);
    button.type = 'button';
    button.dataset.cutLabChipCard = match.cardName;
    button.dataset.cutlabCardOpen = match.cardName;
    button.setAttribute('aria-pressed', match.checkbox.checked ? 'true' : 'false');
    button.classList.toggle('cutlab-role-chip--locked', match.checkbox.checked);
    appendComboBadge(button, comboBadgeByCardName, match.cardName);
    return button;
  };

  const getCardTextData = (): Record<string, CutLabCardTextEntry> => {
    if (cardTextByCardNameCache) {
      return cardTextByCardNameCache;
    }

    const dataElement = document.getElementById('cutlab-card-text-data');
    if (!dataElement) {
      cardTextByCardNameCache = {};
      return cardTextByCardNameCache;
    }

    try {
      const parsed = JSON.parse(dataElement.textContent ?? '') as unknown;
      cardTextByCardNameCache = parsed && typeof parsed === 'object' ? parsed as Record<string, CutLabCardTextEntry> : {};
    } catch {
      cardTextByCardNameCache = {};
    }

    return cardTextByCardNameCache;
  };

  const syncCardTextComboContexts = (
    comboBadgeByCardName: Record<string, { badgeState: 'CompletePiece' | 'NeedsPartner'; context: string }>,
  ): void => {
    const cardTextData = getCardTextData();
    Object.keys(comboBadgeByCardName).forEach(cardName => {
      const existing = cardTextData[cardName] ?? {};
      existing.comboContext = comboBadgeByCardName[cardName].context;
      cardTextData[cardName] = existing;
    });
  };

  const getCardRowByName = (cardName: string): HTMLTableRowElement | null =>
    document.querySelector<HTMLTableRowElement>(`tr[data-cut-lab-card="${cssEscape(cardName)}"]`);

  const getCardMetaLine = (entry: CutLabCardTextEntry | null): string => {
    if (!entry) {
      return '';
    }

    const metaParts: string[] = [];
    if (entry.typeLine?.trim()) {
      metaParts.push(entry.typeLine.trim());
    }

    if (entry.manaCost?.trim()) {
      metaParts.push(entry.manaCost.trim());
    }

    const printingParts: string[] = [];
    if (entry.setCode?.trim()) {
      printingParts.push(entry.setCode.trim());
    }

    if (entry.collectorNumber?.trim()) {
      printingParts.push(`#${entry.collectorNumber.trim()}`);
    }

    if (printingParts.length > 0) {
      metaParts.push(printingParts.join(' '));
    }

    return metaParts.join(' · ');
  };

  const syncCardModalLockButton = (cardName: string): void => {
    const lockButton = getCardModalLockButton();
    if (!lockButton) {
      return;
    }

    const row = getCardRowByName(cardName);
    const checkbox = row ? getLockCheckbox(row) : null;
    if (!checkbox) {
      lockButton.disabled = true;
      lockButton.textContent = 'Unavailable';
      return;
    }

    if (checkbox.disabled) {
      lockButton.disabled = true;
      lockButton.textContent = 'Locked';
      return;
    }

    lockButton.disabled = false;
    lockButton.textContent = checkbox.checked ? 'Unlock' : 'Lock';
  };

  const openCardModal = (cardName: string): void => {
    const dialog = getCardModal();
    const title = getCardModalTitle();
    const oracle = getCardModalOracle();
    if (!dialog || !title || !oracle) {
      return;
    }

    activeModalCardName = cardName;
    const entry = getCardTextData()[cardName] ?? null;
    const metaLine = getCardMetaLine(entry);
    const oracleText = entry?.oracleText?.trim() ?? '';
    const comboText = entry?.comboContext?.trim() ?? '';
    const meta = getCardModalMeta();
    const combo = getCardModalCombo();

    title.textContent = cardName;
    if (meta) {
      meta.textContent = metaLine;
      meta.hidden = metaLine === '';
    }

    oracle.textContent = oracleText !== '' ? oracleText : 'No card text available.';

    if (combo) {
      combo.textContent = comboText;
      combo.hidden = comboText === '';
    }

    syncCardModalLockButton(cardName);

    if (!dialog.hasAttribute('open')) {
      try {
        dialog.showModal();
      } catch {
        return;
      }
    }
  };

  const createCardOpenButton = (cardName: string, className: string): HTMLButtonElement => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = className;
    button.dataset.cutlabCardOpen = cardName;
    button.textContent = cardName;
    return button;
  };

  const replaceChildren = (element: Element, children: Node[]): void => {
    while (element.firstChild) {
      element.removeChild(element.firstChild);
    }

    children.forEach(child => {
      element.appendChild(child);
    });
  };

  const createPoolCardChip = (entry: CutLabSubtypeEntry): HTMLElement => {
    if (entry.isCommander) {
      const chip = createTextElement('span', 'kb-chip cutlab-lock-badge--commander', entry.name);
      chip.dataset.cutlabCardOpen = entry.name;
      chip.dataset.cutLabChipCard = entry.name;
      return chip;
    }

    const className = `kb-chip cutlab-role-chip${entry.isLocked ? ' cutlab-role-chip--locked' : ''}`;
    const chip = createTextElement('button', className, entry.name);
    chip.type = 'button';
    chip.dataset.cutlabCardOpen = entry.name;
    chip.dataset.cutLabChipCard = entry.name;
    chip.setAttribute('aria-pressed', entry.isLocked ? 'true' : 'false');
    return chip;
  };

  const getPoolSubtypeEntries = (): CutLabSubtypeEntry[] =>
    getPoolRows()
      .filter(row => row.hasAttribute('data-cut-lab-type-line'))
      .map(row => {
        const name = row.dataset.cutLabCard?.trim() ?? '';
        const typeLine = row.dataset.cutLabTypeLine?.trim() ?? '';
        if (name === '' || typeLine === '') {
          return null;
        }

        const checkbox = getLockCheckbox(row);
        return {
          name,
          typeLine,
          isLocked: checkbox?.checked ?? false,
          isCommander: row.dataset.cutLabCommander === 'true',
        } satisfies CutLabSubtypeEntry;
      })
      .filter((entry): entry is CutLabSubtypeEntry => entry !== null);

  const renderSubtypeSearchResults = (): void => {
    const results = getSubtypeResults();
    const searchInput = getSubtypeSearchInput();
    if (!results || !searchInput) {
      return;
    }

    const query = searchInput.value.trim();
    if (query === '') {
      replaceChildren(results, []);
      return;
    }

    const matches = api.filterPoolBySubtype(getPoolSubtypeEntries(), query);
    if (matches.length === 0) {
      replaceChildren(results, [
        createTextElement('p', 'cutlab-subtype-results__empty', `No cards match '${query}'.`),
      ]);
      return;
    }

    const summary = createTextElement('p', 'cutlab-subtype-results__summary', `${query} · ${formatCountLabel(matches.length, 'card', 'cards')}`);
    const chips = document.createElement('div');
    chips.className = 'kb-chip-area__chips';
    matches.forEach(entry => {
      chips.appendChild(createPoolCardChip(entry));
    });

    replaceChildren(results, [summary, chips]);
  };

  const formatScenarioSavedAt = (savedAt: string): string => {
    const parsed = new Date(savedAt);
    return Number.isNaN(parsed.getTime()) ? savedAt : parsed.toLocaleString();
  };

  const renderScenarioStatus = (message: string): void => {
    const status = getScenarioStatus();
    if (!status) {
      return;
    }

    status.textContent = message;
  };

  const renderScenarioList = (): void => {
    const list = getScenarioList();
    if (!list) {
      return;
    }

    const scenarios = api.listScenarios();
    if (scenarios.length === 0) {
      replaceChildren(list, [
        createTextElement('p', 'cutlab-scenarios__empty prompt-size-note', 'No saved scenarios yet.'),
      ]);
      return;
    }

    replaceChildren(list, scenarios.map(scenario => {
      const row = document.createElement('div');
      row.className = 'cutlab-scenarios__item';

      const copy = document.createElement('div');
      copy.className = 'cutlab-scenarios__copy';
      copy.appendChild(createTextElement('strong', 'cutlab-scenarios__name', scenario.name));

      const timestamp = document.createElement('time');
      timestamp.className = 'prompt-size-note cutlab-scenarios__saved-at';
      timestamp.dateTime = scenario.savedAt;
      timestamp.textContent = formatScenarioSavedAt(scenario.savedAt);
      copy.appendChild(timestamp);

      const actions = document.createElement('div');
      actions.className = 'cutlab-scenarios__actions';

      const loadButton = document.createElement('button');
      loadButton.type = 'button';
      loadButton.className = 'run-button';
      loadButton.dataset.cutLabScenarioLoad = scenario.id;
      loadButton.textContent = 'Load';

      const deleteButton = document.createElement('button');
      deleteButton.type = 'button';
      deleteButton.className = 'clear-cache-button';
      deleteButton.dataset.cutLabScenarioDelete = scenario.id;
      deleteButton.textContent = 'Delete';

      actions.append(loadButton, deleteButton);
      row.append(copy, actions);
      return row;
    }));
  };

  const isPortableSessionState = (value: unknown): value is { pool?: unknown; version?: unknown } => {
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
      return false;
    }

    const state = value as { pool?: unknown; version?: unknown };
    return Array.isArray(state.pool) || typeof state.version === 'number' || typeof state.version === 'string';
  };

  const restoreSessionFromStateJson = (stateJson: string): void => {
    const form = getForm();
    const stateInput = form ? getStateInput(form) : null;
    if (!form || !stateInput) {
      return;
    }

    stateInput.value = stateJson;
    form.dataset.cutLabPreserveSubmittedState = 'true';
    const deckInputSource = getDeckInputSourceSelect();
    if (deckInputSource) {
      deckInputSource.value = 'PasteText';
    }

    const deckUrlInput = getDeckUrlInput();
    if (deckUrlInput) {
      deckUrlInput.value = '';
    }

    const deckTextInput = getDeckTextInput();
    if (deckTextInput) {
      deckTextInput.value = '';
    }

    try {
      // Why: the main Cut Lab form always renders data-cache-key; this literal only guards
      // unexpected markup drift so scenario-load cleanup still targets the default storage slot.
      const formCacheKey = form.dataset.cacheKey?.trim() || 'cut-lab';
      form.dataset.skipPersistence = 'true';
      window.sessionStorage.removeItem(`decksync-form-state-${formCacheKey}`);
      window.sessionStorage.removeItem(`decksync-form-state-${formCacheKey}:savedAt`);
    } catch {
      // Why: deck-sync persists intake form-state in sessionStorage; clear it (and skip re-persist)
      // so the scenario-load page reflects only the server-restored session.
    }

    try {
      if (typeof root.DeckFlow?.clearLastDeck === 'function') {
        root.DeckFlow.clearLastDeck();
      }
    } catch {
      // Why: scenario load should not rehydrate stale pasted deck input from client cache.
    }

    renderScenarioStatus('Scenario loaded. Rebuilding Cut Lab…');
    form.requestSubmit();
  };

  const downloadPortableSession = (): void => {
    if (typeof Blob !== 'function' || typeof URL.createObjectURL !== 'function' || typeof URL.revokeObjectURL !== 'function') {
      renderScenarioStatus('This browser cannot download session files here.');
      return;
    }

    const snapshot = buildSnapshotFromDom();
    if (snapshot.pool.length === 0) {
      renderScenarioStatus('Build or load a Cut Lab session first.');
      return;
    }

    const stateJson = api.buildCutLabStateJson(snapshot);
    const stamp = new Date();
    const timestamp = [
      stamp.getFullYear().toString(),
      String(stamp.getMonth() + 1).padStart(2, '0'),
      String(stamp.getDate()).padStart(2, '0'),
    ].join('') + '-'
      + [
        String(stamp.getHours()).padStart(2, '0'),
        String(stamp.getMinutes()).padStart(2, '0'),
        String(stamp.getSeconds()).padStart(2, '0'),
      ].join('');

    const blob = new Blob([stateJson], { type: 'application/json' });
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = `cutlab-session-${timestamp}.json`;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(objectUrl);
    renderScenarioStatus('Session file downloaded.');
  };

  const readPortableSessionFileText = (file: File): Promise<string> => {
    if (typeof file.text === 'function') {
      return file.text();
    }

    if (typeof FileReader !== 'function') {
      return Promise.reject(new Error('file-reader-unavailable'));
    }

    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onerror = () => {
        reject(new Error('file-read-failed'));
      };
      reader.onload = () => {
        resolve(typeof reader.result === 'string' ? reader.result : '');
      };
      reader.readAsText(file);
    });
  };

  const loadPortableSessionFile = async (input: HTMLInputElement): Promise<void> => {
    const file = input.files?.[0];
    if (!file) {
      renderScenarioStatus('Choose a session file first.');
      return;
    }

    if (typeof file.text !== 'function' && typeof FileReader !== 'function') {
      renderScenarioStatus('This browser cannot read session files here.');
      input.value = '';
      return;
    }

    let text = '';
    try {
      text = await readPortableSessionFileText(file);
    } catch {
      renderScenarioStatus('Unable to read that session file.');
      input.value = '';
      return;
    }

    let parsed: unknown;
    try {
      parsed = JSON.parse(text);
    } catch {
      renderScenarioStatus('That file isn\'t a Cut Lab session.');
      input.value = '';
      return;
    }

    if (!isPortableSessionState(parsed)) {
      renderScenarioStatus('That file isn\'t a Cut Lab session.');
      input.value = '';
      return;
    }

    input.value = '';
    restoreSessionFromStateJson(text);
  };

  const saveCurrentScenario = (): void => {
    const nameInput = getScenarioNameInput();
    const form = getForm();
    const stateInput = form ? getStateInput(form) : null;
    if (!nameInput || !form || !stateInput) {
      return;
    }

    writeStateToHiddenInput();
    const result = api.saveScenario(nameInput.value, stateInput.value);
    switch (result) {
      case 'ok':
        nameInput.value = '';
        renderScenarioList();
        renderScenarioStatus('Scenario saved.');
        break;
      case 'cap-reached':
      case 'quota-exceeded':
        renderScenarioStatus('Delete a scenario first (max 20).');
        break;
      case 'disabled':
        renderScenarioStatus('Your browser blocked local storage.');
        break;
      case 'invalid':
        renderScenarioStatus('Name required.');
        break;
    }
  };

  const loadSavedScenario = (scenarioId: string): void => {
    const stateJson = api.loadScenario(scenarioId);
    if (!stateJson) {
      renderScenarioStatus('Scenario unavailable in this browser.');
      renderScenarioList();
      return;
    }
    restoreSessionFromStateJson(stateJson);
  };

  const deleteSavedScenario = (scenarioId: string): void => {
    const deleted = api.deleteScenario(scenarioId);
    renderScenarioList();
    renderScenarioStatus(deleted ? 'Scenario deleted.' : 'Scenario unavailable in this browser.');
  };

  const renderRoundBanner = (nextProposal: CutLabDecisionNextProposal): void => {
    const banner = getRoundBanner();
    if (!banner) {
      return;
    }

    if (nextProposal.isTerminal) {
      banner.remove();
      return;
    }

    replaceChildren(banner, [
      createTextElement('p', 'cutlab-finding__heading', nextProposal.roundLabel),
      createTextElement('p', '', nextProposal.roundBannerBody),
    ]);
  };

  const appendDeltaLine = (
    container: HTMLElement,
    cardName: string,
    delta: CutLabDecisionMetricDelta,
  ): void => {
    const line = document.createElement('div');
    line.className = 'cutlab-delta__line';

    const sentence = document.createElement('span');
    sentence.className = 'cutlab-delta__sentence';
    sentence.textContent = delta.isMeaningful
      ? `cutting ${cardName} ${directionVerbFor(delta.direction)} ${delta.label.toLowerCase()} by ${formatDeltaToken(delta.delta, delta.unit)}.`
      : `${delta.label}: no meaningful change`;

    const value = document.createElement('span');
    value.className = `cutlab-delta__value ${deltaClassFor(delta.direction)}`;
    if (delta.direction !== 'None') {
      const glyph = document.createElement('span');
      glyph.setAttribute('aria-hidden', 'true');
      glyph.textContent = glyphFor(delta.direction);
      value.appendChild(glyph);
    }

    const token = document.createElement('span');
    token.className = deltaClassFor(delta.direction);
    token.textContent = delta.isMeaningful ? formatDeltaToken(delta.delta, delta.unit) : formatDeltaToken(0, delta.unit);
    value.appendChild(token);

    line.appendChild(sentence);
    line.appendChild(value);
    container.appendChild(line);
  };

  const buildDecisionFormBase = (
    cardName: string,
    roundKey: string,
    decisionValue: CutLabDecisionAction,
    serializedState: string,
    antiForgeryToken: string,
  ): HTMLFormElement => {
    const form = document.createElement('form');
    form.method = 'post';
    form.action = getCutLabDecideAction();
    form.dataset.cutLabDecideForm = 'true';

    const appendHiddenInput = (name: string, value: string): void => {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = name;
      input.value = value;
      form.appendChild(input);
    };

    if (antiForgeryToken !== '') {
      appendHiddenInput(cutLabAntiForgeryFieldName, antiForgeryToken);
    }

    appendHiddenInput('CutLabStateJson', serializedState);
    appendHiddenInput('CardName', cardName);
    appendHiddenInput('RoundKey', roundKey);
    appendHiddenInput('Decision', decisionValue);

    return form;
  };

  const createDecisionForm = (
    action: CutLabDecisionAction,
    buttonText: string,
    buttonClassName: string,
    cardName: string,
    roundKey: string,
    serializedState: string,
    antiForgeryToken: string,
  ): HTMLFormElement => {
    const form = buildDecisionFormBase(cardName, roundKey, action, serializedState, antiForgeryToken);
    const button = document.createElement('button');
    button.type = 'submit';
    button.className = `cutlab-decision-btn ${buttonClassName}`;
    button.setAttribute('aria-label', `${buttonText} for ${cardName}`);
    button.dataset.cutLabDecision = action;
    button.dataset.cutLabCard = cardName;
    button.textContent = buttonText;
    form.appendChild(button);

    return form;
  };

  const createRestoreForm = (
    cut: CutLabDecisionCutRecord,
    serializedState: string,
    antiForgeryToken: string,
  ): HTMLFormElement => {
    const form = buildDecisionFormBase(cut.cardName, cut.roundKey, 'restore', serializedState, antiForgeryToken);
    const button = document.createElement('button');
    button.type = 'submit';
    button.className = 'cutlab-restore-btn';
    button.setAttribute('aria-label', `Restore ${cut.cardName}`);
    button.dataset.cutLabRestore = '';
    button.dataset.cutLabCard = cut.cardName;
    button.textContent = 'Restore';
    form.appendChild(button);

    return form;
  };

  const ensureCutsMadeSection = (): HTMLDetailsElement | null => {
    const existing = getCutsMadeDetails();
    if (existing) {
      existing.closest('section.result-panel')?.setAttribute('data-cut-lab-cuts-made-section', 'true');
      return existing;
    }

    const cutRoundsSection = getCutRoundsSection();
    if (!cutRoundsSection || !cutRoundsSection.parentElement) {
      return null;
    }

    const section = document.createElement('section');
    section.className = 'result-panel';
    section.setAttribute('data-cut-lab-cuts-made-section', 'true');

    const panelHeading = document.createElement('div');
    panelHeading.className = 'panel-heading';
    const headingCopy = document.createElement('div');
    headingCopy.appendChild(createTextElement('h2', '', 'Cuts made'));
    headingCopy.appendChild(createTextElement('p', '', 'Every accepted cut, restorable any time — order doesn\'t matter.'));
    panelHeading.appendChild(headingCopy);

    const details = document.createElement('details');
    details.className = 'cutlab-cuts-made';

    const summary = document.createElement('summary');
    details.appendChild(summary);

    section.appendChild(panelHeading);
    section.appendChild(details);
    cutRoundsSection.insertAdjacentElement('afterend', section);
    return details;
  };

  const renderCutsMadeStatus = (
    attributeName: 'data-cut-lab-decision-error' | 'data-cut-lab-restore-confirmation',
    message: string,
  ): void => {
    const section = getCutsMadeSection();
    if (!section) {
      return;
    }

    const details = section.querySelector<HTMLDetailsElement>('details.cutlab-cuts-made');
    const statusContainer: HTMLElement = details ?? section;
    let messageLine = statusContainer.querySelector<HTMLElement>(`[${attributeName}]`);
    if (!messageLine) {
      messageLine = document.createElement('p');
      messageLine.className = 'cutlab-degradation-note';
      messageLine.setAttribute(attributeName, 'true');
      if (details) {
        details.insertBefore(messageLine, details.querySelector('.cutlab-cuts-made__row'));
      } else {
        section.appendChild(messageLine);
      }
    }

    messageLine.textContent = message;
  };

  const clearRestoreConfirmation = (): void => {
    document.querySelectorAll<HTMLElement>('[data-cut-lab-restore-confirmation]').forEach(element => {
      element.remove();
    });
  };

  const renderCutsMade = (
    cutsMade: CutLabDecisionCutRecord[],
    serializedState: string,
    antiForgeryToken: string,
    preserveSection: boolean = false,
  ): void => {
    const existing = getCutsMadeDetails();
    const section = getCutsMadeSection();
    section?.setAttribute('data-cut-lab-cuts-made-section', 'true');
    if (cutsMade.length === 0) {
      existing?.remove();
      if (!preserveSection && section && !section.querySelector('[data-cut-lab-restore-confirmation]')) {
        section.remove();
      }
      return;
    }

    const details = ensureCutsMadeSection();
    if (!details) {
      return;
    }

    details.open = cutsMade.length <= 5;

    const summary = details.querySelector('summary') ?? document.createElement('summary');
    summary.textContent = `Cuts made · ${formatCutsMadeCount(cutsMade.length)}`;
    if (!summary.parentElement) {
      details.appendChild(summary);
    }

    Array.from(details.querySelectorAll('.cutlab-cuts-made__row')).forEach(row => {
      row.remove();
    });

    cutsMade.forEach(cut => {
      const row = document.createElement('div');
      row.className = 'cutlab-cuts-made__row';
      row.appendChild(createTextElement('span', '', cut.cardName));
      row.appendChild(createTextElement('span', 'prompt-size-note', `cut in ${cut.roundLabel}`));
      row.appendChild(createRestoreForm(cut, serializedState, antiForgeryToken));
      details.appendChild(row);
    });
  };

  const removeRestoredCutRow = (cardName: string): void => {
    const details = getCutsMadeDetails();
    if (!details) {
      return;
    }

    details.closest<HTMLElement>('section.result-panel')?.setAttribute('data-cut-lab-cuts-made-section', 'true');

    const row = Array.from(details.querySelectorAll<HTMLDivElement>('.cutlab-cuts-made__row'))
      .find(candidate => candidate.querySelector('span')?.textContent?.trim() === cardName);
    if (!row) {
      return;
    }

    row.remove();
    const remainingCount = details.querySelectorAll('.cutlab-cuts-made__row').length;
    if (remainingCount === 0) {
      details.remove();
      return;
    }

    const summary = details.querySelector('summary');
    if (summary) {
      summary.textContent = `Cuts made · ${formatCutsMadeCount(remainingCount)}`;
    }
  };

  const renderStructuralFindings = (patch: CutLabUiPatch): void => {
    const section = getStructuralFindingsSection();
    const body = getStructuralFindingsBody();
    if (!section || !body) {
      return;
    }

    // A patch may omit the combo map (older/cached responses, light adjust path); treat a
    // missing map as "no badges" so a stale fixture cannot abort the whole findings re-render.
    const comboBadgeByCardName = patch.comboBadgeByCardName ?? {};
    syncCardTextComboContexts(comboBadgeByCardName);

    const totalFindings = patch.structuralFindings.reduce((count, group) => count + group.items.length, 0);
    const countBadge = section.querySelector<HTMLElement>('[data-cut-lab-findings-count-slot] .cutlab-findings-count');
    if (countBadge) {
      countBadge.textContent = totalFindings > 0 ? formatStructuralFindingsCount(totalFindings) : '';
      countBadge.classList.toggle('hidden', totalFindings === 0);
    }

    if (totalFindings === 0) {
      replaceChildren(body, [
        createTextElement('p', '', "No structural issues found. Your pool's curve, themes, finishers, and role coverage all look self-supporting at the current floors."),
      ]);
    } else {
      replaceChildren(body, patch.structuralFindings.map(group => {
        const groupElement = document.createElement('div');
        groupElement.className = 'cutlab-finding';
        groupElement.appendChild(createTextElement('p', 'cutlab-finding__heading', group.heading));

        group.items.forEach(item => {
          const itemElement = document.createElement('div');
          itemElement.className = 'cutlab-finding__item';
          itemElement.appendChild(createTextElement('p', 'cutlab-finding__lead', item.lead));

          if (item.evidence.length > 0) {
            const chips = document.createElement('div');
            chips.className = 'kb-chip-area__chips';
            item.evidence.forEach(evidence => {
              chips.appendChild(createStructuralEvidenceChip(evidence, comboBadgeByCardName));
            });
            itemElement.appendChild(chips);
          }

          groupElement.appendChild(itemElement);
        });

        return groupElement;
      }));
    }

    section.querySelector<HTMLElement>('[data-cut-lab-degradation="combo"]')
      ?.classList.toggle('hidden', patch.comboDataAvailable);
    section.querySelector<HTMLElement>('[data-cut-lab-degradation="category"]')
      ?.classList.toggle('hidden', patch.categoryDataAvailable);
  };

  const renderFloorWarnings = (proposal: HTMLDivElement, warnings: CutLabDecisionFloorWarning[]): void => {
    proposal.querySelectorAll('.cutlab-proposal__floor-warning').forEach(node => {
      node.remove();
    });

    warnings.forEach(warning => {
      const warningPanel = document.createElement('div');
      warningPanel.className = 'cutlab-finding cutlab-proposal__floor-warning';
      warningPanel.appendChild(createTextElement('p', 'cutlab-finding__lead', warning.message));
      proposal.appendChild(warningPanel);
    });
  };

  const renderProposalTerminalState = (proposal: HTMLDivElement, heading: string, body: string): void => {
    proposal.removeAttribute('data-cut-lab-card');
    proposal.removeAttribute('data-cut-lab-round');
    replaceChildren(proposal, [
      createTextElement('p', 'cutlab-proposal__heading', heading),
      createTextElement('p', '', body),
    ]);
  };

  const renderProposalCard = (
    patch: CutLabUiPatch,
    antiForgeryToken: string,
  ): void => {
    const proposal = getProposalCard();
    if (!proposal) {
      return;
    }

    const nextProposal = patch.nextProposal;
    if (nextProposal.isTerminal) {
      renderProposalTerminalState(
        proposal,
        nextProposal.isAtTarget ? "You're at 100 cards" : 'Nothing to cut',
        nextProposal.isAtTarget
          ? 'Review the cuts you made below, or reopen a card from the Cuts made list if you want to reconsider.'
          : 'Every remaining card is either locked or your working list is already at 100 cards. Review your locks and packages above, or adjust a role floor if you want to reconsider.',
      );
      renderFloorWarnings(proposal, []);
      return;
    }

    proposal.dataset.cutLabCard = nextProposal.cardName;
    proposal.dataset.cutLabRound = nextProposal.roundKey;
    proposal.textContent = '';

    const heading = document.createElement('p');
    heading.className = 'cutlab-proposal__heading';
    heading.appendChild(document.createTextNode('Proposed cut: '));
    heading.appendChild(createCardOpenButton(nextProposal.cardName, 'cutlab-card-link'));
    proposal.appendChild(heading);

    const evidence = document.createElement('div');
    evidence.className = 'cutlab-proposal__evidence';
    evidence.appendChild(createTextElement(
      'p',
      '',
      nextProposal.findingCount > 0
        ? `Flagged by ${nextProposal.findingCount} findings:`
        : 'No structural finding flags this card — it\'s a preference call.',
    ));

    if (nextProposal.findingChips.length > 0) {
      const chips = document.createElement('div');
      chips.className = 'kb-chip-area__chips';
      nextProposal.findingChips.forEach(chipText => {
        chips.appendChild(createTextElement('span', 'kb-chip', chipText));
      });
      evidence.appendChild(chips);
    }

    proposal.appendChild(evidence);

    if (patch.proposalDeltas) {
      const changedLines = patch.proposalDeltas.deltas.filter(delta => delta.isMeaningful);
      const deltaSummary = document.createElement('div');
      deltaSummary.className = 'cutlab-delta';
      deltaSummary.appendChild(createTextElement('p', '', `${patch.proposalDeltas.changedFamilyCount} of 7 metric families changed meaningfully.`));
      changedLines.forEach(delta => {
        appendDeltaLine(deltaSummary, nextProposal.cardName, delta);
      });
      proposal.appendChild(deltaSummary);

      const details = document.createElement('details');
      details.dataset.cutLabDeltaExpander = '';
      details.appendChild(createTextElement('summary', '', 'Show full metric breakdown'));
      const fullDelta = document.createElement('div');
      fullDelta.className = 'cutlab-delta';
      patch.proposalDeltas.deltas.forEach(delta => {
        appendDeltaLine(fullDelta, nextProposal.cardName, delta);
      });
      details.appendChild(fullDelta);
      proposal.appendChild(details);
    } else {
      const unavailable = document.createElement('div');
      unavailable.className = 'cutlab-finding cutlab-proposal__floor-warning';
      unavailable.appendChild(createTextElement('p', 'cutlab-finding__lead', cutLabDecisionErrorCopy));
      proposal.appendChild(unavailable);
    }

    renderFloorWarnings(proposal, patch.floorWarnings);

    const actions = document.createElement('div');
    actions.className = 'cutlab-proposal__actions';
    actions.appendChild(createDecisionForm('accept', 'Accept cut', 'cutlab-decision-btn--accept', nextProposal.cardName, nextProposal.roundKey, patch.cutLabStateJson, antiForgeryToken));
    actions.appendChild(createDecisionForm('reject', 'Reject cut', 'cutlab-decision-btn--reject', nextProposal.cardName, nextProposal.roundKey, patch.cutLabStateJson, antiForgeryToken));
    actions.appendChild(createDecisionForm('defer', 'Defer decision', 'cutlab-decision-btn--defer', nextProposal.cardName, nextProposal.roundKey, patch.cutLabStateJson, antiForgeryToken));
    proposal.appendChild(actions);
  };

  const renderDecisionError = (form: HTMLFormElement, message: string): void => {
    const proposal = form.closest<HTMLDivElement>('.cutlab-proposal');
    if (!proposal) {
      if (form.closest('.cutlab-cuts-made__row') || form.closest('details.cutlab-cuts-made')) {
        renderCutsMadeStatus('data-cut-lab-decision-error', message);
        return;
      }

      const errorBanner = getErrorBanner();
      if (errorBanner) {
        errorBanner.textContent = message;
        errorBanner.classList.remove('hidden');
      }
      return;
    }

    let errorLine = proposal.querySelector<HTMLElement>('[data-cut-lab-decision-error]');
    if (!errorLine) {
      errorLine = document.createElement('p');
      errorLine.dataset.cutLabDecisionError = 'true';
      errorLine.className = 'cutlab-degradation-note';
      proposal.appendChild(errorLine);
    }

    errorLine.textContent = message;
  };

  const clearDecisionError = (): void => {
    document.querySelectorAll<HTMLElement>('[data-cut-lab-decision-error]').forEach(element => {
      element.remove();
    });

    const errorBanner = getErrorBanner();
    if (errorBanner) {
      errorBanner.textContent = '';
      errorBanner.classList.add('hidden');
    }
  };

  const isWhatifForm = (form: HTMLFormElement): boolean =>
    form.hasAttribute('data-cut-lab-whatif-form');

  const syncWhatifStateInputFromMainForm = (form: HTMLFormElement): string => {
    writeStateToHiddenInput();

    const mainForm = getForm();
    const mainStateInput = mainForm ? getStateInput(mainForm) : null;
    const serializedState = mainStateInput?.value ?? '';
    const whatifStateInput = getStateInput(form);
    if (whatifStateInput) {
      whatifStateInput.value = serializedState;
      return whatifStateInput.value;
    }

    return serializedState;
  };

  const extractWhatifPayload = (form: HTMLFormElement): { cutLabStateJson: string; cardOut: string; cardIn: string } | null => {
    const cardOut = getWhatifCardOutSelect()?.value.trim() ?? '';
    const cardIn = getWhatifCardInSelect()?.value.trim() ?? '';
    const cutLabStateJson = syncWhatifStateInputFromMainForm(form);
    if (cutLabStateJson === '' || cardOut === '' || cardIn === '') {
      return null;
    }

    return {
      cutLabStateJson,
      cardOut,
      cardIn,
    };
  };

  const setWhatifControlsVisible = (hasPreview: boolean): void => {
    getWhatifPreviewContainer()?.classList.toggle('hidden', !hasPreview);
    getWhatifKeepButton()?.classList.toggle('hidden', !hasPreview);
    getWhatifDiscardButton()?.classList.toggle('hidden', !hasPreview);
  };

  const renderWhatifError = (message: string): void => {
    const container = getWhatifPreviewContainer();
    if (!container) {
      return;
    }

    let errorLine = container.querySelector<HTMLElement>('[data-cut-lab-whatif-error]');
    if (!errorLine) {
      errorLine = document.createElement('p');
      errorLine.className = 'cutlab-degradation-note';
      errorLine.dataset.cutLabWhatifError = 'true';
      container.appendChild(errorLine);
    }

    errorLine.textContent = message;
  };

  const clearWhatifError = (): void => {
    document.querySelectorAll<HTMLElement>('[data-cut-lab-whatif-error]').forEach(element => {
      element.remove();
    });
  };

  const createWhatifDeltaRow = (delta: CutLabDecisionMetricDelta): HTMLTableRowElement => {
    const row = document.createElement('tr');

    const metricCell = document.createElement('td');
    metricCell.setAttribute('data-label', 'Metric');
    const metricStrong = document.createElement('strong');
    metricStrong.textContent = delta.label;
    metricCell.appendChild(metricStrong);

    const beforeCell = document.createElement('td');
    beforeCell.setAttribute('data-label', 'Before');
    beforeCell.textContent = formatMetricValue(delta.before, delta.unit);

    const afterCell = document.createElement('td');
    afterCell.setAttribute('data-label', 'After');
    afterCell.textContent = formatMetricValue(delta.after, delta.unit);

    const deltaCell = document.createElement('td');
    deltaCell.setAttribute('data-label', 'Delta');
    const value = document.createElement('span');
    value.className = `cutlab-delta__value ${deltaClassFor(delta.direction)}`;
    if (delta.direction !== 'None') {
      const glyph = document.createElement('span');
      glyph.setAttribute('aria-hidden', 'true');
      glyph.textContent = glyphFor(delta.direction);
      value.appendChild(glyph);
    }

    const token = document.createElement('span');
    token.textContent = formatDeltaToken(delta.delta, delta.unit);
    value.appendChild(token);
    deltaCell.appendChild(value);

    row.append(metricCell, beforeCell, afterCell, deltaCell);
    return row;
  };

  const renderWhatifPreview = (response: CutLabWhatifResponse): void => {
    const container = getWhatifPreviewContainer();
    const selection = getWhatifSelection();
    const deltaBody = getWhatifDeltaBody();
    if (!container || !selection || !deltaBody) {
      return;
    }

    clearWhatifError();
    selection.classList.remove('hidden');
    selection.innerHTML =
      `Previewing: cut <strong>${escapeHtml(response.cardOut)}</strong>, restore <strong>${escapeHtml(response.cardIn)}</strong>. ` +
      `${response.changedFamilyCount} ${cutLabWhatifPreviewSummaryCopy}`;

    replaceChildren(deltaBody, response.deltas.map(createWhatifDeltaRow));
    setWhatifControlsVisible(true);
  };

  const clearWhatifPreview = (): void => {
    clearWhatifError();
    const selection = getWhatifSelection();
    const deltaBody = getWhatifDeltaBody();
    if (selection) {
      selection.textContent = '';
      selection.classList.add('hidden');
    }

    if (deltaBody) {
      replaceChildren(deltaBody, []);
    }

    setWhatifControlsVisible(false);
  };

  const setWhatifBusyState = (form: HTMLFormElement, submitter: HTMLButtonElement | null): (() => void) => {
    const originalButtonStates = [
      getWhatifPreviewButton(),
      getWhatifKeepButton(),
      getWhatifDiscardButton(),
    ].filter((button): button is HTMLButtonElement => button !== null)
      .map(button => ({ button, wasDisabled: button.disabled }));
    const originalSelectStates = [
      getWhatifCardOutSelect(),
      getWhatifCardInSelect(),
    ].filter((select): select is HTMLSelectElement => select !== null)
      .map(select => ({ select, wasDisabled: select.disabled }));

    originalButtonStates.forEach(({ button }) => {
      button.disabled = true;
    });
    originalSelectStates.forEach(({ select }) => {
      select.disabled = true;
    });
    form.setAttribute('aria-busy', 'true');

    const restoreSubmitter = submitter ? setSubmitterBusyState(submitter) : () => undefined;
    return () => {
      originalButtonStates.forEach(({ button, wasDisabled }) => {
        button.disabled = wasDisabled;
      });
      originalSelectStates.forEach(({ select, wasDisabled }) => {
        select.disabled = wasDisabled;
      });
      form.removeAttribute('aria-busy');
      restoreSubmitter();
    };
  };

  const sortCardNames = (cardNames: Iterable<string>): string[] =>
    Array.from(cardNames).sort((left, right) => left.localeCompare(right, undefined, { sensitivity: 'accent' }));

  const replaceWhatifSelectOptions = (select: HTMLSelectElement, cardNames: string[]): void => {
    const emptyOptionLabel = Array.from(select.options).find(option => option.value === '')?.text ?? '';
    replaceChildren(select, [
      new Option(emptyOptionLabel, ''),
      ...cardNames.map(cardName => new Option(cardName, cardName)),
    ]);
    select.value = '';
  };

  const renderWhatifSelectOptions = (cardOutOptions: string[], cardInOptions: string[]): void => {
    const cardOutSelect = getWhatifCardOutSelect();
    const cardInSelect = getWhatifCardInSelect();
    if (!cardOutSelect || !cardInSelect) {
      return;
    }

    replaceWhatifSelectOptions(cardOutSelect, cardOutOptions);
    replaceWhatifSelectOptions(cardInSelect, cardInOptions);
  };

  const setExportEnabled = (atTarget: boolean): void => {
    const exportTab = getExportStepTab();
    if (exportTab) {
      exportTab.disabled = !atTarget;
      exportTab.setAttribute('aria-disabled', atTarget ? 'false' : 'true');
      exportTab.classList.toggle('is-disabled', !atTarget);
    }

    const buildExportSubmit = getBuildExportSubmit();
    if (buildExportSubmit) {
      buildExportSubmit.disabled = !atTarget;
    }
  };

  const patchExportCountStatus = (currentCount: number): void => {
    const exportCountStatus = getExportCountStatus();
    if (!exportCountStatus) {
      return;
    }

    const countText = document.createElement('strong');
    countText.dataset.cutLabExportCountText = 'true';
    countText.textContent = formatCutLabExportCount(currentCount);

    const children: HTMLElement[] = [countText];
    if (currentCount !== 100) {
      const helper = document.createElement('span');
      helper.dataset.cutLabExportCountHelper = 'true';
      helper.textContent = cutLabExportCountLockedHelperCopy;
      children.push(helper);
    }

    replaceChildren(exportCountStatus, children);
  };

  const createAdjustHiddenInput = (name: string, value: string): HTMLInputElement => {
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = name;
    input.value = value;
    return input;
  };

  const createAdjustSubmitButton = (
    cardName: string,
    delta: number,
    isAddedBasic: boolean,
    text: string,
    ariaLabel: string,
    disabled: boolean,
  ): HTMLButtonElement => {
    const button = document.createElement('button');
    button.type = 'submit';
    button.className = 'cutlab-stepper-btn';
    button.setAttribute('aria-label', ariaLabel);
    button.dataset.cutLabAdjust = '';
    button.dataset.cutLabCard = cardName;
    button.dataset.cutLabDelta = `${delta}`;
    button.dataset.cutLabAddedBasic = isAddedBasic ? 'true' : 'false';
    button.disabled = disabled;
    button.setAttribute('aria-disabled', disabled ? 'true' : 'false');
    button.textContent = text;
    return button;
  };

  const createAdjustForm = (
    row: CutLabQuantityTunerRow,
    delta: number,
    antiForgeryToken: string,
    serializedState: string,
    disabled: boolean,
  ): HTMLFormElement => {
    const form = document.createElement('form');
    form.method = 'post';
    form.action = '/cut-lab/adjust';
    form.dataset.cutLabAdjustForm = '';

    if (antiForgeryToken !== '') {
      form.appendChild(createAdjustHiddenInput(cutLabAntiForgeryFieldName, antiForgeryToken));
    }

    form.appendChild(createAdjustHiddenInput('CutLabStateJson', serializedState));
    form.appendChild(createAdjustHiddenInput('CardName', row.cardName));
    form.appendChild(createAdjustHiddenInput('Delta', `${delta}`));
    form.appendChild(createAdjustHiddenInput('IsAddedBasic', row.isAddedBasic ? 'true' : 'false'));
    form.appendChild(createAdjustSubmitButton(
      row.cardName,
      delta,
      row.isAddedBasic,
      delta < 0 ? '−' : '+',
      `${delta < 0 ? 'Remove' : 'Add'} one ${row.cardName}`,
      disabled,
    ));
    return form;
  };

  const createQuantityTunerRow = (
    row: CutLabQuantityTunerRow,
    antiForgeryToken: string,
    serializedState: string,
  ): HTMLTableRowElement => {
    const tunerRow = document.createElement('tr');
    tunerRow.dataset.cutLabTunerRow = row.cardName;
    tunerRow.dataset.cutLabQuantity = `${row.currentQuantity}`;
    tunerRow.dataset.cutLabLegalMax = `${row.legalMax}`;

    const cardCell = document.createElement('td');
    cardCell.setAttribute('data-label', 'Card');
    cardCell.appendChild(createTextElement('strong', '', row.cardName));
    if (row.isAddedBasic) {
      cardCell.appendChild(createTextElement('span', 'kb-chip cutlab-tuner-badge--added', 'Added'));
    }

    const roleCell = document.createElement('td');
    roleCell.setAttribute('data-label', 'Role');
    roleCell.textContent = row.roleLabel;

    const quantityCell = document.createElement('td');
    quantityCell.setAttribute('data-label', 'Quantity');
    const stepper = document.createElement('div');
    stepper.className = 'cutlab-stepper';
    stepper.appendChild(createAdjustForm(row, -1, antiForgeryToken, serializedState, row.removeDisabled));
    stepper.appendChild(createTextElement('span', 'cutlab-stepper__count tabular', `${row.currentQuantity}`));
    stepper.lastElementChild?.setAttribute('data-cut-lab-quantity-value', '');
    stepper.appendChild(createAdjustForm(row, 1, antiForgeryToken, serializedState, row.addDisabled));
    quantityCell.appendChild(stepper);

    tunerRow.append(cardCell, roleCell, quantityCell);
    return tunerRow;
  };

  const ensureAddBasicFallbackNote = (): HTMLElement | null => {
    const section = getQuantityTunerSection();
    if (!section) {
      return null;
    }

    const existing = section.querySelector<HTMLElement>('p.cutlab-floor-source-default');
    if (existing) {
      return existing;
    }

    const note = createTextElement(
      'p',
      'cutlab-floor-source-default',
      'All basic land types are already in your working list. Use the steppers above to add more copies.',
    );
    section.appendChild(note);
    return note;
  };

  const createAddBasicForm = (antiForgeryToken: string, serializedState: string): HTMLFormElement | null => {
    const section = getQuantityTunerSection();
    if (!section) {
      return null;
    }

    const form = document.createElement('form');
    form.method = 'post';
    form.action = '/cut-lab/adjust';
    form.dataset.cutLabAdjustForm = '';
    form.className = 'toolbar cutlab-tuner__add-basic';

    if (antiForgeryToken !== '') {
      form.appendChild(createAdjustHiddenInput(cutLabAntiForgeryFieldName, antiForgeryToken));
    }

    form.appendChild(createAdjustHiddenInput('CutLabStateJson', serializedState));
    form.appendChild(createAdjustHiddenInput('Delta', '1'));
    form.appendChild(createAdjustHiddenInput('IsAddedBasic', 'true'));

    const label = document.createElement('label');
    label.htmlFor = 'cut-lab-add-basic-select';
    label.textContent = 'Add a basic land';
    form.appendChild(label);

    const select = document.createElement('select');
    select.id = 'cut-lab-add-basic-select';
    select.name = 'CardName';
    select.dataset.dfSelect = '';
    select.dataset.cutLabAddBasicSelect = '';
    select.required = true;
    form.appendChild(select);

    const button = document.createElement('button');
    button.type = 'submit';
    button.className = 'run-button';
    button.dataset.cutLabAdjust = '';
    button.dataset.cutLabAddBasic = '';
    button.dataset.cutLabDelta = '1';
    button.dataset.cutLabAddedBasic = 'true';
    button.textContent = 'Add basic land';
    form.appendChild(button);

    const fallbackNote = section.querySelector<HTMLElement>('p.cutlab-floor-source-default');
    if (fallbackNote) {
      section.insertBefore(form, fallbackNote);
    } else {
      section.appendChild(form);
    }

    return form;
  };

  const reconcileQuantityTuners = (
    quantityTuners: CutLabQuantityTunerRow[],
    antiForgeryToken: string,
    serializedState: string,
  ): void => {
    const tunerBody = getQuantityTunerBody();
    if (!tunerBody) {
      return;
    }

    const visibleRows = quantityTuners
      .filter(row => row.isVisible && row.isLegalMultiple)
      .map(row => createQuantityTunerRow(row, antiForgeryToken, serializedState));
    replaceChildren(tunerBody, visibleRows);
  };

  const reconcileAddableBasics = (
    addableBasics: string[],
    antiForgeryToken: string,
    serializedState: string,
  ): void => {
    const section = getQuantityTunerSection();
    if (!section) {
      return;
    }

    const fallbackNote = ensureAddBasicFallbackNote();
    let form = getAddBasicForm();
    if (addableBasics.length === 0) {
      form?.classList.add('hidden');
      if (fallbackNote) {
        fallbackNote.classList.remove('hidden');
      }
      return;
    }

    if (!form) {
      form = createAddBasicForm(antiForgeryToken, serializedState);
    }

    if (!form) {
      return;
    }

    Array.from(form.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]'))
      .forEach(input => {
        input.value = serializedState;
      });

    const select = getAddBasicSelect();
    if (!select) {
      return;
    }

    replaceChildren(select, [
      new Option('Choose a basic…', ''),
      ...addableBasics.map(cardName => new Option(cardName, cardName)),
    ]);
    select.options[0].disabled = true;
    select.value = '';
    window.DeckFlow?.refreshDfSelect?.(select);
    form.classList.remove('hidden');
    fallbackNote?.classList.add('hidden');
  };

  const patchStickyBar = (
    patch: CutLabUiPatch,
    options: { preserveProposal?: boolean; preserveCutsSection?: boolean } = {},
  ): void => {
    setExportEnabled(patch.canBuildExport);

    const stickyRound = getStickyRound();
    const stickyRemaining = getStickyRemaining();
    const stickyAccepted = getStickyAccepted();
    const stickyCurrent = getStickyCurrent();
    const shouldHideRoundFields = !options.preserveProposal
      && Boolean(patch.nextProposal)
      && (patch.nextProposal.isTerminal || patch.nextProposal.roundLabel.trim() === '');

    if (stickyCurrent) {
      stickyCurrent.textContent = `${patch.currentCount}/100 cards`;
    }

    if (shouldHideRoundFields) {
      stickyRound?.setAttribute('hidden', '');
      stickyRemaining?.setAttribute('hidden', '');
      stickyAccepted?.setAttribute('hidden', '');
      return;
    }

    if (stickyRound && !options.preserveProposal && patch.nextProposal) {
      stickyRound.textContent = patch.nextProposal.roundLabel;
      stickyRound.removeAttribute('hidden');
    }

    if (stickyRemaining) {
      stickyRemaining.textContent = `${patch.cardsRemaining} to cut`;
      stickyRemaining.removeAttribute('hidden');
    }

    if (stickyAccepted) {
      stickyAccepted.textContent = formatCutsAcceptedSoFar(patch.cutsMade.length);
      stickyAccepted.removeAttribute('hidden');
    }
  };

  const applyServerPatch = (
    patch: CutLabUiPatch,
    antiForgeryToken: string,
    options: { preserveCutsSection?: boolean; preserveProposal?: boolean; preserveFindings?: boolean; adjustedCardName?: string } = {},
  ): void => {
    void options.adjustedCardName;
    const shouldRenderProposal = Boolean(patch.nextProposal) && (!options.preserveProposal || patch.nextProposal.isTerminal);
    writeDecisionStateToHiddenInputs(patch.cutLabStateJson);
    setExportEnabled(patch.canBuildExport);
    patchExportCountStatus(patch.currentCount);
    renderWhatifSelectOptions(patch.whatifCardOutOptions, patch.whatifCardInOptions);
    patchStickyBar(patch, options);
    if (shouldRenderProposal) {
      renderRoundBanner(patch.nextProposal);
      renderProposalCard(patch, antiForgeryToken);
    }
    if (!(options.preserveCutsSection && patch.cutsMade.length === 0)) {
      renderCutsMade(patch.cutsMade, patch.cutLabStateJson, antiForgeryToken, options.preserveCutsSection ?? false);
    }
    if (!options.preserveFindings) {
      renderStructuralFindings(patch);
    }
    reconcileQuantityTuners(patch.quantityTuners, antiForgeryToken, patch.cutLabStateJson);
    reconcileAddableBasics(patch.addableBasics, antiForgeryToken, patch.cutLabStateJson);
    updateLockedCountChip();
  };

  const handleWhatifPreview = async (form: HTMLFormElement, submitter: HTMLButtonElement | null): Promise<void> => {
    if (whatifSubmitInFlight) {
      return;
    }

    const payload = extractWhatifPayload(form);
    if (!payload) {
      renderWhatifError(cutLabWhatifPreviewErrorCopy);
      return;
    }

    whatifSubmitInFlight = true;
    clearWhatifError();
    const antiForgeryToken = getAntiForgeryToken(form);
    const restoreBusyState = setWhatifBusyState(form, submitter);
    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), cutLabDecisionTimeoutMs);

    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };
      if (antiForgeryToken !== '') {
        headers.RequestVerificationToken = antiForgeryToken;
      }

      const response = await fetch(cutLabWhatifApiEndpoint, {
        method: 'POST',
        headers,
        body: JSON.stringify(payload),
        signal: controller.signal,
      });

      if (!response.ok) {
        renderWhatifError(await readErrorMessage(response));
        return;
      }

      const data = await response.json() as CutLabWhatifResponse;
      renderWhatifPreview(data);
    } catch (error) {
      renderWhatifError(error instanceof DOMException && error.name === 'AbortError'
        ? cutLabDecisionTimeoutCopy
        : cutLabWhatifPreviewErrorCopy);
    } finally {
      window.clearTimeout(timeoutId);
      whatifSubmitInFlight = false;
      restoreBusyState();
    }
  };

  const handleWhatifKeep = async (form: HTMLFormElement, submitter: HTMLButtonElement | null): Promise<void> => {
    if (whatifSubmitInFlight) {
      return;
    }

    const payload = extractWhatifPayload(form);
    if (!payload) {
      renderWhatifError(cutLabWhatifKeepErrorCopy);
      return;
    }

    whatifSubmitInFlight = true;
    clearWhatifError();
    const antiForgeryToken = getAntiForgeryToken(form);
    const restoreBusyState = setWhatifBusyState(form, submitter);
    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), cutLabDecisionTimeoutMs);

    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };
      if (antiForgeryToken !== '') {
        headers.RequestVerificationToken = antiForgeryToken;
      }

      const response = await fetch(cutLabWhatifCommitApiEndpoint, {
        method: 'POST',
        headers,
        body: JSON.stringify(payload),
        signal: controller.signal,
      });

      if (!response.ok) {
        renderWhatifError(await readErrorMessage(response));
        return;
      }

      const data = await response.json() as CutLabWhatifResponse;
      if (!data.patch?.cutLabStateJson) {
        renderWhatifError(cutLabWhatifKeepErrorCopy);
        return;
      }

      applyServerPatch(data.patch, antiForgeryToken, { preserveCutsSection: false });
      clearWhatifPreview();
    } catch (error) {
      renderWhatifError(error instanceof DOMException && error.name === 'AbortError'
        ? cutLabDecisionTimeoutCopy
        : cutLabWhatifKeepErrorCopy);
    } finally {
      window.clearTimeout(timeoutId);
      whatifSubmitInFlight = false;
      restoreBusyState();
    }
  };

  const extractDecisionPayload = (form: HTMLFormElement): { cutLabStateJson: string; cardName: string; decision: CutLabDecisionAction } | null => {
    const stateInput = getStateInput(form);
    const cardNameInput = getCardNameInput(form);
    const decisionInput = getDecisionInput(form);
    const decision = decisionInput?.value.trim().toLowerCase();

    if (!stateInput || !cardNameInput || !decisionInput) {
      return null;
    }

    if (decision !== 'accept' && decision !== 'reject' && decision !== 'defer' && decision !== 'restore') {
      return null;
    }

    return {
      cutLabStateJson: stateInput.value,
      cardName: cardNameInput.value,
      decision,
    };
  };

  const setSubmitterBusyState = (button: HTMLButtonElement): (() => void) => {
    const originalText = button.textContent ?? '';
    button.disabled = true;
    button.textContent = '';

    const spinner = document.createElement('span');
    spinner.className = 'cutlab-busy-spinner';
    spinner.setAttribute('aria-hidden', 'true');
    const label = document.createElement('span');
    label.textContent = cutLabDecisionBusyCopy;
    button.appendChild(spinner);
    button.appendChild(label);

    return () => {
      button.disabled = false;
      button.textContent = originalText;
    };
  };

  const setDecisionButtonsBusy = (form: HTMLFormElement, submitter: HTMLButtonElement | null): (() => void) => {
    const proposal = form.closest<HTMLDivElement>('.cutlab-proposal');
    Array.from(document.querySelectorAll<HTMLFormElement>('form'))
      .filter(candidate => isDecisionForm(candidate))
      .forEach(candidate => {
        candidate.dataset.cutLabDecideForm = 'true';
      });
    const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>('form[data-cut-lab-decide-form] button[type="submit"]'));
    const originalStates = buttons.map(button => ({
      button,
      wasDisabled: button.disabled,
    }));

    originalStates.forEach(({ button }) => {
      button.disabled = true;
    });

    if (proposal) {
      proposal.setAttribute('aria-busy', 'true');
    }

    const restoreSubmitter = submitter ? setSubmitterBusyState(submitter) : () => undefined;
    return () => {
      originalStates.forEach(({ button, wasDisabled }) => {
        button.disabled = wasDisabled;
      });

      restoreSubmitter();
      proposal?.removeAttribute('aria-busy');
    };
  };

  const readErrorMessage = async (response: Response): Promise<string> => {
    try {
      const payload = await response.json() as { message?: string; Message?: string };
      return payload.message ?? payload.Message ?? cutLabDecisionErrorCopy;
    } catch {
      return cutLabDecisionErrorCopy;
    }
  };

  const handleDecisionSubmit = async (form: HTMLFormElement, submitter: HTMLButtonElement | null): Promise<void> => {
    if (decisionSubmitInFlight) {
      return;
    }

    const payload = extractDecisionPayload(form);
    if (!payload) {
      return;
    }

    decisionSubmitInFlight = true;
    clearDecisionError();

    const antiForgeryToken = getAntiForgeryToken(form);
    const restoreBusyState = setDecisionButtonsBusy(form, submitter);
    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), cutLabDecisionTimeoutMs);

    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };
      if (antiForgeryToken !== '') {
        headers.RequestVerificationToken = antiForgeryToken;
      }

      const response = await fetch(getCutLabDecideApi(), {
        method: 'POST',
        headers,
        body: JSON.stringify(payload),
        signal: controller.signal,
      });

      if (!response.ok) {
        renderDecisionError(form, await readErrorMessage(response));
        return;
      }

      const data = await response.json() as CutLabPatchResponse;
      if (!data.patch?.cutLabStateJson) {
        renderDecisionError(form, cutLabDecisionErrorCopy);
        return;
      }

      clearRestoreConfirmation();
      applyServerPatch(data.patch, antiForgeryToken, { preserveCutsSection: payload.decision === 'restore' });
      if (payload.decision === 'restore') {
        removeRestoredCutRow(payload.cardName);
        renderCutsMadeStatus('data-cut-lab-restore-confirmation', `${payload.cardName} restored — metrics recalculating…`);
      }
    } catch (error) {
      renderDecisionError(form, error instanceof DOMException && error.name === 'AbortError'
        ? cutLabDecisionTimeoutCopy
        : cutLabDecisionErrorCopy);
    } finally {
      window.clearTimeout(timeoutId);
      decisionSubmitInFlight = false;
      restoreBusyState();
    }
  };

  const handleRestartRoundsSubmit = async (form: HTMLFormElement, submitter: HTMLButtonElement | null): Promise<void> => {
    if (decisionSubmitInFlight) {
      return;
    }

    const stateInput = getStateInput(form);
    if (!stateInput) {
      return;
    }

    const confirmed = window.confirm("Reconsider rejected/deferred cards from Round 1 & 2 with today's findings?");
    if (!confirmed) {
      return;
    }

    decisionSubmitInFlight = true;
    clearDecisionError();

    const antiForgeryToken = getAntiForgeryToken(form);
    const restoreBusyState = submitter ? setSubmitterBusyState(submitter) : () => undefined;
    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), cutLabDecisionTimeoutMs);

    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };
      if (antiForgeryToken !== '') {
        headers.RequestVerificationToken = antiForgeryToken;
      }

      const response = await fetch(getCutLabRestartRoundsApi(), {
        method: 'POST',
        headers,
        body: JSON.stringify({
          cutLabStateJson: stateInput.value,
        }),
        signal: controller.signal,
      });

      if (!response.ok) {
        renderDecisionError(form, await readErrorMessage(response));
        return;
      }

      const data = await response.json() as CutLabPatchResponse;
      if (!data.patch?.cutLabStateJson) {
        renderDecisionError(form, cutLabDecisionErrorCopy);
        return;
      }

      applyServerPatch(data.patch, antiForgeryToken);
    } catch (error) {
      renderDecisionError(form, error instanceof DOMException && error.name === 'AbortError'
        ? cutLabDecisionTimeoutCopy
        : cutLabDecisionErrorCopy);
    } finally {
      window.clearTimeout(timeoutId);
      decisionSubmitInFlight = false;
      restoreBusyState();
    }
  };

  const extractAdjustPayload = (form: HTMLFormElement): { cutLabStateJson: string; cardName: string; delta: number; isAddedBasic: boolean } | null => {
    const stateInput = getStateInput(form);
    const cardNameField = form.querySelector<HTMLInputElement | HTMLSelectElement>('[name="CardName"]');
    const deltaInput = getDeltaInput(form);
    const isAddedBasicInput = getIsAddedBasicInput(form);
    const delta = Number.parseInt(deltaInput?.value ?? '', 10);
    const cardName = cardNameField?.value ?? '';

    if (!stateInput || cardName.trim() === '' || Number.isNaN(delta)) {
      return null;
    }

    return {
      cutLabStateJson: stateInput.value,
      cardName,
      delta,
      isAddedBasic: isAddedBasicInput?.value.trim().toLowerCase() === 'true',
    };
  };

  const setAdjustBusyState = (form: HTMLFormElement, submitter: HTMLButtonElement | null): (() => void) => {
    const row = form.closest<HTMLElement>('tr[data-cut-lab-tuner-row]');
    const buttons = row
      ? Array.from(row.querySelectorAll<HTMLButtonElement>('button[type="submit"]'))
      : Array.from(form.querySelectorAll<HTMLButtonElement>('button[type="submit"]'));
    const originalStates = buttons.map(button => ({
      button,
      wasDisabled: button.disabled,
    }));

    originalStates.forEach(({ button }) => {
      button.disabled = true;
    });

    const restoreSubmitter = submitter ? setSubmitterBusyState(submitter) : () => undefined;
    row?.setAttribute('aria-busy', 'true');
    return () => {
      originalStates.forEach(({ button, wasDisabled }) => {
        button.disabled = wasDisabled;
      });

      restoreSubmitter();
      row?.removeAttribute('aria-busy');
    };
  };

  const handleAdjustSubmit = async (form: HTMLFormElement, submitter: HTMLButtonElement | null): Promise<void> => {
    if (adjustSubmitInFlight) {
      return;
    }

    const payload = extractAdjustPayload(form);
    if (!payload) {
      return;
    }

    adjustSubmitInFlight = true;
    clearDecisionError();

    const antiForgeryToken = getAntiForgeryToken(form);
    const restoreBusyState = setAdjustBusyState(form, submitter);
    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), cutLabDecisionTimeoutMs);

    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };
      if (antiForgeryToken !== '') {
        headers.RequestVerificationToken = antiForgeryToken;
      }

      const response = await fetch(cutLabAdjustApiEndpoint, {
        method: 'POST',
        headers,
        body: JSON.stringify(payload),
        signal: controller.signal,
      });

      if (!response.ok) {
        renderDecisionError(form, await readErrorMessage(response));
        return;
      }

      const data = await response.json() as CutLabPatchResponse;
      if (!data.patch?.cutLabStateJson) {
        renderDecisionError(form, cutLabDecisionErrorCopy);
        return;
      }

      applyServerPatch(data.patch, antiForgeryToken, {
        adjustedCardName: payload.cardName,
        preserveProposal: true,
        preserveFindings: true,
      });
    } catch (error) {
      renderDecisionError(form, error instanceof DOMException && error.name === 'AbortError'
        ? cutLabDecisionTimeoutCopy
        : cutLabDecisionErrorCopy);
    } finally {
      window.clearTimeout(timeoutId);
      adjustSubmitInFlight = false;
      restoreBusyState();
    }
  };

  const updatePackageContainerVisualState = (container: HTMLDivElement, state: PackageCheckboxState): void => {
    const toggle = getPackageToggle(container);
    if (!toggle) {
      return;
    }

    toggle.indeterminate = state === 'indeterminate';
    toggle.checked = state === 'checked';

    if (state === 'checked') {
      container.classList.add('cutlab-package--locked');
    } else {
      container.classList.remove('cutlab-package--locked');
    }
  };

  const getPackageMemberRows = (packageId: string): HTMLTableRowElement[] =>
    getPoolRows().filter(row => getPackageSelect(row)?.value === packageId);

  const syncPackageState = (packageId: string): void => {
    const container = findPackageContainer(packageId);
    if (!container) {
      return;
    }

    const memberRows = getPackageMemberRows(packageId);
    const memberLocked = memberRows
      .map(row => getLockCheckbox(row))
      .filter((checkbox): checkbox is HTMLInputElement => checkbox !== null)
      .map(checkbox => checkbox.checked);

    const state = api.computePackageCheckboxState(memberLocked);
    updatePackageContainerVisualState(container, state);
    updatePackageMemberCount(container, memberRows.length);
    renderPackageMembers(container, memberRows);
  };

  const syncAllPackageStates = (): void => {
    getPackageContainers().forEach(container => {
      const packageId = container.dataset.cutLabPackageId;
      if (packageId) {
        syncPackageState(packageId);
      }
    });
  };

  const renderPackageMembers = (container: HTMLDivElement, rows: HTMLTableRowElement[]): void => {
    const chipArea = container.querySelector<HTMLElement>('.kb-chip-area__chips');
    const emptyHint = container.querySelector<HTMLElement>('.kb-chip-area__empty-hint');

    if (rows.length === 0) {
      if (chipArea) {
        chipArea.innerHTML = '';
      }

      if (emptyHint) {
        emptyHint.textContent = 'No cards assigned yet.';
        emptyHint.hidden = false;
      }

      return;
    }

    const chipsMarkup = rows
      .map(row => `<span class="kb-chip">${escapeHtml(row.dataset.cutLabCard ?? '')}</span>`)
      .join('');

    if (chipArea) {
      chipArea.innerHTML = chipsMarkup;
    }

    if (emptyHint) {
      emptyHint.hidden = true;
    }
  };

  const updatePackageMemberCount = (container: HTMLDivElement, memberCount: number): void => {
    const countParagraph = Array.from(container.querySelectorAll<HTMLParagraphElement>('p'))
      .find(element => /member/.test(element.textContent ?? ''));
    if (!countParagraph) {
      return;
    }

    countParagraph.textContent = `${memberCount} member${memberCount === 1 ? '' : 's'}`;
  };

  const escapeHtml = (value: string): string =>
    value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');

  const applyPackageToggle = (packageId: string, locked: boolean): void => {
    const memberRows = getPackageMemberRows(packageId);
    memberRows.forEach(row => {
      const checkbox = getLockCheckbox(row);
      if (!checkbox || checkbox.disabled) {
        return;
      }

      checkbox.checked = locked;
    });

    syncPackageState(packageId);
    refreshAndSerialize();
  };

  const toggleRoleLock = (roleKey: string): void => {
    const roleRows = getPoolRows().filter(row => api.hasRoleToken(row.dataset.cutLabRole, roleKey));
    const lockableMembers = roleRows
      .map(row => getLockCheckbox(row))
      .filter((checkbox): checkbox is HTMLInputElement => checkbox !== null && !checkbox.disabled);
    const nextLockedState = !(lockableMembers.length > 0 && lockableMembers.every(checkbox => checkbox.checked));

    lockableMembers.forEach(checkbox => {
      checkbox.checked = nextLockedState;
    });

    refreshAndSerialize();
  };

  const updateFloorRow = (row: HTMLTableRowElement, input: HTMLInputElement, isUserSet: boolean): void => {
    const floor = clampFloorValue(input);
    setFloorUserSetState(row, isUserSet);
    updateFloorRowMarker(row, floor);
  };

  const clearPendingNewPackageUi = (): void => {
    pendingNewPackageTarget = null;
    const row = document.querySelector<HTMLElement>('[data-cut-lab-new-package-row]');
    const input = document.querySelector<HTMLInputElement>('[data-cut-lab-new-package-input]');
    if (row) {
      row.classList.add('hidden');
    }

    if (input) {
      input.value = '';
    }

    document.querySelectorAll<HTMLSelectElement>('select[data-cut-lab-package-card]').forEach(select => {
      if (select.value === newPackageOptionValue) {
        select.value = unlockedPoolOptionValue;
        window.DeckFlow?.refreshDfSelect?.(select);
      }
    });
  };

  const showNewPackageUi = (select: HTMLSelectElement): void => {
    pendingNewPackageTarget = { select };
    const row = document.querySelector<HTMLElement>('[data-cut-lab-new-package-row]');
    const input = document.querySelector<HTMLInputElement>('[data-cut-lab-new-package-input]');
    if (!row || !input) {
      return;
    }

    row.classList.remove('hidden');
    input.value = '';
    input.focus();
  };

  const buildPackageId = (name: string): string => {
    generatedPackageCounter += 1;
    const slug = name
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '');
    return `pkg-${slug || 'package'}-${generatedPackageCounter}`;
  };

  const createPackageContainer = (packageId: string, packageName: string): HTMLDivElement | null => {
    const insertionPoint = document.querySelector<HTMLElement>('[data-cut-lab-new-package-row]')?.closest<HTMLElement>('.card-picker__rows');
    const packageContainerParent = insertionPoint?.parentElement;
    if (!insertionPoint || !packageContainerParent) {
      return null;
    }

    const container = document.createElement('div');
    container.className = 'result-panel nested-panel';
    container.dataset.cutLabPackageId = packageId;
    container.dataset.cutLabPackageName = packageName;
    container.innerHTML =
      `<div class="panel-heading">` +
      `<div><h2>${escapeHtml(packageName)}</h2><p>0 members</p></div>` +
      `<div class="panel-heading__actions">` +
      `<label class="kb-chip" for="cut-lab-package-lock-${escapeHtml(packageId)}">` +
      `<input id="cut-lab-package-lock-${escapeHtml(packageId)}" type="checkbox" data-cut-lab-package-toggle="${escapeHtml(packageId)}" />` +
      `<span>Lock package</span>` +
      `</label>` +
      `<button type="button" class="clear-cache-button" data-cut-lab-package-delete="${escapeHtml(packageId)}">Delete package</button>` +
      `</div></div>` +
      `<div class="kb-chip-area__chips"></div>` +
      `<p class="kb-chip-area__empty-hint">No cards assigned yet.</p>`;

    packageContainerParent.insertBefore(container, insertionPoint);
    return container;
  };

  const addPackageOptionToSelects = (packageId: string, packageName: string): void => {
    document.querySelectorAll<HTMLSelectElement>('select[data-cut-lab-package-card]').forEach(select => {
      const hasExistingOption = Array.from(select.options).some(option => option.value === packageId);
      if (hasExistingOption) {
        return;
      }

      const newOption = new Option(packageName, packageId);
      const newPackageOption = Array.from(select.options).find(option => option.value === newPackageOptionValue);
      if (newPackageOption) {
        select.add(newOption, newPackageOption);
      } else {
        select.add(newOption);
      }

      window.DeckFlow?.refreshDfSelect?.(select);
    });
  };

  const savePendingNewPackage = (): void => {
    if (!pendingNewPackageTarget) {
      return;
    }

    const input = document.querySelector<HTMLInputElement>('[data-cut-lab-new-package-input]');
    const packageName = input?.value.trim() ?? '';
    if (packageName === '') {
      input?.focus();
      return;
    }

    const packageId = buildPackageId(packageName);
    addPackageOptionToSelects(packageId, packageName);
    pendingNewPackageTarget.select.value = packageId;
    const container = createPackageContainer(packageId, packageName);
    clearPendingNewPackageUi();
    if (container) {
      syncPackageState(packageId);
    }

    refreshAndSerialize();
  };

  const removePackageOptionFromSelects = (packageId: string): void => {
    document.querySelectorAll<HTMLSelectElement>('select[data-cut-lab-package-card]').forEach(select => {
      if (select.value === packageId) {
        select.value = unlockedPoolOptionValue;
      }

      Array.from(select.options)
        .filter(option => option.value === packageId)
        .forEach(option => option.remove());

      window.DeckFlow?.refreshDfSelect?.(select);
    });
  };

  const deletePackage = (packageId: string): void => {
    const packageName = getPackageName(packageId);
    const confirmed = window.confirm(
      `Delete package '${packageName}'? Cards return to the unlocked pool — this doesn't discard them.`,
    );
    if (!confirmed) {
      return;
    }

    getPackageMemberRows(packageId).forEach(row => {
      const checkbox = getLockCheckbox(row);
      if (checkbox && !checkbox.disabled) {
        checkbox.checked = false;
      }
    });

    removePackageOptionFromSelects(packageId);
    findPackageContainer(packageId)?.remove();
    refreshAndSerialize();
  };

  const handlePackageSelectChange = (select: HTMLSelectElement): void => {
    const row = select.closest<HTMLTableRowElement>('tr[data-cut-lab-card]');
    if (!row) {
      return;
    }

    const previousPackageId = Array.from(getPackageContainers())
      .find(container => getPackageMemberRows(container.dataset.cutLabPackageId ?? '').some(member => member === row))
      ?.dataset.cutLabPackageId;

    if (select.value === newPackageOptionValue) {
      showNewPackageUi(select);
      return;
    }

    if (previousPackageId) {
      syncPackageState(previousPackageId);
    }

    if (select.value !== '') {
      syncPackageState(select.value);
    }

    clearPendingNewPackageUi();
    refreshAndSerialize();
  };

  const refreshAndSerialize = (syncDecisionForms = true): void => {
    updateLockedCountChip();
    updatePoolFilterState();
    syncAllPackageStates();
    syncRoleGroupLockState();
    syncRoleLockButtons();
    const serializedState = api.buildCutLabStateJson(buildSnapshotFromDom());
    writeStateToHiddenInput(serializedState);
    if (syncDecisionForms) {
      writeDecisionStateToHiddenInputs(serializedState);
    }
  };

  const attachRowHandlers = (): void => {
    getPoolRows().forEach(row => {
      const checkbox = getLockCheckbox(row);
      if (checkbox) {
        checkbox.addEventListener('change', () => {
          refreshAndSerialize();
        });
      }

      const select = getPackageSelect(row);
      if (select) {
        select.addEventListener('change', () => {
          handlePackageSelectChange(select);
        });
      }
    });

    getFloorRows().forEach(({ row, input }) => {
      const handleFloorChange = (): void => {
        updateFloorRow(row, input, true);
        refreshAndSerialize();
      };

      input.addEventListener('input', handleFloorChange);
      input.addEventListener('change', handleFloorChange);
    });
  };

  const attachPoolFilterHandlers = (): void => {
    const filterContainer = getPoolFilterContainer();
    if (!filterContainer) {
      return;
    }

    filterContainer.hidden = false;

    Array.from(filterContainer.querySelectorAll<HTMLInputElement>('input[name="CutLabPoolFilter"]'))
      .forEach(input => {
        input.addEventListener('change', () => {
          updatePoolFilterState();
        });
      });

    const searchInput = getPoolSearchInput();
    if (searchInput) {
      searchInput.addEventListener('input', () => {
        updatePoolFilterState();
      });
    }
  };

  const attachSubtypeSearchHandlers = (): void => {
    const searchInput = getSubtypeSearchInput();
    if (!searchInput) {
      return;
    }

    // Debounce so the pool scan + chip rebuild fires once per typing pause, not per keystroke.
    let debounceId = 0;
    searchInput.addEventListener('input', () => {
      window.clearTimeout(debounceId);
      debounceId = window.setTimeout(renderSubtypeSearchResults, 120);
    });
  };

  const attachPackageHandlers = (): void => {
    if (packageHandlersAttached) {
      return;
    }

    packageHandlersAttached = true;

    document.addEventListener('change', event => {
      const target = event.target;
      if (!(target instanceof HTMLInputElement)) {
        return;
      }

      const packageId = target.dataset.cutLabPackageToggle;
      if (!packageId) {
        return;
      }

      applyPackageToggle(packageId, target.checked);
    });

    document.addEventListener('click', event => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      const lockRoleButton = target.closest<HTMLElement>('[data-cut-lab-lock-role]');
      if (lockRoleButton?.dataset.cutLabLockRole) {
        toggleRoleLock(lockRoleButton.dataset.cutLabLockRole);
        return;
      }

      const roleChipButton = target.closest<HTMLButtonElement>('button[data-cut-lab-chip-card]');
      if (roleChipButton?.dataset.cutLabChipCard) {
        openCardModal(roleChipButton.dataset.cutLabChipCard);
        return;
      }

      const floorResetButton = target.closest<HTMLElement>('[data-cut-lab-floor-reset]');
      if (floorResetButton?.dataset.cutLabFloorReset) {
        const row = floorResetButton.closest<HTMLTableRowElement>('tr[data-cut-lab-floor-row]');
        const input = row?.querySelector<HTMLInputElement>('input[data-cut-lab-floor]');
        const defaultValue = floorResetButton.dataset.cutLabFloorDefault ?? '';
        if (row && input) {
          input.value = defaultValue;
          updateFloorRow(row, input, false);
          refreshAndSerialize();
        }
        return;
      }

      const deleteButton = target.closest<HTMLElement>('[data-cut-lab-package-delete]');
      if (deleteButton?.dataset.cutLabPackageDelete) {
        deletePackage(deleteButton.dataset.cutLabPackageDelete);
        return;
      }

      if (target.closest('[data-cut-lab-new-package-save]')) {
        savePendingNewPackage();
        return;
      }

      if (target.closest('[data-cut-lab-new-package-cancel]')) {
        clearPendingNewPackageUi();
        return;
      }

      if (target.closest('[data-cut-lab-recalculate]')) {
        getForm()?.requestSubmit();
      }
    });

    const input = document.querySelector<HTMLInputElement>('[data-cut-lab-new-package-input]');
    if (input) {
      input.addEventListener('keydown', event => {
        if (event.key === 'Enter') {
          event.preventDefault();
          savePendingNewPackage();
        }

        if (event.key === 'Escape') {
          clearPendingNewPackageUi();
        }
      });
    }
  };

  const attachDecisionSubmitHandler = (): void => {
    if (decisionHandlersAttached) {
      return;
    }

    decisionHandlersAttached = true;

    document.addEventListener('submit', event => {
      const target = event.target;
      if (!(target instanceof HTMLFormElement) || !isDecisionForm(target)) {
        return;
      }

      event.preventDefault();
      if (decisionSubmitInFlight) {
        return;
      }

      void handleDecisionSubmit(
        target,
        event instanceof SubmitEvent && event.submitter instanceof HTMLButtonElement ? event.submitter : null,
      );
    });
  };

  const attachAdjustSubmitHandler = (): void => {
    document.addEventListener('submit', event => {
      const target = event.target;
      if (!(target instanceof HTMLFormElement) || !isAdjustForm(target)) {
        return;
      }

      event.preventDefault();
      if (adjustSubmitInFlight) {
        return;
      }

      void handleAdjustSubmit(
        target,
        event instanceof SubmitEvent && event.submitter instanceof HTMLButtonElement ? event.submitter : null,
      );
    });
  };

  const attachRestartRoundsSubmitHandler = (): void => {
    if (restartRoundsHandlersAttached) {
      return;
    }

    restartRoundsHandlersAttached = true;

    document.addEventListener('submit', event => {
      const target = event.target;
      if (!(target instanceof HTMLFormElement) || !isRestartRoundsForm(target)) {
        return;
      }

      event.preventDefault();
      if (decisionSubmitInFlight) {
        return;
      }

      void handleRestartRoundsSubmit(
        target,
        event instanceof SubmitEvent && event.submitter instanceof HTMLButtonElement ? event.submitter : null,
      );
    });
  };

  const attachScenarioHandlers = (): void => {
    if (scenarioHandlersAttached) {
      return;
    }

    scenarioHandlersAttached = true;

    document.addEventListener('click', event => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      if (target.closest('[data-cut-lab-scenario-save]')) {
        saveCurrentScenario();
        return;
      }

      if (target.closest('[data-cut-lab-session-download]')) {
        downloadPortableSession();
        return;
      }

      const loadButton = target.closest<HTMLElement>('[data-cut-lab-scenario-load]');
      if (loadButton?.dataset.cutLabScenarioLoad) {
        loadSavedScenario(loadButton.dataset.cutLabScenarioLoad);
        return;
      }

      const deleteButton = target.closest<HTMLElement>('[data-cut-lab-scenario-delete]');
      if (deleteButton?.dataset.cutLabScenarioDelete) {
        deleteSavedScenario(deleteButton.dataset.cutLabScenarioDelete);
      }
    });

    document.addEventListener('keydown', event => {
      const target = event.target;
      if (!(target instanceof HTMLInputElement) || !target.hasAttribute('data-cut-lab-scenario-name')) {
        return;
      }

      if (event.key === 'Enter') {
        event.preventDefault();
        saveCurrentScenario();
      }
    });

    document.addEventListener('change', event => {
      const target = event.target;
      if (!(target instanceof HTMLInputElement) || target !== getSessionFileInput()) {
        return;
      }

      void loadPortableSessionFile(target);
    });
  };

  const attachCardModalHandlers = (): void => {
    if (!cardModalHandlersAttached) {
      cardModalHandlersAttached = true;

      document.addEventListener('click', event => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
          return;
        }

        const openTrigger = target.closest<HTMLElement>('[data-cutlab-card-open]');
        if (openTrigger?.dataset.cutlabCardOpen) {
          openCardModal(openTrigger.dataset.cutlabCardOpen);
          return;
        }

        if (target.closest('[data-cutlab-modal-close]')) {
          getCardModal()?.close();
          activeModalCardName = null;
          return;
        }

        if (!target.closest('[data-cutlab-modal-lock]')) {
          return;
        }

        const cardName = activeModalCardName?.trim() ?? '';
        if (cardName === '') {
          return;
        }

        const row = getCardRowByName(cardName);
        const checkbox = row ? getLockCheckbox(row) : null;
        if (!checkbox || checkbox.disabled) {
          syncCardModalLockButton(cardName);
          return;
        }

        checkbox.checked = !checkbox.checked;
        refreshAndSerialize();
        syncCardModalLockButton(cardName);
      });
    }

    const dialog = getCardModal();
    if (!dialog || dialog.dataset.cutlabModalWired === 'true') {
      return;
    }

    dialog.dataset.cutlabModalWired = 'true';
    dialog.addEventListener('click', event => {
      if (event.target === dialog) {
        dialog.close();
      }
    });

    dialog.addEventListener('close', () => {
      activeModalCardName = null;
    });
  };

  const attachWhatifSubmitHandler = (): void => {
    if (whatifHandlersAttached) {
      return;
    }

    whatifHandlersAttached = true;

    document.addEventListener('submit', event => {
      const target = event.target;
      if (!(target instanceof HTMLFormElement) || !isWhatifForm(target)) {
        return;
      }

      const submitter = event instanceof SubmitEvent && event.submitter instanceof HTMLButtonElement
        ? event.submitter
        : null;
      const intent = submitter?.value.trim().toLowerCase() ?? '';
      if (intent !== 'preview' && intent !== 'keep') {
        return;
      }

      event.preventDefault();
      if (intent === 'preview') {
        void handleWhatifPreview(target, submitter);
        return;
      }

      void handleWhatifKeep(target, submitter);
    });

    document.addEventListener('click', event => {
      const target = event.target;
      if (!(target instanceof HTMLElement) || !target.closest('[data-cut-lab-whatif-discard]')) {
        return;
      }

      clearWhatifPreview();
    });
  };

  const attachGoalSubmitHandler = (): void => {
    document.addEventListener('submit', event => {
      const target = event.target;
      if (!(target instanceof HTMLFormElement) || !target.hasAttribute('data-cut-lab-goals-form')) {
        return;
      }

      event.preventDefault();
      writeStateToHiddenInput();
      getForm()?.requestSubmit();
    });
  };

  const copyExportText = async (button: HTMLButtonElement): Promise<void> => {
    const targetId = button.dataset.copyTarget?.trim();
    if (!targetId) {
      return;
    }

    const target = document.getElementById(targetId);
    if (!(target instanceof HTMLTextAreaElement || target instanceof HTMLInputElement || target instanceof HTMLElement)) {
      return;
    }

    const text = target instanceof HTMLTextAreaElement || target instanceof HTMLInputElement
      ? target.value
      : target.textContent ?? '';
    if (text.trim().length === 0) {
      return;
    }

    const originalLabel = button.textContent ?? 'Copy';

    try {
      await navigator.clipboard.writeText(text);
      button.textContent = 'Copied';
      button.classList.add('is-copied');
    } catch {
      button.textContent = 'Copy failed';
      button.classList.add('is-copy-failed');
    }

    window.setTimeout(() => {
      button.textContent = originalLabel;
      button.classList.remove('is-copied', 'is-copy-failed');
    }, 1500);
  };

  const attachCopyHandlers = (): void => {
    if (copyHandlersAttached) {
      return;
    }

    copyHandlersAttached = true;

    document.addEventListener('click', event => {
      const target = event.target;
      const button = target instanceof HTMLElement
        ? target.closest<HTMLButtonElement>('button[data-copy-target]')
        : null;
      if (!(button instanceof HTMLButtonElement) || button.disabled) {
        return;
      }

      event.preventDefault();
      void copyExportText(button);
    });
  };

  const attachSubmitHandler = (): void => {
    const form = getForm();
    if (!form) {
      return;
    }

    form.addEventListener('submit', () => {
      writeStateToHiddenInput();
    });
  };

  const initializeCutLab = (): void => {
    collapseMobileCollapsiblesOnLoad();
    restoreSectionCollapseState();
    attachSectionCollapsePersistence();
    attachAnchorNavHandler();
    cardTextByCardNameCache = null;
    activeModalCardName = null;

    const form = getForm();
    if (!form) {
      return;
    }

    attachRowHandlers();
    attachPoolFilterHandlers();
    attachSubtypeSearchHandlers();
    attachPackageHandlers();
    attachDecisionSubmitHandler();
    attachAdjustSubmitHandler();
    attachRestartRoundsSubmitHandler();
    attachGoalSubmitHandler();
    attachScenarioHandlers();
    attachWhatifSubmitHandler();
    attachCopyHandlers();
    attachCardModalHandlers();
    attachSubmitHandler();
    syncCardTextComboContexts({});
    refreshAndSerialize(false);
    renderSubtypeSearchResults();
    renderScenarioList();
    setWhatifControlsVisible((getWhatifDeltaBody()?.children.length ?? 0) > 0);
  };

  document.addEventListener('DOMContentLoaded', initializeCutLab);
})(globalThis as CutLabRoot);

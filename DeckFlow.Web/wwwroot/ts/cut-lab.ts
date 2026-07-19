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
}

interface CutLabRoleFloorSnapshot {
  role: string;
  floor: number;
  isUserSet: boolean;
}

interface CutLabStateSnapshot {
  commander: string;
  pool: CutLabPoolSnapshotCard[];
  packages: CutLabPackageSnapshot[];
  intent: CutLabIntentSnapshot;
  roleFloors: CutLabRoleFloorSnapshot[];
}

type PackageCheckboxState = 'checked' | 'unchecked' | 'indeterminate';

interface CutLabFloorDomRow {
  row: HTMLTableRowElement;
  input: HTMLInputElement;
}

interface CutLabApi {
  computePackageCheckboxState(memberLocked: boolean[]): PackageCheckboxState;
  hasRoleToken(roleList: string | null | undefined, role: string): boolean;
  isLandRole(roleList: string | null | undefined): boolean;
  buildCutLabStateJson(snapshot: CutLabStateSnapshot): string;
}

interface CutLabRoot {
  DeckFlowCutLab?: CutLabApi;
}

interface PendingNewPackageTarget {
  select: HTMLSelectElement;
}

const newPackageOptionValue = '__new__';
const unlockedPoolOptionValue = '';

(function (root: CutLabRoot): void {
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
        intent: {
          primaryPlan: snapshot.intent.primaryPlan,
          secondaryPlan: snapshot.intent.secondaryPlan,
          bracket: snapshot.intent.bracket,
          playExperience: snapshot.intent.playExperience,
        },
        roleFloors: snapshot.roleFloors
          .filter(row => row.isUserSet)
          .map(row => ({
            role: row.role,
            floor: Math.trunc(row.floor),
            isUserSet: row.isUserSet,
          })),
      };

      return JSON.stringify(normalizedSnapshot);
    },
  };

  root.DeckFlowCutLab = api;

  let pendingNewPackageTarget: PendingNewPackageTarget | null = null;
  let generatedPackageCounter = 0;

  const getForm = (): HTMLFormElement | null =>
    document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]');

  const getStateInput = (form: HTMLFormElement): HTMLInputElement | null =>
    form.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]');

  const getPoolRows = (): HTMLTableRowElement[] =>
    Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-cut-lab-card]'));

  const getPackageContainers = (): HTMLDivElement[] =>
    Array.from(document.querySelectorAll<HTMLDivElement>('[data-cut-lab-package-id]'));

  const getRoleLockButtons = (): HTMLButtonElement[] =>
    Array.from(document.querySelectorAll<HTMLButtonElement>('[data-cut-lab-lock-role]'));

  const getRoleGroupLockedCount = (roleKey: string): HTMLElement | null =>
    document.querySelector<HTMLElement>(`[data-cut-lab-group-locked="${cssEscape(roleKey)}"]`);

  const getRoleGroupChips = (cardName: string): HTMLElement[] =>
    Array.from(document.querySelectorAll<HTMLElement>(`[data-cut-lab-chip-card="${cssEscape(cardName)}"]`));

  const getFloorRows = (): CutLabFloorDomRow[] =>
    Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-cut-lab-floor-row]'))
      .map(row => {
        const input = row.querySelector<HTMLInputElement>('input[data-cut-lab-floor]');
        return input ? { row, input } : null;
      })
      .filter((entry): entry is CutLabFloorDomRow => entry !== null);

  const getLockCheckbox = (row: HTMLTableRowElement): HTMLInputElement | null =>
    row.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card]');

  const getPackageSelect = (row: HTMLTableRowElement): HTMLSelectElement | null =>
    row.querySelector<HTMLSelectElement>('select[data-cut-lab-package-card]');

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
    const summary = document.querySelector<HTMLElement>('[data-cut-lab-lock-count]');
    if (!summary) {
      return;
    }

    const rows = getPoolRows();
    const nonCommanderCount = rows
      .filter(row => row.dataset.cutLabCommander !== 'true')
      .reduce((total, row) => total + parseRowQuantity(row), 0);
    const lockedCount = rows.reduce((total, row) => {
      const checkbox = getLockCheckbox(row);
      return checkbox?.checked ? total + 1 : total;
    }, 0);

    summary.textContent = `${nonCommanderCount} cards in pool · ${lockedCount} locked`;
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
      button.classList.toggle('is-selected', allLocked);
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
      });

      (row.dataset.cutLabRole ?? '')
        .split(/\s+/)
        .map(token => token.trim())
        .filter(token => token !== '')
        .forEach(roleKey => {
          const previous = lockedCounts.get(roleKey) ?? 0;
          lockedCounts.set(roleKey, previous + (isLocked ? 1 : 0));
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
      intent: {
        primaryPlan: readNamedFieldValue('PrimaryPlan'),
        secondaryPlan: secondaryPlan === '' ? null : secondaryPlan,
        bracket: bracketValue === '' ? null : Number.parseInt(bracketValue, 10),
        playExperience: readCheckedValue('PlayExperience'),
      },
      roleFloors: getFloorRows()
        .filter(({ row }) => row.dataset.cutLabFloorUserSet === 'true')
        .map(({ row, input }) => ({
          role: input.dataset.cutLabFloor ?? '',
          floor: clampFloorValue(input),
          isUserSet: true,
        })),
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

  const normalizePackageId = (packageId: string): string | null => {
    const trimmed = packageId.trim();
    if (trimmed === '' || trimmed === newPackageOptionValue) {
      return null;
    }

    return trimmed;
  };

  const writeStateToHiddenInput = (): void => {
    const form = getForm();
    if (!form) {
      return;
    }

    const stateInput = getStateInput(form);
    if (!stateInput) {
      return;
    }

    stateInput.value = api.buildCutLabStateJson(buildSnapshotFromDom());
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

  const lockAllRole = (roleKey: string): void => {
    getPoolRows().forEach(row => {
      if (!api.hasRoleToken(row.dataset.cutLabRole, roleKey)) {
        return;
      }

      const checkbox = getLockCheckbox(row);
      if (!checkbox || checkbox.disabled) {
        return;
      }

      checkbox.checked = true;
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
    const packageSection = document.querySelector<HTMLElement>('[data-cut-lab-new-package-row]')?.closest('.result-panel.nested-panel');
    if (!packageSection) {
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

    const insertionPoint = packageSection.querySelector<HTMLElement>('.card-picker__rows');
    if (!insertionPoint) {
      return null;
    }

    packageSection.insertBefore(container, insertionPoint);
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

  const refreshAndSerialize = (): void => {
    updateLockedCountChip();
    syncAllPackageStates();
    syncRoleGroupLockState();
    syncRoleLockButtons();
    writeStateToHiddenInput();
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

  const attachPackageHandlers = (): void => {
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
        lockAllRole(lockRoleButton.dataset.cutLabLockRole);
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
    const form = getForm();
    if (!form) {
      return;
    }

    attachRowHandlers();
    attachPackageHandlers();
    attachSubmitHandler();
    refreshAndSerialize();
  };

  document.addEventListener('DOMContentLoaded', initializeCutLab);
})(globalThis as CutLabRoot);

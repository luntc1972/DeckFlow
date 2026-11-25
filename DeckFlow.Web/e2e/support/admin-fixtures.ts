/**
 * <summary>
 * Provides test-scoped admin access so tests can enable tools without exposing
 * temporary flag changes to other workers that share the same server.
 * </summary>
 */
import { test as base } from '@playwright/test';
import {
  acquireAdminLock,
  adminLockMaxWaitMs,
  adminLockWorkBudgetMs,
  applyAdminHeaders,
  releaseAdminLockForTest,
} from './admin-lock';
import { getToolEnabled, setToolEnabled } from './admin-tools';

export { expect } from '@playwright/test';

type AdminFixtures = {
  enabledTools: string[];
  adminLock: void;
  toolFlags: void;
};

export const test = base.extend<AdminFixtures>({
  enabledTools: [[], { option: true }],

  // Why: The separate slot keeps a lock wait of up to 30 minutes out of the test's
  // work budget. Setup and teardown share this slot, so allow time for release.
  adminLock: [async ({ page, enabledTools }, use) => {
    if (enabledTools.length === 0) {
      await use();
      return;
    }

    await applyAdminHeaders(page);
    const handle = await acquireAdminLock();
    try {
      await use();
    } finally {
      await releaseAdminLockForTest(handle);
    }
  }, { auto: true, scope: 'test', timeout: adminLockMaxWaitMs + 60_000 }],

  // Why: Restore flags while the lock is still held so no other locked test
  // observes a temporary flag state.
  toolFlags: [async ({ adminLock, page, enabledTools }, use, testInfo) => {
    if (enabledTools.length === 0) {
      await use();
      return;
    }

    if (testInfo.timeout > 0 && testInfo.timeout < adminLockWorkBudgetMs) {
      testInfo.setTimeout(adminLockWorkBudgetMs);
    }

    const priorStates: { label: string; enabled: boolean }[] = [];
    const restoreFlags = async (): Promise<void> => {
      let firstFailure: { error: unknown } | undefined;
      for (const { label, enabled } of [...priorStates].reverse()) {
        try {
          await setToolEnabled(page, label, enabled);
        } catch (error) {
          firstFailure ??= { error };
        }
      }

      if (firstFailure) {
        throw firstFailure.error;
      }
    };

    try {
      for (const label of enabledTools) {
        const enabled = await getToolEnabled(page, label);
        priorStates.push({ label, enabled });
        await setToolEnabled(page, label, true);
      }
    } catch (error) {
      try {
        await restoreFlags();
      } finally {
        throw error;
      }
    }

    try {
      await use();
    } finally {
      await restoreFlags();
    }
  }, { auto: true, scope: 'test' }],
});

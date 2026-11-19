import { test, type Page } from '@playwright/test';
import type { Stats } from 'node:fs';
import { open, readFile, stat, unlink } from 'node:fs/promises';
import { resolveE2EPort } from './e2e-port';

const adminUser = process.env.FEEDBACK_ADMIN_USER ?? 'admin';
const adminPassword = process.env.FEEDBACK_ADMIN_PASSWORD ?? 'changeme-local';
const basicAuthHeader = `Basic ${Buffer.from(`${adminUser}:${adminPassword}`).toString('base64')}`;

const adminLockPort = resolveE2EPort();
export const adminLockPath = `/tmp/deckflow-admin-e2e-${adminLockPort}.lock`;
export const adminLockTimeoutMs = 90_000;

// Why: A local parallel run can queue many two-minute tests. Cap each wait at 30 minutes.
const adminLockMaxWaitMs = 30 * 60_000;
// Why: Ten minutes leaves room beyond the longest current admin-locked test timeout (four minutes).
const adminLockMaxHoldMs = 10 * 60_000;
const adminLockInitializationGraceMs = 5_000;

type LockHandle = Awaited<ReturnType<typeof open>>;
type LockFilePayload = {
  pid: number;
  createdAt: number;
};

function getAdminForwardedIp(): string {
  const info = test.info();
  const key = `${info.project.name}:${info.file}:${info.title}:${info.retry}`;
  let hash = 0;
  for (const char of key) {
    hash = (hash * 31 + char.charCodeAt(0)) % 200;
  }

  return `203.0.113.${hash + 1}`;
}

function errorCode(error: unknown): string {
  return typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : '';
}

async function tryReclaimStaleLock(): Promise<void> {
  let lock: Stats;
  let payload: LockFilePayload | null = null;

  try {
    lock = await stat(adminLockPath);
  } catch (error: unknown) {
    if (errorCode(error) === 'ENOENT') {
      return;
    }

    throw error;
  }

  try {
    payload = JSON.parse(await readFile(adminLockPath, 'utf8')) as LockFilePayload;
  } catch (error: unknown) {
    if (error instanceof SyntaxError) {
      payload = null;
    } else if (errorCode(error) === 'ENOENT') {
      return;
    } else {
      throw error;
    }
  }

  // The owner writes its PID just after the exclusive create.
  if (!payload && Date.now() - lock.mtimeMs < adminLockInitializationGraceMs) {
    return;
  }

  const pidAlive =
    typeof payload?.pid === 'number' &&
    Number.isInteger(payload.pid) &&
    payload.pid > 0 &&
    (() => {
      try {
        process.kill(payload.pid, 0);
        return true;
      } catch (error: unknown) {
        if (errorCode(error) === 'ESRCH') {
          return false;
        }

        return true;
      }
    })();

  const lockAgeMs =
    typeof payload?.createdAt === 'number' && Number.isFinite(payload.createdAt)
      ? Date.now() - payload.createdAt
      : Date.now() - lock.mtimeMs;

  if (pidAlive && lockAgeMs < adminLockMaxHoldMs) {
    return;
  }

  await unlinkAdminLock(lock);
}

async function unlinkAdminLock(lock: Stats): Promise<void> {
  try {
    const current = await stat(adminLockPath);
    if (current.dev === lock.dev && current.ino === lock.ino) {
      await unlink(adminLockPath);
    }
  } catch (error: unknown) {
    if (errorCode(error) !== 'ENOENT') {
      throw error;
    }
  }
}

async function acquireAdminLock(): Promise<LockHandle> {
  const startedAt = performance.now();

  while (performance.now() - startedAt < adminLockMaxWaitMs) {
    try {
      const handle = await open(adminLockPath, 'wx');
      await handle.writeFile(
        JSON.stringify({
          pid: process.pid,
          createdAt: Date.now(),
        }),
      );
      return handle;
    } catch (error: unknown) {
      if (errorCode(error) !== 'EEXIST') {
        throw error;
      }
    }

    await tryReclaimStaleLock();
    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  throw new Error(`Timed out waiting for admin e2e lock at ${adminLockPath}`);
}

export async function acquireAdminLockForTest(page: Page): Promise<LockHandle> {
  const info = test.info();
  const workBudgetMs = adminLockTimeoutMs + 30_000;
  info.setTimeout(adminLockMaxWaitMs + workBudgetMs);
  await page.setExtraHTTPHeaders({
    Authorization: basicAuthHeader,
    'CF-Connecting-IP': getAdminForwardedIp(),
  });

  const startedAt = performance.now();
  const handle = await acquireAdminLock();
  const waitedMs = Math.ceil(performance.now() - startedAt);
  const originalSetTimeout = info.setTimeout.bind(info);
  // Why: Some callers adjust their work budget after beforeEach. Keep their lock wait excluded too.
  info.setTimeout = (timeout) => originalSetTimeout(timeout === 0 ? 0 : timeout + waitedMs);
  info.setTimeout(workBudgetMs);
  return handle;
}

export async function releaseAdminLockForTest(handle: LockHandle | null): Promise<void> {
  if (!handle) {
    return;
  }

  try {
    await unlinkAdminLock(await handle.stat());
  } finally {
    await handle.close();
  }
}

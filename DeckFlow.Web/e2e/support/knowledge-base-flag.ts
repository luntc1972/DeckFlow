import { test, type BrowserContext, type Page } from '@playwright/test';
import { mkdir, readFile, rename, rmdir, rm, stat, unlink, utimes, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { acquireAdminLockForTest, adminLockPath, adminLockTimeoutMs, releaseAdminLockForTest } from './admin-lock';
import { getToolEnabled, setToolEnabled } from './admin-tools';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;
type KnowledgeBaseLock = { owner: string };
const adminLockHeartbeatMs = 30_000;
const knowledgeBaseLockPath = `${adminLockPath}.knowledge-base`;
const knowledgeBaseLockOwnerPath = `${knowledgeBaseLockPath}/owner`;

async function acquireKnowledgeBaseLock(): Promise<KnowledgeBaseLock> {
  const startedAt = Date.now();

  while (Date.now() - startedAt < adminLockTimeoutMs) {
    try {
      await mkdir(knowledgeBaseLockPath);
      const lock = { owner: randomUUID() };
      await writeFile(knowledgeBaseLockOwnerPath, lock.owner, 'utf8');
      return lock;
    } catch (error: unknown) {
      const code = typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : '';
      if (code !== 'EEXIST') {
        throw error;
      }
    }

    try {
      if (Date.now() - (await stat(knowledgeBaseLockPath)).mtimeMs >= adminLockTimeoutMs) {
        const staleLockPath = `${knowledgeBaseLockPath}.stale-${randomUUID()}`;
        await rename(knowledgeBaseLockPath, staleLockPath);
        await rm(staleLockPath, { recursive: true, force: true });
      }
    } catch (error: unknown) {
      const code = typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : '';
      if (code !== 'ENOENT') {
        throw error;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  throw new Error(`Timed out waiting for Content KB e2e lock at ${knowledgeBaseLockPath}`);
}

async function releaseKnowledgeBaseLock(lock: KnowledgeBaseLock): Promise<void> {
  try {
    const owner = await readFile(knowledgeBaseLockOwnerPath, 'utf8');
    if (owner !== lock.owner) {
      return;
    }

    await unlink(knowledgeBaseLockOwnerPath);
    await rmdir(knowledgeBaseLockPath);
  } catch (error: unknown) {
    const code = typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : '';
    if (code !== 'ENOENT') {
      throw error;
    }
  }
}

function getTestForwardedIp(uniquePerRun = false): string {
  const info = test.info();
  const key = `${info.project.name}:${info.file}:${info.title}:${info.retry}${uniquePerRun ? `:${Date.now()}` : ''}`;
  let hash = 0;
  for (const character of key) {
    hash = (hash * 31 + character.charCodeAt(0)) % 200;
  }

  return `203.0.113.${hash + 1}`;
}

export async function configureAdminPageForTest(page: Page, uniqueForwardedIp = false): Promise<void> {
  const adminUser = process.env.FEEDBACK_ADMIN_USER ?? 'admin';
  const adminPassword = process.env.FEEDBACK_ADMIN_PASSWORD ?? 'changeme-local';
  await page.setExtraHTTPHeaders({
    Authorization: `Basic ${Buffer.from(`${adminUser}:${adminPassword}`).toString('base64')}`,
    'CF-Connecting-IP': getTestForwardedIp(uniqueForwardedIp),
  });
}

async function refreshAdminLockLease(handle: LockHandle): Promise<void> {
  await handle.write(JSON.stringify({ pid: process.pid, createdAt: Date.now() }), 0, 'utf8');
}

/** Enables the public Content KB for a test and restores its prior admin setting afterwards. */
export function withKnowledgeBaseEnabled(): void {
  let context: BrowserContext | null = null;
  let page: Page | null = null;
  let heldLock: LockHandle | null = null;
  let heldKnowledgeBaseLock: KnowledgeBaseLock | null = null;
  let lockHeartbeat: ReturnType<typeof setInterval> | null = null;
  let wasEnabled = false;
  let captured = false;

  test.beforeEach(async ({ page }) => {
    await configureAdminPageForTest(page);
  });

  test.beforeAll(async ({ browser }) => {
    test.setTimeout(120_000);
    context = await browser.newContext();
    page = await context.newPage();
    heldKnowledgeBaseLock = await acquireKnowledgeBaseLock();
    lockHeartbeat = setInterval(() => {
      void utimes(knowledgeBaseLockPath, new Date(), new Date()).catch(() => {});
      if (heldLock) {
        void refreshAdminLockLease(heldLock).catch(() => {});
      }
    }, adminLockHeartbeatMs);
    heldLock = await acquireAdminLockForTest(page);
    wasEnabled = await getToolEnabled(page, 'Knowledge Base');
    captured = true;
    await setToolEnabled(page, 'Knowledge Base', true);
  });

  test.afterAll(async () => {
    try {
      if (page && heldLock && captured) {
        await setToolEnabled(page, 'Knowledge Base', wasEnabled);
      }
    } finally {
      if (lockHeartbeat) {
        clearInterval(lockHeartbeat);
        lockHeartbeat = null;
      }
      try {
        await releaseAdminLockForTest(heldLock);
      } finally {
        try {
          if (heldKnowledgeBaseLock) {
            await releaseKnowledgeBaseLock(heldKnowledgeBaseLock);
            heldKnowledgeBaseLock = null;
          }
        } finally {
          await context?.close();
          heldLock = null;
          page = null;
          context = null;
        }
      }
    }
  });
}

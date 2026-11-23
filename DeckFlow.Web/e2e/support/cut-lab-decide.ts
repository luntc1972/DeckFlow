import { expect, type Page, type Response } from '@playwright/test';

// No-JS decide returns a 200 with a rendered view and no redirect. The response
// proves the round trip, and the caller's web-first DOM assertion proves the new
// document committed. This replaces the deprecated navigation wait.
export const waitForDecide = async (
  page: Page,
  action: () => Promise<unknown>,
  { decision, timeout, message, navigation }: {
    decision?: string;
    timeout?: number;
    message?: string;
    navigation?: boolean;
  } = {},
): Promise<Response> => {
  const [response] = await Promise.all([
    page.waitForResponse(response => {
      const request = response.request();
      const { pathname } = new URL(request.url());
      return request.method() === 'POST'
        && (pathname === '/api/cut-lab/decide' || pathname === '/cut-lab/decide')
        && (!navigation || request.isNavigationRequest())
        && (decision === undefined || request.postDataJSON()?.decision === decision);
    }, timeout === undefined ? undefined : { timeout }),
    Promise.resolve().then(action),
  ]);

  expect(response.ok(), message).toBeTruthy();
  return response;
};

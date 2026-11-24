import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { enterCredentials, registerLearner } from './test-account';

test('password recovery keeps the old password until reset and rejects a reused link', async ({
  page,
  request,
  baseURL,
}) => {
  const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
  test.skip(!inboxPort, 'Requires the disposable email service.');
  test.setTimeout(120_000);
  const { email, password } = await registerLearner(page, request, baseURL!, inboxPort!);
  const signOut = async () => {
    await page.goto('/account');
    await page.getByRole('button', { name: 'Sign out ↗', exact: true }).click();
    await expect(page).toHaveURL(/\/login(?:\?|$)/);
  };
  await signOut();
  await page.getByRole('link', { name: 'Forgot your password?' }).click();
  await page.getByLabel('Your email').fill(email);
  const resetRequest = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === '/api/v1/auth/forgot-password' &&
      response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: /Send my reset link/ }).click();
  const resetResponse = await resetRequest;
  expect(resetResponse.status(), 'The reset request must be accepted before checking its UI').toBe(
    202,
  );
  await expect(page.getByRole('status')).toContainText('Your password hasn’t changed yet.');

  let resetLink = '';
  await expect
    .poll(
      async () => {
        const inbox = `http://127.0.0.1:${inboxPort}/api/v1`;
        const response = await request.get(`${inbox}/messages`);
        expect(response.ok()).toBeTruthy();
        const { messages } = (await response.json()) as {
          messages: { ID: string; To: { Address: string }[] }[];
        };
        for (const message of messages.filter((item) =>
          item.To.some((recipient) => recipient.Address === email),
        )) {
          const body = await request.get(`${inbox}/message/${message.ID}`);
          expect(body.ok()).toBeTruthy();
          const { HTML } = (await body.json()) as { HTML: string };
          const match = HTML.match(/href="([^"]+\/reset-password\?[^"]+)"/);
          if (match) resetLink = match[1].replaceAll('&amp;', '&');
        }
        return Boolean(resetLink);
      },
      { timeout: 35_000 },
    )
    .toBe(true);
  expect(new URL(resetLink).origin).toBe(new URL(baseURL!).origin);

  await page.goto('/login');
  await enterCredentials(page, email, password);
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
  await signOut();

  const newPassword = `New!${randomUUID()}`;
  await page.goto(resetLink);
  await page.getByLabel('Your new password').fill(newPassword);
  await page.getByRole('button', { name: /Save my new password/ }).click();
  await expect(page.getByRole('status')).toContainText('Your password is updated');
  await expect(page).toHaveURL(/\/reset-password$/);
  await page.getByRole('link', { name: /Sign in again/ }).click();
  await enterCredentials(page, email, password);
  await expect(page.getByRole('alert')).toContainText('Unable to sign in.');
  await enterCredentials(page, email, newPassword);
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
  await signOut();

  await page.goto(resetLink);
  await page.getByLabel('Your new password').fill(`Other!${randomUUID()}`);
  await page.getByRole('button', { name: /Save my new password/ }).click();
  await expect(page.getByRole('alert')).toContainText('This reset request is invalid');
  await page.goto('/login');
  await enterCredentials(page, email, newPassword);
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
});

import { readFile } from 'node:fs/promises';
import { expect, test } from '@playwright/test';
import { buySimulatedPremium } from './simulated-premium';
import { registerLearner } from './test-account';

const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];

test('a learner compares plans, buys Premium and downloads the receipt from their wallet', async ({
  page,
  request,
  baseURL,
}) => {
  test.skip(!inboxPort, 'Requires the disposable email service.');
  test.setTimeout(60_000);
  const { email } = await registerLearner(page, request, baseURL!, inboxPort!);
  await page.goto('/pricing');
  await expect(page.getByRole('heading', { name: 'Free', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Premium', exact: true })).toBeVisible();
  const [orderCreated] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === '/api/v1/me/billing-orders',
    ),
    buySimulatedPremium(page),
  ]);
  expect(orderCreated.status()).toBe(201);
  expect(await orderCreated.headerValue('location')).toMatch(
    /\/api\/v1\/me\/billing-orders\/[a-f0-9-]{36}$/,
  );

  await page.goto('/billing/subscription');
  await expect(page.getByRole('heading', { name: 'Premium', exact: true })).toBeVisible();
  await page.getByRole('link', { name: /Your history/ }).click();
  await expect(page.getByRole('heading', { name: 'Monthly Premium' })).toBeVisible();
  await page.getByRole('link', { name: /Your transaction trail/ }).click();
  await page.getByRole('link', { name: /^View receipt/ }).click();
  await expect(page.getByRole('heading', { name: 'Succeeded' })).toBeVisible();
  await expect(page.getByText('Monthly', { exact: true })).toBeVisible();

  const receiptDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: /Download receipt/ }).click();
  const receipt = await receiptDownload;
  expect(receipt.suggestedFilename()).toMatch(/\.txt$/);
  const text = await readFile(await receipt.path(), 'utf8');
  expect(text).toContain('LEXIGO — PAYMENT RECEIPT');
  expect(text).toContain('Period: Monthly');
  expect(text).toContain('Amount: 9.99 GEL');

  await page.goto('/account');
  await page.reload();
  await expect(page.getByRole('region', { name: 'Your current plan' })).toContainText(
    'Premium access is saved to your account.',
  );
  await page.getByRole('link', { name: /Find a teacher/ }).click();
  await expect(page).toHaveURL(/\/classes\/extra-study$/);
  await page.getByRole('button', { name: 'Go back to the previous page' }).click();
  await expect(page).toHaveURL(/\/account$/);

  const inbox = `http://127.0.0.1:${inboxPort}/api/v1`;
  await expect
    .poll(
      async () => {
        const response = await request.get(`${inbox}/messages`);
        expect(response.ok()).toBeTruthy();
        const { messages } = (await response.json()) as {
          messages: { Subject: string; To: { Address: string }[] }[];
        };
        return messages.some(
          (message) =>
            message.Subject === 'Your LexiGo Premium purchase is successful' &&
            message.To.some((recipient) => recipient.Address === email),
        );
      },
      { timeout: 35_000 },
    )
    .toBe(true);
});

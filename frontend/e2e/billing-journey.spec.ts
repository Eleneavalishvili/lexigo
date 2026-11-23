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
  await registerLearner(page, request, baseURL!, inboxPort!);
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
  await page.getByRole('link', { name: /^View test receipt/ }).click();
  await expect(page.getByRole('heading', { name: 'Succeeded' })).toBeVisible();
  await expect(page.getByText('Monthly', { exact: true })).toBeVisible();

  const receiptDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: /Download test receipt/ }).click();
  const receipt = await receiptDownload;
  expect(receipt.suggestedFilename()).toMatch(/\.txt$/);
  const text = await readFile(await receipt.path(), 'utf8');
  expect(text).toContain('NO REAL PAYMENT');
  expect(text).toContain('Period: Monthly');
  expect(text).toContain('Amount: 9.99 GEL');
});

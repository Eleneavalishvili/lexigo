import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';

const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
const monthlyPrice = '71000000-0000-0000-0000-000000000001';

test('a learner can cancel an unpaid Premium checkout and start a fresh one', async ({
  page,
  request,
  baseURL,
}) => {
  test.skip(!inboxPort, 'Requires the disposable email service.');
  await registerLearner(page, request, baseURL!, inboxPort!);
  await page.goto('/billing/subscribe');
  await page.getByLabel('Premium period').selectOption(monthlyPrice);
  const review = page.getByRole('button', { name: /Review order/ });
  await review.click();
  await expect(page.getByText('saved simulated price · Created')).toBeVisible();

  await page.getByRole('button', { name: 'Cancel checkout' }).click();
  await expect(page.getByText('saved simulated price · Cancelled')).toBeVisible();
  await expect(page.getByRole('button', { name: /Confirm payment/ })).toHaveCount(0);

  await review.click();
  await expect(page.getByText('saved simulated price · Created')).toBeVisible();
});

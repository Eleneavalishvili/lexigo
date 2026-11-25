import { expect, test } from '@playwright/test';
import { buySimulatedPremium } from './simulated-premium';
import { signInStaff } from './staff-account';
import { registerLearner } from './test-account';

test('withdrawing a teacher request enables the card and permits a fresh request after reload', async ({
  page,
  browser,
  request,
  baseURL,
}, testInfo) => {
  const database = process.env['LEXIGO_E2E_DATABASE'];
  const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
  test.skip(!database || !inbox, 'Requires disposable staff and email services.');
  test.setTimeout(150_000);
  const teacher = await browser.newPage({ baseURL });
  const name = `Cancellation Tutor ${testInfo.project.name}`;
  try {
    await signInStaff(teacher, request, baseURL!, inbox!, database!, ['Teacher'], name);
    await teacher.goto('/classes');
    await expect(teacher.getByRole('heading', { name: 'Your teacher desk.' })).toBeVisible();
    await expect(teacher.getByRole('heading', { name: 'Create a class', exact: true })).toBeVisible();
    await teacher.getByRole('link', { name: 'Study requests and messages ↗', exact: true }).click();
    await teacher.getByRole('button', { name: 'Spanish · Hidden', exact: true }).click();
    await expect(
      teacher.getByRole('button', { name: 'Spanish · Listed', exact: true }),
    ).toBeVisible();
    await registerLearner(page, request, baseURL!, inbox!);
    await buySimulatedPremium(page);
    await page.goto('/classes/extra-study');
    const card = page
      .getByRole('listitem')
      .filter({ has: page.getByRole('heading', { name, exact: true }) });
    await card.getByRole('button', { name: 'Request extra study', exact: true }).click();
    await expect(page.getByText('Spanish · Pending')).toBeVisible();
    await page.getByRole('button', { name: 'Withdraw request', exact: true }).click();
    await page.getByRole('button', { name: 'Confirm close', exact: true }).click();
    await expect(page.getByText('Study request closed.', { exact: true })).toBeVisible();
    await expect(
      card.getByRole('button', { name: 'Request extra study', exact: true }),
    ).toBeEnabled();
    await expect(page.getByText('Spanish · Closed')).toBeVisible();
    await page.reload();
    await card.getByRole('button', { name: 'Request extra study', exact: true }).click();
    await expect(page.getByText('Spanish · Pending')).toBeVisible();
    await expect(page.getByText('Spanish · Closed')).toBeVisible();
    await expect(
      card.getByRole('button', { name: 'Request recorded', exact: true }),
    ).toBeDisabled();
    await teacher.getByRole('button', { name: 'Refresh requests', exact: true }).click();
    await expect(teacher.getByRole('button', { name: 'Accept', exact: true })).toHaveCount(1);
  } finally {
    await teacher.close();
  }
});

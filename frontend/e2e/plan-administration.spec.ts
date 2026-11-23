import { expect, test } from '@playwright/test';
import { signInStaff } from './staff-account';

const database = process.env['LEXIGO_E2E_DATABASE'];
const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];

test('a billing administrator creates and edits a private draft', async ({
  page,
  request,
  baseURL,
}, testInfo) => {
  test.skip(!database || !inboxPort, 'Requires disposable staff and email services.');
  const draftCode = `BROWSER_${testInfo.project.name.toUpperCase()}`;
  test.setTimeout(90_000);
  const password = await signInStaff(page, request, baseURL!, inboxPort!, database!, [
    'SuperAdmin',
  ]);
  await page.goto('/admin/plans');

  const editor = page.locator('app-plan-metadata-editor').first();
  await editor.getByText('Create a draft plan', { exact: true }).click();
  await editor.getByLabel('Code', { exact: true }).fill(draftCode);
  await editor.getByLabel('Name', { exact: true }).fill('Browser draft');
  await editor.getByLabel('Reason').fill('Browser verification');
  await editor.getByLabel('Current password').fill(password);
  await editor.getByRole('button', { name: 'Save plan' }).click();
  await expect(page.getByText('Draft plan saved.', { exact: false })).toBeVisible();

  const card = page.getByRole('article').filter({ hasText: `${draftCode} ·` });
  await card.getByText('Rename plan', { exact: true }).click();
  await card.getByLabel('New name').fill('Renamed browser draft');
  await card.getByRole('button', { name: 'Edit included features' }).click();
  await card.getByRole('checkbox', { name: 'Listening inside core lessons' }).check();
  await card.getByLabel('Limit for Listening inside core lessons').fill('3');
  await card.getByLabel('Reason').fill('Verify draft name and features');
  await card.getByLabel('Current password').fill(password);
  await card.getByRole('button', { name: 'Save plan' }).click();
  await expect(card.getByRole('heading', { name: 'Renamed browser draft' })).toBeVisible();
  await page.reload();
  await expect(card.getByRole('heading', { name: 'Renamed browser draft' })).toBeVisible();
  await expect(card.getByText('CoreListening', { exact: true })).toBeVisible();
  await expect(card.getByText('Limit: 3', { exact: true })).toBeVisible();
  const publicPlans = await request.get('/api/v1/plans');
  expect(publicPlans.ok()).toBe(true);
  expect(await publicPlans.text()).not.toContain(draftCode);

  // Premium is shared by every test in this database. Only a dedicated run can change it.
  if (process.env['LEXIGO_E2E_PLAN_LIFECYCLE'] !== 'true') return;

  const premium = page.getByRole('article').filter({
    has: page.getByRole('heading', { name: 'Premium', exact: true }),
  });
  const premiumEditor = premium.locator('app-plan-metadata-editor');
  await premiumEditor.getByText('Rename plan', { exact: true }).click();
  await premiumEditor.getByRole('checkbox', { name: 'Retire Premium from new sales' }).check();
  await premiumEditor.getByLabel('Reason').fill('Verify retirement');
  await premiumEditor.getByLabel('Current password').fill(password);
  await premiumEditor.getByRole('button', { name: 'Save plan' }).click();
  await expect(premium.getByText('PREMIUM · INACTIVE', { exact: true })).toBeVisible();
  expect(await (await request.get('/api/v1/plans')).text()).not.toContain('"code":"PREMIUM"');

  await premiumEditor.getByText('Rename plan', { exact: true }).click();
  await premiumEditor.getByRole('checkbox', { name: 'Reopen Premium sales' }).check();
  await premiumEditor.getByLabel('Reason').fill('Verify reopening');
  await premiumEditor.getByLabel('Current password').fill(password);
  await premiumEditor.getByRole('button', { name: 'Save plan' }).click();
  await expect(premium.getByText('PREMIUM · ACTIVE', { exact: true })).toBeVisible();
  expect(await (await request.get('/api/v1/plans')).text()).toContain('"code":"PREMIUM"');
});

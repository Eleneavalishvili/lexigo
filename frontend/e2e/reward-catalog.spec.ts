import { expect, test } from '@playwright/test';
import { signInStaff } from './staff-account';

test('administrator creation and filtered paging work for badges and challenges', async ({
  page,
  request,
  baseURL,
}) => {
  const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
  const database = process.env['LEXIGO_E2E_DATABASE'];
  test.skip(!inbox || !database, 'Requires disposable staff and email services.');
  test.setTimeout(240_000);
  await signInStaff(page, request, baseURL!, inbox!, database!, ['Admin']);
  await page.goto('/manage/rewards');
  const prefix = `Catalog ${test.info().project.name} ${Date.now()}`;
  await page.getByRole('button', { name: 'Create badge', exact: true }).click();
  await page.getByLabel('Permanent code').fill(`browser-${Date.now()}`);
  await page.getByLabel('Title', { exact: true }).fill(prefix + ' badge');
  await page.getByLabel('Target', { exact: true }).fill('2');
  await page.getByLabel('Reason for this change').fill('Verify the catalog management workflow');
  const badgeCreated = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === '/api/v1/manage/achievements' &&
      response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Save badge', exact: true }).click();
  expect((await badgeCreated).status()).toBe(201);
  await expect(page.getByText('Badge saved.', { exact: true })).toBeVisible();
  await page.getByLabel('Search badges by title or code').fill(prefix);
  await page.getByRole('button', { name: 'Search catalog', exact: true }).click();
  await expect(page.locator('.records .record')).toHaveCount(1);
  const badgePage = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return (
      url.pathname === '/api/v1/manage/achievements' && url.searchParams.get('pageSize') === '10'
    );
  });
  await page.getByLabel('Records per page').selectOption('10');
  const badgeResult = await (await badgePage).json();
  expect(badgeResult.totalCount).toBe(1);
  expect(badgeResult.pageSize).toBe(10);
  await expect(page.getByText('1 record · Page 1', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Challenges', exact: true }).click();
  await page.getByRole('button', { name: 'Create challenge', exact: true }).click();
  await page.getByLabel('Title', { exact: true }).fill(prefix + ' challenge');
  await page.getByLabel('Target', { exact: true }).fill('3');
  await page.getByLabel('Bonus XP', { exact: true }).fill('20');
  const offset = test.info().project.name === 'phone' ? 16 : 14;
  const date = (days: number) =>
    new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 16);
  await page.getByLabel('Starts at (UTC)', { exact: true }).fill(date(offset));
  await page.getByLabel('Ends at (UTC, exclusive)', { exact: true }).fill(date(offset + 7));
  await page.getByLabel('Reason for this change').fill('Verify the challenge catalog workflow');
  const challengeCreated = page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === '/api/v1/manage/challenges' &&
      response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Save challenge', exact: true }).click();
  expect((await challengeCreated).status()).toBe(201);
  await expect(page.getByText('Challenge saved.', { exact: true })).toBeVisible();
  await page.getByLabel('Search challenges by title').fill(prefix);
  await page.getByRole('button', { name: 'Search catalog', exact: true }).click();
  await expect(page.locator('.records .record')).toHaveCount(1);
  await expect(page.locator('.records')).toContainText(prefix + ' challenge');
  await expect(page.getByText('1 record · Page 1', { exact: true })).toBeVisible();
});

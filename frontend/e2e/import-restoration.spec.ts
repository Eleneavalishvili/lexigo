import { expect, test } from '@playwright/test';
import { signInStaff } from './staff-account';

test('a saved import report resumes after reload and never creates a second draft', async ({
  page,
  request,
  baseURL,
}) => {
  const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
  const database = process.env['LEXIGO_E2E_DATABASE'];
  test.skip(!inbox || !database, 'Requires disposable staff and email services.');
  test.setTimeout(180_000);
  await signInStaff(page, request, baseURL!, inbox!, database!, ['Admin']);
  await page.goto('/manage/imports');
  await page
    .getByRole('combobox', { name: 'Course', exact: true })
    .selectOption('10000000-0000-0000-0000-000000000001');
  await page.getByRole('combobox', { name: 'Unit', exact: true }).selectOption({ index: 1 });
  const title = `Saved import ${test.info().project.name} ${Date.now()}`;
  await page.getByLabel('Lesson title', { exact: true }).fill(title);
  await page.getByLabel('Objective', { exact: true }).fill('Practise a friendly greeting.');
  await page
    .getByLabel('English explanation', { exact: true })
    .fill('Hola is a common Spanish greeting.');
  await page.getByLabel('Exercise CSV', { exact: true }).setInputFiles({
    name: 'greeting.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from('prompt,answer,feedback\nSay hello in Spanish,Hola,A friendly greeting.\n'),
  });
  const validation = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/api/v1/manage/imports/validate',
  );
  await page.getByRole('button', { name: 'Validate CSV', exact: true }).click();
  expect((await validation).status()).toBe(201);
  await expect(page).toHaveURL(/\?job=[0-9a-f-]+/);
  await expect(page.getByText('1 valid exercise rows.', { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByText('Saved validation report restored.', { exact: true })).toBeVisible();
  await expect(page.getByText(title, { exact: true })).toBeVisible();
  await expect(page.locator('input[type=file]')).toHaveCount(0);
  const commit = page.waitForResponse((response) =>
    new URL(response.url()).pathname.endsWith('/commit'),
  );
  await page.getByRole('button', { name: 'Create draft', exact: true }).click();
  expect((await commit).status()).toBe(201);
  await expect(page.getByText(/Draft created. Open the lesson workshop/)).toBeVisible();
  await page.reload();
  await expect(page.getByText(/Draft created. Open the lesson workshop/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Create draft', exact: true })).toHaveCount(0);
  await expect(page.getByText(title, { exact: true })).toBeVisible();
});

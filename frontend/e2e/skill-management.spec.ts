import { expect, test } from '@playwright/test';
import { signInStaff } from './staff-account';

test('an administrator manages course skills and preserves an archived record through reload', async ({
  page,
  request,
  baseURL,
}) => {
  const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
  const database = process.env['LEXIGO_E2E_DATABASE'];
  test.skip(!inbox || !database, 'Requires the disposable staff environment.');
  test.setTimeout(180_000);
  await signInStaff(page, request, baseURL!, inbox!, database!, ['Admin']);
  await page.goto('/manage/lessons');
  await page.getByRole('link', { name: 'Course skills' }).click();
  await expect(page.getByRole('heading', { name: 'Course skills' })).toBeVisible();
  const name = `Travel skills ${test.info().project.name} ${Date.now()}`;
  await page.getByLabel('Skill name', { exact: true }).fill(name);
  await page.getByLabel('Category', { exact: true }).fill('Travel');
  await page.getByRole('button', { name: 'Save skill', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Skill saved.' })).toBeVisible();
  await expect(page.getByRole('button', { name: `Edit ${name}`, exact: true })).toBeVisible();
  await page.getByLabel('Reason for change').fill('Use the updated topic label');
  await page.getByLabel('Skill name', { exact: true }).fill(name + ' updated');
  await page.getByRole('button', { name: 'Save skill', exact: true }).click();
  await expect(
    page.getByRole('button', { name: `Edit ${name} updated`, exact: true }),
  ).toBeVisible();
  await page.getByLabel('Reason for change').fill('Retain the topic history for review');
  await page.getByRole('button', { name: 'Archive skill', exact: true }).click();
  await expect(
    page.getByText('Skill archived. Existing lessons and progress are preserved.'),
  ).toBeVisible();
  await page.getByLabel('Include archived skills').check();
  await expect(
    page.getByRole('button', { name: `Edit ${name} updated`, exact: true }),
  ).toBeVisible();
  await page.reload();
  await page.getByLabel('Include archived skills').check();
  await page.getByRole('button', { name: `Edit ${name} updated`, exact: true }).click();
  await page.getByLabel('Reason for change').fill('Ready to use this topic again');
  await page.getByRole('button', { name: 'Restore skill', exact: true }).click();
  await expect(page.getByText('Skill restored.', { exact: true })).toBeVisible();
  await page.getByLabel('Search by name or category').fill(name);
  await page.getByRole('button', { name: 'Search', exact: true }).click();
  await expect(page.getByText('1 skill · Page 1', { exact: true })).toBeVisible();
  await expect(
    page.getByRole('button', { name: `Edit ${name} updated`, exact: true }),
  ).toBeVisible();
});

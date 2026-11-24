import { expect, test } from '@playwright/test';
import { signInStaff } from './staff-account';

test('administrator creates an unpublished course and cannot duplicate its language pair', async ({
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
  await page.getByRole('button', { name: 'Create a course', exact: true }).click();
  const source = test.info().project.name === 'desktop' ? 'Spanish' : 'Italian';
  const title = `Staff course ${test.info().project.name}`;
  await page.getByLabel('Teaching language', { exact: true }).selectOption({ label: source });
  await page.getByLabel('Language to learn', { exact: true }).selectOption({ label: 'English' });
  await page.getByLabel('New course title', { exact: true }).fill(title);
  await page
    .getByLabel('New course description', { exact: true })
    .fill('A private curriculum awaiting reviewed lessons.');
  await page.getByRole('button', { name: 'Create unpublished course', exact: true }).click();
  await expect(page.getByRole('button', { name: title, exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('button', { name: title, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Create a course', exact: true }).click();
  await page.getByLabel('Teaching language', { exact: true }).selectOption({ label: source });
  await page.getByLabel('Language to learn', { exact: true }).selectOption({ label: 'English' });
  await page.getByLabel('New course title', { exact: true }).fill('Duplicate pair');
  await page
    .getByLabel('New course description', { exact: true })
    .fill('This duplicate must be rejected.');
  await page.getByRole('button', { name: 'Create unpublished course', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('A course already exists');
});

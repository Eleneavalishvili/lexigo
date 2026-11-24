import { expect, test } from '@playwright/test';
import { signInStaff } from './staff-account';

test('staff creates a connected dialogue draft and reloads its saved replies', async ({
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
  await page.getByRole('link', { name: 'Dialogue workshop' }).click();
  await page
    .getByRole('navigation', { name: 'Dialogue courses' })
    .getByRole('button', { name: 'Spanish', exact: true })
    .click();
  const title = `Original conversation ${test.info().project.name}`;
  await page.getByLabel('Dialogue title', { exact: true }).fill(title);
  await page.getByLabel('Scenario key', { exact: true }).fill(`visit-${test.info().project.name}`);
  await page.getByRole('button', { name: 'Create dialogue draft', exact: true }).click();
  await expect(page.getByRole('heading', { name: title, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Add conversation node', exact: true }).click();
  await page.getByRole('button', { name: 'Add conversation node', exact: true }).click();
  const nodes = page.locator('.dialogue-node');
  await nodes.nth(0).getByLabel('Character’s line', { exact: true }).fill('¡Hola!');
  await nodes.nth(1).getByLabel('Character’s line', { exact: true }).fill('¡Bienvenido!');
  await nodes.nth(1).getByLabel('Ending node', { exact: true }).check();
  await nodes.nth(0).getByRole('button', { name: 'Add reply', exact: true }).click();
  await nodes.nth(0).getByLabel('Learner’s reply', { exact: true }).fill('Hola.');
  await nodes.nth(0).getByLabel('English feedback', { exact: true }).fill('A polite greeting.');
  await nodes.nth(0).getByLabel('Continue to', { exact: true }).selectOption({ label: 'Node 2' });
  await page.getByRole('button', { name: 'Save dialogue draft', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Draft saved.' })).toBeVisible();
  await page.reload();
  await page
    .getByRole('navigation', { name: 'Dialogue courses' })
    .getByRole('button', { name: 'Spanish', exact: true })
    .click();
  await page.getByRole('button', { name: new RegExp(title) }).click();
  await expect(nodes.nth(0).getByLabel('Character’s line', { exact: true })).toHaveValue('¡Hola!');
  await expect(nodes.nth(0).getByLabel('Learner’s reply', { exact: true })).toHaveValue('Hola.');
  await page
    .getByLabel('Review or action note', { exact: true })
    .fill('Ready for independent language review.');
  await page.getByRole('button', { name: 'Submit dialogue for review', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Submit dialogue for review', exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole('button', { name: 'Publish reviewed dialogue', exact: true }),
  ).toHaveCount(0);
});
